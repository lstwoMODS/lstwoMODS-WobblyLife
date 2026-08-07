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

    /// <summary>Fires on the host when a client asks to whisper another player (sender, recipient name, text).</summary>
    public static event Action<HawkConnection, string, string> WhisperRequested
    {
        add    => ChatNetworkManager.WhisperRequested += value;
        remove => ChatNetworkManager.WhisperRequested -= value;
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

    /// <summary>Client: ask the host to deliver a private message to a named player (a client can't
    /// reach another client directly, so whispers route through the host).</summary>
    public static void SendWhisperRelay(string recipient, string text)
        => ChatNetworkManager.Instance?.SendWhisperRequest(recipient, text);


    // ── Roster ────────────────────────────────────────────────────────────
    // The player list is sourced from the replicated player controllers, NOT from
    // HawkNetworkManager.GetPlayers(): that manager list only holds the full roster on the host
    // (clients keep a direct HawkConnection to the host and themselves only, never to their peers),
    // so anything that read it was empty for non-host clients. Every PlayerController is a
    // replicated network object present on all clients, and its owner connection is populated even
    // for remote players (this is what the player-based mods window uses), so this works everywhere.
    // Names come from GetPlayerName() (replicated); a connection's own Name is not reliably set on
    // clients.

    private static IEnumerable<PlayerController> Controllers()
        => GameInstance.InstanceExists
            ? GameInstance.Instance.GetPlayerControllers().Where(c => c != null)
            : Enumerable.Empty<PlayerController>();

    public static HawkConnection FindBySteamId(ulong steamId)
    {
        foreach (var pc in Controllers())
            if (pc.networkObject?.GetOwner() is SteamConnection sc && sc.steamId.Value == steamId)
                return sc;
        return null;
    }

    public static HawkConnection FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var pc = Controllers().FirstOrDefault(c => string.Equals(c.GetPlayerName(), name, StringComparison.OrdinalIgnoreCase));
        return pc?.networkObject?.GetOwner();
    }

    /// <summary>Every player's owning connection, deduplicated (split-screen players share one).</summary>
    public static IEnumerable<HawkConnection> AllConnections()
    {
        var seen = new HashSet<HawkConnection>();
        foreach (var pc in Controllers())
        {
            var conn = pc.networkObject?.GetOwner();
            if (conn != null && seen.Add(conn)) yield return conn;
        }
    }

    /// <summary>Display name of every player in the game (one per controller, split-screen included),
    /// from replicated controller state so it is correct on every client.</summary>
    public static IEnumerable<string> AllPlayerNames()
        => Controllers().Select(c => c.GetPlayerName()).Where(n => !string.IsNullOrEmpty(n));

    public static HawkConnection LocalConnection()
        => HawkNetworkManager.DefaultInstance?.GetMe();

    /// <summary>The Steam ID behind a connection, or 0 when it is not a Steam connection
    /// (direct/LAN transport, or no connection at all).</summary>
    public static ulong SteamIdOf(HawkConnection conn)
        => conn is SteamConnection sc ? sc.steamId.Value : 0UL;

    /// <summary>The remote endpoint ("ip:port") behind a connection on the direct/LAN transport,
    /// or null under Steam (where peers are addressed by Steam ID, not by address).</summary>
    public static string AddressOf(HawkConnection conn)
        => conn is LiteConnection lite ? lite.Peer?.EndPoint?.ToString() : null;

    public static bool IsOnline() => HawkNetworkManager.DefaultInstance != null && !HawkNetworkManager.DefaultInstance.IsOffline();

    public static bool IsHost() => HawkNetworkManager.DefaultInstance?.IsServer() == true;

    public static bool IsReady() => ChatNetworkManager.Instance != null;
}
