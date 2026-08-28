using System;
using Concentus.Enums;
using Concentus.Structs;

namespace WLProxChat.Audio
{
    /// <summary>
    /// Opus through Concentus, a managed port of libopus.
    /// <para>
    /// Managed rather than a native <c>opus.dll</c> so there is no second binary to ship, no
    /// architecture to pin, and nothing to preload out of the plugin folder. It costs a couple of
    /// milliseconds per frame, which is why encoding and decoding both live on the voice worker
    /// thread rather than on the game thread.
    /// </para>
    /// </summary>
    public sealed class OpusVoiceEncoder : IVoiceEncoder
    {
        /// <summary>
        /// Encoder search effort, 0 to 10. 5 is the usual voice setting: most of the quality of 10
        /// for a fraction of the work, which matters more here than in a native implementation.
        /// </summary>
        private const int Complexity = 5;

        /// <summary>
        /// What we tell the encoder to expect, so it sizes its redundancy data for a connection that
        /// is losing packets rather than a perfect one.
        /// </summary>
        private const int ExpectedPacketLossPercent = 10;

        private const int MinBitrate = 6000;
        private const int MaxBitrate = 128000;

        private OpusEncoder encoder;
        private int bitrate;

        public string Name => "Opus (Concentus)";

        /// <summary>Last encode failure, for the debug overlay. Null while everything is fine.</summary>
        public string LastError { get; private set; }

        public int Bitrate
        {
            get => bitrate;
            set
            {
                var clamped = value < MinBitrate ? MinBitrate : value > MaxBitrate ? MaxBitrate : value;
                if (clamped == bitrate) return;

                bitrate = clamped;

                if (encoder != null)
                    encoder.Bitrate = clamped;
            }
        }

        public OpusVoiceEncoder(int bitrate)
        {
            encoder = new OpusEncoder(VoiceFormat.SampleRate, VoiceFormat.Channels,
                OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Complexity = Complexity,
                SignalType = OpusSignal.OPUS_SIGNAL_VOICE,

                // Inband FEC piggybacks a coarse copy of the previous frame onto each packet, which
                // the receiver can decode when a frame goes missing. It only exists if the encoder
                // is told to expect loss, hence the hint below.
                UseInbandFEC = true,
                PacketLossPercent = ExpectedPacketLossPercent,

                // Constrained VBR keeps the quality benefit of variable bitrate while bounding how
                // large any single packet can get, which is what a datagram transport wants.
                UseVBR = true,
                UseConstrainedVBR = true,

                // Discontinuous transmission is left off on purpose: the gate upstream already stops
                // sending during silence, so DTX would only add near empty frames to reason about.
                UseDTX = false
            };

            this.bitrate = 0;
            Bitrate = bitrate;
        }

        public int Encode(float[] pcm, int offset, byte[] destination, int destinationOffset, int maxBytes)
        {
            if (encoder == null) return 0;

            try
            {
                var written = encoder.Encode(pcm, offset, VoiceFormat.FrameSamples,
                    destination, destinationOffset, maxBytes);

                LastError = null;

                // One byte means the encoder decided this frame carries nothing worth sending.
                return written <= 1 ? 0 : written;
            }
            catch (Exception e)
            {
                LastError = "encode: " + e.Message;
                return 0;
            }
        }

        public void Dispose()
        {
            encoder = null;
        }
    }

    /// <summary>One speaker's Opus decoder, including concealment and FEC recovery.</summary>
    public sealed class OpusVoiceDecoder : IVoiceDecoder
    {
        private OpusDecoder decoder;

        /// <summary>Last decode failure, for the debug overlay. Null while everything is fine.</summary>
        public string LastError { get; private set; }

        public OpusVoiceDecoder()
        {
            decoder = new OpusDecoder(VoiceFormat.SampleRate, VoiceFormat.Channels);
        }

        public int Decode(byte[] data, int offset, int length, short[] pcm, bool fec)
        {
            // Read once into a local: the owning stream can be disposed from the game thread while
            // the worker is mid decode, and a field re-read after the null check would be a crash.
            var opus = decoder;
            if (opus == null) return 0;

            try
            {
                // A null payload is not an error: it is how libopus is asked to synthesise a frame
                // that never arrived, from the shape of the ones that did.
                var written = opus.Decode(data, offset, length, pcm, 0, VoiceFormat.FrameSamples, fec);

                LastError = null;
                return written < 0 ? 0 : written;
            }
            catch (Exception e)
            {
                LastError = "decode: " + e.Message;
                return 0;
            }
        }

        public void Reset()
        {
            try
            {
                var opus = decoder;
                opus?.ResetState();
            }
            catch (Exception e)
            {
                LastError = "reset: " + e.Message;
            }
        }

        public void Dispose()
        {
            decoder = null;
        }
    }
}
