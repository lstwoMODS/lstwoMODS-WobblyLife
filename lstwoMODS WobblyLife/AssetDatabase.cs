using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using HawkNetworking;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace lstwoMODS_WobblyLife;

public static class AssetDatabase
{
    public static readonly string CachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lstwoMODS", "WobblyLife", "asset_database.json");

    private static readonly List<AssetEntry> _entries = new();
    private static readonly List<AssetEntry> _sceneEntries = new();
    private static readonly Dictionary<string, AssetEntry> _keyIndex = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<AssetEntry>> _labelIndex = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsInitialized { get; private set; }
    public static IReadOnlyList<AssetEntry> Entries => _entries;
    public static IReadOnlyList<AssetEntry> SceneEntries => _sceneEntries;

    /// <summary>Fires on the Unity main thread once all entries have component data (cache or full scan).</summary>
    public static event Action OnReady;

    /// <summary>True once the database is initialized and all entries have been component-scanned.</summary>
    public static bool IsReady => IsInitialized && _entries.Count > 0 && _entries.All(e => e.ComponentsLoaded);

    public class AssetEntry
    {
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Address { get; set; }
        /// <summary>32-char hex Unity asset GUID, if the asset is addressed by one.</summary>
        [JsonProperty] public string Guid { get; set; }
        [JsonProperty] public string[] Labels { get; set; } = Array.Empty<string>();
        /// <summary>Full type names of all components. Null until LoadComponentsAsync is called.</summary>
        [JsonProperty] public string[] ComponentTypes   { get; set; }
        /// <summary>HawkNetworkBehaviour.assetID scraped at scan time. Null for non-networked prefabs.</summary>
        [JsonProperty] public string   NetworkAssetId   { get; set; }

        [JsonProperty] public string ResourceTypeName { get; set; }

        [JsonIgnore] public bool ComponentsLoaded => ComponentTypes != null;
        [JsonIgnore] public bool IsGameObject => ResourceTypeName == "UnityEngine.GameObject";
        [JsonIgnore] public bool IsAssetBundle => ResourceTypeName != null && ResourceTypeName.Contains("IAssetBundleResource");
        [JsonIgnore] public bool IsScene => ResourceTypeName != null && ResourceTypeName.Contains("SceneInstance");

        /// <summary>Best key to pass to Addressables.LoadAssetAsync, prefers Address over Guid.</summary>
        [JsonIgnore] public string LoadKey => Address ?? Guid;

        public bool HasComponent(string typeName)
            => ComponentTypes != null && ComponentTypes.Any(t =>
                t.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                t.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase));

        public bool HasComponent<T>() => HasComponent(typeof(T).FullName);

