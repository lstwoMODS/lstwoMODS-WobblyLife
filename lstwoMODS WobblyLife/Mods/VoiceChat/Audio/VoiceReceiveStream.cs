using System;

namespace WLProxChat.Audio
{
    /// <summary>
    /// One speaker's inbound frames, turned back into PCM.
    /// <para>
    /// There is deliberately no reorder window here. Frames are played in the order they arrive and a
    /// late one is dropped, because the playback buffer downstream is already the jitter buffer and
    /// stacking a second one on top of it would double the delay to fix a problem that a host relay
    /// on either transport barely has. What loss does get is proper treatment: a gap in the sequence
    /// is concealed by the decoder, and the frame immediately before a surviving packet is rebuilt
    /// from that packet's inband redundancy instead of being invented.
    /// </para>
    /// <para>Lives on the voice worker thread. One instance per speaker.</para>
    /// </summary>
    public sealed class VoiceReceiveStream : IDisposable
    {
        /// <summary>
        /// Longest run of missing frames worth synthesising. Past this the speaker did not lose
        /// packets, they stopped talking or fell off the network, and concealing 200 ms of nothing
        /// only produces artefacts before the real audio resumes.
        /// </summary>
        private const int MaxConceal = 10;

        private readonly IVoiceDecoder decoder;
        private readonly Action<byte[], int> sink;

        private readonly short[] pcm = new short[VoiceFormat.FrameSamples];
        private readonly byte[] bytes = new byte[VoiceFormat.FramePcmBytes];

        private ushort expected;
        private bool synced;

        #region Diagnostics

        public int FramesDecoded { get; private set; }

        /// <summary>Frames invented by the decoder because they never arrived.</summary>
        public int FramesConcealed { get; private set; }

        /// <summary>Frames rebuilt out of the following packet's redundancy data.</summary>
        public int FramesRecovered { get; private set; }

        /// <summary>Frames that arrived after their slot had already been played.</summary>
        public int FramesLate { get; private set; }

        /// <summary>Payloads this build does not speak, most likely a different mod version.</summary>
        public int FramesRejected { get; private set; }

        public int Resyncs { get; private set; }

        #endregion

        public VoiceReceiveStream(IVoiceDecoder decoder, Action<byte[], int> sink)
        {
            this.decoder = decoder;
            this.sink = sink;
        }

        /// <summary>Feeds one received frame, header included. Worker thread only.</summary>
        public void Push(byte[] data, int offset, int length)
        {
            if (!VoiceFrame.TryRead(data, offset, length,
                    out var flags, out var sequence, out var payloadOffset, out var payloadLength))
            {
                FramesRejected++;
                return;
            }

            // A burst that starts fresh has nothing in common with whatever came before it, so the
            // decoder is better off forgetting than trying to bridge the gap.
            if (!synced || (flags & VoiceFrame.FlagSpeechStart) != 0)
            {
                decoder.Reset();
                expected = sequence;
                synced = true;
            }

            // Signed difference so the comparison keeps working across the 16 bit wrap.
            var delta = (short)(sequence - expected);

            if (delta < 0)
            {
                FramesLate++;
                return;
            }

            if (delta > MaxConceal)
            {
                decoder.Reset();
                Resyncs++;
                delta = 0;
            }

            for (var i = 0; i < delta; i++)
            {
                // The last missing frame is the one this packet carries redundancy for. Everything
                // further back has to be concealed outright.
                var recoverable = i == delta - 1;

                var written = recoverable
                    ? decoder.Decode(data, payloadOffset, payloadLength, pcm, true)
                    : decoder.Decode(null, 0, 0, pcm, false);

                if (written <= 0) continue;

                if (recoverable) FramesRecovered++;
                else FramesConcealed++;

                Emit(written);
            }

            var decoded = decoder.Decode(data, payloadOffset, payloadLength, pcm, false);

            if (decoded > 0)
            {
                FramesDecoded++;
                Emit(decoded);
            }

            expected = (ushort)(sequence + 1);
        }

        private void Emit(int samples)
        {
            AudioConvert.ShortsToBytes(pcm, samples, bytes);
            sink(bytes, samples * VoiceFormat.BytesPerSample);
        }

        public void Dispose()
        {
            decoder?.Dispose();
        }
    }
}
