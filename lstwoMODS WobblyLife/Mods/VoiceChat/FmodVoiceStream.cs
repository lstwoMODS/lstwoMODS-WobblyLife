using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FMODUnity;
using UnityEngine;
using lstwoMODS_WobblyLife;

namespace WLProxChat
{
    /// <summary>
    /// One remote speaker's audio, streamed into the game's FMOD core system.
    /// <para>
    /// The stream is an FMOD user-created looping stream (<c>OPENUSER | CREATESTREAM</c>) whose PCM
    /// read callback is served from a lock free ring buffer. Decoded voice (16 bit mono PCM) is
    /// pushed in from the voice worker via <see cref="Enqueue"/>; FMOD pulls it out on its own mixer
    /// thread. Nothing in the pull path may touch a Unity API.
    /// </para>
    /// </summary>
    public class FmodVoiceStream : IDisposable
    {
        private const int BytesPerSample = 2;

        /// <summary>Total ring capacity. Only a fraction is ever used, it is headroom against stalls.</summary>
        private const int RingSeconds = 2;

        /// <summary>Length of the phantom looping sound handed to FMOD. Arbitrary, the callback wraps it.</summary>
        private const int StreamSeconds = 5;

        /// <summary>
        /// How much audio FMOD asks for per callback. Fixed at creation, so it is sized for the
        /// lowest latency preset and simply never dominates the higher ones.
        /// </summary>
        private const int DecodeBufferMs = 40;

        /// <summary>Cutoff the muffle filter sits at when a speaker is within min distance, effectively open.</summary>
        private const float OpenCutoffHz = 22000f;

        // FMOD invokes the PCM read callback from its mixer thread. A single static delegate is kept
        // alive for the process lifetime and dispatches by sound handle, so no per-stream delegate can
        // ever be collected out from under native code.
        private static readonly FMOD.SOUND_PCMREAD_CALLBACK ReadCallback = OnPcmRead;
        private static readonly ConcurrentDictionary<long, FmodVoiceStream> Streams = new();

        // Every stream wants the listener position on the same frame, so fetch it once and share it.
        private static int listenerFrame = -1;
        private static Vector3 listenerPosition;

        private readonly string name;
        private readonly int sampleRate;
        private readonly FMOD.System system;

        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private FMOD.DSP lowpass;

        // Single producer (game thread) / single consumer (FMOD mixer thread) ring buffer of 16 bit
        // mono PCM. Only the producer advances writePos and only the consumer advances readPos, so
        // the two never need to agree on anything beyond those two ints.
        private readonly byte[] ring;
        private volatile int writePos;
        private volatile int readPos;

        // Written by the game thread when the latency preset changes, read by the mixer thread.
        private volatile int prebufferBytes;
        private volatile int highWaterBytes;
        private volatile int targetLatencyBytes;

        /// <summary>Consumer-only. While set, the callback emits silence until the jitter buffer refills.</summary>
        private bool priming = true;

        private bool disposed;

        // Last values pushed to FMOD, so the per frame update only pays for what actually changed.
        private float appliedVolume = -1f;
        private float appliedSpatialBlend = -1f;
        private float appliedMinDistance = -1f;
        private float appliedMaxDistance = -1f;
        private float appliedCutoff = -1f;
        private VoiceRolloffMode appliedRolloff = (VoiceRolloffMode)(-1);
        private VoiceLatencyMode appliedLatency = (VoiceLatencyMode)(-1);

        private bool lowpassAttached;
        private bool muffleBypassed;

        /// <summary>True once the sound and channel exist and playback has started.</summary>
        public bool IsValid => !disposed && sound.hasHandle();

        /// <summary>Bytes of decoded audio waiting to be played.</summary>
        public int BufferedBytes
        {
            get
            {
                var buffered = writePos - readPos;
                if (buffered < 0) buffered += ring.Length;
                return buffered;
            }
        }

        /// <summary>Buffered audio expressed as playback time.</summary>
        public float BufferedSeconds => BufferedBytes / (float)(sampleRate * BytesPerSample);

        /// <summary>Diagnostics: bytes accepted by <see cref="Enqueue"/> since creation.</summary>
        public long BytesQueued { get; private set; }

        /// <summary>Diagnostics: bytes <see cref="Enqueue"/> had to throw away because the ring was full.</summary>
        public long BytesDropped { get; private set; }

        /// <summary>Diagnostics: times the buffer ran dry mid-playback.</summary>
        public int Underruns { get; private set; }

        /// <summary>Diagnostics: true while waiting for the jitter buffer to fill.</summary>
        public bool IsPriming => priming;

