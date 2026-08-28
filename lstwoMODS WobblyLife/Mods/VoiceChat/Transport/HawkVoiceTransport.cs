using System;
using System.Collections.Generic;
using HawkNetworking;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Voice over the game's own Hawk channel, as unreliable RPCs on
    /// <see cref="VoiceNetworkManager"/>.
    /// <para>
    /// This is the transport for the crossplay build. There, every peer talks EOS P2P and the game
    /// never accepts a Steam P2P session (<c>SteamP2PNetworkManager.OnP2PSessionRequest</c> is empty
    /// and the Steam lobby exists only as an invite surface), so <see cref="SteamVoiceTransport"/>
    /// has nothing to send over. Riding the game's channel instead works on every build and every
    /// transport, and is the only path that reaches a player who is not on Steam at all.
    /// </para>
    /// <para>
    /// Relayed by the host for the same reason the other two transports relay: a client can only
    /// address the host. Frames are framed exactly like <see cref="SteamVoiceTransport"/>'s, and the
    /// host restamps the speaker id from the verified sender connection rather than trusting the
    /// claim, so a modded client cannot put its voice at another player's position.
    /// </para>
    /// <para>
    /// The channel lives on a network object only the host spawns, so this transport needs a modded
    /// host, the same condition the chat mod carries.
    /// </para>
    /// </summary>
    public class HawkVoiceTransport : IVoiceTransport
    {
        private const byte Magic = 0x56;
        private const byte TypeVoice = 0x01;

        /// <summary>magic + type + speaker id.</summary>
        private const int Header = 1 + 1 + 4;

        /// <summary>
        /// Hawk refuses a write past this, and a voice frame is nowhere near it. Enforced anyway so
        /// an oversized frame is dropped here rather than throwing inside the writer.
        /// </summary>
        private const int MaxFrame = 4096;

        /// <summary>A received frame and how much of its buffer is actually the frame: buffers come
        /// off a free list and are usually larger than what was copied into them.</summary>
        private readonly struct Received
        {
            public readonly byte[] Buffer;
            public readonly int Length;

            public Received(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }
        }

        /// <summary>Received frames waiting to be drained by the pump, oldest first.</summary>
        private readonly Queue<Received> inbound = new();

        /// <summary>Spent receive buffers, kept so the RPC path does not allocate per frame.</summary>
        private readonly Queue<byte[]> spare = new();

        private readonly object gate = new();

        private byte[] sendBuffer = new byte[1024];
        private byte[] current;

        private int packetsSent;
        private int packetsReceived;
        private int packetsRelayed;
        private int packetsRejected;
        private int packetsDropped;

        public HawkVoiceTransport()
        {
            VoiceNetworkManager.VoiceReceived += OnVoiceReceived;
        }

        public string Name => "Game channel";

        public bool IsReady => VoiceNetworkManager.Instance != null
                               && HawkNetworkManager.InstanceExists
                               && HawkNetworkManager.DefaultInstance != null
                               && HawkNetworkManager.DefaultInstance.IsConnected();

        public string Status
        {
            get
            {
                if (VoiceNetworkManager.Instance == null) return "waiting for the host's voice channel";
                if (!IsReady) return "not connected";

                var role = IsHost ? "relay" : "client";
                return $"hawk rpc ({role}), sent {packetsSent}, received {packetsReceived}, "
                     + $"relayed {packetsRelayed}, rejected {packetsRejected}, dropped {packetsDropped}";
            }
        }

        private static bool IsHost => HawkNetworkManager.DefaultInstance?.IsServer() ?? false;

        #region Send

        public void Broadcast(byte[] data, int length)
        {
            if (!IsReady || length <= 0) return;

            var total = Header + length;
            if (total > MaxFrame) { packetsDropped++; return; }

            EnsureSendBuffer(total);

            sendBuffer[0] = Magic;
            sendBuffer[1] = TypeVoice;
            WriteUInt32(sendBuffer, 2, VoiceIdentity.Local());

            Buffer.BlockCopy(data, 0, sendBuffer, Header, length);

            // Hawk writes a byte[] argument whole, so the wire copy has to be exactly frame-sized.
            var frame = new byte[total];
            Buffer.BlockCopy(sendBuffer, 0, frame, 0, total);

            var manager = VoiceNetworkManager.Instance;
            if (manager == null) return;

            if (IsHost) manager.RelayVoice(frame, skip: null);
            else manager.SendVoiceToHost(frame);

            packetsSent++;
        }

        #endregion

        #region Receive

        /// <summary>
        /// Runs on whichever thread Hawk dispatched the RPC on, so the queue is guarded and the
        /// payload is copied out: the reader hands back a segment of a buffer it reuses.
        /// </summary>
        private void OnVoiceReceived(HawkConnection sender, bool fromHostRelay, ArraySegment<byte> frame)
        {
            var array = frame.Array;
            var offset = frame.Offset;
            var count = frame.Count;

            if (array == null || count <= Header || count > MaxFrame ||
                array[offset] != Magic || array[offset + 1] != TypeVoice)
            {
                packetsRejected++;
                return;
            }

            byte[] copy;
            lock (gate)
            {
                copy = spare.Count > 0 ? spare.Dequeue() : null;
            }

            if (copy == null || copy.Length < count) copy = new byte[count];
            Buffer.BlockCopy(array, offset, copy, 0, count);

            // The host is the only machine that can resolve a connection to a controller, so it is
            // the only one that can replace the sender's claim with the truth. Doing it before the
            // relay copy means every listener gets the verified id.
            if (!fromHostRelay && IsHost)
            {
                WriteUInt32(copy, 2, SpeakerIdOf(sender));

                var manager = VoiceNetworkManager.Instance;
                if (manager != null)
                {
                    var relayed = new byte[count];
                    Buffer.BlockCopy(copy, 0, relayed, 0, count);
                    manager.RelayVoice(relayed, sender);
                    packetsRelayed++;
                }
            }

            lock (gate)
            {
                // A pump that has stalled must not grow the queue without bound; voice is worthless
                // late anyway, so the oldest frames are the ones to lose.
                while (inbound.Count >= 64)
                {
                    spare.Enqueue(inbound.Dequeue().Buffer);
                    packetsDropped++;
                }

                inbound.Enqueue(new Received(copy, count));
            }

            packetsReceived++;
        }

        public bool TryReceive(out VoicePacket packet)
        {
            packet = default;

            Received received;

            lock (gate)
            {
                // The buffer handed out last time is only promised to live until this call, so it
                // goes back on the spare list now rather than when the caller is done with it.
                if (current != null)
                {
                    spare.Enqueue(current);
                    current = null;
                }

                if (inbound.Count == 0) return false;

                received = inbound.Dequeue();
                current = received.Buffer;
            }

            var speakerId = ReadUInt32(current, 2);
            packet = new VoicePacket(speakerId, current, Header, received.Length - Header);
            return true;
        }

        /// <summary>Host: the authoritative speaker id behind a connection.</summary>
        private static uint SpeakerIdOf(HawkConnection sender)
        {
            try
            {
                return sender == null ? VoiceIdentity.Unknown : VoiceIdentity.ForConnection(sender.Id);
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] {sender} did not resolve to a speaker: {e.Message}");
                return VoiceIdentity.Unknown;
            }
        }

        #endregion

        public void Tick() { }

        public void Dispose()
        {
            VoiceNetworkManager.VoiceReceived -= OnVoiceReceived;

            lock (gate)
            {
                inbound.Clear();
                spare.Clear();
                current = null;
            }
        }

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
