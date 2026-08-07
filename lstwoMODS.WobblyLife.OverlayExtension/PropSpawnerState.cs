using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using lstwoMODS.WobblyLife.SharedObjects;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.OverlayExtension;

internal class PropTreeNode
{
    public string Segment  { get; }
    public string FullPath { get; }

    public SortedDictionary<string, PropTreeNode> Children { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public PropCacheEntry? Entry { get; set; }

    public PropTreeNode(string segment = "", string fullPath = "")
    {
        Segment  = segment;
        FullPath = fullPath;
    }

    public static PropTreeNode Build(IEnumerable<PropCacheEntry> entries)
    {
        var root = new PropTreeNode();

        foreach (var entry in entries)
        {
            var key = entry.LoadKey ?? entry.Name ?? "";
            if (string.IsNullOrWhiteSpace(key)) continue;

            var parts = key.Split('/');
            var node  = root;
            var path  = "";

            for (int i = 0; i < parts.Length; i++)
            {
                var raw = parts[i];
                var seg = (i == parts.Length - 1 && raw.IndexOf('.') >= 0)
                    ? raw.Substring(0, raw.LastIndexOf('.'))
                    : raw;
                if (string.IsNullOrEmpty(seg)) continue;

                path = path.Length > 0 ? path + "/" + seg : seg;

                if (!node.Children.TryGetValue(seg, out var child))
                {
                    child = new PropTreeNode(seg, path);
                    node.Children[seg] = child;
                }
                node = child;
            }

            node.Entry = entry;
        }

        return root;
    }
}

internal class SpawnedPropEntry
{
    public int             SpawnId      { get; set; }
    public string?         Address      { get; set; }
    public string?         Name         { get; set; }
    public bool            Networked    { get; set; }
    public string?         GroupId      { get; set; }
    public PropCacheEntry? LibraryEntry { get; set; }
}

/// <summary>Overlay-local view state. Nothing here belongs to the mod — the active group and the
/// groups themselves live on the game side.</summary>
internal class PropSpawnerUiState
{
    public bool FavOpen    { get; set; } = true;
    public bool NetOpen    { get; set; } = true;
    public bool NnetOpen   { get; set; }
    public bool CustomOpen { get; set; } = true;
    public bool GroupsOpen { get; set; } = true;

    public string?      SelectedGroupId  { get; set; }
    public List<string> ExpandedGroupIds { get; set; } = new();
}

internal class PropSpawnerState
{
    public bool IsLoaded { get; private set; }

    /// <summary>Bumped only when the asset library itself is replaced. Still the trigger for
    /// rebuilding filters and dropping library selections.</summary>
    public int Version { get; private set; }

    /// <summary>Bumped on every applied snapshot. Deliberately separate from <see cref="Version"/>:
    /// snapshots arrive on every mutation, and clearing the user's multi-selection each time would
    /// make reassigning a dozen props impossible.</summary>
    public int GroupsRevision { get; private set; }

    public List<PropCacheEntry> Networked    { get; private set; } = new();
    public List<PropCacheEntry> NonNetworked { get; private set; } = new();

    public PropTreeNode NetworkedTree    { get; private set; } = new();
    public PropTreeNode NonNetworkedTree { get; private set; } = new();

