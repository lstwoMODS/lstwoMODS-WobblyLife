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
        => ChatNetworkManager.Instance?.SendTo(target, msg);

    /// <summary>Build a message from a connection we hold ourselves, applying the same clamping and
    /// control-character stripping the wire path applies, so a locally delivered message renders
    /// exactly like the same message would after a round trip.</summary>
    public static ChatMessage BuildInbound(ChatMessageKind kind, HawkConnection sender, string recipientName, string text)
        => ChatNetworkManager.BuildInbound(kind, KeyOf(sender), sender?.Name, recipientName, text,
                                           packedNameColor: 0u,
                                           // We hold the connection, so its controller is authoritative here.
                                           senderNetworkId: NetworkIdOf(FindControllerByConnection(sender)),
                                           senderAddress: AddressOf(sender));

    /// <summary>Clamp and strip a message body the same way the receiving side will, so a local echo
    /// cannot render differently from the copy everyone else gets.</summary>
    public static string SanitizeText(string text) => ChatNetworkManager.SanitizeText(text);

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

    /// <summary>The connection behind an account, or null when nobody in the game matches.</summary>
    public static HawkConnection FindByKey(PlayerKey key)
    {
        if (!key.IsValid) return null;

        foreach (var pc in Controllers())
        {
            var owner = pc.networkObject?.GetOwner();
            if (owner != null && PlayerIdentity.Of(owner) == key) return owner;
        }
        return null;
    }

    public static HawkConnection FindByName(string name)
        => FindControllerByName(name)?.networkObject?.GetOwner();

    /// <summary>
    /// The player controller for a display name, or null when nobody in the game matches. Names are
    /// what <see cref="GetPlayerName"/> replicates, so this resolves on every client, unlike anything
    /// that goes through a connection.
    /// </summary>
    public static PlayerController FindControllerByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Controllers().FirstOrDefault(c => string.Equals(c.GetPlayerName(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The player controller behind a Hawk network id, or null when no such object exists here.
    /// Ids are server-assigned and packets route by them, so one id means one controller everywhere.</summary>
    public static PlayerController FindControllerById(uint networkId)
        => networkId == 0u ? null : HawkNetworkManager.DefaultInstance?.FindNetworkBehaviour<PlayerController>(networkId);

    /// <summary>The controller owned by a connection, or null. Host only, since ownership is only
    /// populated server-side; for split screen this is the first of that connection's players.</summary>
    public static PlayerController FindControllerByConnection(HawkConnection conn)
        => conn == null ? null : Controllers().FirstOrDefault(c => c.networkObject?.GetOwner() == conn);

    /// <summary>
    /// The player controller that sent a chat message, or null when they are not in the game (they may
    /// have left, or the line may be locally produced rather than sent by a player).
    ///
    /// Prefers <see cref="ChatMessage.SenderNetworkId"/>, which is exact and unaffected by two players
    /// picking the same name, and falls back to the name for messages that carry no id (an older build
    /// on the other end, or a locally produced line). The other identity fields cannot do this job:
    /// <see cref="ChatMessage.SenderKey"/> is empty outside an account-backed transport, and
    /// <see cref="ChatMessage.SenderAddress"/> never travels the wire, so it is empty for every message
    /// the host relayed.
    /// </summary>
    public static PlayerController FindSender(ChatMessage msg)
    {
        if (msg == null) return null;
        return FindControllerById(msg.SenderNetworkId) ?? FindControllerByName(msg.SenderName);
    }

    /// <summary>Network id of a controller, or 0 when it has no network object yet.</summary>
    public static uint NetworkIdOf(PlayerController pc) => pc?.networkObject?.GetNetworkID() ?? 0u;

    /// <summary>Our own controller's network id, stamped onto everything we send.</summary>
    public static uint LocalNetworkId()
        => GameInstance.InstanceExists ? NetworkIdOf(GameInstance.Instance.GetFirstLocalPlayerController()) : 0u;

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

    /// <summary>The account behind a connection, or <see cref="PlayerKey.None"/> when it carries
    /// none (direct/LAN transport, or no connection at all).</summary>
    public static PlayerKey KeyOf(HawkConnection conn) => PlayerIdentity.Of(conn);

    /// <summary>The remote endpoint ("ip:port") behind a connection on the direct/LAN transport,
    /// or null under Steam (where peers are addressed by Steam ID, not by address).</summary>
    public static string AddressOf(HawkConnection conn)
        => conn is LiteConnection lite ? lite.Peer?.EndPoint?.ToString() : null;

    public static bool IsOnline() => HawkNetworkManager.DefaultInstance != null && !HawkNetworkManager.DefaultInstance.IsOffline();

    public static bool IsHost() => HawkNetworkManager.DefaultInstance?.IsServer() == true;

    public static bool IsReady() => ChatNetworkManager.Instance != null;
}
