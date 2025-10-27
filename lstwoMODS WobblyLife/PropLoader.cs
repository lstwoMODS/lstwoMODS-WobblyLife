using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HawkNetworking;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace lstwoMODS_WobblyLife;

public static class PropLoader
{
    public static async Task<Dictionary<string, object>> BuildAddressTreeAsync()
    {
        var root = new Dictionary<string, object>();

        IList<IResourceLocation> locations = null;

        foreach (var locator in Addressables.ResourceLocators.ToList())
        {
            Plugin.LogSource.LogDebug($"Loading {locator}");
            
            if (locator.Keys == null) continue;

            foreach (var key in locator.Keys)
            {
                if (key is not string strKey) continue;
                if (!locator.Locate(strKey, typeof(GameObject), out var locs)) continue;
                
                Plugin.LogSource.LogDebug($"Found locators {locs}");

                locations ??= new List<IResourceLocation>();
                
                foreach (var loc in locs)
                {
                    Plugin.LogSource.LogDebug($"Found locator {loc}");
                    ((List<IResourceLocation>)locations).Add(loc);
                }
            }
        }

        if (locations == null) return root;

        foreach (var loc in locations)
        {
            var address = loc.PrimaryKey;

            if (address.StartsWith("Assets/Content/"))
                address = address.Substring("Assets/Content/".Length);

            var handle = Addressables.LoadAssetAsync<GameObject>(loc.PrimaryKey);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                continue;
            }
            
            var go = handle.Result;
            
            if (go.GetComponent<HawkNetworkBehaviour>() != null)
            {
                InsertIntoTree(root, address);
            }

            Addressables.Release(handle);
        }

        return root;
    }

    private static void InsertIntoTree(Dictionary<string, object> tree, string path)
    {
        var parts = path.Split('/');
        var current = tree;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (i == parts.Length - 1)
            {
                if (!current.TryGetValue("__files", out var obj) || obj is not List<string> list)
                {
                    list = new List<string>();
                    current["__files"] = list;
                }

                list.Add(part);
            }
            else
            {
                if (!current.TryGetValue(part + "/", out var nextObj) || nextObj is not Dictionary<string, object> nextDict)
                {
                    nextDict = new Dictionary<string, object>();
                    current[part + "/"] = nextDict;
                }
                current = nextDict;
            }
        }
    }

    public static void FinalizeTree(Dictionary<string, object> tree)
    {
        var keys = tree.Keys.ToList();
        foreach (var key in keys)
        {
            if (tree[key] is Dictionary<string, object> child)
            {
                FinalizeTree(child);
            }
            else if (key == "__files" && tree[key] is List<string> list)
            {
                tree[key] = list.ToArray();
            }
        }
    }
}
