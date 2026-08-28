using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Audio
{
    /// <summary>
    /// The voice worker: everything between the microphone and the transport, and between the
    /// transport and the playback streams.
    /// <para>
    /// It all runs on one background thread on purpose. Encoding costs a millisecond or two per
    /// frame and decoding costs a fraction of one per speaker, which is nothing spread over a core
    /// of its own and a visible stutter on the game thread. Capture polling belongs here too:
    /// chasing FMOD's record position from <c>Update</c> would tie how smoothly your voice is sampled
    /// to how well the game happens to be running.
    /// </para>
    /// <para>
    /// One thread rather than two also settles a subtler question. <see cref="FmodVoiceStream"/> is a
    /// single producer ring buffer, and with one worker every stream, remote or loopback, has exactly
    /// one thread writing into it.
    /// </para>
    /// </summary>
    public sealed class VoicePipeline : IDisposable
    {
        /// <summary>Worker wake up interval. Well under a frame of audio, so nothing ever waits long.</summary>
        private const int TickMs = 5;

        /// <summary>How often capture health is re-examined. Enumerating devices is not free.</summary>
        private const float DeviceCheckSeconds = 1f;

        private readonly MicrophoneCapture capture = new MicrophoneCapture();
        private readonly VoicePreprocessor preprocessor = new VoicePreprocessor();

        private readonly ConcurrentQueue<OutboundFrame> outbound = new ConcurrentQueue<OutboundFrame>();
        private readonly ConcurrentQueue<InboundFrame> inbound = new ConcurrentQueue<InboundFrame>();
        private readonly ConcurrentDictionary<uint, VoiceReceiveStream> receivers = new ConcurrentDictionary<uint, VoiceReceiveStream>();

        private readonly AutoResetEvent wake = new AutoResetEvent(false);

        private Thread worker;
        private volatile bool running;

        private OpusVoiceEncoder encoder;

        // Capture side buffers. Worker thread only.
        private float[] captureBuffer;
        private readonly float[] frameBuffer = new float[VoiceFormat.FrameSamples];
        private readonly byte[] encodeBuffer = new byte[VoiceFrame.HeaderBytes + VoiceFormat.MaxPayloadBytes];
        private int frameFill;
        private ushort sequence;
        private bool wasSending;

        // Requested state, written from the game thread.
        private volatile bool transmitArmed;
        private volatile bool forceOpenGate;

        private float nextDeviceCheck;
        private string requestedDevice = "";

        private struct OutboundFrame
        {
            public byte[] Data;
            public int Length;
        }

        private struct InboundFrame
        {
            public uint SpeakerId;
            public byte[] Data;
            public int Length;
        }

        #region State (game thread)

        public MicrophoneCapture Capture => capture;
        public VoicePreprocessor Preprocessor => preprocessor;

        /// <summary>Whether the microphone should be armed at all this frame.</summary>
        public bool TransmitArmed
        {
            get => transmitArmed;
            set => transmitArmed = value;
        }

        /// <summary>Push to talk is held: the gate is forced open and speech detection is bypassed.</summary>
        public bool ForceOpenGate
        {
            get => forceOpenGate;
            set
            {
                forceOpenGate = value;
                preprocessor.ForceOpen = value;
            }
        }

        /// <summary>Encoder bitrate in bits per second.</summary>
        public int Bitrate
        {
            get => encoder?.Bitrate ?? 0;
            set { if (encoder != null) encoder.Bitrate = value; }
        }

        /// <summary>True while audio is actually being encoded and queued for sending.</summary>
        public bool IsSending { get; private set; }

        public string CodecName => encoder?.Name ?? "none";

        #endregion

        #region Diagnostics

        public int FramesEncoded { get; private set; }
        public int FramesSent { get; private set; }
        public long BytesSent { get; private set; }
        public int LastFrameBytes { get; private set; }
        public int FramesDropped { get; private set; }

        /// <summary>Received frames for a speaker nobody is listening to yet.</summary>
        public int FramesUnrouted { get; private set; }

        public string LastError => capture.LastError ?? encoder?.LastError;

        #endregion

        public void Start()
        {
            if (running) return;

            encoder = new OpusVoiceEncoder(24000);
            captureBuffer = new float[capture.MaxSamplesPerRead];

            running = true;

            worker = new Thread(Run)
            {
                Name = "lstwoMODS Voice",
                IsBackground = true,

                // Above normal because the deadline is hard and tiny: miss it and the audio is not
                // late, it is gone. Still below anything the game itself runs at.
                Priority = System.Threading.ThreadPriority.AboveNormal
            };

            worker.Start();
        }

        #region Capture control (game thread)

        /// <summary>
        /// Brings capture in line with what the settings ask for. Cheap to call every frame: the
        /// expensive part, listing devices, only happens on a slow timer or when the requested
        /// device changes.
        /// </summary>
        public void EnsureCapture(bool wanted, string deviceName)
        {
            deviceName ??= "";

            if (!wanted)
            {
                if (capture.IsRunning)
                {
                    capture.Stop();
                    preprocessor.Reset();
                }

                return;
            }

            var deviceChanged = deviceName != requestedDevice;
            requestedDevice = deviceName;

            var now = Time.realtimeSinceStartup;

            if (!deviceChanged && capture.IsRunning && now < nextDeviceCheck)
                return;

            nextDeviceCheck = now + DeviceCheckSeconds;

            if (capture.IsRunning && !deviceChanged && !capture.Disconnected && capture.CheckAlive())
                return;

            var devices = MicrophoneCapture.Enumerate();
            var index = MicrophoneCapture.Resolve(deviceName, devices);

            if (index < 0)
            {
                if (capture.IsRunning) capture.Stop();
                return;
            }

            // Already on the right device and still healthy enough to leave alone.
            if (capture.IsRunning && !capture.Disconnected
                && capture.DeviceName == devices[index].Name && capture.CheckAlive())
                return;

            preprocessor.Reset();
            capture.Start(index);
        }

        #endregion

        #region Speakers (game thread)

        /// <summary>
        /// Starts decoding for a speaker. <paramref name="sink"/> is called on the worker thread with
        /// 16 bit PCM whenever a frame comes out, and must be safe to call from there.
        /// </summary>
        public void RegisterSpeaker(uint speakerId, Action<byte[], int> sink)
        {
            receivers[speakerId] = new VoiceReceiveStream(new OpusVoiceDecoder(), sink);
        }

        public void RemoveSpeaker(uint speakerId)
        {
            if (receivers.TryRemove(speakerId, out var stream))
                stream.Dispose();
        }

        public VoiceReceiveStream GetReceiver(uint speakerId)
            => receivers.TryGetValue(speakerId, out var stream) ? stream : null;

        #endregion

        #region Queues (game thread)

        /// <summary>Hands a received frame to the worker. The transport's buffer is copied.</summary>
        public void SubmitInbound(uint speakerId, byte[] data, int offset, int length)
        {
            if (data == null || length <= 0 || length > VoiceFormat.MaxPayloadBytes + VoiceFrame.HeaderBytes)
                return;

            var copy = new byte[length];
            Buffer.BlockCopy(data, offset, copy, 0, length);

            inbound.Enqueue(new InboundFrame { SpeakerId = speakerId, Data = copy, Length = length });
            wake.Set();
        }

        /// <summary>
        /// Sends everything the worker has encoded since the last call. Runs on the game thread
        /// because that is where the transports insist on being called from.
        /// </summary>
        public void DrainOutbound(Action<byte[], int> send)
        {
            while (outbound.TryDequeue(out var frame))
            {
                FramesSent++;
                BytesSent += frame.Length;
                LastFrameBytes = frame.Length;

                try
                {
                    send(frame.Data, frame.Length);
                }
                catch (Exception e)
                {
                    Plugin.LogSource.LogError($"[VoiceChat] sending a voice frame threw: {e}");
                }
            }
        }

        #endregion

        #region Worker

        private void Run()
        {
            while (running)
            {
                try
                {
                    // The wait is inside the guard as well: if Dispose gives up waiting for this
                    // thread and closes the handle, an unhandled exception here would take the whole
                    // game down rather than just the worker.
                    wake.WaitOne(TickMs);

                    PumpCapture();
                    PumpInbound();
                }
                catch (Exception e)
                {
                    // The worker must outlive anything one bad frame can do to it.
                    Plugin.LogSource.LogError($"[VoiceChat] voice worker: {e}");

                    if (!running) return;
                }
            }
        }

        private void PumpCapture()
        {
            if (!capture.IsRunning)
            {
                IsSending = false;
                return;
            }

            var count = capture.Read(captureBuffer);
            if (count <= 0) return;

            // Always preprocess, even while muted. The noise floor tracker is only useful if it has
            // been watching the room the whole time rather than starting from scratch at the moment
            // somebody presses the talk key.
            preprocessor.Process(captureBuffer, 0, count);

            var sending = transmitArmed
                          && (forceOpenGate || !preprocessor.NoiseGateEnabled || preprocessor.IsOpen);

            IsSending = sending;

            var read = 0;

            while (read < count)
            {
                var take = Math.Min(VoiceFormat.FrameSamples - frameFill, count - read);
                Array.Copy(captureBuffer, read, frameBuffer, frameFill, take);

                frameFill += take;
                read += take;

                if (frameFill < VoiceFormat.FrameSamples) continue;

                frameFill = 0;

                if (sending) EncodeFrame();
                else wasSending = false;
            }
        }

        private void EncodeFrame()
        {
            var written = encoder.Encode(frameBuffer, 0, encodeBuffer,
                VoiceFrame.HeaderBytes, VoiceFormat.MaxPayloadBytes);

            if (written <= 0)
            {
                FramesDropped++;
                return;
            }

            // The first frame of a burst tells the far end to resynchronise rather than try to
            // conceal however long the silence before it lasted.
            var flags = wasSending ? (byte)0 : VoiceFrame.FlagSpeechStart;
            wasSending = true;

            VoiceFrame.WriteHeader(encodeBuffer, flags, sequence);
            sequence++;

            FramesEncoded++;

            // Encoded into a scratch buffer and copied out at its real size: a frame is around
            // seventy bytes and the queue would otherwise carry the whole worst case every time.
            var length = VoiceFrame.HeaderBytes + written;
            var frame = new byte[length];
            Buffer.BlockCopy(encodeBuffer, 0, frame, 0, length);

            outbound.Enqueue(new OutboundFrame { Data = frame, Length = length });
        }

        private void PumpInbound()
        {
            while (inbound.TryDequeue(out var frame))
            {
                if (!receivers.TryGetValue(frame.SpeakerId, out var stream))
                {
                    FramesUnrouted++;
                    continue;
                }

                stream.Push(frame.Data, 0, frame.Length);
            }
        }

        #endregion

        public void Dispose()
        {
            running = false;
            wake.Set();

            // Joined rather than abandoned: the worker owns the FMOD lock inside capture, and letting
            // it run on into a disposed sound is exactly the kind of crash nobody can reproduce.
            if (worker != null && worker.IsAlive && !worker.Join(500))
                Plugin.LogSource.LogWarning("[VoiceChat] voice worker did not stop in time");

            worker = null;

            capture.Dispose();
            encoder?.Dispose();
            encoder = null;

            foreach (var stream in new List<VoiceReceiveStream>(receivers.Values))
                stream.Dispose();

            receivers.Clear();
            wake.Close();
        }
    }
}
