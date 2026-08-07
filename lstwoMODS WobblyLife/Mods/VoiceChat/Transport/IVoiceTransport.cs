using System;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Moves compressed voice frames between players. Everything above this interface deals only in
    /// Hawk connection ids, so the voice chat itself never knows whether it is running over Steam
    /// P2P, a direct UDP link, or nothing at all.
    /// </summary>
    public interface IVoiceTransport : IDisposable
    {
        /// <summary>Short name for logs and the debug overlay.</summary>
        string Name { get; }

        /// <summary>True once the transport can actually carry a frame.</summary>
        bool IsReady { get; }

        /// <summary>One line of human readable state for the debug overlay.</summary>
        string Status { get; }

        /// <summary>Send a compressed frame to every other player. Game thread.</summary>
        void Broadcast(byte[] data, int length);

        /// <summary>
        /// Pull the next received frame, if any. Called in a loop from the game thread until it
        /// returns false. The returned buffer is only valid until the next call.
        /// </summary>
        bool TryReceive(out VoicePacket packet);

        /// <summary>Per frame housekeeping (keepalives, reconnects). Game thread.</summary>
        void Tick();
    }

    /// <summary>One received frame, tagged with the Hawk connection it came from.</summary>
    public readonly struct VoicePacket
    {
        /// <summary>Hawk connection id of the sender, or -1 when it could not be resolved.</summary>
        public readonly int ConnectionId;

        /// <summary>Compressed frame. Owned by the transport and reused, so copy it if you keep it.</summary>
        public readonly byte[] Data;

        public readonly int Offset;
        public readonly int Length;

        public VoicePacket(int connectionId, byte[] data, int offset, int length)
        {
            ConnectionId = connectionId;
            Data = data;
            Offset = offset;
            Length = length;
        }
    }
}
