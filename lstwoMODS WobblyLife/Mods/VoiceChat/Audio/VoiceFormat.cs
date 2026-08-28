namespace WLProxChat.Audio
{
    /// <summary>
    /// The single audio format everything above the microphone agrees on.
    /// <para>
    /// Capture converts whatever the recording device gives us into this, the codec encodes and
    /// decodes it, and the playback streams are created at this rate. Fixing it here is what lets
    /// two players with completely different audio hardware understand each other's packets.
    /// </para>
    /// </summary>
    public static class VoiceFormat
    {
        /// <summary>
        /// 48 kHz because it is Opus's native rate and by far the most common device rate, so the
        /// usual machine pays for no rate conversion at all on either side.
        /// </summary>
        public const int SampleRate = 48000;

        public const int Channels = 1;

        /// <summary>
        /// Frame length. 20 ms is the Opus default and the usual voice tradeoff: shorter frames add
        /// per packet header overhead, longer ones add delay and lose more audio per lost packet.
        /// </summary>
        public const int FrameMs = 20;

        /// <summary>Samples in one frame, per channel.</summary>
        public const int FrameSamples = SampleRate / 1000 * FrameMs;

        public const int BytesPerSample = 2;

        /// <summary>One frame as 16 bit PCM.</summary>
        public const int FramePcmBytes = FrameSamples * BytesPerSample;

        /// <summary>
        /// Ceiling for one compressed frame. Opus at the bitrates we offer lands around 60 to 170
        /// bytes; this is simply a buffer size that no legitimate frame can exceed.
        /// </summary>
        public const int MaxPayloadBytes = 512;
    }
}
