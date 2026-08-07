using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using lstwoMODS.WobblyLife.SharedObjects;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// Owns every prop this mod has spawned in the current session: what it is, which group it
/// belongs to, and how to get rid of it. The registry is main-thread only — everything here
/// touches Unity objects.
/// </summary>
public static class PropSpawnManager
{
    /// <summary>A prop that exists in the world right now.</summary>
    public sealed class SpawnedProp
    {
        /// <summary>Process-local and monotonic. Never persisted and never reset, so a stale id
        /// held by the overlay can only miss — it can never come to mean a different prop.</summary>
        public int SpawnId;

        /// <summary>Null means ungrouped.</summary>
        public string GroupId;

        /// <summary>Set when this prop came from a restore, linking it to its saved placement.</summary>
        public string PlacementId;

        public PropIdentity Source;
        public GameObject   Object;
        public bool         Networked;
        public string       DisplayName;

        /// <summary>Component enabled flags as the prefab spawned with them. Null when the
        /// hierarchy was too large to scan.</summary>
        public Dictionary<string, bool> ToggleBaseline;

        /// <summary>Unity's overloaded <c>==</c> makes this catch objects destroyed by anything —
        /// UnityExplorer, game code, a network despawn.</summary>
        public bool Alive => Object != null;
    }

    private static readonly Dictionary<int, SpawnedProp> _live = new();

    private static int  _nextSpawnId;
    private static int  _sceneGeneration;
    private static bool _initialized;

    /// <summary>Bumped on every single-scene load. Long-running routines compare against it to
    /// notice that the world they were working in is gone.</summary>
    public static int SceneGeneration => _sceneGeneration;

    /// <summary>Fires when the live set or its group assignment changes.</summary>
    public static event Action Changed;

