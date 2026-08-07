using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Voice over a dedicated UDP socket, for games hosted on a direct IP rather than through Steam.
    /// <para>
    /// Traffic is <b>relayed by the host</b> rather than meshed. A mesh would be one hop shorter, but
    /// it needs every client to be directly reachable by every other client, which outside a LAN
    /// means NAT traversal we have no signalling server for. Relaying only requires the host to be
    /// reachable, which is already true or nobody could have joined the game.
    /// </para>
    /// <para>
    /// The host binds a second port alongside the game's own and its address and port reach clients
    /// over <see cref="VoiceNetworkManager"/>, on the game's authenticated channel. Clients send from
    /// an ephemeral port, so the host learns where to reply from the packets themselves and no client
    /// needs to be reachable or port forwarded.
    /// </para>
    /// </summary>
    public class DirectVoiceTransport : IVoiceTransport
    {
        private const byte Magic = 0x56;

        private const byte TypeHello = 0x00;
        private const byte TypeVoice = 0x01;
        private const byte TypeRelay = 0x02;

        /// <summary>magic + type + token.</summary>
        private const int ClientHeader = 1 + 1 + 8;

        /// <summary>magic + type + sender connection id.</summary>
        private const int RelayHeader = 1 + 1 + 4;

        private const int ReceiveBufferSize = 2048;

        /// <summary>Registers a silent client with the host and keeps its NAT mapping alive.</summary>
        private const float HelloIntervalSeconds = 1f;

        private Socket socket;
        private Thread receiveThread;
        private volatile bool closing;

        private readonly ConcurrentQueue<Received> inbox = new();

        // Host side. Both are written from the receive thread and read from the game thread.
        private readonly ConcurrentDictionary<ulong, int> tokenToConnection = new();
        private readonly ConcurrentDictionary<int, EndPoint> clientEndpoints = new();

        // Client side.
        private EndPoint hostEndpoint;
        private ulong token;
        private float nextHello;

        private byte[] sendBuffer = new byte[ReceiveBufferSize];

        private int packetsSent;
        private int packetsReceived;
        private int packetsRelayed;
        private int packetsRejected;

        /// <summary>True when this instance is the relay rather than a client of one.</summary>
        public bool IsHost { get; private set; }

        /// <summary>Port the host bound, to be advertised to clients. 0 until <see cref="StartHost"/> succeeds.</summary>
        public int BoundPort { get; private set; }

        /// <summary>Our own Hawk connection id, stamped into relayed frames so clients know who spoke.</summary>
        public int LocalConnectionId { get; set; } = -1;

        public string Name => IsHost ? "Direct UDP (host)" : "Direct UDP (client)";

        public bool IsReady => socket != null && (IsHost || (hostEndpoint != null && token != 0));

        public string Status
        {
            get
            {
                if (socket == null) return "socket not open";

                return IsHost
                    ? $"port {BoundPort}, {clientEndpoints.Count} client(s), sent {packetsSent}, " +
                      $"received {packetsReceived}, relayed {packetsRelayed}, rejected {packetsRejected}"
                    : $"host {hostEndpoint}, token {(token == 0 ? "pending" : "ok")}, " +
                      $"sent {packetsSent}, received {packetsReceived}";
            }
        }

        #region Setup

        /// <summary>
        /// Bind the relay socket. <paramref name="port"/> of 0 takes any free port, which works but
        /// cannot be port forwarded ahead of time. Falls back to an ephemeral port if the requested
        /// one is taken.
        /// </summary>
        public bool StartHost(int port)
        {
            IsHost = true;

            if (!Bind(port) && (port == 0 || !Bind(0)))
                return false;

            Plugin.LogSource.LogInfo($"[VoiceChat] direct voice relay listening on UDP {BoundPort}");
            return true;
        }

        /// <summary>Open an ephemeral socket for talking to a host relay.</summary>
        public bool StartClient() => !IsHost && Bind(0);

        /// <summary>Point this client at the host's relay. Called once the endpoint sync arrives.</summary>
        public void ConfigureClient(IPAddress address, int port, ulong authToken)
        {
            if (address == null || port <= 0) return;

            hostEndpoint = new IPEndPoint(address, port);
            token = authToken;
            nextHello = 0f;

            Plugin.LogSource.LogInfo($"[VoiceChat] direct voice client targeting {hostEndpoint}");
        }

        /// <summary>
        /// Host: authorise a client. The token is minted per connection and delivered over the game's
        /// own authenticated channel, so an inbound packet carrying it is proof of who sent it. Without
        /// this the sender id would just be an unverified field in a UDP datagram.
        /// </summary>
        public void RegisterClient(ulong authToken, int connectionId)
        {
            if (authToken == 0) return;
            tokenToConnection[authToken] = connectionId;
        }

        /// <summary>Host: revoke a token and forget where that client was.</summary>
        public void UnregisterClient(int connectionId)
        {
            foreach (var pair in tokenToConnection)
            {
                if (pair.Value == connectionId)
                    tokenToConnection.TryRemove(pair.Key, out _);
            }

            clientEndpoints.TryRemove(connectionId, out _);
        }

        private bool Bind(int port)
        {
            try
            {
                var bound = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // A datagram to a client that has since vanished otherwise surfaces as an ICMP driven
                // ConnectionReset on the *next* receive, killing the loop for everyone else too.
                try { bound.IOControl(unchecked((int)0x9800000C), new byte[] { 0, 0, 0, 0 }, null); }
                catch { /* not supported off Windows, the receive loop tolerates it either way */ }

                bound.Bind(new IPEndPoint(IPAddress.Any, port));

                socket = bound;
                BoundPort = ((IPEndPoint)bound.LocalEndPoint).Port;

                closing = false;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "lstwoMODS Voice Receive"
                };
                receiveThread.Start();

                return true;
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] could not bind voice UDP port {port}: {e.Message}");
                socket = null;
                return false;
            }
        }

        #endregion

        #region Send

        public void Broadcast(byte[] data, int length)
        {
            if (socket == null || length <= 0) return;

            if (IsHost)
                RelayToClients(LocalConnectionId, data, 0, length, null);
            else
                SendToHost(TypeVoice, data, length);
        }

        private void SendToHost(byte type, byte[] data, int length)
        {
            if (hostEndpoint == null || token == 0) return;

            var total = ClientHeader + length;
            EnsureSendBuffer(total);

            sendBuffer[0] = Magic;
            sendBuffer[1] = type;
            WriteUInt64(sendBuffer, 2, token);

            if (length > 0)
                Buffer.BlockCopy(data, 0, sendBuffer, ClientHeader, length);

            Send(sendBuffer, total, hostEndpoint);
        }

        /// <summary>
        /// Host: fan a frame out to every known client except <paramref name="skip"/> (the sender,
        /// who already heard themselves say it).
        /// </summary>
        private void RelayToClients(int senderConnectionId, byte[] data, int offset, int length, EndPoint skip)
        {
            var total = RelayHeader + length;

            // The receive thread relays too, so it cannot share the game thread's scratch buffer.
            var buffer = total <= ReceiveBufferSize ? new byte[ReceiveBufferSize] : new byte[total];

            buffer[0] = Magic;
            buffer[1] = TypeRelay;
            WriteInt32(buffer, 2, senderConnectionId);

            if (length > 0)
                Buffer.BlockCopy(data, offset, buffer, RelayHeader, length);

            foreach (var pair in clientEndpoints)
            {
                if (skip != null && Equals(pair.Value, skip))
                    continue;

                Send(buffer, total, pair.Value);
            }
        }

        private void Send(byte[] buffer, int length, EndPoint target)
        {
            try
            {
                socket.SendTo(buffer, 0, length, SocketFlags.None, target);
                packetsSent++;
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] voice send to {target} failed: {e.Message}");
            }
        }

        private void EnsureSendBuffer(int size)
        {
            if (sendBuffer.Length < size)
                sendBuffer = new byte[size];
        }

        #endregion

        #region Receive

        private void ReceiveLoop()
        {
            var buffer = new byte[ReceiveBufferSize];

            while (!closing)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int length;

                try
                {
                    length = socket.ReceiveFrom(buffer, ref from);
                }
                catch (SocketException) when (!closing)
                {
                    // A single bad datagram (or a client that went away) must not end the loop.
                    continue;
                }
                catch
                {
                    return;
                }

                if (length < 2 || buffer[0] != Magic)
                {
                    packetsRejected++;
                    continue;
                }

                if (IsHost)
                    HandleAsHost(buffer, length, from);
                else
                    HandleAsClient(buffer, length);
            }
        }

        private void HandleAsHost(byte[] buffer, int length, EndPoint from)
        {
            var type = buffer[1];
            if (type != TypeVoice && type != TypeHello) { packetsRejected++; return; }
            if (length < ClientHeader) { packetsRejected++; return; }

            // The token is the whole authentication story: it came over the game's own channel, so
            // only the connection it was minted for can be holding it.
            if (!tokenToConnection.TryGetValue(ReadUInt64(buffer, 2), out var connectionId))
            {
                packetsRejected++;
                return;
            }

            // Learn (and keep following) where this client is speaking from.
            clientEndpoints[connectionId] = from;

            if (type == TypeHello)
                return;

            var payloadLength = length - ClientHeader;
            if (payloadLength <= 0) return;

            packetsReceived++;

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(buffer, ClientHeader, payload, 0, payloadLength);

            inbox.Enqueue(new Received(connectionId, payload));

            RelayToClients(connectionId, payload, 0, payloadLength, from);
            packetsRelayed++;
        }

        private void HandleAsClient(byte[] buffer, int length)
        {
            if (buffer[1] != TypeRelay || length < RelayHeader) { packetsRejected++; return; }

            var payloadLength = length - RelayHeader;
            if (payloadLength <= 0) return;

            packetsReceived++;

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(buffer, RelayHeader, payload, 0, payloadLength);

            inbox.Enqueue(new Received(ReadInt32(buffer, 2), payload));
        }

        public bool TryReceive(out VoicePacket packet)
        {
            if (inbox.TryDequeue(out var received))
            {
                packet = new VoicePacket(received.ConnectionId, received.Data, 0, received.Data.Length);
                return true;
            }

            packet = default;
            return false;
        }

        #endregion

        public void Tick()
        {
            if (IsHost || socket == null || hostEndpoint == null || token == 0)
                return;

            // Register with the relay before ever speaking, so other players' voices can reach us
            // while we are silent, and so the NAT mapping we punched stays open.
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < nextHello) return;

            nextHello = now + HelloIntervalSeconds;
            SendToHost(TypeHello, null, 0);
        }

        public void Dispose()
        {
            closing = true;

            try { socket?.Close(); }
            catch { /* already going away */ }

            socket = null;
            receiveThread = null;

            tokenToConnection.Clear();
            clientEndpoints.Clear();

            while (inbox.TryDequeue(out _)) { }
        }

        #region Wire helpers

        private static void WriteInt32(byte[] b, int o, int v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
            b[o + 2] = (byte)(v >> 16);
            b[o + 3] = (byte)(v >> 24);
        }

        private static int ReadInt32(byte[] b, int o)
            => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);

        private static void WriteUInt64(byte[] b, int o, ulong v)
        {
            for (var i = 0; i < 8; i++)
                b[o + i] = (byte)(v >> (i * 8));
        }

        private static ulong ReadUInt64(byte[] b, int o)
        {
            ulong v = 0;
            for (var i = 0; i < 8; i++)
                v |= (ulong)b[o + i] << (i * 8);
            return v;
        }

        #endregion

        private readonly struct Received
        {
            public readonly int ConnectionId;
            public readonly byte[] Data;

            public Received(int connectionId, byte[] data)
            {
                ConnectionId = connectionId;
                Data = data;
            }
        }
    }
}