    public List<string>             AllComponentNames { get; private set; } = new();
    public HashSet<string>          Favorites         { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CustomItemPackData> CustomItemPacks   { get; private set; } = new();

    // ── group snapshot, render-thread owned ─────────────────────────────────
    public string? ActiveGroupId { get; private set; }
    public List<PropGroupData>               Groups      { get; private set; } = new();
    public Dictionary<string, PropGroupData> GroupsById  { get; private set; } = new(StringComparer.Ordinal);
    public List<SpawnedPropEntry>            Spawned     { get; private set; } = new();
    public Dictionary<int, SpawnedPropEntry> SpawnedById { get; private set; } = new();

    /// <summary>Members per group in spawn order. The empty-string key is the ungrouped bucket.</summary>
    public Dictionary<string, List<SpawnedPropEntry>> MembersByGroup { get; private set; }
        = new(StringComparer.Ordinal);

    public int TruncatedSpawned { get; private set; }

    public string? StatusText  { get; private set; }
    public bool    StatusIsBad { get; private set; }

    public void ClearStatus()
    {
        StatusText  = null;
        StatusIsBad = false;
    }

    public PropSpawnerUiState Ui { get; private set; } = new();

    private Dictionary<string, PropCacheEntry> _byLoadKey = new(StringComparer.OrdinalIgnoreCase);

    // Message handlers run on the IPC reader thread while Render() runs on the window thread, so
    // nothing they touch may be a collection the renderer is walking. They hand over a reference;
    // the render thread does the rebuilding.
    private PropGroupsStateMessage? _pendingSnapshot;
    private PropSpawnStatusMessage? _pendingStatus;
    private int _lastRevision;

    private string? _dataDir;

    public void LoadDatabase(string cachePath)
    {
        try
        {
            var networkTypes = LoadNetworkTypes(cachePath);
            var cache = JsonConvert.DeserializeObject<PropCacheData>(File.ReadAllText(cachePath));
            if (cache?.Assets == null) return;

            var net    = new List<PropCacheEntry>();
            var nonNet = new List<PropCacheEntry>();

            foreach (var entry in cache.Assets.Where(e => e.IsGameObject))
            {
                bool isNetworked = entry.ComponentTypes?.Any(c => networkTypes.Contains(c)) ?? false;
                (isNetworked ? net : nonNet).Add(entry);
            }

            Networked        = net;
            NonNetworked     = nonNet;
            NetworkedTree    = PropTreeNode.Build(net);
            NonNetworkedTree = PropTreeNode.Build(nonNet);

            // One dictionary instead of a Concat().FirstOrDefault() scan of the whole asset
            // database per spawned prop, which a snapshot would otherwise repeat per prop.
            _byLoadKey = new Dictionary<string, PropCacheEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in net.Concat(nonNet))
            {
                var key = entry.LoadKey;
                if (!string.IsNullOrEmpty(key)) _byLoadKey[key!] = entry;
            }

            AllComponentNames = cache.Assets
                .Where(e => e.IsGameObject && e.ComponentTypes != null)
                .SelectMany(e => e.ComponentTypes)
                .Select(ShortTypeName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SetDataDir(cachePath);

            // Deliberately does not clear the spawned list: the mod owns it, and a database reload
            // mid-session must not blank a live build. The plugin asks for a fresh snapshot
            // instead, which re-resolves the library back-references below.
            RelinkLibraryEntries();

            IsLoaded = true;
            Version++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WobblyLife] PropSpawnerState.LoadDatabase failed: {ex.Message}");
        }
    }

    public void LoadCustomItems(List<CustomItemPackData> packs)
    {
        CustomItemPacks = packs ?? new List<CustomItemPackData>();
        Version++;
    }


    // ── snapshot hand-off ───────────────────────────────────────────────────

    /// <summary>IPC thread. Stores a reference and nothing else.</summary>
    public void QueueSnapshot(PropGroupsStateMessage msg) => Interlocked.Exchange(ref _pendingSnapshot, msg);

    /// <summary>IPC thread.</summary>
    public void QueueStatus(PropSpawnStatusMessage msg) => Interlocked.Exchange(ref _pendingStatus, msg);

    /// <summary>Render thread, before anything reads the collections. True when state changed.</summary>
    public bool Pump()
    {
        var changed = false;

        var status = Interlocked.Exchange(ref _pendingStatus, null);
        if (status != null)
        {
            StatusText  = BuildStatusText(status);
            StatusIsBad = status.Failed > 0;
            changed     = true;
        }

        var snapshot = Interlocked.Exchange(ref _pendingSnapshot, null);
        if (snapshot != null && snapshot.Revision > _lastRevision)
        {
            _lastRevision = snapshot.Revision;
            RebuildFromSnapshot(snapshot);
            GroupsRevision++;
            changed = true;
        }

        return changed;
    }

    private static string BuildStatusText(PropSpawnStatusMessage status)
    {
        var text = status.Summary ?? "";
        if (status.Errors is { Length: > 0 })
            text = text.Length > 0
                ? text + "\n" + string.Join("\n", status.Errors)
                : string.Join("\n", status.Errors);
        return text;
    }

    private void RebuildFromSnapshot(PropGroupsStateMessage msg)
    {
        // Fresh collections every time: the renderer may hold references to the old ones for the
        // remainder of the frame it is drawing.
        ActiveGroupId    = msg.ActiveGroupId;
        TruncatedSpawned = msg.Truncated;

        Groups     = msg.Groups ?? new List<PropGroupData>();
        GroupsById = Groups.ToDictionary(g => g.Id, g => g, StringComparer.Ordinal);

        var spawned     = new List<SpawnedPropEntry>();
        var spawnedById = new Dictionary<int, SpawnedPropEntry>();
        var byGroup     = new Dictionary<string, List<SpawnedPropEntry>>(StringComparer.Ordinal);

        foreach (var data in msg.Spawned ?? new List<SpawnedPropData>())
        {
            var entry = new SpawnedPropEntry
            {
                SpawnId      = data.SpawnId,
                Address      = data.Address,
                Name         = data.Name,
                Networked    = data.Networked,
                GroupId      = data.GroupId,
                LibraryEntry = Lookup(data.Address),
            };

            spawned.Add(entry);
            spawnedById[entry.SpawnId] = entry;

            var key = entry.GroupId ?? "";
            if (!byGroup.TryGetValue(key, out var list)) byGroup[key] = list = new List<SpawnedPropEntry>();
            list.Add(entry);
        }

        Spawned        = spawned;
        SpawnedById    = spawnedById;
        MembersByGroup = byGroup;
    }

    private void RelinkLibraryEntries()
    {
        foreach (var entry in Spawned)
            entry.LibraryEntry = Lookup(entry.Address);
    }