        /// <summary>Diagnostics: whether FMOD still considers the channel live.</summary>
        public bool IsPlaying =>
            channel.hasHandle() && channel.isPlaying(out var playing) == FMOD.RESULT.OK && playing;

        /// <summary>
        /// Creates and immediately starts a silent 3D stream. Feed it with <see cref="Enqueue"/> and
        /// keep it positioned with <see cref="UpdateSpatial"/> once per frame.
        /// </summary>
        /// <param name="name">Label used in log messages only.</param>
        /// <param name="sampleRate">Sample rate of the PCM that will be enqueued.</param>
        public FmodVoiceStream(string name, int sampleRate)
        {
            this.name = name;
            this.sampleRate = sampleRate;

            var bytesPerSecond = sampleRate * BytesPerSample;

            ring = new byte[bytesPerSecond * RingSeconds];
            ApplyLatency(VoiceLatencyMode.Normal);

            system = RuntimeManager.CoreSystem;

            var exinfo = new FMOD.CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
                numchannels = 1,
                defaultfrequency = sampleRate,
                format = FMOD.SOUND_FORMAT.PCM16,
                decodebuffersize = (uint)(sampleRate * DecodeBufferMs / 1000),
                length = (uint)(bytesPerSecond * StreamSeconds),
                pcmreadcallback = ReadCallback
            };

            const FMOD.MODE mode = FMOD.MODE.OPENUSER | FMOD.MODE.CREATESTREAM | FMOD.MODE.LOOP_NORMAL | FMOD.MODE._3D | FMOD.MODE._3D_WORLDRELATIVE | FMOD.MODE._3D_INVERSEROLLOFF;

            if (!Check(system.createSound((string)null, mode, ref exinfo, out sound), "createSound"))
            {
                sound.clearHandle();
                disposed = true;
                return;
            }

            // Register before playing so the very first callback can already find us.
            Streams[sound.handle.ToInt64()] = this;

            Play();
        }

        private int MsToBytes(int ms)
        {
            var bytes = sampleRate * BytesPerSample * ms / 1000;
            return bytes & ~1;
        }

        /// <summary>
        /// Resizes the jitter buffer. Safe to call while the mixer thread is consuming: the consumer
        /// reads each threshold independently and self corrects on the next callback either way.
        /// </summary>
        private void ApplyLatency(VoiceLatencyMode latency)
        {
            int prebufferMs, targetMs, highWaterMs;

            switch (latency)
            {
                case VoiceLatencyMode.Low:
                    prebufferMs = 40;
                    targetMs = 60;
                    highWaterMs = 200;
                    break;

                case VoiceLatencyMode.High:
                    prebufferMs = 200;
                    targetMs = 300;
                    highWaterMs = 800;
                    break;

                default:
                    prebufferMs = 80;
                    targetMs = 120;
                    highWaterMs = 400;
                    break;
            }

            prebufferBytes = MsToBytes(prebufferMs);
            targetLatencyBytes = MsToBytes(targetMs);
            highWaterBytes = MsToBytes(highWaterMs);

            appliedLatency = latency;
        }

        #region Producer (game thread)

        /// <summary>
        /// Queues decoded 16 bit mono PCM for playback.
        /// <para>
        /// Single producer: safe from any one thread, but only ever from one. In practice that is
        /// the voice worker, which decodes every speaker including the local loopback.
        /// </para>
        /// </summary>
        /// <param name="pcm">Buffer holding little endian 16 bit samples starting at index 0.</param>
        /// <param name="count">Number of bytes in <paramref name="pcm"/> to consume.</param>
        public void Enqueue(byte[] pcm, int count)
        {
            if (disposed || pcm == null || count <= 0) return;
            if (count > pcm.Length) count = pcm.Length;

            var capacity = ring.Length;
            var write = writePos;

            var free = readPos - write - 1;
            if (free < 0) free += capacity;

            // Tail drop anything that does not fit. The consumer trims the backlog from the other end,
            // so this only ever fires if the mixer has stopped pulling entirely.
            if (count > free)
            {
                BytesDropped += count - (free & ~1);
                count = free & ~1;
            }

            if (count <= 0) return;

            BytesQueued += count;

            var copied = 0;
            while (copied < count)
            {
                var chunk = Math.Min(count - copied, capacity - write);
                Buffer.BlockCopy(pcm, copied, ring, write, chunk);

                write += chunk;
                if (write == capacity) write = 0;
                copied += chunk;
            }

            writePos = write;
        }

        #endregion

        #region Consumer (FMOD mixer thread)

