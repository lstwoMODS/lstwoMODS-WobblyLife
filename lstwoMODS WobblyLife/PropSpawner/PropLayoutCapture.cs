using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// Reads a group's live props out of the world and into its file.
/// </summary>
public static class PropLayoutCapture
{
    /// <summary>
    /// Replaces the group's saved layout with exactly the props that are live right now.
    ///
    /// "Exactly" is the point: if a prop was destroyed, it leaves the file. That keeps Save
    /// predictable — what you see is what gets written — and the UI warns before a save that
    /// would drop saved props, so nothing goes quietly.
    /// </summary>
    public static int SaveGroupFromLive(string groupId)
    {
        var group = PropGroupManager.Find(groupId);
        if (group == null) return 0;

        var live = PropSpawnManager.InGroup(groupId).ToList();

        var placements = BuildPlacements(live);
        placements = SortParentsFirst(placements);

        group.Props       = placements;
        group.ModifiedUtc = PropGroup.NowUnix();
        if (group.CreatedUtc == 0) group.CreatedUtc = group.ModifiedUtc;
        group.GameVersion = Application.version;
        group.SceneName   = SceneManager.GetActiveScene().name;

        // Anchor for "spawn rebased to me": where the author was standing when they saved.
        if (PropSpawnManager.TryGetSpawnPose(out var playerPos, out var playerYaw))
        {
            group.Origin    = playerPos;
            group.OriginYaw = playerYaw;
        }
        else if (placements.Count > 0)
        {
            // No player (main menu, loading): fall back to the layout's own centroid so the
            // rebase still produces something sensible.
            var sum = placements.Aggregate(Vector3.zero, (acc, p) => acc + (Vector3)p.Position);
            group.Origin    = sum / placements.Count;
            group.OriginYaw = 0f;
        }

        PropGroupManager.Persist(group);
        PropGroupManager.ClearDirty(groupId);
        PropGroupManager.NotifyChanged();

        return placements.Count;
    }

    private static List<PropPlacement> BuildPlacements(List<PropSpawnManager.SpawnedProp> live)
    {
        // Which transform belongs to which member, so parenting between props can be detected.
        var rootToIndex = new Dictionary<Transform, int>();
        for (var i = 0; i < live.Count; i++)
            rootToIndex[live[i].Object.transform] = i;

        var placements = new List<PropPlacement>(live.Count);

        foreach (var prop in live)
        {
            var go = prop.Object;
            var t  = go.transform;

            var placement = new PropPlacement
            {
                // Reuse the existing link when there is one, so a save after a restore keeps the
                // same placement identity and dirty tracking stays anchored.
                Id       = string.IsNullOrEmpty(prop.PlacementId) ? Guid.NewGuid().ToString("N") : prop.PlacementId,
                Source   = prop.Source?.Clone() ?? new PropIdentity(),
                Name     = go.name,
                Active   = go.activeSelf,
                Position = t.position,
                Rot      = t.rotation,
                RotEuler = t.eulerAngles,
                Scale    = t.localScale,
                Toggles  = PropComponentState.CaptureDeviations(go, prop.ToggleBaseline),
            };

            // Keep the resolution hints current: the address may have gained a network asset id
            // since the prop was spawned, and a shared file is only as good as its hints.
            PropAddressResolver.PopulateHints(placement.Source);

            AssignParent(placement, t, rootToIndex);

            placements.Add(placement);
            prop.PlacementId = placement.Id;
        }

        return placements;
    }

    /// <summary>
    /// Finds the nearest ancestor that is another prop in this group. Ancestors that aren't
    /// members — chunk scene roots, whatever the game reparents things under — are ignored, so
    /// the prop simply counts as a world root.
    /// </summary>
    private static void AssignParent(PropPlacement placement, Transform t, Dictionary<Transform, int> rootToIndex)
    {
        for (var ancestor = t.parent; ancestor != null; ancestor = ancestor.parent)
        {
            if (!rootToIndex.TryGetValue(ancestor, out var index)) continue;

            placement.ParentIndex   = index;
            // The prop may hang off a sub-transform of the parent prop rather than its root, which
            // is a normal thing to do in UnityExplorer. Record which one.
            placement.ParentPath    = PropComponentState.RelativePath(ancestor, t.parent);
            placement.LocalPosition = t.localPosition;
            placement.LocalRot      = t.localRotation;
            placement.LocalScale    = t.localScale;
            return;
        }
    }

    /// <summary>
    /// Reorders so a parent always precedes its children, and rewrites the indices to match.
    /// Restore then needs a single forward pass, and the file reads in the order things nest.
    /// </summary>
    private static List<PropPlacement> SortParentsFirst(List<PropPlacement> placements)
    {
        var order   = new List<int>(placements.Count);
        var state   = new byte[placements.Count];   // 0 = unvisited, 1 = visiting, 2 = done

        void Visit(int index)
        {
            if (state[index] != 0) return;          // done, or a cycle we refuse to follow
            state[index] = 1;

            var parent = placements[index].ParentIndex;
            if (parent >= 0 && parent < placements.Count && state[parent] == 0) Visit(parent);

            state[index] = 2;
            order.Add(index);
        }

        for (var i = 0; i < placements.Count; i++) Visit(i);

        var oldToNew = new int[placements.Count];
        for (var newIndex = 0; newIndex < order.Count; newIndex++) oldToNew[order[newIndex]] = newIndex;

        var sorted = new List<PropPlacement>(placements.Count);
        foreach (var oldIndex in order)
        {
            var placement = placements[oldIndex];
            if (placement.ParentIndex >= 0 && placement.ParentIndex < oldToNew.Length)
                placement.ParentIndex = oldToNew[placement.ParentIndex];
            sorted.Add(placement);
        }

        return sorted;
    }
}
