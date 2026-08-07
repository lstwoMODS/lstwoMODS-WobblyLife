using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace lstwoMODS_WobblyLife.PropSpawner;

public class PropSpawnOptions
{
    /// <summary>Place the layout relative to the player instead of at its saved coordinates.</summary>
    public bool Rebase;

    /// <summary>Destroy the group's existing live props first.</summary>
    public bool ReplaceExisting;

    /// <summary>
    /// Re-parent even when a networked prop is involved. Off by default: HawkTransformSync
    /// replicates localPosition/localRotation, so a parented networked prop lands somewhere else
    /// entirely for everyone else in the session.
    /// </summary>
    public bool ForceParentNetworked;

    public bool  ZeroVelocities = true;
    public int   BatchSize      = 8;
    public float TimeoutSeconds = 45f;
}

/// <summary>
/// Puts a saved layout back into the world.
/// </summary>
public static class PropLayoutRestore
{
    private static readonly HashSet<string> _restoring = new();

    private const int MaxReportedErrors = 20;

    public static void SpawnGroup(string groupId, PropSpawnOptions options)
    {
        var group = PropGroupManager.Find(groupId);
        if (group == null || group.Props == null || group.Props.Count == 0) return;

        // Two restores of the same group at once would fight over parenting and produce
        // duplicates; different groups are independent and may overlap freely.
        if (!_restoring.Add(groupId))
        {
            PropSpawnerIpc.SendStatus(groupId, 0, 0, null, $"\"{group.Name}\" is already being spawned.");
            return;
        }

        Plugin._StartCoroutine(Routine(group, options ?? new PropSpawnOptions()));
    }

    /// <summary>
    /// Drives the restore and guarantees the per-group lock is released. Without the finally, an
    /// exception anywhere in the restore would leave the group permanently unspawnable for the
    /// rest of the session.
    /// </summary>
    private static IEnumerator Routine(PropGroup group, PropSpawnOptions options)
    {
        try
        {
            var inner = RestoreRoutine(group, options);
            while (inner.MoveNext()) yield return inner.Current;
        }
        finally
        {
            _restoring.Remove(group.Id);
        }
    }