    private PropCacheEntry? Lookup(string? loadKey)
        => !string.IsNullOrEmpty(loadKey) && _byLoadKey.TryGetValue(loadKey!, out var found) ? found : null;


    // ── overlay-local persistence ───────────────────────────────────────────

    private string? FavoritesPath => _dataDir == null ? null : Path.Combine(_dataDir, "favorites.json");
    private string? UiStatePath   => _dataDir == null ? null : Path.Combine(_dataDir, "prop_ui_state.json");

    private void SetDataDir(string cachePath)
    {
        _dataDir = Path.GetDirectoryName(cachePath);
        LoadFavorites();
        LoadUiState();
    }

    public void ToggleFavorite(string loadKey)
    {
        if (!Favorites.Remove(loadKey))
            Favorites.Add(loadKey);
        WriteAtomic(FavoritesPath, JsonConvert.SerializeObject(Favorites.ToArray()));
    }

    /// <summary>
    /// Writes only when something actually changed. This runs every frame, so the check has to be
    /// cheaper than the write: CollapsingHeader reports its state every frame, and serializing to
    /// compare would allocate a string per frame for the privilege of throwing it away.
    /// </summary>
    public void SaveUiStateIfChanged()
    {
        if (!UiStateDiffers()) return;

        _saved = new PropSpawnerUiState
        {
            FavOpen          = Ui.FavOpen,
            NetOpen          = Ui.NetOpen,
            NnetOpen         = Ui.NnetOpen,
            CustomOpen       = Ui.CustomOpen,
            GroupsOpen       = Ui.GroupsOpen,
            SelectedGroupId  = Ui.SelectedGroupId,
            ExpandedGroupIds = new List<string>(Ui.ExpandedGroupIds),
        };

        WriteAtomic(UiStatePath, JsonConvert.SerializeObject(Ui));
    }

    private PropSpawnerUiState? _saved;

    private bool UiStateDiffers()
    {
        if (_saved == null) return true;

        if (_saved.FavOpen    != Ui.FavOpen)    return true;
        if (_saved.NetOpen    != Ui.NetOpen)    return true;
        if (_saved.NnetOpen   != Ui.NnetOpen)   return true;
        if (_saved.CustomOpen != Ui.CustomOpen) return true;
        if (_saved.GroupsOpen != Ui.GroupsOpen) return true;
        if (_saved.SelectedGroupId != Ui.SelectedGroupId) return true;

        if (_saved.ExpandedGroupIds.Count != Ui.ExpandedGroupIds.Count) return true;
        for (var i = 0; i < Ui.ExpandedGroupIds.Count; i++)
            if (_saved.ExpandedGroupIds[i] != Ui.ExpandedGroupIds[i]) return true;

        return false;
    }

    private void LoadFavorites()
    {
        var path = FavoritesPath;
        if (path == null || !File.Exists(path)) return;
        try
        {
            var keys = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(path));
            if (keys == null) return;
            Favorites.Clear();
            foreach (var k in keys) Favorites.Add(k);
        }
        catch { }
    }

    private void LoadUiState()
    {
        var path = UiStatePath;
        if (path == null || !File.Exists(path)) return;
        try
        {
            var loaded = JsonConvert.DeserializeObject<PropSpawnerUiState>(File.ReadAllText(path));
            if (loaded == null) return;
            loaded.ExpandedGroupIds ??= new List<string>();
            Ui = loaded;
            _saved = null;   // recomputed on the first SaveUiStateIfChanged
        }
        catch { }
    }

    /// <summary>Write to a temp file and swap it in, so a crash mid-write cannot truncate the
    /// real one. Only this process writes these files, so there is no cross-process contention.</summary>
    private static void WriteAtomic(string? path, string contents)
    {
        if (path == null) return;
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, contents);

            if (File.Exists(path)) File.Replace(temp, path, null);
            else                   File.Move(temp, path);
        }
        catch { }
    }

    internal static string ShortTypeName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return fullName;
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
    }

    private static HashSet<string> LoadNetworkTypes(string cachePath)
    {
        var companionPath = Path.Combine(Path.GetDirectoryName(cachePath)!, "network_types.json");
        if (!File.Exists(companionPath))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var types = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(companionPath));
        return types != null
            ? new HashSet<string>(types, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}

internal class PropCacheEntry
{
    [JsonProperty] public string   Name             { get; set; }
    [JsonProperty] public string   Address          { get; set; }
    [JsonProperty] public string   Guid             { get; set; }
    [JsonProperty] public string[] ComponentTypes   { get; set; }
    [JsonProperty] public string   ResourceTypeName { get; set; }

    [JsonIgnore] public string LoadKey      => Address ?? Guid;
    [JsonIgnore] public bool   IsGameObject => ResourceTypeName == "UnityEngine.GameObject";
}

internal class PropCacheData
{
    [JsonProperty] public List<PropCacheEntry> Assets { get; set; }
}
