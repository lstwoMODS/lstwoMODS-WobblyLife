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
    /// Turn a freshly spawned clone back into an ordinary scene object.
    /// <para>
    /// Object.Instantiate copies the template's <see cref="HideFlags.HideAndDontSave"/> onto the clone,
    /// and DontSave means Unity keeps that clone alive across a scene load instead of destroying it with
    /// its scene. The survivor is left holding a dangling scene handle, so <c>gameObject.scene.path</c>
    /// reads back null, and the host's chunk streaming walks exactly that field: WorldManager.UnloadScene
    /// does <c>spawnedNetworkBehaviours[i].gameObject.scene.path.GetHashCode()</c> for every spawned
    /// behaviour on each chunk unload. One survivor throws there, which aborts UnloadChunk before it can
    /// clear the chunk from chunksLoaded, so the chunk is retried every tick and logs "Scene isn't
    /// loaded" forever after. Clearing the flags also stops a second manager spawning next to the
    /// survivor in the new scene.
    /// </para>
    /// Call from NetworkPost so it covers both the host spawn and the client spawn-message path.
    /// </summary>
    public static void AdoptSpawnedInstance(HawkNetworkBehaviour behaviour)
    {
        if (!behaviour) return;

        var obj = behaviour.gameObject;
        obj.hideFlags = HideFlags.None;

        // Only relevant for a clone that already lost its scene; a live one is where it belongs.
        var active = SceneManager.GetActiveScene();
        if (!obj.scene.IsValid() && active.IsValid() && active.isLoaded && obj.transform.parent == null)
            SceneManager.MoveGameObjectToScene(obj, active);
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