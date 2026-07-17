using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public string          Address      { get; set; }
    public string          Name         { get; set; }
    public bool            Networked    { get; set; }
    public PropCacheEntry? LibraryEntry { get; set; }
}

internal class PropSpawnerState
{
    public bool IsLoaded { get; private set; }
    public int  Version  { get; private set; }

    public List<PropCacheEntry> Networked    { get; private set; } = new();
    public List<PropCacheEntry> NonNetworked { get; private set; } = new();

    public PropTreeNode NetworkedTree    { get; private set; } = new();
    public PropTreeNode NonNetworkedTree { get; private set; } = new();

    public List<SpawnedPropEntry>  SpawnedProps      { get; } = new();
    public List<string>           AllComponentNames { get; private set; } = new();
    public HashSet<string>        Favorites         { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CustomItemPackData> CustomItemPacks { get; private set; } = new();

    private string? _favoritesPath;

    public void LoadDatabase(string cachePath)
    {
        try
        {
            SpawnedProps.Clear();

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

            AllComponentNames = cache.Assets
                .Where(e => e.IsGameObject && e.ComponentTypes != null)
                .SelectMany(e => e.ComponentTypes)
                .Select(ShortTypeName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _favoritesPath = Path.Combine(Path.GetDirectoryName(cachePath)!, "favorites.json");
            LoadFavorites();

            IsLoaded = true;
            Version++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WobblyLife] PropSpawnerState.LoadDatabase failed: {ex.Message}");
        }
    }

    public void AddSpawned(PropSpawnedMessage msg)
    {
        var libEntry = Networked.Concat(NonNetworked)
            .FirstOrDefault(e => string.Equals(e.LoadKey, msg.Address, StringComparison.OrdinalIgnoreCase));

        SpawnedProps.Insert(0, new SpawnedPropEntry
        {
            SpawnId      = msg.SpawnId,
            Address      = msg.Address,
            Name         = msg.Name,
            Networked    = msg.Networked,
            LibraryEntry = libEntry
        });

        while (SpawnedProps.Count > 50)
            SpawnedProps.RemoveAt(SpawnedProps.Count - 1);
    }

    public void RemoveSpawned(int spawnId)
        => SpawnedProps.RemoveAll(e => e.SpawnId == spawnId);

    public void ClearSpawned() => SpawnedProps.Clear();

    public void LoadCustomItems(List<CustomItemPackData> packs)
    {
        CustomItemPacks = packs ?? new List<CustomItemPackData>();
        Version++;
    }

    public void ToggleFavorite(string loadKey)
    {
        if (!Favorites.Remove(loadKey))
            Favorites.Add(loadKey);
        SaveFavorites();
    }

    private void LoadFavorites()
    {
        if (_favoritesPath == null || !File.Exists(_favoritesPath)) return;
        try
        {
            var keys = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(_favoritesPath));
            if (keys == null) return;
            Favorites.Clear();
            foreach (var k in keys) Favorites.Add(k);
        }
        catch { }
    }

    private void SaveFavorites()
    {
        if (_favoritesPath == null) return;
        try { File.WriteAllText(_favoritesPath, JsonConvert.SerializeObject(Favorites.ToArray())); }
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
