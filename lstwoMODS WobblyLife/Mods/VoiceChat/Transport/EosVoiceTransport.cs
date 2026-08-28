using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using HawkNetworking;
using lstwoMODS_WobblyLife;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Voice over EOS P2P datagrams, the transport used on the crossplay build.
    /// <para>
    /// This is the EOS counterpart of <see cref="SteamVoiceTransport"/> and works the same way: it
    /// borrows the connections the game has already established and opened, and sends on a
    /// <b>channel the game does not read</b>. <c>EOSNetworkManager.ReceivePackets</c> pins
    /// <c>RequestedChannel = 0</c> in both its size query and its receive call, and
    /// <c>EOSConnection.SendInternal</c> pins <c>Channel = 0</c> on the way out, so channel 1 is
    /// invisible to the game in both directions. That is a correctness requirement rather than an
    /// optimisation: voice sent on channel 0 would be pulled out of the queue by the game and handed
    /// to Hawk's deserialiser as if it were a game message.
    /// </para>
    /// <para>
    /// Nothing here touches Hawk, which is the whole point. <see cref="HawkVoiceTransport"/> works,
    /// but every frame it sends becomes an RPC: a serialised message, a per-recipient
    /// <c>params object[]</c>, and a fresh wire copy, fifty times a second per speaker. The
    /// allocation churn alone is enough to produce the stutter and dropouts that sound like a codec
    /// problem. Here a frame is written into one reused buffer and handed to
    /// <c>SendPacket</c> as an <see cref="ArraySegment{T}"/>, so the send path does not allocate.
    /// </para>
    /// <para>
    /// Relayed by the host, like the other two transports, because only the host holds the full
    /// roster and can resolve a sender to a player. The host restamps the speaker id from the
    /// verified sender rather than trusting the claim, so a modded client cannot put its voice at
    /// another player's position.
    /// </para>
    /// <para>
    /// One resource really is shared with the game: EOS keeps a single incoming packet queue per
    /// local user, across every socket and channel. Letting channel 1 back up would push the game's
    /// own replication packets out of it, so the receive path drains to empty every frame whether or
    /// not anybody is listening. This runs on the game thread on purpose; the EOS SDK is not thread
    /// safe and the platform tick lives there.
    /// </para>
    /// </summary>
    public class EosVoiceTransport : IVoiceTransport
    {
        /// <summary>Voice channel. The game is on 0 and never looks anywhere else.</summary>
        private const byte VoiceChannel = 1;

        private const byte Magic = 0x56;
        private const byte TypeVoice = 0x01;

        /// <summary>magic + type + speaker id.</summary>
        private const int Header = 1 + 1 + 4;

        /// <summary>
        /// One byte under the size at which <c>EOSConnection.Send</c> starts splitting a payload into
        /// reliable chunks. A voice frame is nowhere near it, and anything that somehow is has no
        /// business being reassembled: it would arrive as several reliable packets and stall the
        /// stream behind it.
        /// </summary>
        private const int MaxPacket = 1169;

        /// <summary>How often the peer list is rebuilt when the player count has not changed.</summary>
        private const float PeerRefreshInterval = 1f;

        private P2PInterface p2p;
        private ProductUserId localUserId;
        private SocketId socketId;
        private bool bound;

        /// <summary>Peers to relay to, with their ids resolved once rather than per frame.</summary>
        private readonly List<Peer> peers = new();

        private float nextPeerRefresh;
        private int lastPlayerCount = -1;

        private byte[] sendBuffer = new byte[512];
        private byte[] receiveBuffer = new byte[MaxPacket + 1];

        private int packetsSent;
        private int packetsReceived;
        private int packetsRelayed;
        private int packetsRejected;
        private int packetsDropped;
        private bool loggedSendFailure;
        private bool loggedReceiveFailure;

        private readonly struct Peer
        {
            public readonly HawkConnection Connection;
            public readonly ProductUserId Id;

            public Peer(HawkConnection connection, ProductUserId id)
            {
                Connection = connection;
                Id = id;
            }
        }

        public string Name => "EOS P2P";

        public bool IsReady => EnsureBound()
                               && HawkNetworkManager.InstanceExists
                               && HawkNetworkManager.DefaultInstance != null
                               && HawkNetworkManager.DefaultInstance.IsConnected();

        public string Status
        {
            get
            {
                if (EosP2PAccess.PermanentlyUnavailable) return "eos p2p handles unavailable";
                if (!IsReady) return "waiting for the eos session";

                var role = IsHost ? "relay" : "client";
                return $"channel {VoiceChannel} ({role}), sent {packetsSent}, received {packetsReceived}, "
                     + $"relayed {packetsRelayed}, rejected {packetsRejected}, dropped {packetsDropped}";
            }
        }

        private static bool IsHost => HawkNetworkManager.DefaultInstance?.IsServer() ?? false;

        /// <summary>
        /// Whether this transport can run at all. False only after the reflective bind failed for
        /// good; not being in a game yet is a normal transient state the transport binds out of.
        /// </summary>
        public static bool IsSupported => GameBuild.IsCrossplay && !EosP2PAccess.PermanentlyUnavailable;

        #region Send

        public void Broadcast(byte[] data, int length)
        {
            if (!IsReady || length <= 0) return;

            // Also refreshed here, not only from Tick: Unity does not order Update between the
            // voice runtime (which sends) and the transport driver (which ticks), so on the first
            // frames the peer list would otherwise still be empty and the frame would be dropped.
            // The count and timer guards inside make the extra call free once it has settled.
            RefreshPeers();

            var total = Header + length;
            if (total > MaxPacket) { packetsDropped++; return; }

            EnsureSendBuffer(total);

            sendBuffer[0] = Magic;
            sendBuffer[1] = TypeVoice;
            WriteUInt32(sendBuffer, 2, VoiceIdentity.Local());

            Buffer.BlockCopy(data, 0, sendBuffer, Header, length);

            if (IsHost)
            {
                RelayToPeers(sendBuffer, total, null);
                return;
            }

            var host = HostId();

            // Counted rather than silently swallowed: a client whose host id never resolves would
            // otherwise sit at "sent 0" with nothing in the overlay to say why.
            if (host == null) { packetsDropped++; return; }

            SendTo(host, sendBuffer, total);
        }

        /// <summary>
        /// Host: hand a framed packet to every peer except <paramref name="skip"/> (the speaker, who
        /// already heard themselves). Every peer holds an established connection to the host, so
        /// nothing here can open one by accident.
        /// </summary>
        private void RelayToPeers(byte[] frame, int length, ProductUserId skip)
        {
            var skipId = skip?.ToString();

            for (var i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                if (peer.Id == null) continue;
                if (skipId != null && peer.Id.ToString() == skipId) continue;

                SendTo(peer.Id, frame, length);
            }
        }

        private void SendTo(ProductUserId target, byte[] frame, int length)
        {
            if (target == null || !target.IsValid()) return;

            try
            {
                var options = new SendPacketOptions
                {
                    LocalUserId = localUserId,
                    RemoteUserId = target,
                    SocketId = socketId,
                    Channel = VoiceChannel,
                    Data = new ArraySegment<byte>(frame, 0, length),

                    // Voice is worthless late, so never sit in the send buffer waiting for company.
                    AllowDelayedDelivery = false,

                    // Unordered rather than ordered: there is no unreliable-ordered option, and the
                    // reliable ones would stall the whole stream behind one lost frame. The receive
                    // stream sequences frames itself.
                    Reliability = PacketReliability.UnreliableUnordered,

                    // Mirrors the game. Voice must never be the thing that opens a connection: an
                    // unexpected one on the game's socket would be reported to the game's own
                    // handler and land in the roster as a player.
                    DisableAutoAcceptConnection = true
                };

                var result = p2p.SendPacket(ref options);

                if (result == Result.Success) packetsSent++;
                else                          ReportSendFailure(result);
            }
            catch (Exception e)
            {
                ReportSendFailure(e);
            }
        }

        /// <summary>Client: the host's id, off the connection the game already keeps to it.</summary>
        private ProductUserId HostId()
        {
            for (var i = 0; i < peers.Count; i++)
                if (peers[i].Connection != null && peers[i].Connection.IsHost)
                    return peers[i].Id;

            return null;
        }

        #endregion

        #region Receive

        public bool TryReceive(out VoicePacket packet)
        {
            packet = default;

            if (!IsReady) return false;

            // Loops rather than returning false on a bad packet: the caller drains until this says
            // no, so letting one unrecognised datagram end the drain would hold everything behind it
            // in the EOS queue for a frame, which is the queue the game shares.
            while (true)
            {
                uint size;

                try
                {
                    var sizeOptions = new GetNextReceivedPacketSizeOptions
                    {
                        LocalUserId = localUserId,
                        RequestedChannel = VoiceChannel
                    };

                    // NotFound simply means the channel is empty, which is the common case.
                    if (p2p.GetNextReceivedPacketSize(ref sizeOptions, out size) != Result.Success)
                        return false;
                }
                catch (Exception e)
                {
                    ReportReceiveFailure(e);
                    return false;
                }

                if (size == 0) return false;

                ProductUserId sender = null;
                uint written;

                try
                {
                    var options = new ReceivePacketOptions
                    {
                        LocalUserId = localUserId,
                        MaxDataSizeBytes = (uint)receiveBuffer.Length,
                        RequestedChannel = VoiceChannel
                    };

                    var outSocket = socketId;

                    if (p2p.ReceivePacket(ref options, ref sender, ref outSocket, out _,
                                          new ArraySegment<byte>(receiveBuffer), out written) != Result.Success)
                        return false;
                }
                catch (Exception e)
                {
                    ReportReceiveFailure(e);
                    return false;
                }

                var length = (int)written;

                if (length <= Header || length > receiveBuffer.Length ||
                    receiveBuffer[0] != Magic || receiveBuffer[1] != TypeVoice)
                {
                    packetsRejected++;
                    continue;
                }

                var speakerId = ReadUInt32(receiveBuffer, 2);

                if (IsHost)
                {
                    // Stamp the truth rather than trusting the claim. Unresolvable means the speaker
                    // has not spawned here yet, which is transient and better carried unplaced than
                    // dropped.
                    speakerId = SpeakerIdOf(sender);

                    WriteUInt32(receiveBuffer, 2, speakerId);
                    RelayToPeers(receiveBuffer, length, sender);
                    packetsRelayed++;
                }

                packetsReceived++;
                packet = new VoicePacket(speakerId, receiveBuffer, Header, length - Header);
                return true;
            }
        }

        /// <summary>Host: the authoritative speaker id behind a product user id.</summary>
        private static uint SpeakerIdOf(ProductUserId sender)
        {
            try
            {
                var id = sender?.ToString();
                if (string.IsNullOrEmpty(id)) return VoiceIdentity.Unknown;

                var connection = PlayerIdentity.ConnectionFor(PlayerKey.Epic(id));
                return connection == null ? VoiceIdentity.Unknown : VoiceIdentity.ForConnection(connection.Id);
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] {sender} did not resolve to a speaker: {e.Message}");
                return VoiceIdentity.Unknown;
            }
        }

        #endregion

        public void Tick() => RefreshPeers();

        /// <summary>
        /// Rebuilds the relay list. Cheap to call often: it only does work when the roster changed
        /// or the slow timer expired.
        /// </summary>
        private void RefreshPeers()
        {
            if (!EnsureBound()) return;

            var players = HawkNetworkManager.DefaultInstance?.GetPlayers();
            if (players == null) return;

            // Reflection per peer is cheap but not free, and this would otherwise run once per
            // outgoing frame per listener. Rebuild when the roster changes, and on a slow timer
            // besides, so a connection that only became valid later is still picked up.
            if (players.Count == lastPlayerCount && UnityEngine.Time.realtimeSinceStartup < nextPeerRefresh)
                return;

            lastPlayerCount = players.Count;
            nextPeerRefresh = UnityEngine.Time.realtimeSinceStartup + PeerRefreshInterval;

            peers.Clear();

            for (var i = 0; i < players.Count; i++)
            {
                var connection = players[i];
                if (connection == null || connection.Me) continue;

                var id = EosP2PAccess.RemoteIdOf(connection);
                if (id != null && id.IsValid())
                    peers.Add(new Peer(connection, id));
            }
        }

        public void Dispose()
        {
            peers.Clear();
            p2p = null;
            localUserId = null;
            bound = false;
        }

        /// <summary>
        /// Resolves the P2P handles, once. They are the same for the lifetime of the session, but
        /// they only exist once the game holds a connection, which is later than this transport is
        /// built, so this retries until it succeeds rather than failing at construction.
        /// </summary>
        private bool EnsureBound()
        {
            if (bound) return true;
            if (EosP2PAccess.PermanentlyUnavailable) return false;

            if (!EosP2PAccess.TryGetLocal(out p2p, out localUserId, out socketId)) return false;

            bound = true;
            Plugin.LogSource.LogInfo($"[VoiceChat] eos voice bound to socket '{socketId.SocketName}' channel {VoiceChannel}");
            return true;
        }

        #region Diagnostics

        private void ReportSendFailure(Result result)
        {
            if (loggedSendFailure) return;

            loggedSendFailure = true;
            Plugin.LogSource.LogWarning($"[VoiceChat] eos voice send failed: {result}");
        }

        private void ReportSendFailure(Exception e)
        {
            if (loggedSendFailure) return;

            loggedSendFailure = true;
            Plugin.LogSource.LogWarning($"[VoiceChat] eos voice send threw: {e.Message}");
        }

        private void ReportReceiveFailure(Exception e)
        {
            if (loggedReceiveFailure) return;

            loggedReceiveFailure = true;
            Plugin.LogSource.LogWarning($"[VoiceChat] eos voice receive threw: {e.Message}");
        }

        #endregion

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
