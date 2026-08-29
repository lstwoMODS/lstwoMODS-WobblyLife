using System;
using HawkNetworking;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

/// <summary>
/// Networked side-channel that carries each player's extra (stacked) clothing layers between
/// modded clients. This is separate from the vanilla 4-slot clothing sync, the wire format
/// there can't represent extra pieces, so unmodded players never see them. The prefab is spawned
/// only on the server (see <see cref="ClothingStackNetworking"/>), so the host must be modded for
/// the channel to exist; the wearer need not be host.
///
/// Identity is <b>host-authoritative</b>. Hawk relays an <c>Others</c> RPC through the server, and
/// the relayed copy arrives at third clients with <c>info.sender</c> set to the host, not the
/// original wearer, so the wearer's account can't be recovered from the connection there. Instead
/// a client sends its layers to the host only (<see cref="SendStack"/>), the host stamps the
/// <i>verified</i> sender account and rebroadcasts (<see cref="PublishStack"/>). The broadcast RPC
/// is registered with <see cref="RPCValidateMask.Server"/> so the server drops any client that tries
/// to originate it, which prevents a client from spoofing another player's identity.
///
/// The wearer travels as a <see cref="PlayerKey"/> string rather than a bare Steam id: the
/// crossplay build has no Steam ids to send.
/// </summary>
public class ClothingStackNetworkManager : HawkNetworkBehaviour
{
    public static ClothingStackNetworkManager Instance;

    /// <summary>Fires when a peer's full layer list arrives (verified wearer account, encoded payload).</summary>
    public static event Action<PlayerKey, string> StackReceived;

    private const int MaxPayload = 4096;

    // Client -> host: "these are my layers". The host stamps identity and rebroadcasts.
    private byte RPC_REQUEST;
    // Host -> others: authoritative (wearer account, layers). Server-only origin (validate mask).
    private byte RPC_BROADCAST;

    public override void RegisterRPCs(HawkNetworkObject networkObject)
    {
        base.RegisterRPCs(networkObject);
        RPC_REQUEST = networkObject.RegisterRPC(ServerReceiveStack);
        RPC_BROADCAST = networkObject.RegisterRPC(ClientReceiveStack, RPCValidateMask.Server);
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
        NetworkPrefabHelper.AdoptSpawnedInstance(this);
        Instance = this;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    // Host: a client reported its layer list. Attach the authenticated sender identity and publish.
    private void ServerReceiveStack(HawkNetReader reader, HawkRPCInfo info)
    {
        if (networkObject == null || !networkObject.IsServer()) return;
        try
        {
            var payload = Clamp(reader.ReadString());
            var wearer = PlayerIdentity.Of(info.sender);
            if (!wearer.IsValid) return;
            PublishStack(wearer, payload);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ClothingStack] Failed to read stack request RPC: {ex}");
        }
    }

    // Any client: authoritative layer list stamped by the host. Identity comes from the payload,
    // trustworthy because the server-validate mask stops any non-host from originating this RPC.
    private void ClientReceiveStack(HawkNetReader reader, HawkRPCInfo info)
    {
        try
        {
            var wearer = PlayerKey.Parse(reader.ReadString());
            var payload = Clamp(reader.ReadString());
            if (!wearer.IsValid) return;
            StackReceived?.Invoke(wearer, payload);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[ClothingStack] Failed to read stack broadcast RPC: {ex}");
        }
    }

    /// <summary>
    /// Publish the local player's full layer list. The host stamps its own identity and broadcasts
    /// straight away; a non-host asks the host to do so on its behalf.
    /// </summary>
    public void SendStack(string payload)
    {
        if (networkObject == null) return;
        payload = Clamp(payload);

        if (networkObject.IsServer())
        {
            var me = PlayerIdentity.Local();
            if (me.IsValid) PublishStack(me, payload);
            return;
        }

        networkObject.SendRPC(RPC_REQUEST, RPCRecievers.Server, payload);
    }

    // Host only: rebroadcast a wearer's layers with verified identity, and apply on the host itself
    // (Others excludes the sending host, so the local reconcile needs the direct invoke).
    private void PublishStack(PlayerKey wearer, string payload)
    {
        networkObject.SendRPC(RPC_BROADCAST, RPCRecievers.Others, wearer.ToString(), payload ?? "");
        StackReceived?.Invoke(wearer, payload);
    }

    private static string Clamp(string payload)
    {
        payload ??= "";
        return payload.Length > MaxPayload ? payload.Substring(0, MaxPayload) : payload;
    }
}
