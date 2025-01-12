using HawkNetworking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;

namespace NotAzzamods
{
    public static class PropManager
    {
        public static async Task<List<PropListObject>> GetAllProps()
        {
            Debug.Log("Starting GetAllProps...");
            var rootGroup = new List<PropListObject>();
            var handle = Addressables.LoadResourceLocationsAsync("", typeof(GameObject));
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Failed to load resource locations.");
                Addressables.Release(handle);
                return new List<PropListObject>();
            }

            var results = handle.Result;
            Debug.Log($"Found {results.Count} resource locations.");

            foreach (var location in results)
            {
                Debug.Log($"Processing location: {location.PrimaryKey}");
                if (!location.PrimaryKey.StartsWith("Game/Prefabs/Props/"))
                {
                    Debug.Log($"Skipping location: {location.PrimaryKey} (does not start with Game/Prefabs/Props/)");
                    continue;
                }

                var prefabHandle = Addressables.LoadAssetAsync<GameObject>(location.PrimaryKey);
                await prefabHandle.Task;

                if (prefabHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"Failed to load prefab at {location.PrimaryKey}");
                    continue;
                }

                var prefab = prefabHandle.Result;
                Debug.Log($"Loaded prefab: {location.PrimaryKey}");

                if (prefab.GetComponent<HawkNetworkBehaviour>() == null)
                {
                    Debug.Log($"Prefab at {location.PrimaryKey} does not have HawkNetworkBehaviour. Skipping.");
                    Addressables.Release(prefabHandle);
                    continue;
                }

                var address = location.PrimaryKey.Replace("Game/Prefabs/Props/", "");
                var pathParts = address.Split('/');
                Debug.Log($"Adding to hierarchy: {string.Join(" -> ", pathParts)}");

                AddToHierarchy(rootGroup, pathParts, 0);

                Addressables.Release(prefabHandle);
            }

            Addressables.Release(handle);
            Debug.Log("Finished GetAllProps.");
            return rootGroup;
        }

        private static void AddToHierarchy(List<PropListObject> currentGroup, string[] pathParts, int index)
        {
            if (index >= pathParts.Length)
            {
                Debug.Log("Reached end of path parts. Returning.");
                return;
            }

            var currentName = pathParts[index];
            Debug.Log($"Processing path part: {currentName} (Index: {index})");

            if (index == pathParts.Length - 1)
            {
                Debug.Log($"Processing as Prop: {currentName}");
                if (currentGroup.OfType<PropListProp>().All(prop => prop.propName != currentName))
                {
                    currentGroup.Add(new PropListProp
                    {
                        propName = currentName,
                        propFullAddress = string.Join("/", pathParts)
                    });
                    Debug.Log($"Added Prop: {currentName}");
                }
                else
                {
                    Debug.Log($"Duplicate Prop found: {currentName}. Skipping.");
                }
                return;
            }

            Debug.Log($"Processing as Group: {currentName}");
            var existingGroup = currentGroup.OfType<PropListGroup>().FirstOrDefault(group => group.groupName == currentName);

            if (existingGroup == null)
            {
                existingGroup = new PropListGroup
                {
                    groupName = currentName,
                    groupFullAddress = string.Join("/", pathParts.Take(index + 1)),
                    groupChildren = new List<PropListObject>()
                };
                currentGroup.Add(existingGroup);
                Debug.Log($"Added Group: {currentName}");
            }
            else
            {
                Debug.Log($"Group already exists: {currentName}. Using existing group.");
            }

            AddToHierarchy(existingGroup.groupChildren, pathParts, index + 1);
        }

        public static string GetDebugHierarchyText(List<PropListObject> hierarchy, int indentLevel = 0)
        {
            Debug.Log($"Converting hierarchy to debug text at indent level {indentLevel}.");
            var sb = new StringBuilder();

            foreach (var obj in hierarchy)
            {
                var indent = new string(' ', indentLevel * 4);

                if (obj is PropListGroup group)
                {
                    Debug.Log($"Processing Group: {group.groupName}");
                    sb.AppendLine($"{indent}- Group: {group.groupName}");
                    sb.Append(GetDebugHierarchyText(group.groupChildren, indentLevel + 1));
                }
                else if (obj is PropListProp prop)
                {
                    Debug.Log($"Processing Prop: {prop.propName}");
                    sb.AppendLine($"{indent}- Prop: {prop.propName}");
                }
            }

            Debug.Log($"Finished converting hierarchy at indent level {indentLevel}.");
            return sb.ToString();
        }

        public class PropListObject { }

        public class PropListProp : PropListObject
        {
            public string propName;
            public string propFullAddress;
        }

        public class PropListGroup : PropListObject
        {
            public string groupName;
            public string groupFullAddress;
            public List<PropListObject> groupChildren;
        }
    }
}