        public override string ToString() => $"{Name} [{LoadKey}]";
    }

    /// <summary>
    /// Populates the catalog from Addressables metadata (fast, no prefabs loaded).
    /// Use Plugin._StartCoroutine(PropLoader.InitializeAsync()).
    /// </summary>
    public static IEnumerator InitializeAsync(bool forceRefresh = false)
    {
        if (IsInitialized && !forceRefresh) yield break;

        _entries.Clear();
        _sceneEntries.Clear();
        _keyIndex.Clear();
        _labelIndex.Clear();

        if (!forceRefresh && TryLoadCache())
        {
            IsInitialized = true;
            Plugin.LogSource.LogInfo($"[AssetDatabase] Loaded {_entries.Count} entries, {_sceneEntries.Count} scenes from cache.");
            FireOnReady();
            yield break;
        }
        
        var initHandle = Addressables.InitializeAsync();

        if (initHandle.IsValid())
        {
            yield return initHandle;
            
            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Plugin.LogSource.LogWarning("[AssetDatabase] Addressables init failed; using cache only.");
                TryLoadCache();
                IsInitialized = true;
                yield break;
            }
        }

        ScanCatalog();
        IsInitialized = true;
        Plugin.LogSource.LogInfo($"[AssetDatabase] Cataloged {_entries.Count} entries, {_sceneEntries.Count} scenes.");
        SaveCache();
    }

    private static void ScanCatalog()
    {
        var byPrimary = new Dictionary<string, (string internalId, HashSet<string> keys, Type resourceType)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var locator in Addressables.ResourceLocators)
        {
            foreach (var rawKey in locator.Keys)
            {
                if (rawKey is not string key) continue;
                if (!locator.Locate(key, null, out IList<IResourceLocation> locs)) continue;

                foreach (var loc in locs)
                {
                    if (!byPrimary.TryGetValue(loc.PrimaryKey, out var data))
                        byPrimary[loc.PrimaryKey] = data = (loc.InternalId, new HashSet<string>(StringComparer.OrdinalIgnoreCase), loc.ResourceType);
                    data.keys.Add(key);
                }
            }
        }

        foreach (var kvp in byPrimary)
        {
            var internalId = kvp.Value.internalId;
            var keys = kvp.Value.keys;

            var guid = keys.FirstOrDefault(IsUnityGuid);
            var address = keys.Where(k => !IsUnityGuid(k)).OrderBy(k => k.Length).FirstOrDefault();
            if (guid == null && address == null) continue;

            var effectiveAddress = address ?? guid;
            var labels = keys.Where(k => k != address && k != guid).ToArray();

            Register(new AssetEntry
            {
                Name = ExtractName(internalId, effectiveAddress),
                Address = address,
                Guid = guid,
                Labels = labels,
                ResourceTypeName = kvp.Value.resourceType?.FullName
            });
        }
    }
    

    /// <summary>
    /// Loads a single prefab and records its component type names.
    /// Updates the cache file after a successful load.
    /// </summary>
    public static IEnumerator LoadComponentsAsync(AssetEntry entry)
    {
        if (entry.ComponentsLoaded) yield break;

        if (entry.IsAssetBundle || entry.IsScene || entry.ResourceTypeName == null)
        {
            entry.ComponentTypes = Array.Empty<string>();
            yield break;
        }

        if (entry.IsGameObject)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(entry.LoadKey);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                entry.ComponentTypes = handle.Result
                    .GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType()?.FullName)
                    .Where(n => n != null)
                    .ToArray();
                entry.NetworkAssetId = handle.Result.GetComponent<HawkNetworkBehaviour>()?.assetID;
                Addressables.Release(handle);
            }
            else
            {
                entry.ComponentTypes = Array.Empty<string>();
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
        else
        {
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(entry.LoadKey);
            yield return handle;
            if (handle.IsValid()) Addressables.Release(handle);
            entry.ComponentTypes = Array.Empty<string>();
        }
    }

    /// <summary>
    /// Iterates every entry and loads its component data. Slow, run once and cache.
    /// onProgress(loaded, total) fires after each entry.
    /// </summary>
    public static IEnumerator ScanAllComponentsAsync(Action<int, int> onProgress = null)
    {
        int total = _entries.Count + _sceneEntries.Count;
        int done = 0;

        foreach (var entry in _entries)
        {
            yield return LoadComponentsAsync(entry);
            onProgress?.Invoke(++done, total);
        }

        foreach (var entry in _sceneEntries)
        {
            yield return LoadComponentsAsync(entry);
            onProgress?.Invoke(++done, total);
        }

        SaveCache();
        FireOnReady();
    }


    private class CacheData
    {
        [JsonProperty] public string GameVersion { get; set; }
        [JsonProperty] public List<AssetEntry> Assets { get; set; } = new();
        [JsonProperty] public List<AssetEntry> Scenes { get; set; } = new();
    }

    private static bool TryLoadCache()
    {
        if (!File.Exists(CachePath)) return false;
        try
        {
            var cache = JsonConvert.DeserializeObject<CacheData>(File.ReadAllText(CachePath));
            if (cache?.Assets is not { Count: > 0 }) return false;
            if (cache.GameVersion != Application.version) return false;
            if (cache.Assets.Any(e => e.ResourceTypeName == null)) return false;
            foreach (var e in cache.Assets) Register(e);
            if (cache.Scenes != null)
                foreach (var e in cache.Scenes) Register(e);
            return true;
        }
        catch { return false; }
    }

    private static void FireOnReady()
    {
        try { OnReady?.Invoke(); }
        catch (Exception ex) { Plugin.LogSource.LogWarning($"[AssetDatabase] OnReady handler threw: {ex.Message}"); }
    }

    public static void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var cache = new CacheData { GameVersion = Application.version, Assets = _entries, Scenes = _sceneEntries };
            File.WriteAllText(CachePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"[AssetDatabase] Cache save failed: {ex.Message}");
        }
    }

    private static void Register(AssetEntry entry)
    {
        if (entry.IsScene)
            _sceneEntries.Add(entry);
        else
            _entries.Add(entry);

        if (entry.Address != null) _keyIndex[entry.Address] = entry;
        if (entry.Guid != null) _keyIndex[entry.Guid] = entry;

        foreach (var label in entry.Labels)
        {
            if (!_labelIndex.TryGetValue(label, out var list))
                _labelIndex[label] = list = new();
            list.Add(entry);
        }
    }


    /// <summary>O(1) lookup by address or GUID.</summary>
    public static AssetEntry Get(string key)
        => _keyIndex.TryGetValue(key, out var e) ? e : null;

    /// <summary>Exact case-insensitive Name match, with key fallback.</summary>
    public static AssetEntry FindExact(string name)
    {
        if (_keyIndex.TryGetValue(name, out var direct)) return direct;
        return _entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Substring match on Name and Address.</summary>
    public static IEnumerable<AssetEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _entries;
        var q = query.ToLowerInvariant();
        return _entries.Where(e =>
            e.Name.ToLowerInvariant().Contains(q) ||
            (e.Address?.ToLowerInvariant().Contains(q) ?? false));
    }

    /// <summary>
    /// Score-ranked approximate match across Name. Handles substrings, token matches, and typos.
    /// Returns (entry, score) pairs ordered by descending relevance.
    /// </summary>
    public static IEnumerable<(AssetEntry Entry, float Score)> FuzzySearch(
        string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _entries.Take(maxResults).Select(e => (e, 1f));

        return _entries
            .Select(e => (Entry: e, Score: ComputeScore(query, e.Name)))
            .Where(x => x.Score > 0.15f)
            .OrderByDescending(x => x.Score)
            .Take(maxResults);
    }

    /// <summary>All entries that carry the given Addressables label.</summary>
    public static IEnumerable<AssetEntry> GetByLabel(string label)
        => _labelIndex.TryGetValue(label, out var list) ? list : Enumerable.Empty<AssetEntry>();

    /// <summary>
    /// All entries whose loaded component set includes typeName.
    /// Only returns results for entries where ComponentsLoaded is true.
    /// </summary>
    public static IEnumerable<AssetEntry> GetByComponent(string typeName)
        => _entries.Where(e => e.HasComponent(typeName));

    /// <inheritdoc cref="GetByComponent(string)"/>
    public static IEnumerable<AssetEntry> GetByComponent<T>()
        => GetByComponent(typeof(T).FullName);

    /// <summary>All known entries.</summary>
    public static IEnumerable<AssetEntry> GetAll() => _entries;


    private static float ComputeScore(string query, string target)
    {
        if (string.IsNullOrEmpty(target)) return 0f;

        var q = query.ToLowerInvariant();
        var t = target.ToLowerInvariant();

        if (t == q) return 1.0f;
        if (t.StartsWith(q)) return 0.9f;
        if (t.Contains(q)) return 0.75f;

        var qTokens = Tokenize(q);
        var tTokens = Tokenize(t);
        if (qTokens.Length > 0 && tTokens.Length > 0)
        {
            int matched = qTokens.Count(qt => tTokens.Any(tt => tt.StartsWith(qt)));
            if (matched > 0) return 0.4f + 0.35f * ((float)matched / qTokens.Length);
        }

        if (q.Length <= 16)
        {
            var tSlice = t.Length > 16 ? t.Substring(0, 16) : t;
            float sim = 1f - (float)LevenshteinDistance(q, tSlice) / Math.Max(q.Length, tSlice.Length);
            if (sim > 0.5f) return sim * 0.35f;
        }

        return 0f;
    }

    private static string[] Tokenize(string s)
        => Regex.Split(s, @"[\s_\-]+|(?<=[a-z])(?=[A-Z])")
            .Where(t => t.Length > 0)
            .ToArray();

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        for (int j = 1; j <= b.Length; j++)
        {
            int cost = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
        }

        return dp[a.Length, b.Length];
    }


    private static bool IsUnityGuid(string key)
        => key.Length == 32 && key.All(IsHexChar);

    private static bool IsHexChar(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static string ExtractName(string internalId, string fallback)
    {
        var src = string.IsNullOrEmpty(internalId) ? fallback : internalId;
        var normalized = src.Replace('\\', '/');
        var last = normalized.Contains('/')
            ? normalized.Substring(normalized.LastIndexOf('/') + 1)
            : normalized;
        var name = last.Contains('.')
            ? last.Substring(0, last.LastIndexOf('.'))
            : last;
        return string.IsNullOrEmpty(name) ? fallback : name;
    }
}
