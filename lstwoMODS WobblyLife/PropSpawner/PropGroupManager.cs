using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using lstwoMODS_Core;
using UnityEngine;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// The groups themselves: naming, ordering, and the one-file-per-group store that makes a build
/// shareable. Modelled on core's MacroManager, with one deliberate difference — group ids are
/// slugs rather than GUIDs, so a shared build arrives as <c>castle.json</c> and not as a hex blob.
/// </summary>
public static class PropGroupManager
{
    private const string StorageId    = "WobblyLife";
    private const string GroupsFolder = "prop_groups";
    private const string ActiveGroupBagKey = "propspawner.activeGroup";

    private static string GroupKey(string id) => $"{GroupsFolder}/{id}";

    private static List<PropGroup> _groups;
    private static string _activeGroupId;
    private static bool   _activeGroupLoaded;

    /// <summary>Group id -> whether its live props differ from what is on disk.</summary>
    private static readonly Dictionary<string, bool> _dirty = new();

    /// <summary>Fires when a group is created, renamed, deleted, saved or reloaded.</summary>
    public static event Action Changed;

    public static IReadOnlyList<PropGroup> Groups
    {
        get { EnsureLoaded(); return _groups; }
    }

    /// <summary>
    /// The group new spawns land in, null for ungrouped. Owned here rather than by the overlay so
    /// that a prop spawned by a macro or a chat command lands in the right group too, and so the
    /// choice survives an overlay restart.
    /// </summary>
    public static string ActiveGroupId
    {
        get
        {
            if (!_activeGroupLoaded)
            {
                _activeGroupLoaded = true;
                _activeGroupId = DataStorage.LoadFromBag<string>(StorageId, ActiveGroupBagKey);
            }

            // A group deleted while it was active must not leave a dangling id behind.
            if (_activeGroupId != null && Find(_activeGroupId) == null) _activeGroupId = null;
            return _activeGroupId;
        }
        set
        {
            if (string.Equals(_activeGroupId, value, StringComparison.Ordinal)) return;
            _activeGroupId     = value;
            _activeGroupLoaded = true;
            DataStorage.SaveToBag(StorageId, ActiveGroupBagKey, value);
            NotifyChanged();
        }
    }

    public static PropGroup Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        return _groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDirty(string groupId)
        => groupId != null && _dirty.TryGetValue(groupId, out var d) && d;

    public static bool HasFile(string groupId)
        => groupId != null && DataStorage.Exists(StorageId, GroupKey(groupId));

    /// <summary>Absolute path of the file backing a group — what to copy in order to share it.
    /// Flushes any pending write first, so the file is current when the path is handed out.</summary>
    public static string GroupFilePath(string groupId)
        => groupId == null ? null : DataStorage.GetFilePath(StorageId, GroupKey(groupId));

    private static readonly Dictionary<string, string> _pathCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Same path, memoised. The snapshot carries the path for a tooltip and is rebuilt as often
    /// as once a frame; <see cref="GroupFilePath"/> creates directories and flushes on every call,
    /// which is not something to do at that rate.
    /// </summary>
    public static string DisplayFilePath(string groupId)
    {
        if (groupId == null) return null;
        if (_pathCache.TryGetValue(groupId, out var cached)) return cached;

        var path = GroupFilePath(groupId);
        _pathCache[groupId] = path;
        return path;
    }


    // ── mutations ────────────────────────────────────────────────────────────

    public static PropGroup CreateGroup(string name, IEnumerable<int> assignSpawnIds = null, bool makeActive = true)
    {
        EnsureLoaded();

        var group = new PropGroup
        {
            Id         = UniqueSlug(name),
            Name       = string.IsNullOrWhiteSpace(name) ? "New Group" : name.Trim(),
            CreatedUtc = PropGroup.NowUnix(),
        };

        _groups.Add(group);
        Sort();

        if (assignSpawnIds != null) PropSpawnManager.Reassign(assignSpawnIds, group.Id);
        if (makeActive) ActiveGroupId = group.Id;

        // Written straight away so the group survives a crash and shows up as a file the user can
        // find, even before anything has been captured into it.
        Persist(group);
        NotifyChanged();
        return group;
    }

    public static void RenameGroup(string groupId, string name)
    {
        var group = Find(groupId);
        if (group == null || string.IsNullOrWhiteSpace(name)) return;

        // The id is the file name, and renaming the file would break anyone who already has a
        // copy of it. Only the display name changes.
        group.Name        = name.Trim();
        group.ModifiedUtc = PropGroup.NowUnix();

        Persist(group);
        Sort();
        NotifyChanged();
    }

    public static void DeleteGroup(string groupId, bool deleteFile)
    {
        var group = Find(groupId);
        if (group == null) return;

        EnsureLoaded();
        _groups.Remove(group);
        _dirty.Remove(group.Id);
        _pathCache.Remove(group.Id);

        if (deleteFile) DataStorage.Delete(StorageId, GroupKey(group.Id));

        // Live props outlive their group; they just become ungrouped.
        PropSpawnManager.ForgetGroup(group.Id);

        if (string.Equals(_activeGroupId, group.Id, StringComparison.Ordinal)) ActiveGroupId = null;

        NotifyChanged();
    }

