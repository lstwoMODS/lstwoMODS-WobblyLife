using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public static class ChatNetworking
{
    private const string ChatManagerAssetId = "5d1b9c2a-1b22-4f5e-a0b3-7b41c8f1c7a1";

    private static GameObject _chatManagerPrefab;
    private static bool _initialized;

    /// <summary>Fires when a chat message is received from the network.</summary>
    public static event Action<ChatMessage> MessageReceived
    {
        add    => ChatNetworkManager.MessageReceived += value;
        remove => ChatNetworkManager.MessageReceived -= value;
    }

    /// <summary>Fires on the host when a remote client asks for a server-scope command.</summary>
    public static event Action<HawkConnection, string> ServerCommandRequested
    {
        add    => ChatNetworkManager.ServerCommandRequested += value;
        remove => ChatNetworkManager.ServerCommandRequested -= value;
    }

    /// <summary>Fires on a client when the host pushes its server-command definitions (JSON payload).</summary>
    public static event Action<string> SyncCommandsReceived
    {
        add    => ChatNetworkManager.SyncCommandsReceived += value;
        remove => ChatNetworkManager.SyncCommandsReceived -= value;
    }

    /// <summary>Fires on the host when a client asks for the current server-command definitions.</summary>
    public static event Action<HawkConnection> SyncRequested
    {
        add    => ChatNetworkManager.SyncRequested += value;
        remove => ChatNetworkManager.SyncRequested -= value;
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _chatManagerPrefab = NetworkPrefabHelper.CreateNetworkPrefab(
            "ChatNetworkManager",
            typeof(ChatNetworkManager),
            ChatManagerAssetId);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (scene.name is "MainMenu" or "LoadingScene") return;
        if (_chatManagerPrefab == null) return;

        if (HawkNetworkManager.DefaultInstance == null) return;
        if (!HawkNetworkManager.DefaultInstance.IsServer()) return;

        NetworkPrefab.SpawnNetworkPrefab(_chatManagerPrefab, Vector3.zero);
    }


    public static void Broadcast(ChatMessage msg)
        => ChatNetworkManager.Instance?.SendBroadcast(msg);

    public static void Whisper(HawkConnection target, ChatMessage msg)
        => ChatNetworkManager.Instance?.SendWhisper(target, msg);

    public static void SendServerCommand(string commandLine)
        => ChatNetworkManager.Instance?.SendCommandToHost(commandLine);

    /// <summary>Host: push the current server-command definitions to every other client.</summary>
    public static void BroadcastServerCommands(string payload)
        => ChatNetworkManager.Instance?.BroadcastSyncedCommands(payload);

    /// <summary>Host: push the current server-command definitions to a single client.</summary>
    public static void SendServerCommandsTo(HawkConnection target, string payload)
        => ChatNetworkManager.Instance?.SendSyncedCommandsTo(target, payload);

    /// <summary>Client: ask the host for its current server-command definitions.</summary>
    public static void RequestServerCommandSync()
        => ChatNetworkManager.Instance?.RequestSync();


    public static HawkConnection FindBySteamId(ulong steamId)
    {
        var mgr = HawkNetworkManager.DefaultInstance;
        if (mgr == null) return null;
        return mgr.GetPlayers()
            .OfType<SteamConnection>()
            .FirstOrDefault(c => c.steamId.Value == steamId);
    }

    public static HawkConnection FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var mgr = HawkNetworkManager.DefaultInstance;
        if (mgr == null) return null;
        return mgr.GetPlayers().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<HawkConnection> AllConnections()
    {
        var mgr = HawkNetworkManager.DefaultInstance;
        return mgr != null ? mgr.GetPlayers() : Enumerable.Empty<HawkConnection>();
    }

    public static HawkConnection LocalConnection()
        => HawkNetworkManager.DefaultInstance?.GetMe();

    public static bool IsOnline() => HawkNetworkManager.DefaultInstance != null && !HawkNetworkManager.DefaultInstance.IsOffline();

    public static bool IsHost() => HawkNetworkManager.DefaultInstance?.IsServer() == true;

    public static bool IsReady() => ChatNetworkManager.Instance != null;
}