        [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMREAD_CALLBACK))]
        private static FMOD.RESULT OnPcmRead(IntPtr soundraw, IntPtr data, uint datalen)
        {
            // A managed exception must never unwind into native FMOD code.
            try
            {
                if (Streams.TryGetValue(soundraw.ToInt64(), out var stream))
                    return stream.Read(data, (int)datalen);

                Zero(data, 0, (int)datalen);
            }
            catch
            {
                try { Zero(data, 0, (int)datalen); }
                catch { /* nothing sensible left to do on the mixer thread */ }
            }

            return FMOD.RESULT.OK;
        }

        private FMOD.RESULT Read(IntPtr data, int datalen)
        {
            var capacity = ring.Length;
            var read = readPos;

            var buffered = writePos - read;
            if (buffered < 0) buffered += capacity;

            // Skip ahead if the backlog has grown past the point where it is just added latency.
            if (buffered > highWaterBytes)
            {
                var drop = (buffered - targetLatencyBytes) & ~1;
                read = (read + drop) % capacity;
                buffered -= drop;
            }

            if (priming)
            {
                if (buffered < prebufferBytes)
                {
                    Zero(data, 0, datalen);
                    readPos = read;
                    return FMOD.RESULT.OK;
                }

                priming = false;
            }

            var toCopy = Math.Min(datalen, buffered) & ~1;

            var copied = 0;
            while (copied < toCopy)
            {
                var chunk = Math.Min(toCopy - copied, capacity - read);
                Marshal.Copy(ring, read, IntPtr.Add(data, copied), chunk);

                read += chunk;
                if (read == capacity) read = 0;
                copied += chunk;
            }

            readPos = read;

            if (copied < datalen)
            {
                Zero(data, copied, datalen - copied);

                // Ran dry. Rebuild the jitter buffer before letting audio through again, otherwise
                // every late packet turns into another click.
                if (!priming) Underruns++;
                priming = true;
            }

            return FMOD.RESULT.OK;
        }

        private static unsafe void Zero(IntPtr data, int offset, int count)
        {
            var p = (byte*)data + offset;
            for (var i = 0; i < count; i++) p[i] = 0;
        }

        #endregion

        #region Channel state (game thread)

        /// <summary>
        /// Applies position and playback settings for this frame, restarting the channel if FMOD
        /// dropped it. Must be called from the game thread.
        /// </summary>
        public void UpdateSpatial(Vector3 position, in VoiceStreamSettings settings)
        {
            if (disposed || !sound.hasHandle()) return;

            if (settings.Latency != appliedLatency)
                ApplyLatency(settings.Latency);

            EnsurePlaying();
            if (!channel.hasHandle()) return;

            var pos = RuntimeUtils.ToFMODVector(position);
            var vel = new FMOD.VECTOR();

            if (channel.set3DAttributes(ref pos, ref vel) != FMOD.RESULT.OK)
            {
                channel.clearHandle();
                return;
            }

            if (!Mathf.Approximately(settings.Volume, appliedVolume))
            {
                channel.setVolume(Mathf.Max(0f, settings.Volume));
                appliedVolume = settings.Volume;
            }

            if (!Mathf.Approximately(settings.SpatialBlend, appliedSpatialBlend))
            {
                channel.set3DLevel(Mathf.Clamp01(settings.SpatialBlend));
                appliedSpatialBlend = settings.SpatialBlend;
            }

            if (!Mathf.Approximately(settings.MinDistance, appliedMinDistance) ||
                !Mathf.Approximately(settings.MaxDistance, appliedMaxDistance))
            {
                channel.set3DMinMaxDistance(
                    Mathf.Max(0.01f, settings.MinDistance),
                    Mathf.Max(settings.MinDistance, settings.MaxDistance));

                appliedMinDistance = settings.MinDistance;
                appliedMaxDistance = settings.MaxDistance;
            }

            if (settings.Rolloff != appliedRolloff)
            {
                channel.setMode(ToMode(settings.Rolloff));
                appliedRolloff = settings.Rolloff;
            }

            UpdateMuffle(position, settings);
        }

        /// <summary>
        /// Drives a low-pass on the channel from the speaker's distance, so far away voices lose their
        /// highs the way a real one would. FMOD's own <c>set3DDistanceFilter</c> would do this too, but
        /// only if the game happened to init with <c>CHANNEL_DISTANCEFILTER</c>, which is not ours to
        /// choose. An explicit DSP works either way and lets us shape the curve.
        /// </summary>
        private void UpdateMuffle(Vector3 position, in VoiceStreamSettings settings)
        {
            if (!settings.Muffle)
            {
                if (lowpassAttached && !muffleBypassed)
                {
                    lowpass.setBypass(true);
                    muffleBypassed = true;
                }

                return;
            }

            if (!lowpassAttached && !AttachLowpass()) return;

            if (muffleBypassed)
            {
                lowpass.setBypass(false);
                muffleBypassed = false;
            }

            var distance = Vector3.Distance(position, ListenerPosition());
            var t = Mathf.Clamp01(Mathf.InverseLerp(settings.MinDistance, settings.MaxDistance, distance));

            // Interpolate in log space: octaves are what the ear hears, not hertz.
            var floorHz = Mathf.Clamp(settings.MuffleCutoff, 100f, OpenCutoffHz);
            var cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoffHz), Mathf.Log(floorHz), t));

            // A native parameter set every frame per speaker is worth avoiding for inaudible deltas.
            if (appliedCutoff < 0f || Mathf.Abs(cutoff - appliedCutoff) > appliedCutoff * 0.02f)
            {
                lowpass.setParameterFloat((int)FMOD.DSP_LOWPASS_SIMPLE.CUTOFF, cutoff);
                appliedCutoff = cutoff;
            }
        }

        private bool AttachLowpass()
        {
            if (!channel.hasHandle()) return false;

            if (!lowpass.hasHandle())
            {
                if (!Check(system.createDSPByType(FMOD.DSP_TYPE.LOWPASS_SIMPLE, out lowpass), "createDSPByType"))
                {
                    lowpass.clearHandle();
                    return false;
                }
            }

            if (!Check(channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, lowpass), "addDSP"))
                return false;

            lowpassAttached = true;
            muffleBypassed = false;
            appliedCutoff = -1f;
            return true;
        }

        /// <summary>
        /// World position of FMOD's first listener, cached per frame so N streams cost one native
        /// call. Game thread only.
        /// </summary>
        public static Vector3 ListenerPosition()
        {
            var frame = Time.frameCount;
            if (frame == listenerFrame) return listenerPosition;

            if (RuntimeManager.CoreSystem.get3DListenerAttributes(0, out var pos, out _, out _, out _) == FMOD.RESULT.OK)
                listenerPosition = new Vector3(pos.x, pos.y, pos.z);

            listenerFrame = frame;
            return listenerPosition;
        }

        private static FMOD.MODE ToMode(VoiceRolloffMode rolloff)
        {
            switch (rolloff)
            {
                case VoiceRolloffMode.Linear: return FMOD.MODE._3D_LINEARROLLOFF;
                case VoiceRolloffMode.LinearSquare: return FMOD.MODE._3D_LINEARSQUAREROLLOFF;
                default: return FMOD.MODE._3D_INVERSEROLLOFF;
            }
        }

        private void EnsurePlaying()
        {
            if (channel.hasHandle())
            {
                if (channel.isPlaying(out var playing) == FMOD.RESULT.OK && playing) return;
                channel.clearHandle();
            }

            Play();
        }

        private void Play()
        {
            if (!Check(system.playSound(sound, default(FMOD.ChannelGroup), false, out channel), "playSound"))
            {
                channel.clearHandle();
                return;
            }

            // Voice matters more than ambience, keep it off the virtual channel chopping block.
            channel.setPriority(0);
            channel.set3DDopplerLevel(0f);
            channel.setVolumeRamp(true);

            // Force the next UpdateSpatial to push everything again onto the new channel. The old
            // channel's DSP chain died with it, so the low-pass needs reattaching too.
            appliedVolume = -1f;
            appliedSpatialBlend = -1f;
            appliedMinDistance = -1f;
            appliedMaxDistance = -1f;
            appliedCutoff = -1f;
            appliedRolloff = (VoiceRolloffMode)(-1);
            lowpassAttached = false;
            muffleBypassed = false;
        }

        #endregion

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            // Unregister first: an in flight callback then simply writes silence.
            if (sound.hasHandle())
                Streams.TryRemove(sound.handle.ToInt64(), out _);

            // The DSP has to leave the chain before it can be released, and the chain dies with
            // the channel, so unhook it while the channel is still alive.
            if (lowpassAttached && channel.hasHandle())
                channel.removeDSP(lowpass);

            lowpassAttached = false;

            if (channel.hasHandle())
            {
                channel.stop();
                channel.clearHandle();
            }

            if (lowpass.hasHandle())
            {
                lowpass.release();
                lowpass.clearHandle();
            }

            if (sound.hasHandle())
            {
                sound.release();
                sound.clearHandle();
            }
        }

        private bool Check(FMOD.RESULT result, string what)
        {
            if (result == FMOD.RESULT.OK) return true;

            Plugin.LogSource.LogWarning($"[VoiceChat] {what} failed for \"{name}\": {result}");
            return false;
        }
    }
}
