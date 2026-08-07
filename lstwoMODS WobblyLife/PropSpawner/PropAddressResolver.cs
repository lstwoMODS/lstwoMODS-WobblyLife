using System;
using System.Collections.Generic;
using System.Linq;
using CustomItems;
using lstwoMODS_WobblyLife.CustomItems;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// Turns a stored prop identity back into something the game will actually load.
/// </summary>
public static class PropAddressResolver
{
    private const string ContentPrefix = "Assets/Content/";
    private const string PrefabSuffix  = ".prefab";

    /// <summary>
    /// Keys to try, best first, for a prop that was spawned interactively (no stored hints).
    /// </summary>
    public static IEnumerable<string> Fallbacks(string address) => Fallbacks(address, null, null, null);

    /// <summary>
    /// Keys to try, best first. The hints come from the group file, so a layout shared from a
    /// different game version can still land even if the addressables key has moved.
    /// </summary>
    public static IEnumerable<string> Fallbacks(string address, string networkAssetId, string guid, string assetName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool IsNew(string key) => !string.IsNullOrEmpty(key) && seen.Add(key);

        // Hint captured when the group was saved, then the live database's view of the same asset.
        if (IsNew(networkAssetId)) yield return networkAssetId;

        var entry = string.IsNullOrEmpty(address) ? null : AssetDatabase.Get(address);
        if (IsNew(entry?.NetworkAssetId)) yield return entry.NetworkAssetId;

        if (IsNew(address)) yield return address;

        if (!string.IsNullOrEmpty(address))
        {
            if (address.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = address.Substring(ContentPrefix.Length);
                if (IsNew(stripped)) yield return stripped;

                if (stripped.EndsWith(PrefabSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    var bare = stripped.Substring(0, stripped.Length - PrefabSuffix.Length);
                    if (IsNew(bare)) yield return bare;
                }
            }
            else if (address.EndsWith(PrefabSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var bare = address.Substring(0, address.Length - PrefabSuffix.Length);
                if (IsNew(bare)) yield return bare;
            }
        }

        if (IsNew(entry?.Guid)) yield return entry.Guid;
        if (IsNew(guid)) yield return guid;

        // Last resort: the asset moved and only its name still matches.
        if (!string.IsNullOrEmpty(assetName))
        {
            var byName = AssetDatabase.FindExact(assetName);
            if (IsNew(byName?.NetworkAssetId)) yield return byName.NetworkAssetId;
            if (IsNew(byName?.LoadKey)) yield return byName.LoadKey;
        }
    }

    /// <inheritdoc cref="Fallbacks(string,string,string,string)"/>
    public static IEnumerable<string> Fallbacks(PropIdentity identity)
        => Fallbacks(identity?.Address, identity?.NetworkAssetId, identity?.Guid, identity?.AssetName);

    /// <summary>Fills in the resolution hints from the live asset database.</summary>
    public static void PopulateHints(PropIdentity identity)
    {
        if (identity == null || identity.IsCustomItem || string.IsNullOrEmpty(identity.Address)) return;

        var entry = AssetDatabase.Get(identity.Address);
        if (entry == null) return;

        identity.NetworkAssetId = entry.NetworkAssetId;
        identity.Guid           = entry.Guid;
        identity.AssetName      = entry.Name;
    }

    /// <summary>Resolves a custom item by pack and item name. Null when the pack isn't installed.</summary>
    public static CustomItem ResolveCustomItem(string packName, string itemName)
    {
        if (string.IsNullOrEmpty(packName) || string.IsNullOrEmpty(itemName)) return null;

        var pack = Plugin.CustomItemPacks.FirstOrDefault(
            p => string.Equals(p.packName, packName, StringComparison.OrdinalIgnoreCase));

        return pack?.items.FirstOrDefault(
            i => string.Equals(i.itemName, itemName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Why a custom item couldn't be resolved, for the restore error report.</summary>
    public static string DescribeCustomItemFailure(string packName, string itemName)
    {
        var pack = Plugin.CustomItemPacks.FirstOrDefault(
            p => string.Equals(p.packName, packName, StringComparison.OrdinalIgnoreCase));

        return pack == null
            ? $"custom item pack \"{packName}\" is not installed"
            : $"custom item \"{itemName}\" not found in pack \"{packName}\"";
    }

    /// <summary>Looks up the pack/item names for a live custom-item selection, for persistence.</summary>
    public static bool TryDescribeCustomItem(int packIndex, int itemIndex, out string packName, out string itemName)
    {
        packName = null;
        itemName = null;

        if (packIndex < 0 || packIndex >= Plugin.CustomItemPacks.Count) return false;
        var pack = Plugin.CustomItemPacks[packIndex];
        if (itemIndex < 0 || itemIndex >= pack.items.Count) return false;

        packName = pack.packName;
        itemName = pack.items[itemIndex].itemName;
        return true;
    }
}
