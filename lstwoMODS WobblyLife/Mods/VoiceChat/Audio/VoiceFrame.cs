namespace WLProxChat.Audio
{
    /// <summary>
    /// The four byte header in front of every compressed frame.
    /// <para>
    /// It carries three things the raw Steam payloads never had: which codec produced the frame, a
    /// sequence number, and whether the frame starts a new burst of speech. The sequence number is
    /// what makes packet loss recoverable at all, since a receiver cannot conceal a gap it cannot
    /// see, and the codec tag is what keeps a frame from a mod version that speaks a different
    /// format from being fed to the decoder as noise.
    /// </para>
    /// </summary>
    public static class VoiceFrame
    {
        public const int HeaderBytes = 4;

        /// <summary>Opus, 48 kHz mono, 20 ms frames. Bump this if any of that ever changes.</summary>
        public const byte CodecOpusV1 = 0xC1;

        /// <summary>
        /// Constant high nibble of the flags byte. Pure paranoia against a foreign payload whose
        /// first byte happens to equal the codec tag: two fixed fields have to line up, not one.
        /// </summary>
        private const byte FlagsTag = 0xA0;
        private const byte FlagsMask = 0x0F;

        /// <summary>First frame of a burst. Tells the receiver to resynchronise rather than conceal.</summary>
        public const byte FlagSpeechStart = 0x01;

        public static void WriteHeader(byte[] buffer, byte flags, ushort sequence)
        {
            buffer[0] = CodecOpusV1;
            buffer[1] = (byte)(FlagsTag | (flags & FlagsMask));
            buffer[2] = (byte)(sequence & 0xFF);
            buffer[3] = (byte)(sequence >> 8);
        }

        /// <summary>
        /// Validates and splits a received frame. Returns false for anything this build does not
        /// speak, which is the whole compatibility story: an unknown frame is silently ignored
        /// rather than decoded into noise.
        /// </summary>
        public static bool TryRead(
            byte[] buffer, int offset, int length,
            out byte flags, out ushort sequence, out int payloadOffset, out int payloadLength)
        {
            flags = 0;
            sequence = 0;
            payloadOffset = 0;
            payloadLength = 0;

            if (buffer == null || length <= HeaderBytes) return false;
            if (buffer[offset] != CodecOpusV1) return false;
            if ((buffer[offset + 1] & 0xF0) != FlagsTag) return false;

            flags = (byte)(buffer[offset + 1] & FlagsMask);
            sequence = (ushort)(buffer[offset + 2] | (buffer[offset + 3] << 8));
            payloadOffset = offset + HeaderBytes;
            payloadLength = length - HeaderBytes;

            return true;
        }
    }
}
