using System;
using Concentus.Common;

namespace WLProxChat.Audio
{
    /// <summary>Format plumbing between the recording device and <see cref="VoiceFormat"/>.</summary>
    public static class AudioConvert
    {
        private const float ShortToFloat = 1f / 32768f;

        /// <summary>
        /// Folds interleaved 16 bit device audio down to mono.
        /// <para>
        /// Averaging rather than taking the first channel: plenty of interfaces present a mono mic as
        /// the left half of a stereo pair, but plenty of others put it on the right, and a headset
        /// that really is stereo carries the same voice in both. Averaging is the only option that is
        /// never silent.
        /// </para>
        /// </summary>
        /// <returns>Number of mono samples written.</returns>
        public static int DownmixToMono(short[] source, int sampleFrames, int channels, short[] destination)
        {
            if (channels <= 1)
            {
                Array.Copy(source, destination, sampleFrames);
                return sampleFrames;
            }

            var read = 0;

            for (var i = 0; i < sampleFrames; i++)
            {
                var sum = 0;

                for (var c = 0; c < channels; c++)
                    sum += source[read++];

                destination[i] = (short)(sum / channels);
            }

            return sampleFrames;
        }

        /// <summary>16 bit PCM to floats in -1..1, which is what the preprocessor and encoder work in.</summary>
        public static void ShortsToFloats(short[] source, int count, float[] destination)
        {
            for (var i = 0; i < count; i++)
                destination[i] = source[i] * ShortToFloat;
        }

        /// <summary>
        /// Reinterprets 16 bit samples as the little endian bytes <see cref="FmodVoiceStream"/> wants.
        /// BlockCopy already produces that layout on every platform the game ships on.
        /// </summary>
        public static void ShortsToBytes(short[] source, int count, byte[] destination)
        {
            Buffer.BlockCopy(source, 0, destination, 0, count * VoiceFormat.BytesPerSample);
        }
    }

    /// <summary>
    /// Mono sample rate conversion onto <see cref="VoiceFormat.SampleRate"/>, using the libspeexdsp
    /// resampler that ships inside Concentus. A device already running at the target rate, which is
    /// most of them, takes a straight copy instead.
    /// <para>
    /// It works in 16 bit rather than float on purpose. This port of the resampler is fixed point
    /// internally, and its float entry points simply round to <c>short</c> on the way in and widen
    /// back out again, so handing it floats would cost two conversions and buy nothing. Device audio
    /// arrives as 16 bit PCM anyway; the float conversion happens once, afterwards.
    /// </para>
    /// </summary>
    public sealed class MonoResampler
    {
        /// <summary>
        /// Speex quality 0 to 10. 4 is comfortably transparent for voice and costs a fraction of what
        /// the codec itself does, so there is no reason to go lower.
        /// </summary>
        private const int Quality = 4;

        private readonly SpeexResampler resampler;

        public int InputRate { get; }
        public int OutputRate { get; }

        /// <summary>True when input and output rates match and no filtering happens at all.</summary>
        public bool IsPassThrough => resampler == null;

        public MonoResampler(int inputRate, int outputRate)
        {
            InputRate = inputRate;
            OutputRate = outputRate;

            if (inputRate != outputRate)
                resampler = new SpeexResampler(1, inputRate, outputRate, Quality);
        }

        /// <summary>Worst case output samples for a given input count, for buffer sizing.</summary>
        public int MaxOutputFor(int inputSamples)
            => IsPassThrough ? inputSamples : (int)((long)inputSamples * OutputRate / InputRate) + 16;

        /// <summary>
        /// Converts <paramref name="inputCount"/> mono samples into <paramref name="output"/>.
        /// The resampler keeps its own history between calls, so a stream can be fed in arbitrary
        /// chunks without a seam at every boundary.
        /// </summary>
        /// <returns>Number of samples written to <paramref name="output"/>.</returns>
        public int Process(short[] input, int inputCount, short[] output)
        {
            if (inputCount <= 0) return 0;

            if (IsPassThrough)
            {
                var copy = Math.Min(inputCount, output.Length);
                Array.Copy(input, 0, output, 0, copy);
                return copy;
            }

            var written = 0;
            var consumed = 0;

            // Speex consumes as much input as the output buffer has room for, so a short buffer is
            // not an error, it just needs going round again.
            while (consumed < inputCount && written < output.Length)
            {
                var inLength = inputCount - consumed;
                var outLength = output.Length - written;

                resampler.Process(0, input, consumed, ref inLength, output, written, ref outLength);

                if (inLength == 0 && outLength == 0) break;

                consumed += inLength;
                written += outLength;
            }

            return written;
        }
    }
}
