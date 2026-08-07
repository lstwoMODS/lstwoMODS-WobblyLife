using System;
using System.Collections.Generic;
using HawkNetworking;
using Steamworks;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Voice over Steam's P2P datagrams, the transport used when the game is hosted through Steam.
    /// Fully meshed: Steam handles NAT traversal and relaying, so every player sends straight to
    /// every other one.
    /// </summary>
    public class SteamVoiceTransport : IVoiceTransport
    {
        /// <summary>P2P channel. Separate from anything the game itself uses.</summary>
        private const int VoiceChannel = 1;

        private readonly Dictionary<ulong, int> connectionIdCache = new();

        private int packetsSent;
        private int packetsReceived;
        private bool loggedInterfaceFailure;

        public string Name => "Steam P2P";

        public bool IsReady => SteamClient.IsValid
                               && SteamP2PNetworkManager.SteamInstance != null
                               && SteamP2PNetworkManager.SteamInstance.IsConnected();

        public string Status => IsReady
            ? $"channel {VoiceChannel}, sent {packetsSent}, received {packetsReceived}"
            : "steam networking unavailable";

        public void Broadcast(byte[] data, int length)
        {
            if (!IsReady) return;

            var self = SteamClient.SteamId;
            var lobby = SteamP2PNetworkManager.SteamInstance.GetLobby();

            try
            {
                foreach (var member in lobby.Members)
                {
                    // Steam never loops a packet back to its sender, so addressing ourselves here
                    // would be a silent no-op. Local monitoring is handled above the transport.
                    if (member.Id == self)
                        continue;

                    SteamNetworking.SendP2PPacket(member.Id, data, length, VoiceChannel, P2PSend.UnreliableNoDelay);
                    packetsSent++;
                }
            }
            catch (Exception e)
            {
                ReportInterfaceFailure("send", e);
            }
        }

        public bool TryReceive(out VoicePacket packet)
        {
            packet = default;

            if (!IsReady) return false;

            Steamworks.Data.P2Packet? received;

            // Steamworks' networking interface can be torn down under us on disconnect, and the
            // wrappers dereference it without a null check. Losing the pump to that would be
            // permanent, so treat it as "nothing to read" and let the next Refresh sort it out.
            try
            {
                if (!SteamNetworking.IsP2PPacketAvailable(VoiceChannel))
                    return false;

                received = SteamNetworking.ReadP2PPacket(VoiceChannel);
            }
            catch (Exception e)
            {
                ReportInterfaceFailure("receive", e);
                return false;
            }

            if (!received.HasValue)
                return false;

            var data = received.Value.Data;
            if (data == null || data.Length == 0)
                return false;

            packetsReceived++;
            packet = new VoicePacket(ResolveConnectionId(received.Value.SteamId), data, 0, data.Length);
            return true;
        }

        /// <summary>Logs the first failure only: this sits on a path that runs every frame.</summary>
        private void ReportInterfaceFailure(string stage, Exception e)
        {
            if (loggedInterfaceFailure) return;

            loggedInterfaceFailure = true;
            Plugin.LogSource.LogWarning($"[VoiceChat] steam p2p {stage} unavailable: {e.Message}");
        }

        public void Tick() { }

        /// <summary>
        /// Steam id to Hawk connection id. Cached because it walks the connection list, and a talking
        /// player produces ~50 packets a second.
        /// </summary>
        private int ResolveConnectionId(SteamId steamId)
        {
            if (connectionIdCache.TryGetValue(steamId.Value, out var cached))
                return cached;

            var id = -1;

            try
            {
                var connection = SteamP2PNetworkManager.SteamInstance.GetSteamConnection(steamId);
                if (connection != null)
                {
                    id = connection.Id;
                    connectionIdCache[steamId.Value] = id;
                }
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] steam id {steamId} did not resolve to a connection: {e.Message}");
            }

            return id;
        }

        public void Dispose() => connectionIdCache.Clear();
    }
}
