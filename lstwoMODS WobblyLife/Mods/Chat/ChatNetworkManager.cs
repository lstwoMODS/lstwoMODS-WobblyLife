using System;
using System.Collections.Generic;
using System.Text;
using HawkNetworking;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public class ChatNetworkManager : HawkNetworkBehaviour
{
    public static ChatNetworkManager Instance;

    // Hard limits applied to remote-controlled payloads.
    private const int MaxTextLength = 512;
    private const int MaxNameLength = 64;
    private const int MaxCommandLength = 512;
    private const int MaxSyncLength = 16384;
    private const float CommandRateLimitSeconds = 0.5f;
    private const float ChatRateLimitSeconds = 0.25f;

    // A rate-limit bucket is pruned once it grows past this, so a long-lived lobby does not keep an
    // entry for every player who ever connected.
    private const int RateLimitPruneThreshold = 32;
    private const float RateLimitEntryTtlSeconds = 60f;

    // Per-sender timestamp of the last accepted request. One bucket per kind of traffic, so sending a
    // whisper does not throttle a server command (and vice versa).
    private readonly Dictionary<HawkConnection, float> _lastCommandAt = new();
    private readonly Dictionary<HawkConnection, float> _lastChatAt = new();
    private static readonly List<HawkConnection> PruneCache = new();

    /// <summary>Fires when a chat message is received from the network.</summary>
    public static event Action<ChatMessage> MessageReceived;

    /// <summary>Fires on the host when a remote client asks for a server-scope command.</summary>
    public static event Action<HawkConnection, string> ServerCommandRequested;

    /// <summary>Fires on a client when the host pushes its server-command definitions (JSON payload).</summary>
    public static event Action<string> SyncCommandsReceived;

    /// <summary>Fires on the host when a client asks for the current server-command definitions.</summary>
    public static event Action<HawkConnection> SyncRequested;

    /// <summary>Fires on the host when a client asks to whisper another player (sender, recipient name, text).
    /// A client can't reach another client's connection directly, so whispers are relayed through the host.</summary>
    public static event Action<HawkConnection, string, string> WhisperRequested;

    private byte RPC_CHAT_MESSAGE;
    private byte RPC_SERVER_COMMAND;
    private byte RPC_SYNC_COMMANDS;
    private byte RPC_REQUEST_SYNC;
    private byte RPC_WHISPER;

    public override void RegisterRPCs(HawkNetworkObject networkObject)
    {
        base.RegisterRPCs(networkObject);

        RPC_CHAT_MESSAGE   = networkObject.RegisterRPC(ClientReceiveChat);
        RPC_SERVER_COMMAND = networkObject.RegisterRPC(ServerReceiveCommand);
        RPC_SYNC_COMMANDS  = networkObject.RegisterRPC(ClientReceiveSync);
        RPC_REQUEST_SYNC   = networkObject.RegisterRPC(ServerReceiveSyncRequest);
        RPC_WHISPER        = networkObject.RegisterRPC(ServerReceiveWhisper);
    }

    /// <summary>
    /// Claim the singleton here rather than in Start: the registered prefab is a live GameObject whose
    /// own Unity Start runs too, and Instantiate copies its hideFlags onto the clone, so a flag check
    /// can't tell the two apart. NetworkPost only ever runs from HawkNetworkBehaviour.Initialize, which
    /// the template never goes through, and it lands during the spawn rather than a frame later.
    /// </summary>
    public override void NetworkPost(HawkNetworkObject networkObject)
    {
        base.NetworkPost(networkObject);
        Instance = this;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    
    // Whispers ride the same RPC as broadcasts (see SendTo), so this one path validates both.
    private void ClientReceiveChat(HawkNetReader reader, HawkRPCInfo info)
    {
        try
        {
            var rawKind = reader.ReadByte();
            var payloadKey = PlayerKey.Parse(reader.ReadString());
            var senderName = reader.ReadString();
            var recipientName = reader.ReadString();
            var text = reader.ReadString();
            var packedNameColor = reader.ReadUInt32();
            var payloadNetworkId = reader.ReadUInt32();

            var sender = info.sender;

            // True when this arrived over the host's connection: the host either sent it itself or
            // relayed it for another player. Everything else is a client talking to the host, which is
            // the untrusted case.
            var fromHost = sender != null && sender.IsHost && !sender.Me;

            var kind = SanitizeKind(rawKind, fromHost);

            // Throttle client-origin chat at the host, before RelayToOthers amplifies it to the lobby.
            // Host-origin messages are never throttled: that one connection carries every other
            // player's chat, so a per-connection limit there would drop legitimate traffic. A message
            // with no sender connection has nothing to throttle against, so it is left alone.
            if (sender != null && !fromHost && !PassesRateLimit(_lastChatAt, sender, ChatRateLimitSeconds)) return;

            // A relayed message carries the real sender in the payload (the transport sender is just
            // "the host", which stamped it), so trust the payload identity there. A message received
            // directly keeps the verified-connection identity, so a client can't spoof its own name.
            string displayName;
            PlayerKey displayKey;
            string displayAddress = null;
            uint displayNetworkId;
            if (fromHost && !string.IsNullOrEmpty(senderName))
            {
                displayName = senderName;
                displayKey = payloadKey;
                displayNetworkId = payloadNetworkId; // already vetted by the host that stamped it
            }
            else
            {
                var verifiedName = sender?.Name;
                displayKey = ChatNetworking.KeyOf(sender);
                displayAddress = ChatNetworking.AddressOf(sender);
                displayName = !string.IsNullOrEmpty(verifiedName) ? verifiedName : senderName;

                // Only the host holds controller ownership, so only the host can check the claim.
                displayNetworkId = networkObject != null && networkObject.IsServer()
                    ? VerifyOwnedController(payloadNetworkId, sender)
                    : payloadNetworkId;
            }

            var msg = BuildInbound(kind, displayKey, displayName, recipientName, text,
                                   packedNameColor, displayNetworkId, displayAddress);

            Plugin.LogSource.LogDebug(
                $"[ChatNetworkManager] RPC in: {kind} from '{msg.SenderName}' " +
                $"(fromHost={fromHost}, server={networkObject != null && networkObject.IsServer()})");

            // Display first, and in its own guard: a handler that throws must not take the relay with it.
            try { MessageReceived?.Invoke(msg); }
            catch (Exception ex) { Plugin.LogSource.LogError($"[ChatNetworkManager] Chat message handler threw: {ex}"); }

            // Clients address their chat to the host (see SendBroadcast), so the host is what makes it
            // global. Private kinds are never relayed here; whispers take the explicit RPC_WHISPER route.
            if (!fromHost && kind == ChatMessageKind.PlayerSay && networkObject != null && networkObject.IsServer())
            {
                try { RelayToOthers(msg, sender); }
                catch (Exception ex) { Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to relay chat: {ex}"); }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to deserialize chat RPC: {ex}");
        }
    }

    /// <summary>
    /// Build a display-ready message out of remote-controlled fields: clamp and strip every string,
    /// and resolve the name color. Shared by the wire path above and by the host's local whisper
    /// delivery (see <see cref="ChatMod.DeliverWhisperFromHost"/>), so the host renders remote text
    /// under exactly the same rules as the client it would otherwise have been forwarded to.
    /// </summary>
    public static ChatMessage BuildInbound(ChatMessageKind kind, PlayerKey senderKey, string senderName,
                                           string recipientName, string text, uint packedNameColor,
                                           uint senderNetworkId = 0u, string senderAddress = null)
    {
        var safeName = Sanitize(senderName, MaxNameLength);
        if (string.IsNullOrEmpty(safeName)) safeName = "?";

        return new ChatMessage
        {
            Kind = kind,
            SenderKey = senderKey,
            SenderNetworkId = senderNetworkId,
            SenderAddress = senderAddress ?? "",
            SenderName = safeName,
            RecipientName = Sanitize(recipientName, MaxNameLength),
            Text = Sanitize(text, MaxTextLength),
            // Only normal player chat carries a colored name; other kinds keep their fixed kind color.
            NameColor = kind == ChatMessageKind.PlayerSay
                ? NameColorUtil.Resolve(packedNameColor, senderKey, safeName)
                : (Color?)null,
        };
    }

    /// <summary>
    /// Host: send one targeted copy to every player except itself and <paramref name="origin"/>, which
    /// is the sender when relaying a client's message (stamped with the identity the host verified) and
    /// null when the host is the author. Targeted sends are what let the origin be excluded at all.
    /// </summary>
    private void RelayToOthers(ChatMessage msg, HawkConnection origin)
    {
        var players = HawkNetworkManager.DefaultInstance?.GetPlayers();
        if (players == null) return;

        // Indexed walk over the live roster: a disconnect mid-send would invalidate an enumerator.
        for (var i = 0; i < players.Count; i++)
        {
            var conn = players[i];
            if (conn == null || conn.Me || conn == origin) continue;
            SendTo(conn, msg);
        }
    }

    private void ServerReceiveCommand(HawkNetReader reader, HawkRPCInfo info)
    {
        if (!networkObject.IsServer()) return;
        try
        {
            var commandLine = reader.ReadString();
            if (string.IsNullOrEmpty(commandLine)) return;
            if (commandLine.Length > MaxCommandLength)
                commandLine = commandLine.Substring(0, MaxCommandLength);

            if (!PassesRateLimit(_lastCommandAt, info.sender, CommandRateLimitSeconds)) return;

            ServerCommandRequested?.Invoke(info.sender, commandLine);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to deserialize server-command RPC: {ex}");
        }
    }

    // Host pushed its server-command definitions to this client.
    private void ClientReceiveSync(HawkNetReader reader, HawkRPCInfo info)
    {
        try
        {
            var payload = reader.ReadString();
            SyncCommandsReceived?.Invoke(string.IsNullOrEmpty(payload) ? "[]" : payload);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to deserialize sync RPC: {ex}");
        }
    }

    // A client asked the host for the current server-command definitions.
    private void ServerReceiveSyncRequest(HawkNetReader reader, HawkRPCInfo info)
    {
        if (!networkObject.IsServer()) return;
        SyncRequested?.Invoke(info.sender);
    }

    // A client asked to whisper another player; the host resolves the recipient and forwards it.
    private void ServerReceiveWhisper(HawkNetReader reader, HawkRPCInfo info)
    {
        if (!networkObject.IsServer()) return;
        try
        {
            var recipient = reader.ReadString();
            var text = reader.ReadString();
            if (string.IsNullOrEmpty(recipient) || string.IsNullOrEmpty(text)) return;
            if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
            if (recipient.Length > MaxNameLength) recipient = recipient.Substring(0, MaxNameLength);

            if (!PassesRateLimit(_lastCommandAt, info.sender, CommandRateLimitSeconds)) return;

            WhisperRequested?.Invoke(info.sender, recipient, text);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to deserialize whisper RPC: {ex}");
        }
    }

    /// <summary>
    /// Host: accept a claimed controller id only when that controller really belongs to the connection
    /// the message arrived on, so a client cannot pin its chat on somebody else. Returns 0 when the
    /// claim fails, which simply falls the receiver back to name matching.
    /// </summary>
    private static uint VerifyOwnedController(uint networkId, HawkConnection sender)
    {
        if (networkId == 0u || sender == null) return 0u;

        var obj = HawkNetworkManager.DefaultInstance?.FindNetworkObject(networkId);
        return obj != null && obj.GetOwner() == sender ? networkId : 0u;
    }

    /// <summary>
    /// Decide which kind an incoming message is allowed to claim. Trust depends on where it came from:
    /// the host legitimately sends command replies, notices and errors (see the server-command Reply
    /// path in <see cref="ChatMod"/>), while a client must not be able to forge a line that renders as
    /// system output. Anything a client is not allowed to claim, and any out-of-range byte, becomes
    /// normal chat.
    /// </summary>
    private static ChatMessageKind SanitizeKind(byte raw, bool fromHost)
    {
        switch ((ChatMessageKind)raw)
        {
            // Anyone may say something, or send a private message.
            case ChatMessageKind.PlayerSay:
            case ChatMessageKind.PrivateFrom:
                return (ChatMessageKind)raw;

            // Host-only kinds.
            case ChatMessageKind.System:
            case ChatMessageKind.CommandReply:
            case ChatMessageKind.Error:
                return fromHost ? (ChatMessageKind)raw : ChatMessageKind.PlayerSay;

            // PrivateTo is a local echo that never travels; CommandEcho likewise.
            default:
                return ChatMessageKind.PlayerSay;
        }
    }

    /// <summary>Clamp and strip a message body to the limits the receiving side enforces.</summary>
    public static string SanitizeText(string text) => Sanitize(text, MaxTextLength);

    /// <summary>Clamp length and strip control characters (incl. \r \n \t) so one message can't span rows or smuggle control codes.</summary>
    private static string Sanitize(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(Math.Min(s.Length, maxLen));
        foreach (var ch in s)
        {
            if (sb.Length >= maxLen) break;
            if (char.IsControl(ch)) continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>Simple per-sender throttle: true when <paramref name="sender"/> may be served again.</summary>
    private static bool PassesRateLimit(Dictionary<HawkConnection, float> bucket, HawkConnection sender, float minInterval)
    {
        if (sender == null) return false;
        var now = Time.realtimeSinceStartup;

        if (bucket.Count > RateLimitPruneThreshold) PruneRateLimit(bucket, now);

        if (bucket.TryGetValue(sender, out var last) && now - last < minInterval)
            return false;

        bucket[sender] = now;
        return true;
    }

    /// <summary>Drop bookkeeping for senders that have gone quiet (in practice, disconnected).</summary>
    private static void PruneRateLimit(Dictionary<HawkConnection, float> bucket, float now)
    {
        PruneCache.Clear();
        foreach (var entry in bucket)
            if (now - entry.Value > RateLimitEntryTtlSeconds) PruneCache.Add(entry.Key);

        for (var i = 0; i < PruneCache.Count; i++) bucket.Remove(PruneCache[i]);
        PruneCache.Clear();
    }

    // ── Send paths ──────────────────────────────────────────────────────────

    /// <summary>
    /// Send to everyone but ourselves. The host fans the message out itself; a client hands it to the
    /// host, which fans it out on receipt (see RelayToOthers).
    /// </summary>
    public void SendBroadcast(ChatMessage msg)
    {
        if (networkObject == null) return;

        if (networkObject.IsServer())
        {
            RelayToOthers(msg, origin: null);
            return;
        }

        // Deliberately Server and not Others: Hawk auto-relays an Others/All RPC that arrives from a
        // client (HawkNetworkObject.HandleMessage_RPC), but it re-sends with exceptConnection = null,
        // so the message comes straight back to whoever sent it and lands on top of their local echo.
        // Addressing the host directly suppresses that bounce and lets RelayToOthers, which does skip
        // the origin, do the fan-out.
        networkObject.SendRPC(RPC_CHAT_MESSAGE, RPCRecievers.Server, Payload(msg));
    }

    /// <summary>Send to a single connection (a whisper, or one leg of the host's relay).</summary>
    public void SendTo(HawkConnection target, ChatMessage msg)
    {
        if (networkObject == null || target == null) return;
        networkObject.SendRPC(RPC_CHAT_MESSAGE, target, Payload(msg));
    }

    /// <summary>The wire fields of a chat message, in the order <see cref="ClientReceiveChat"/> reads
    /// them. Note SenderAddress is deliberately absent: a peer's endpoint is not ours to hand out.</summary>
    private static object[] Payload(ChatMessage msg) => new object[]
    {
        (byte)msg.Kind, msg.SenderKey.ToString(), msg.SenderName ?? "", msg.RecipientName ?? "", msg.Text ?? "",
        PackNameColor(msg), msg.SenderNetworkId,
    };

    /// <summary>0x00RRGGBB of the sender's name color, or 0 ("unset") when the message has none.</summary>
    private static uint PackNameColor(ChatMessage msg)
        => msg.NameColor.HasValue ? NameColorUtil.Pack(msg.NameColor.Value) : 0u;

    /// <summary>Ask the host to execute a command line on our behalf.</summary>
    public void SendCommandToHost(string commandLine)
    {
        if (networkObject == null || string.IsNullOrEmpty(commandLine)) return;
        networkObject.SendRPC(RPC_SERVER_COMMAND, RPCRecievers.Server, commandLine);
    }

    /// <summary>Host: push the current server-command definitions to every other client.</summary>
    public void BroadcastSyncedCommands(string payload)
    {
        if (networkObject == null) return;
        networkObject.SendRPC(RPC_SYNC_COMMANDS, RPCRecievers.Others, ClampSync(payload));
    }

    /// <summary>Host: push the current server-command definitions to a single client.</summary>
    public void SendSyncedCommandsTo(HawkConnection target, string payload)
    {
        if (networkObject == null || target == null) return;
        networkObject.SendRPC(RPC_SYNC_COMMANDS, target, ClampSync(payload));
    }

    /// <summary>Client: ask the host for its current server-command definitions.</summary>
    public void RequestSync()
    {
        if (networkObject == null) return;
        networkObject.SendRPC(RPC_REQUEST_SYNC, RPCRecievers.Server);
    }

    /// <summary>Client: ask the host to deliver a private message to a named player.</summary>
    public void SendWhisperRequest(string recipient, string text)
    {
        if (networkObject == null) return;
        networkObject.SendRPC(RPC_WHISPER, RPCRecievers.Server, recipient ?? "", text ?? "");
    }

    private static string ClampSync(string payload)
    {
        payload ??= "[]";
        if (payload.Length <= MaxSyncLength) return payload;

        Plugin.LogSource.LogWarning($"[ChatNetworkManager] Sync payload too large ({payload.Length} chars); not sent.");
        return "[]";
    }
}
