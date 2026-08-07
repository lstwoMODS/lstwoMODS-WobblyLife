using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HawkNetworking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife;

public static class NetworkPrefabHelper
{
    private static bool isInitialized;
    public static List<NetworkSingleton> NetworkSingletons = [];

    public static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }
        
        SceneManager.sceneLoaded += OnSceneLoaded;
        isInitialized = true;
    }
    
    public static GameObject CreateNetworkPrefab(string name, Type networkBehaviourType, string guid, params Type[] components)
    {
        Initialize();
        
        if (!networkBehaviourType.IsSubclassOf(typeof(HawkNetworkBehaviour)))
        {
            throw new Exception("Could not register network prefab: type '" + networkBehaviourType.FullName + "' is not a subclass of HawkNetworkBehaviour");
        }
        
        var obj = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        var behaviour = obj.AddComponent(networkBehaviourType) as HawkNetworkBehaviour;
        
        var assetIdField = typeof(HawkNetworkBehaviour).GetField("assetID", BindingFlags.Instance | BindingFlags.NonPublic);
        assetIdField.SetValue(behaviour, guid);
        
        HawkNetworkManager.DefaultInstance.RegisterPrefab(behaviour);
        
        return obj;
    }

    /// <summary>
    /// Adds a network behaviour to be spawned on scene load when the condition is met.
    /// </summary>
    /// <param name="networkBehaviour">the behaviour to be spawned</param>
    /// <param name="spawnCondition">used to filter scene names or load modes</param>
    public static void AddNetworkSingleton(HawkNetworkBehaviour networkBehaviour, Func<Scene, LoadSceneMode, bool> spawnCondition, Action<HawkNetworkBehaviour> onSpawned = null)
    {
        Initialize();

        var singleton = new NetworkSingleton(networkBehaviour, spawnCondition, onSpawned);
        NetworkSingletons.Add(singleton);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var singleton in NetworkSingletons.Where(singleton => singleton.condition.Invoke(scene, mode)))
        {
            HawkNetworkManager.DefaultInstance.InstantiateNetworkPrefab(singleton.networkBehaviour.GetAssetId(), singleton.onSpawned);
        }
    }

    public class NetworkSingleton(HawkNetworkBehaviour networkBehaviour, Func<Scene, LoadSceneMode, bool> condition, Action<HawkNetworkBehaviour> onSpawned)
    {
        public HawkNetworkBehaviour networkBehaviour = networkBehaviour;
        public Func<Scene, LoadSceneMode, bool> condition = condition;
        public Action<HawkNetworkBehaviour> onSpawned = onSpawned;
    }
}