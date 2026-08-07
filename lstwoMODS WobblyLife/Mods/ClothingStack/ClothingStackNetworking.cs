using System;
using HawkNetworking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

public static class ClothingStackNetworking
{
    private const string ManagerAssetId = "a7e3d1b2-9c84-4f60-b1a3-2e6f8c4d0517";

    private static GameObject _managerPrefab;
    private static bool _initialized;

    /// <summary>Fires when a peer's layer list arrives (verified wearer Steam id, encoded payload).</summary>
    public static event Action<ulong, string> StackReceived
    {
        add    => ClothingStackNetworkManager.StackReceived += value;
        remove => ClothingStackNetworkManager.StackReceived -= value;
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _managerPrefab = NetworkPrefabHelper.CreateNetworkPrefab(
            "ClothingStackNetworkManager",
            typeof(ClothingStackNetworkManager),
            ManagerAssetId);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (scene.name is "MainMenu" or "LoadingScene") return;
        if (_managerPrefab == null) return;

        if (HawkNetworkManager.DefaultInstance == null) return;
        if (!HawkNetworkManager.DefaultInstance.IsServer()) return;

        NetworkPrefab.SpawnNetworkPrefab(_managerPrefab, Vector3.zero);
    }

    public static void Broadcast(string payload)
        => ClothingStackNetworkManager.Instance?.SendStack(payload);

    public static bool IsReady() => ClothingStackNetworkManager.Instance != null;
}