    private static IEnumerator RestoreRoutine(PropGroup group, PropSpawnOptions options)
    {
        var generation = PropSpawnManager.SceneGeneration;
        var props      = group.Props;
        var errors     = new List<string>();

        if (options.ReplaceExisting)
        {
            PropSpawnManager.DestroyGroup(group.Id, forceLocal: false, out var refusals);
            errors.AddRange(refusals);
        }

        // Networked spawns are server-gated inside Hawk and silently do nothing on a client.
        // Say so up front rather than letting the user watch nothing happen.
        var networkedCount = props.Count(p => p.Source.Networked);
        if (networkedCount > 0 && !PropSpawnManager.IsServer)
        {
            PropSpawnerIpc.SendStatus(group.Id, 0, networkedCount, null,
                $"{networkedCount} networked prop(s) can only be spawned by the host.");
            yield break;
        }

        ComputeRebase(group, options, out var origin, out var yawDelta);

        var results   = new GameObject[props.Count];
        var completed = 0;

        // ── Pass A: spawn everything at its world pose ───────────────────────
        for (var i = 0; i < props.Count; i++)
        {
            var index     = i;
            var placement = props[i];
            var pos       = RebasePosition(placement.Position, group.Origin, origin, yawDelta, options.Rebase);
            var rot       = options.Rebase ? yawDelta * placement.Rotation() : placement.Rotation();

            void Done(GameObject go)
            {
                results[index] = go;
                if (go == null && errors.Count < MaxReportedErrors)
                    errors.Add($"{placement.Source.Describe()}: {DescribeFailure(placement)}");
                completed++;
            }

            if (placement.Source.IsCustomItem)
            {
                PropSpawnManager.SpawnCustomItemAt(placement.Source, pos, rot, Done);
            }
            else
            {
                var candidates = PropAddressResolver.Fallbacks(placement.Source).ToList();
                if (placement.Source.Networked)
                    PropSpawnManager.SpawnNetworked(candidates, pos, rot, Done);
                else
                    Plugin._StartCoroutine(PropSpawnManager.SpawnLocal(candidates, pos, rot, Done));
            }

            // Spread the Addressables load requests over frames instead of firing 60 at once.
            if ((i + 1) % options.BatchSize == 0) yield return null;
        }

        // ── Barrier ──────────────────────────────────────────────────────────
        // The deadline matters: an exception swallowed inside an Addressables callback would
        // otherwise leave the counter short and hang this routine for the rest of the session.
        var deadline = Time.realtimeSinceStartup + options.TimeoutSeconds;
        yield return new WaitUntil(() => completed >= props.Count || Time.realtimeSinceStartup > deadline);

        if (generation != PropSpawnManager.SceneGeneration) yield break;

        if (completed < props.Count)
            errors.Add($"{props.Count - completed} prop(s) timed out while loading.");

        // ── Pass B: hierarchy, now that every member exists ──────────────────
        var parented         = new bool[props.Count];
        var flattenedByNetwork = 0;

        for (var i = 0; i < props.Count; i++)
        {
            var placement = props[i];
            var child     = results[i];
            if (child == null || !placement.HasParent) continue;
            if (placement.ParentIndex >= props.Count) continue;

            var parentGo = results[placement.ParentIndex];
            if (parentGo == null) continue;   // parent failed to spawn: leave the child at world pose

            var parentPlacement = props[placement.ParentIndex];
            if ((placement.Source.Networked || parentPlacement.Source.Networked) && !options.ForceParentNetworked)
            {
                flattenedByNetwork++;
                continue;
            }

            var parentTransform = string.IsNullOrEmpty(placement.ParentPath)
                ? parentGo.transform
                : parentGo.transform.Find(placement.ParentPath) ?? parentGo.transform;

            child.transform.SetParent(parentTransform, worldPositionStays: false);
            child.transform.localPosition = placement.LocalPosition;
            child.transform.localRotation = placement.LocalRotation();
            parented[i] = true;
        }

        if (flattenedByNetwork > 0)
            errors.Add($"{flattenedByNetwork} networked prop(s) kept their world position instead of " +
                       "being re-parented, which would desync them for other players.");

        // A physics step between spawning and finalising would undo the poses we just set:
        // NetworkPrefab.HandleSpawnedObject explicitly wakes rigidbodies.
        yield return new WaitForFixedUpdate();

        if (generation != PropSpawnManager.SceneGeneration) yield break;

        // ── Pass C: finalise and take ownership ──────────────────────────────
        var spawned = 0;

        for (var i = 0; i < props.Count; i++)
        {
            var go = results[i];
            if (go == null) continue;

            var placement = props[i];

            // Register first: the toggle baseline has to be read from the prefab's own state,
            // before the saved overrides go on top of it.
            var spawnId = PropSpawnManager.Register(go, placement.Source.Clone(), group.Id, placement.Id);

            if (!parented[i])
            {
                go.transform.position = RebasePosition(placement.Position, group.Origin, origin, yawDelta, options.Rebase);
                go.transform.rotation = options.Rebase ? yawDelta * placement.Rotation() : placement.Rotation();
            }

            go.transform.localScale = placement.Scale;

            if (!string.IsNullOrEmpty(placement.Name)) go.name = placement.Name;
            go.SetActive(placement.Active);

            PropComponentState.Apply(go, placement.Toggles);

            if (options.ZeroVelocities)
            {
                foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.velocity        = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            if (PropSpawnManager.TryGet(spawnId, out var prop)) prop.DisplayName = go.name;
            spawned++;
        }

        var failed = props.Count - spawned;
        PropSpawnerIpc.SendStatus(group.Id, spawned, failed, errors.ToArray(),
            failed == 0
                ? $"Spawned {spawned} prop(s) from \"{group.Name}\"."
                : $"Spawned {spawned} of {props.Count} prop(s) from \"{group.Name}\".");

        PropGroupManager.NotifyChanged();
    }

    private static void ComputeRebase(PropGroup group, PropSpawnOptions options,
                                      out Vector3 origin, out Quaternion yawDelta)
    {
        origin   = group.Origin;
        yawDelta = Quaternion.identity;

        if (!options.Rebase) return;

        if (!PropSpawnManager.TryGetSpawnPose(out var playerPos, out var playerYaw))
        {
            options.Rebase = false;   // no player to rebase onto; fall back to saved coordinates
            return;
        }

        origin   = playerPos;
        yawDelta = Quaternion.Euler(0f, playerYaw - group.OriginYaw, 0f);
    }

    private static Vector3 RebasePosition(Vector3 saved, Vector3 savedOrigin, Vector3 newOrigin,
                                          Quaternion yawDelta, bool rebase)
        => rebase ? newOrigin + yawDelta * (saved - savedOrigin) : saved;

    private static string DescribeFailure(PropPlacement placement)
        => placement.Source.IsCustomItem
            ? PropAddressResolver.DescribeCustomItemFailure(placement.Source.PackName, placement.Source.ItemName)
            : "address did not resolve";
}
