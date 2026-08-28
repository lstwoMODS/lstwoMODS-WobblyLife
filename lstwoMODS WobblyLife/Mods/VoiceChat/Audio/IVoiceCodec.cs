using System;

namespace WLProxChat.Audio
{
    /// <summary>
    /// Compresses one <see cref="VoiceFormat.FrameSamples"/> frame at a time.
    /// <para>
    /// The seam exists so the managed Opus implementation can be swapped for a native one later
    /// without anything above it noticing. Encoders are not thread safe and each one belongs to
    /// exactly one capture stream.
    /// </para>
    /// </summary>
    public interface IVoiceEncoder : IDisposable
    {
        /// <summary>Short name for logs and the debug overlay.</summary>
        string Name { get; }

        /// <summary>Target bitrate in bits per second. Applied on the next frame.</summary>
        int Bitrate { get; set; }

        /// <summary>
        /// Encodes exactly one frame of mono float samples in -1..1.
        /// </summary>
        /// <returns>Bytes written to <paramref name="destination"/>, or 0 if the frame was dropped.</returns>
        int Encode(float[] pcm, int offset, byte[] destination, int destinationOffset, int maxBytes);
    }

    /// <summary>
    /// Decodes frames for one speaker. Stateful: it carries the decoder's history, which is what
    /// makes packet loss concealment and forward error correction possible at all.
    /// </summary>
    public interface IVoiceDecoder : IDisposable
    {
        /// <summary>
        /// Decodes one frame into 16 bit PCM.
        /// </summary>
        /// <param name="data">Compressed payload, or null to conceal a frame that never arrived.</param>
        /// <param name="fec">
        /// Decode the <i>previous</i> frame out of this packet's redundancy data rather than the
        /// packet itself. Only meaningful when the sender had inband FEC enabled.
        /// </param>
        /// <returns>Samples written, or 0 if nothing could be decoded.</returns>
        int Decode(byte[] data, int offset, int length, short[] pcm, bool fec);

        /// <summary>Throws away decoder history, for when a stream restarts or jumps.</summary>
        void Reset();
    }
}
