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

    // Per-sender timestamp of the last accepted server-command request (rate limiting).
    private readonly Dictionary<HawkConnection, float> _lastCommandAt = new();

    /// <summary>Fires when a chat message is received from the network.</summary>
    public static event Action<ChatMessage> MessageReceived;

    /// <summary>Fires on the host when a remote client asks for a server-scope command.</summary>
    public static event Action<HawkConnection, string> ServerCommandRequested;

    /// <summary>Fires on a client when the host pushes its server-command definitions (JSON payload).</summary>
    public static event Action<string> SyncCommandsReceived;

    /// <summary>Fires on the host when a client asks for the current server-command definitions.</summary>
    public static event Action<HawkConnection> SyncRequested;

    private byte RPC_CHAT_MESSAGE;
    private byte RPC_SERVER_COMMAND;
    private byte RPC_SYNC_COMMANDS;
    private byte RPC_REQUEST_SYNC;

    public override void Start()
    {
        base.Start();

        if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

        Instance = this;
    }

    public override void RegisterRPCs(HawkNetworkObject networkObject)
    {
        base.RegisterRPCs(networkObject);

        RPC_CHAT_MESSAGE   = networkObject.RegisterRPC(ClientReceiveChat);
        RPC_SERVER_COMMAND = networkObject.RegisterRPC(ServerReceiveCommand);
        RPC_SYNC_COMMANDS  = networkObject.RegisterRPC(ClientReceiveSync);
        RPC_REQUEST_SYNC   = networkObject.RegisterRPC(ServerReceiveSyncRequest);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    
    // Whispers ride the same RPC as broadcasts (see SendWhisper), so this one path validates both.
    private void ClientReceiveChat(HawkNetReader reader, HawkRPCInfo info)
    {
        try
        {
            var rawKind = reader.ReadByte();
            reader.ReadUInt64();
            var senderName = reader.ReadString();
            var recipientName = reader.ReadString();
            var text = reader.ReadString();

            var kind = SanitizeKind(rawKind);

            var sender = info.sender;
            var verifiedName = sender?.Name;
            var verifiedSteamId = (sender is SteamConnection sc) ? sc.steamId.Value : 0UL;

            var displayName = !string.IsNullOrEmpty(verifiedName) ? verifiedName : senderName;

            MessageReceived?.Invoke(new ChatMessage
            {
                Kind = kind,
                SenderSteamId = verifiedSteamId,
                SenderName = Sanitize(displayName, MaxNameLength),
                RecipientName = Sanitize(recipientName, MaxNameLength),
                Text = Sanitize(text, MaxTextLength),
            });
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ChatNetworkManager] Failed to deserialize chat RPC: {ex}");
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

            if (!PassesCommandRateLimit(info.sender)) return;

            ServerCommandRequested?.Invoke(info.sender, commandLine);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatNetworkManager] Failed to deserialize server-command RPC: {ex}");
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

    /// <summary>Whitelist remote-origin kinds; coerce everything else (and out-of-range bytes) to PlayerSay.</summary>
    private static ChatMessageKind SanitizeKind(byte raw)
    {
        switch ((ChatMessageKind)raw)
        {
            case ChatMessageKind.PlayerSay:   return ChatMessageKind.PlayerSay;
            case ChatMessageKind.PrivateFrom: return ChatMessageKind.PrivateFrom;
            default:                          return ChatMessageKind.PlayerSay;
        }
    }

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

    /// <summary>Simple per-sender throttle for forwarded server commands.</summary>
    private bool PassesCommandRateLimit(HawkConnection sender)
    {
        if (sender == null) return false;
        var now = Time.realtimeSinceStartup;
        if (_lastCommandAt.TryGetValue(sender, out var last) && now - last < CommandRateLimitSeconds)
            return false;
        _lastCommandAt[sender] = now;
        return true;
    }

    // ── Send paths ──────────────────────────────────────────────────────────

    /// <summary>Broadcast to all other clients (excluding the local sender).</summary>
    public void SendBroadcast(ChatMessage msg)
    {
        if (networkObject == null) return;
        networkObject.SendRPC(RPC_CHAT_MESSAGE, RPCRecievers.Others,
            (byte)msg.Kind, msg.SenderSteamId, msg.SenderName ?? "", msg.RecipientName ?? "", msg.Text ?? "");
    }

    /// <summary>Send to a single connection.</summary>
    public void SendWhisper(HawkConnection target, ChatMessage msg)
    {
        if (networkObject == null || target == null) return;
        networkObject.SendRPC(RPC_CHAT_MESSAGE, target,
            (byte)msg.Kind, msg.SenderSteamId, msg.SenderName ?? "", msg.RecipientName ?? "", msg.Text ?? "");
    }

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

    private static string ClampSync(string payload)
    {
        payload ??= "[]";
        if (payload.Length <= MaxSyncLength) return payload;

        Plugin.LogSource.LogWarning($"[ChatNetworkManager] Sync payload too large ({payload.Length} chars); not sent.");
        return "[]";
    }
}