    /// <summary>Offline counts as server, so single-player can always spawn and destroy.</summary>
    public static bool IsServer => HawkNetworkManager.DefaultInstance?.IsServer() ?? false;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Plugin._StartCoroutine(TickRoutine());
        PropSpawnerIpc.StartPump();
    }

    public static void OnSceneLoaded(Scene scene)
    {
        _sceneGeneration++;
        _live.Clear();
        NotifyChanged();
    }


    // ── queries ──────────────────────────────────────────────────────────────

    public static IEnumerable<SpawnedProp> Live
    {
        get
        {
            Prune();
            return _live.Values.OrderBy(p => p.SpawnId);
        }
    }

    public static IEnumerable<SpawnedProp> InGroup(string groupId)
        => Live.Where(p => string.Equals(p.GroupId, groupId, StringComparison.Ordinal));

    /// <summary>
    /// Every live prop bucketed by group, ungrouped under the empty-string key. Prunes and sorts
    /// once for the whole set — the callers that need per-group counts want all of them at the
    /// same moment anyway, and asking group by group repeats that work per group.
    /// </summary>
    public static Dictionary<string, List<SpawnedProp>> LiveByGroup()
    {
        var result = new Dictionary<string, List<SpawnedProp>>(StringComparer.Ordinal);

        foreach (var prop in Live)
        {
            var key = prop.GroupId ?? "";
            if (!result.TryGetValue(key, out var list)) result[key] = list = new List<SpawnedProp>();
            list.Add(prop);
        }

        return result;
    }

    public static bool TryGet(int spawnId, out SpawnedProp prop)
    {
        Prune();
        return _live.TryGetValue(spawnId, out prop);
    }

    /// <summary>Drops entries whose object died by other means. Returns how many went.</summary>
    public static int Prune()
    {
        List<int> dead = null;
        foreach (var kvp in _live)
        {
            if (kvp.Value.Alive) continue;
            dead ??= new List<int>();
            dead.Add(kvp.Key);
        }

        if (dead == null) return 0;
        foreach (var id in dead) _live.Remove(id);
        return dead.Count;
    }

    /// <summary>Group assignments pointing at a group that no longer exists become ungrouped.</summary>
    public static void ForgetGroup(string groupId)
    {
        var touched = false;
        foreach (var prop in _live.Values)
        {
            if (!string.Equals(prop.GroupId, groupId, StringComparison.Ordinal)) continue;
            prop.GroupId    = null;
            prop.PlacementId = null;
            touched = true;
        }
        if (touched) NotifyChanged();
    }

    public static void Reassign(IEnumerable<int> spawnIds, string groupId)
    {
        if (spawnIds == null) return;

        var touched = false;
        foreach (var id in spawnIds)
        {
            if (!_live.TryGetValue(id, out var prop) || !prop.Alive) continue;
            if (string.Equals(prop.GroupId, groupId, StringComparison.Ordinal)) continue;

            // The link to a saved placement belongs to the group it was restored into.
            prop.PlacementId = null;
            prop.GroupId     = groupId;
            touched = true;
        }

        if (touched) NotifyChanged();
    }


    // ── spawning ─────────────────────────────────────────────────────────────

    /// <summary>Interactive spawn from the prop library, in front of the local player.</summary>
    public static void SpawnFromLibrary(SpawnPropMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.Address)) return;
        if (!TryGetSpawnPose(out var pos, out _)) return;

        var identity = new PropIdentity
        {
            Kind      = PropSourceKind.Addressable,
            Address   = msg.Address,
            Networked = msg.Networked,
        };
        PropAddressResolver.PopulateHints(identity);

        var candidates = PropAddressResolver.Fallbacks(identity).ToList();
        var groupId    = PropGroupManager.ActiveGroupId;

        void Done(GameObject go)
        {
            if (go == null)
            {
                var how = msg.Networked ? "Networked" : "Local";
                Plugin.LogSource.LogWarning($"[PropSpawner] {how} spawn failed: no valid key for {msg.Address}.");
                PropSpawnerIpc.SendStatus(groupId, 0, 1,
                    new[] { $"{identity.Describe()}: {FailureReason(msg.Networked)}" });
                return;
            }
            Register(go, identity, groupId, null);
        }

        if (msg.Networked)
            SpawnNetworked(candidates, pos, Quaternion.identity, Done);
        else
            Plugin._StartCoroutine(SpawnLocal(candidates, pos, Quaternion.identity, Done));
    }

    /// <summary>Interactive spawn of a custom item. Unlike before, the result is registered, so
    /// custom items can be grouped, saved and destroyed like anything else.</summary>
    public static void SpawnCustomItem(SpawnCustomItemMessage msg)
    {
        if (msg == null) return;
        if (!PropAddressResolver.TryDescribeCustomItem(msg.PackIndex, msg.ItemIndex, out var packName, out var itemName))
            return;

        var item = PropAddressResolver.ResolveCustomItem(packName, itemName);
        if (item == null) return;

        if (!TryGetSpawnPose(out var pos, out _)) return;
        if (item.spawnAtPos) pos = item.customSpawnPos;

        var identity = new PropIdentity
        {
            Kind      = PropSourceKind.CustomItem,
            PackName  = packName,
            ItemName  = itemName,
            Networked = true,
        };

        SpawnCustomItemAt(identity, pos, Quaternion.identity,
            go => { if (go != null) Register(go, identity, PropGroupManager.ActiveGroupId, null); });
    }

    /// <summary>
    /// Tries each candidate key in turn. Calls back with null once they are all exhausted.
    /// Shared by interactive spawns and layout restores.
    /// </summary>
    internal static void SpawnNetworked(List<string> candidates, Vector3 pos, Quaternion rot, Action<GameObject> onDone)
    {
        if (candidates == null || candidates.Count == 0) { onDone?.Invoke(null); return; }

        void Try(int index)
        {
            if (index >= candidates.Count) { onDone?.Invoke(null); return; }

            NetworkPrefab.SpawnNetworkPrefab(
                candidates[index],
                behaviour =>
                {
                    if (behaviour == null) { Try(index + 1); return; }
                    onDone?.Invoke(behaviour.gameObject);
                },
                position:       pos,
                rotation:       rot,
                owner:          null,
                bUseChunkSystem: true,
                bSendTransform:  true,
                // Force the destination chunk to load, or a layout restored away from the player
                // spawns into an unloaded chunk and is culled straight away.
                bCheckChunk:     true);
        }

        Try(0);
    }

    /// <inheritdoc cref="SpawnNetworked"/>
    internal static IEnumerator SpawnLocal(List<string> candidates, Vector3 pos, Quaternion rot, Action<GameObject> onDone)
    {
        if (candidates != null)
        {
            foreach (var key in candidates)
            {
                var handle = Addressables.InstantiateAsync(key, pos, rot);
                yield return handle;

                if (handle.Result != null)
                {
                    onDone?.Invoke(handle.Result);
                    yield break;
                }

                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        onDone?.Invoke(null);
    }

    /// <inheritdoc cref="SpawnNetworked"/>
    internal static void SpawnCustomItemAt(PropIdentity identity, Vector3 pos, Quaternion rot, Action<GameObject> onDone)
    {
        var item = PropAddressResolver.ResolveCustomItem(identity.PackName, identity.ItemName);
        if (item == null) { onDone?.Invoke(null); return; }

        NetworkPrefab.SpawnNetworkPrefab(
            item.gameObject,
            behaviour => onDone?.Invoke(behaviour == null ? null : behaviour.gameObject),
            position:        pos,
            rotation:        rot,
            owner:           null,
            bUseChunkSystem: true,
            bSendTransform:  true,
            bCheckChunk:     true);
    }

    /// <summary>Takes ownership of a freshly spawned object. Returns its SpawnId.</summary>
    public static int Register(GameObject go, PropIdentity source, string groupId, string placementId)
    {
        var spawnId = ++_nextSpawnId;

        _live[spawnId] = new SpawnedProp
        {
            SpawnId        = spawnId,
            GroupId        = groupId,
            PlacementId    = placementId,
            Source         = source,
            Object         = go,
            Networked      = source?.Networked ?? false,
            DisplayName    = go.name,
            ToggleBaseline = PropComponentState.CaptureBaseline(go),
        };

        NotifyChanged();
        return spawnId;
    }


    // ── destroying ───────────────────────────────────────────────────────────

    public static void HandleAction(SpawnedPropActionMessage msg)
    {
        if (msg == null) return;

        var ids = msg.SpawnIds is { Length: > 0 } ? msg.SpawnIds : new[] { msg.SpawnId };

        switch (msg.Action)
        {
            case "Delete":
            {
                var refusals = new List<string>();
                var destroyed = 0;
                foreach (var id in ids)
                {
                    if (!_live.TryGetValue(id, out var prop)) continue;
                    if (TryDestroy(prop, forceLocal: false, out var reason)) destroyed++;
                    else if (reason != null) refusals.Add(reason);
                }
                ReportDestroy(null, destroyed, refusals);
                break;
            }

            case "Inspect":
            {
                if (_live.TryGetValue(ids[0], out var prop) && prop.Alive)
                    TryInspectWithUnityExplorer(prop.Object);
                break;
            }
        }
    }

    public static int DestroyGroup(string groupId, bool forceLocal, out List<string> refusals)
        => DestroyMany(InGroup(groupId).ToList(), forceLocal, out refusals);

    public static int DestroyAll(bool forceLocal, out List<string> refusals)
        => DestroyMany(Live.ToList(), forceLocal, out refusals);

    private static int DestroyMany(List<SpawnedProp> props, bool forceLocal, out List<string> refusals)
    {
        refusals = new List<string>();
        var destroyed = 0;

        // Iterating a snapshot: destroying a parent nulls out its registered children, which the
        // loop then simply sees as already gone.
        foreach (var prop in props)
        {
            if (TryDestroy(prop, forceLocal, out var reason)) destroyed++;
            else if (reason != null) refusals.Add(reason);
        }

        return destroyed;
    }

    /// <summary>
    /// Destroys one prop, honouring how it was created. Returns false only when it is still there.
    /// </summary>
    public static bool TryDestroy(SpawnedProp prop, bool forceLocal, out string reason)
    {
        reason = null;
        if (prop == null) return false;

        if (!prop.Alive)
        {
            _live.Remove(prop.SpawnId);
            NotifyChanged();
            return true;
        }

        var hawk = prop.Object.GetComponent<HawkNetworkBehaviour>();
        if (hawk?.networkObject != null)
        {
            // HawkNetworkObject.Destroy() returns early on a client, and destroying the object
            // locally instead would leave a live network object addressing a dead behaviour.
            if (!IsServer && !forceLocal)
            {
                reason = $"{prop.DisplayName}: networked props can only be destroyed by the host";
                return false;
            }

            if (IsServer) hawk.networkObject.Destroy();
            else          Object.Destroy(prop.Object);
        }
        else
        {
            // The local spawn path used Addressables.InstantiateAsync, so the instance owns a
            // handle that a plain Destroy would leak.
            if (!Addressables.ReleaseInstance(prop.Object))
                Object.Destroy(prop.Object);
        }

        _live.Remove(prop.SpawnId);
        NotifyChanged();
        return true;
    }

    private static void ReportDestroy(string groupId, int destroyed, List<string> refusals)
    {
        if (refusals is { Count: > 0 })
            PropSpawnerIpc.SendStatus(groupId, destroyed, refusals.Count, refusals.ToArray(),
                $"Destroyed {destroyed}, {refusals.Count} refused.");
    }


    // ── plumbing ─────────────────────────────────────────────────────────────

    /// <summary>Where an interactive spawn goes: just in front of the local player.</summary>
    public static bool TryGetSpawnPose(out Vector3 position, out float yaw)
    {
        position = Vector3.zero;
        yaw      = 0f;

        var player = GameInstance.Instance?.GetFirstLocalPlayerController();
        var character = player?.GetPlayerCharacter();
        if (character == null) return false;

        var forward = character.GetPlayerForward();
        position = character.GetPlayerPosition() + forward;
        yaw      = Quaternion.LookRotation(
            new Vector3(forward.x, 0f, forward.z).sqrMagnitude > 0.0001f
                ? new Vector3(forward.x, 0f, forward.z)
                : Vector3.forward).eulerAngles.y;
        return true;
    }

    private static string FailureReason(bool networked)
        => networked && !IsServer
            ? "networked props can only be spawned by the host"
            : "address did not resolve";

    internal static void NotifyChanged() => Changed?.Invoke();

    private static IEnumerator TickRoutine()
    {
        // Realtime: the overlay holds a GamePause handle while it is open, so scaled time stops.
        var wait = new WaitForSecondsRealtime(1f);

        while (true)
        {
            yield return wait;

            // One bad frame must not kill the tick: without it, props destroyed elsewhere would
            // linger in the UI for the rest of the session and dirty flags would freeze.
            try
            {
                var changed = Prune() > 0;

                // Recomputing dirty flags here rather than per frame is what makes a
                // transform-aware comparison affordable: a handful of float compares per live
                // prop, once a second. It is also the only way the UI can tell the user that
                // dragging a prop around in UnityExplorer left them something to save.
                if (PropGroupManager.RefreshDirtyFlags()) changed = true;

                if (changed) NotifyChanged();
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PropSpawner] Tick failed: {ex.Message}");
            }
        }
        // ReSharper disable once IteratorNeverReturns — lives for the process, like the plugin.
    }

    private static void TryInspectWithUnityExplorer(GameObject go)
    {
        try
        {
            UnityExplorer.InspectorManager.Inspect(go);
            UnityExplorer.UI.UIManager.ShowMenu = true;
        }
        catch { }
    }
}