    /// <summary>Re-scans the folder, so a file someone shared can be dropped in and picked up.</summary>
    public static void Refresh()
    {
        _groups = null;
        _dirty.Clear();
        _pathCache.Clear();
        EnsureLoaded();
        NotifyChanged();
    }

    /// <summary>Writes a group to its file. Used after a capture.</summary>
    public static void Persist(PropGroup group)
    {
        if (group == null) return;
        DataStorage.Save(StorageId, GroupKey(group.Id), group);
    }


    // ── dirty tracking ───────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes every group's unsaved-changes flag. Called once a second from
    /// <see cref="PropSpawnManager"/>'s tick, never per frame. Returns whether anything flipped.
    /// </summary>
    public static bool RefreshDirtyFlags()
    {
        EnsureLoaded();
        var changed = false;
        var byGroup = PropSpawnManager.LiveByGroup();

        foreach (var group in _groups)
        {
            var live  = byGroup.TryGetValue(group.Id, out var l) ? l : EmptyLive;
            var value = ComputeDirty(group, live);
            if (_dirty.TryGetValue(group.Id, out var previous) && previous == value) continue;
            _dirty[group.Id] = value;
            changed = true;
        }

        return changed;
    }

    private static readonly List<PropSpawnManager.SpawnedProp> EmptyLive = new();

    public static void ClearDirty(string groupId)
    {
        if (groupId != null) _dirty[groupId] = false;
    }

    private const float PositionEpsilon = 0.001f;
    private const float RotationEpsilon = 0.1f;     // degrees
    private const float ScaleEpsilon    = 0.001f;

    private static bool ComputeDirty(PropGroup group, List<PropSpawnManager.SpawnedProp> live)
    {
        var saved = group.Props ?? new List<PropPlacement>();

        if (live.Count != saved.Count) return live.Count > 0 || saved.Count > 0;

        var byPlacement = new Dictionary<string, PropPlacement>(StringComparer.Ordinal);
        foreach (var placement in saved)
            if (!string.IsNullOrEmpty(placement.Id)) byPlacement[placement.Id] = placement;

        foreach (var prop in live)
        {
            // No link to a saved placement means it was spawned or reassigned since the last save.
            if (string.IsNullOrEmpty(prop.PlacementId)) return true;
            if (!byPlacement.TryGetValue(prop.PlacementId, out var placement)) return true;
            if (HasMoved(prop, placement)) return true;
        }

        return false;
    }

    private static bool HasMoved(PropSpawnManager.SpawnedProp prop, PropPlacement placement)
    {
        var t = prop.Object.transform;

        if (Vector3.Distance(t.position, placement.Position) > PositionEpsilon) return true;
        if (Quaternion.Angle(t.rotation, placement.Rotation()) > RotationEpsilon) return true;
        if (Vector3.Distance(t.localScale, placement.Scale) > ScaleEpsilon) return true;
        if (prop.Object.activeSelf != placement.Active) return true;
        if (!string.Equals(prop.Object.name, placement.Name, StringComparison.Ordinal)) return true;

        return false;
    }


    // ── loading ──────────────────────────────────────────────────────────────

    private static void EnsureLoaded()
    {
        if (_groups != null) return;
        _groups = new List<PropGroup>();

        foreach (var id in DataStorage.ListKeys(StorageId, GroupsFolder))
        {
            if (!IsSafeStem(id)) continue;

            PropGroup group;
            try
            {
                group = DataStorage.Load<PropGroup>(StorageId, GroupKey(id));
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning($"[PropSpawner] Skipping unreadable group \"{id}\": {ex.Message}");
                continue;
            }

            if (group == null) continue;

            if (group.Version > PropGroup.CurrentVersion)
            {
                Plugin.LogSource.LogWarning(
                    $"[PropSpawner] Skipping group \"{id}\": file version {group.Version} is newer than " +
                    $"this build supports ({PropGroup.CurrentVersion}).");
                continue;
            }

            // The file name is the identity, so a shared file dropped into the folder imports as
            // itself no matter what id the person who exported it happened to have.
            group.Id     = id;
            group.Props ??= new List<PropPlacement>();
            if (string.IsNullOrWhiteSpace(group.Name)) group.Name = id;

            _groups.Add(group);
        }

        Sort();
    }

    private static void Sort()
        => _groups.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private static bool IsSafeStem(string stem)
        => !string.IsNullOrWhiteSpace(stem) && stem.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string UniqueSlug(string name)
    {
        var baseSlug = Slugify(name);
        var slug     = baseSlug;
        var n        = 2;

        while (_groups.Any(g => string.Equals(g.Id, slug, StringComparison.OrdinalIgnoreCase)) ||
               DataStorage.Exists(StorageId, GroupKey(slug)))
        {
            slug = $"{baseSlug}-{n++}";
        }

        return slug;
    }

    private static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "group";

        var sb = new StringBuilder(name.Length);
        var lastWasDash = false;

        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "group" : slug;
    }

    internal static void NotifyChanged() => Changed?.Invoke();
}
