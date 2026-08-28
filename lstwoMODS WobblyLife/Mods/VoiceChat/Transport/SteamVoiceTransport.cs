using System;
using HawkNetworking;
using lstwoMODS_WobblyLife;
using Steamworks;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Voice over Steam's P2P datagrams, the transport used when the game is hosted through Steam.
    /// <para>
    /// Traffic is <b>relayed by the host</b> rather than meshed, for the same reason the direct
    /// transport relays: not every pair of players can talk to each other. Steam would happily carry
    /// a mesh, but the game refuses to build one. <c>SteamP2PNetworkManager.OnP2PSessionRequest</c>
    /// only calls <c>AcceptP2PSessionWithUser</c> for the lobby owner (or, on the host, for any lobby
    /// member), so a session opened from one client to another is never accepted. Steam gives that
    /// session about ten seconds and then reports <c>P2PSessionError.Timeout</c> to the sender, and
    /// because P2P sessions are per-user rather than per-channel that lands on the game's own
    /// callback: the client is shown "Error 4" and dropped to the main menu.
    /// </para>
    /// <para>
    /// So a client only ever addresses the host, and only the host fans frames out. Both are sessions
    /// the game itself already established and accepts.
    /// </para>
    /// </summary>
    public class SteamVoiceTransport : IVoiceTransport
    {
        /// <summary>P2P channel. Separate from anything the game itself uses (the game is on 0).</summary>
        private const int VoiceChannel = 1;

        private const byte Magic = 0x56;
        private const byte TypeVoice = 0x01;

        /// <summary>magic + type + speaker id.</summary>
        private const int Header = 1 + 1 + 4;

        private byte[] sendBuffer = new byte[2048];

        private int packetsSent;
        private int packetsReceived;
        private int packetsRelayed;
        private int packetsRejected;
        private bool loggedInterfaceFailure;

        public string Name => "Steam P2P";

        public bool IsReady => SteamClient.IsValid
                               && SteamP2PNetworkManager.SteamInstance != null
                               && SteamP2PNetworkManager.SteamInstance.IsConnected();

        public string Status
        {
            get
            {
                if (!IsReady) return "steam networking unavailable";

                var role = IsHost ? "relay" : "client";
                return $"channel {VoiceChannel} ({role}), sent {packetsSent}, received {packetsReceived}, "
                     + $"relayed {packetsRelayed}, rejected {packetsRejected}";
            }
        }

        private static bool IsHost => SteamP2PNetworkManager.SteamInstance?.IsServer() ?? false;

        #region Send

        public void Broadcast(byte[] data, int length)
        {
            if (!IsReady || length <= 0) return;

            var total = Header + length;
            EnsureSendBuffer(total);

            sendBuffer[0] = Magic;
            sendBuffer[1] = TypeVoice;
            WriteUInt32(sendBuffer, 2, VoiceIdentity.Local());

            Buffer.BlockCopy(data, 0, sendBuffer, Header, length);

            if (IsHost) RelayToClients(sendBuffer, total, default);
            else        SendTo(HostSteamId(), sendBuffer, total);
        }

        /// <summary>
        /// Host: hand a framed packet to every connected player except <paramref name="skip"/> (the
        /// speaker, who already heard themselves). Every client accepts a session with the host
        /// because the host is the lobby owner, so nothing here can strand a session.
        /// </summary>
        private void RelayToClients(byte[] frame, int length, SteamId skip)
        {
            var manager = SteamP2PNetworkManager.SteamInstance;
            if (manager == null) return;

            // GetPlayers() is the host-only full roster, which is exactly where this runs. Going
            // through connections rather than lobby members keeps us from opening a session with
            // somebody who is in the lobby but not yet in the game.
            foreach (var connection in manager.GetPlayers())
            {
                if (connection is not SteamConnection steam || steam.Me) continue;
                if (skip.Value != 0 && steam.steamId == skip) continue;

                SendTo(steam.steamId, frame, length);
            }
        }

        private void SendTo(SteamId target, byte[] frame, int length)
        {
            if (target.Value == 0) return;

            try
            {
                SteamNetworking.SendP2PPacket(target, frame, length, VoiceChannel, P2PSend.UnreliableNoDelay);
                packetsSent++;
            }
            catch (Exception e)
            {
                ReportInterfaceFailure("send", e);
            }
        }

        /// <summary>
        /// The host's Steam id, read from the lobby owner. Deliberately the same source the game's own
        /// accept check uses, so the session we open is guaranteed to be one it will accept.
        /// </summary>
        private static SteamId HostSteamId()
        {
            try
            {
                return SteamP2PNetworkManager.SteamInstance?.GetLobby().Owner.Id ?? default;
            }
            catch
            {
                return default;
            }
        }

        #endregion

        #region Receive

        public bool TryReceive(out VoicePacket packet)
        {
            packet = default;

            if (!IsReady) return false;

            // Loops rather than returning false on a bad frame: the caller drains until this says
            // no, so letting one unrecognised datagram end the drain would hold everything queued
            // behind it back a frame. A peer on an older build sending unframed voice would do it
            // fifty times a second.
            while (true)
            {
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

                if (data == null || data.Length <= Header || data[0] != Magic || data[1] != TypeVoice)
                {
                    packetsRejected++;
                    continue;
                }

                var speakerId = ReadUInt32(data, 2);

                if (IsHost)
                {
                    // Stamp the truth rather than trusting the claim: a modded client could otherwise
                    // put its voice at another player's position. Unresolvable means the speaker has
                    // not spawned here yet, which is transient and better carried unplaced than
                    // dropped. ReadP2PPacket hands back a freshly allocated array, so overwriting
                    // the claim in place and forwarding it is safe.
                    speakerId = SpeakerIdOf(received.Value.SteamId);

                    WriteUInt32(data, 2, speakerId);
                    RelayToClients(data, data.Length, received.Value.SteamId);
                    packetsRelayed++;
                }

                packetsReceived++;
                packet = new VoicePacket(speakerId, data, Header, data.Length - Header);
                return true;
            }
        }

        /// <summary>
        /// Host: the authoritative speaker id behind a Steam id, via its Hawk connection.
        /// <para>
        /// Resolved through <see cref="PlayerIdentity"/> rather than
        /// <c>SteamP2PNetworkManager.GetSteamConnection</c>: that method does not exist on the
        /// crossplay build, so calling it would fail to resolve the moment this method was JITted
        /// there, even though this transport never runs on that build.
        /// </para>
        /// </summary>
        private static uint SpeakerIdOf(SteamId steamId)
        {
            try
            {
                var connection = PlayerIdentity.ConnectionFor(PlayerKey.Steam(steamId.Value));
                return connection == null ? VoiceIdentity.Unknown : VoiceIdentity.ForConnection(connection.Id);
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] steam id {steamId} did not resolve to a connection: {e.Message}");
                return VoiceIdentity.Unknown;
            }
        }

        #endregion

        /// <summary>Logs the first failure only: this sits on a path that runs every frame.</summary>
        private void ReportInterfaceFailure(string stage, Exception e)
        {
            if (loggedInterfaceFailure) return;

            loggedInterfaceFailure = true;
            Plugin.LogSource.LogWarning($"[VoiceChat] steam p2p {stage} unavailable: {e.Message}");
        }

        public void Tick() { }

        public void Dispose() { }

        #region Wire helpers

        private void EnsureSendBuffer(int size)
        {
            if (sendBuffer.Length < size)
                sendBuffer = new byte[size];
        }

        private static void WriteUInt32(byte[] b, int o, uint v)
        {
            b[o]     = (byte)v;
            b[o + 1] = (byte)(v >> 8);
            b[o + 2] = (byte)(v >> 16);
            b[o + 3] = (byte)(v >> 24);
        }

        private static uint ReadUInt32(byte[] b, int o)
            => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

        #endregion
    }
}
