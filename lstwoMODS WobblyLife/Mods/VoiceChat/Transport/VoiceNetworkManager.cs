using System;
using System.Collections.Generic;
using System.Net;
using HawkNetworking;
using UnityEngine;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Carries the direct transport's out-of-band setup over the game's own authenticated channel:
    /// the host's voice port, and a per-client token.
    /// <para>
    /// The host's <i>address</i> is never sent. A client already holds a live connection to the host
    /// and can read the address off it, which means a host behind a shared or changing public address
    /// cannot advertise the wrong one, and cannot be tricked into pointing clients somewhere else.
    /// Only the port needs telling.
    /// </para>
    /// </summary>
    public class VoiceNetworkManager : HawkNetworkBehaviour
    {
        public static VoiceNetworkManager Instance;

        private byte RPC_ENDPOINT;
        private byte RPC_REQUEST_ENDPOINT;

        /// <summary>Host: token issued to each connection, so a reconnecting client keeps working.</summary>
        private readonly Dictionary<int, ulong> issuedTokens = new();

        private readonly System.Random tokenSource = new();

        public override void Start()
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.Start();
            Instance = this;
        }

        public override void RegisterRPCs(HawkNetworkObject networkObject)
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.RegisterRPCs(networkObject);

            RPC_ENDPOINT = networkObject.RegisterRPC(ClientReceiveEndpoint);
            RPC_REQUEST_ENDPOINT = networkObject.RegisterRPC(ServerReceiveEndpointRequest);
        }

        public override void NetworkPost(HawkNetworkObject networkObject)
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.NetworkPost(networkObject);

            Instance = this;
            VoiceTransport.Refresh();

            if (networkObject.IsServer())
            {
                PublishToAll();
                HawkNetworkManager.DefaultInstance.onPlayerAccepted += OnPlayerAccepted;
            }
            else
            {
                // The transport may have come up before this object existed, or the other way round;
                // asking on spawn covers both orderings.
                networkObject.SendRPC(RPC_REQUEST_ENDPOINT, RPCRecievers.Server);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (HawkNetworkManager.DefaultInstance != null)
                HawkNetworkManager.DefaultInstance.onPlayerAccepted -= OnPlayerAccepted;

            if (Instance == this) Instance = null;
        }

        /// <summary>Called when a direct transport finishes starting, which may be after we spawned.</summary>
        public void OnTransportReady(DirectVoiceTransport direct)
        {
            if (networkObject == null) return;

            if (networkObject.IsServer())
                PublishToAll();
            else
                networkObject.SendRPC(RPC_REQUEST_ENDPOINT, RPCRecievers.Server);
        }

        #region Host

        private void OnPlayerAccepted(HawkConnection connection)
        {
            if (connection == null || networkObject == null) return;
            PublishTo(connection);
        }

        private void PublishToAll()
        {
            var direct = Host();
            if (direct == null) return;

            foreach (var connection in HawkNetworkManager.DefaultInstance.GetPlayers())
            {
                if (connection == null || connection.Me) continue;
                PublishTo(connection);
            }
        }

        private void PublishTo(HawkConnection connection)
        {
            var direct = Host();
            if (direct == null || connection == null || connection.Me) return;

            var token = TokenFor(connection.Id);
            direct.RegisterClient(token, connection.Id);

            networkObject.SendRPC(RPC_ENDPOINT, connection, direct.BoundPort, token);
        }

        private ulong TokenFor(int connectionId)
        {
            if (issuedTokens.TryGetValue(connectionId, out var existing))
                return existing;

            ulong token;
            do
            {
                // System.Random is fine here: the token is an unguessable-enough handle on a channel
                // that only carries voice, not a secret protecting anything else.
                var hi = (ulong)(uint)tokenSource.Next(int.MinValue, int.MaxValue);
                var lo = (ulong)(uint)tokenSource.Next(int.MinValue, int.MaxValue);
                token = (hi << 32) | lo;
            }
            while (token == 0);

            issuedTokens[connectionId] = token;
            return token;
        }

        /// <summary>The running transport, if it is a direct one we are hosting.</summary>
        private static DirectVoiceTransport Host()
            => VoiceTransport.Current is DirectVoiceTransport { IsHost: true } direct ? direct : null;

        private void ServerReceiveEndpointRequest(HawkNetReader reader, HawkRPCInfo info)
        {
            if (networkObject == null || !networkObject.IsServer()) return;
            PublishTo(info.sender);
        }

        #endregion

        #region Client

        private void ClientReceiveEndpoint(HawkNetReader reader, HawkRPCInfo info)
        {
            int port;
            ulong token;

            try
            {
                port = reader.ReadInt32();
                token = reader.ReadUInt64();
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError($"[VoiceChat] malformed voice endpoint RPC: {e.Message}");
                return;
            }

            if (VoiceTransport.Current is not DirectVoiceTransport direct || direct.IsHost)
                return;

            var address = HostAddress();
            if (address == null)
            {
                Plugin.LogSource.LogWarning("[VoiceChat] got a voice port but could not read the host's address off our own connection");
                return;
            }

            direct.ConfigureClient(address, port, token);
        }

        /// <summary>
        /// The host's address, taken from the live game connection we are already talking over. A
        /// client keeps a connection to the host and to itself, and only the host one is marked so.
        /// </summary>
        private static IPAddress HostAddress()
        {
            var manager = HawkNetworkManager.DefaultInstance;
            if (manager == null) return null;

            foreach (var connection in manager.GetPlayers())
            {
                if (connection is not LiteConnection { IsHost: true } lite) continue;

                var endpoint = lite.Peer?.EndPoint;
                if (endpoint != null) return endpoint.Address;
            }

            return null;
        }

        #endregion
    }
}
