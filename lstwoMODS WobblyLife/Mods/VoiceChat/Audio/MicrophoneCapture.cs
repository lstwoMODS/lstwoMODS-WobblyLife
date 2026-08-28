using System;
using System.Runtime.InteropServices;
using FMODUnity;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Audio
{
    /// <summary>
    /// Records the microphone through the game's own FMOD system and hands back mono
    /// <see cref="VoiceFormat"/> audio.
    /// <para>
    /// FMOD rather than Unity's <c>Microphone</c> class: we already hold the core system for
    /// playback, so there is no second audio backend to keep alive, the driver list comes with real
    /// device names and native rates rather than names alone, and nothing here has to happen on the
    /// game thread. Recording writes into a looping sound on FMOD's own thread and we chase its write
    /// position, which is the same producer / consumer shape the playback side already uses.
    /// </para>
    /// <para>
    /// <see cref="Start"/> and <see cref="Stop"/> are called from the game thread, <see cref="Read"/>
    /// from the voice worker. They share a lock, which costs nothing at these rates and means a
    /// device change can never free the sound out from under a read in progress.
    /// </para>
    /// </summary>
    public sealed class MicrophoneCapture : IDisposable
    {
        /// <summary>
        /// Length of the ring FMOD records into. Only a few milliseconds are ever in flight; the rest
        /// is slack for the frames where the game stalls and nobody comes to collect.
        /// </summary>
        private const int RecordSeconds = 1;

        /// <summary>
        /// Most audio a single <see cref="Read"/> will pull. A poll that arrives late catches up over
        /// several reads rather than in one huge one, and anything older than this is discarded
        /// outright because playing half a second of stale voice is worse than dropping it.
        /// </summary>
        private const int MaxReadMs = 250;

        /// <summary>One recording device as FMOD sees it.</summary>
        public readonly struct DeviceInfo
        {
            public readonly int Index;
            public readonly string Name;
            public readonly int SampleRate;
            public readonly int Channels;
            public readonly bool IsDefault;
            public readonly bool IsConnected;

            public DeviceInfo(int index, string name, int sampleRate, int channels, bool isDefault, bool isConnected)
            {
                Index = index;
                Name = string.IsNullOrEmpty(name) ? "Device " + index : name;
                SampleRate = sampleRate;
                Channels = channels;
                IsDefault = isDefault;
                IsConnected = isConnected;
            }
        }

        private readonly object sync = new object();

        private FMOD.System system;
        private FMOD.Sound sound;

        private int driverIndex = -1;
        private uint soundFrames;
        private int bytesPerFrame;
        private uint lastPosition;

        private short[] interleaved;
        private short[] mono;
        private short[] resampled;
        private MonoResampler resampler;

        #region State

        public bool IsRunning { get; private set; }

        /// <summary>Name of the device recording is running on, for the UI and the logs.</summary>
        public string DeviceName { get; private set; } = "";

        public int DeviceSampleRate { get; private set; }
        public int DeviceChannels { get; private set; }

        /// <summary>Set when FMOD reports the device went away. The pipeline restarts on this.</summary>
        public bool Disconnected { get; private set; }

        /// <summary>Last failure, for the debug overlay. Null while everything is fine.</summary>
        public string LastError { get; private set; }

        /// <summary>Times audio was thrown away because nobody read it in time.</summary>
        public int Overruns { get; private set; }

        /// <summary>Largest buffer <see cref="Read"/> can return, for sizing the caller's buffer.</summary>
        public int MaxSamplesPerRead => VoiceFormat.SampleRate / 1000 * MaxReadMs + 512;

        #endregion

        #region Devices

        /// <summary>Every recording device FMOD can see. Game thread.</summary>
        public static DeviceInfo[] Enumerate()
        {
            try
            {
                var system = RuntimeManager.CoreSystem;

                if (system.getRecordNumDrivers(out var count, out _) != FMOD.RESULT.OK || count <= 0)
                    return Array.Empty<DeviceInfo>();

                var devices = new DeviceInfo[count];

                for (var i = 0; i < count; i++)
                {
                    var result = system.getRecordDriverInfo(i, out var name, 256, out _,
                        out var rate, out _, out var channels, out var state);

                    if (result != FMOD.RESULT.OK)
                    {
                        devices[i] = new DeviceInfo(i, "Device " + i, 0, 0, false, false);
                        continue;
                    }

                    devices[i] = new DeviceInfo(i, name, rate, channels,
                        (state & FMOD.DRIVER_STATE.DEFAULT) != 0,
                        (state & FMOD.DRIVER_STATE.CONNECTED) != 0);
                }

                return devices;
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] could not list recording devices: {e.Message}");
                return Array.Empty<DeviceInfo>();
            }
        }

        /// <summary>
        /// Finds the device a saved name refers to. Names are stored rather than indices because the
        /// index of a given microphone changes whenever another one is plugged in or removed.
        /// Falls back to whatever Windows considers the default.
        /// </summary>
        public static int Resolve(string deviceName, DeviceInfo[] devices)
        {
            if (devices == null || devices.Length == 0) return -1;

            if (!string.IsNullOrEmpty(deviceName))
                foreach (var device in devices)
                    if (device.IsConnected && device.Name == deviceName)
                        return device.Index;

            foreach (var device in devices)
                if (device.IsDefault && device.IsConnected)
                    return device.Index;

            foreach (var device in devices)
                if (device.IsConnected)
                    return device.Index;

            return -1;
        }

        #endregion

        #region Start / stop (game thread)

        public bool Start(int index)
        {
            lock (sync)
            {
                StopLocked();

                if (index < 0) return Fail("no recording device");

                try
                {
                    system = RuntimeManager.CoreSystem;

                    var info = system.getRecordDriverInfo(index, out var name, 256, out _,
                        out var rate, out _, out var channels, out var state);

                    if (info != FMOD.RESULT.OK) return Fail($"getRecordDriverInfo: {info}");
                    if ((state & FMOD.DRIVER_STATE.CONNECTED) == 0) return Fail("device is not connected");
                    if (rate <= 0 || channels <= 0) return Fail($"device reports an unusable format ({rate} Hz, {channels} ch)");

                    bytesPerFrame = channels * VoiceFormat.BytesPerSample;

                    var exinfo = new FMOD.CREATESOUNDEXINFO
                    {
                        cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
                        numchannels = channels,
                        defaultfrequency = rate,
                        format = FMOD.SOUND_FORMAT.PCM16,
                        length = (uint)(rate * bytesPerFrame * RecordSeconds)
                    };

                    // The recording target has to be a plain looping sample in the driver's own
                    // format. Asking for anything else is where FMOD starts refusing devices.
                    const FMOD.MODE mode = FMOD.MODE.OPENUSER | FMOD.MODE.LOOP_NORMAL
                                         | FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D;

                    var created = system.createSound((string)null, mode, ref exinfo, out sound);
                    if (created != FMOD.RESULT.OK)
                    {
                        sound.clearHandle();
                        return Fail($"createSound: {created}");
                    }

                    if (sound.getLength(out soundFrames, FMOD.TIMEUNIT.PCM) != FMOD.RESULT.OK || soundFrames == 0)
                    {
                        ReleaseSoundLocked();
                        return Fail("recording buffer has no length");
                    }

                    var started = system.recordStart(index, sound, true);
                    if (started != FMOD.RESULT.OK)
                    {
                        ReleaseSoundLocked();
                        return Fail($"recordStart: {started}");
                    }

                    driverIndex = index;
                    DeviceName = string.IsNullOrEmpty(name) ? "Device " + index : name;
                    DeviceSampleRate = rate;
                    DeviceChannels = channels;
                    lastPosition = 0;
                    Disconnected = false;
                    LastError = null;
                    lastLoggedError = null;

                    AllocateLocked(rate, channels);

                    IsRunning = true;

                    Plugin.LogSource.LogInfo(
                        $"[VoiceChat] recording from \"{DeviceName}\" at {rate} Hz, {channels} channel(s)"
                        + (resampler.IsPassThrough ? "" : $", resampling to {VoiceFormat.SampleRate} Hz"));

                    return true;
                }
                catch (Exception e)
                {
                    ReleaseSoundLocked();
                    return Fail(e.Message);
                }
            }
        }

        private void AllocateLocked(int rate, int channels)
        {
            // Sized off the device rate, since that is what the raw side of the conversion deals in.
            var deviceFrames = rate / 1000 * MaxReadMs + 64;

            interleaved = new short[deviceFrames * channels];
            mono = new short[deviceFrames];

            resampler = new MonoResampler(rate, VoiceFormat.SampleRate);
            resampled = new short[resampler.MaxOutputFor(deviceFrames)];
        }

        public void Stop()
        {
            lock (sync) StopLocked();
        }

        private void StopLocked()
        {
            if (driverIndex >= 0)
            {
                try { system.recordStop(driverIndex); }
                catch (Exception e) { LastError = e.Message; }
            }

            ReleaseSoundLocked();

            driverIndex = -1;
            IsRunning = false;
            DeviceName = "";
            DeviceSampleRate = 0;
            DeviceChannels = 0;
        }

        private void ReleaseSoundLocked()
        {
            if (!sound.hasHandle()) return;

            try { sound.release(); }
            catch (Exception e) { LastError = e.Message; }

            sound.clearHandle();
        }

        private string lastLoggedError;

        private bool Fail(string message)
        {
            LastError = message;

            // Starting is retried on a timer, so an unplugged microphone would otherwise write the
            // same warning to the log once a second for the rest of the session.
            if (message != lastLoggedError)
            {
                lastLoggedError = message;
                Plugin.LogSource.LogWarning($"[VoiceChat] microphone capture failed: {message}");
            }

            return false;
        }

        /// <summary>
        /// True while FMOD still believes the device is recording. Checked on a slow timer from the
        /// game thread: a headset can be unplugged mid game and FMOD will not tell anyone, it just
        /// stops producing audio.
        /// </summary>
        public bool CheckAlive()
        {
            lock (sync)
            {
                if (!IsRunning) return false;

                var result = system.isRecording(driverIndex, out var recording);

                if (result == FMOD.RESULT.ERR_RECORD_DISCONNECTED)
                {
                    Disconnected = true;
                    return false;
                }

                return result == FMOD.RESULT.OK && recording;
            }
        }

        #endregion

        #region Read (worker thread)

        /// <summary>
        /// Pulls everything recorded since the last call, converted to mono at
        /// <see cref="VoiceFormat.SampleRate"/>.
        /// </summary>
        /// <returns>Samples written to <paramref name="destination"/>.</returns>
        public int Read(float[] destination)
        {
            lock (sync)
            {
                if (!IsRunning || !sound.hasHandle()) return 0;

                try
                {
                    var result = system.getRecordPosition(driverIndex, out var position);

                    if (result == FMOD.RESULT.ERR_RECORD_DISCONNECTED)
                    {
                        Disconnected = true;
                        return 0;
                    }

                    if (result != FMOD.RESULT.OK || position == lastPosition) return 0;

                    var available = (position + soundFrames - lastPosition) % soundFrames;
                    var capacity = (uint)mono.Length;

                    // Fell too far behind to matter. Skip to the newest audio rather than playing
                    // catch up through a backlog nobody wants to hear.
                    if (available > capacity)
                    {
                        lastPosition = (position + soundFrames - capacity) % soundFrames;
                        available = capacity;
                        Overruns++;
                    }

                    var locked = sound.@lock(lastPosition * (uint)bytesPerFrame, available * (uint)bytesPerFrame,
                        out var ptr1, out var ptr2, out var len1, out var len2);

                    if (locked != FMOD.RESULT.OK)
                    {
                        LastError = $"lock: {locked}";
                        return 0;
                    }

                    var shorts1 = (int)(len1 / VoiceFormat.BytesPerSample);
                    var shorts2 = (int)(len2 / VoiceFormat.BytesPerSample);

                    if (shorts1 > 0) Marshal.Copy(ptr1, interleaved, 0, shorts1);
                    if (shorts2 > 0) Marshal.Copy(ptr2, interleaved, shorts1, shorts2);

                    sound.unlock(ptr1, ptr2, len1, len2);

                    lastPosition = (lastPosition + available) % soundFrames;

                    var frames = (shorts1 + shorts2) / DeviceChannels;
                    if (frames <= 0) return 0;

                    var monoCount = AudioConvert.DownmixToMono(interleaved, frames, DeviceChannels, mono);
                    var count = resampler.Process(mono, monoCount, resampled);

                    if (count > destination.Length) count = destination.Length;

                    AudioConvert.ShortsToFloats(resampled, count, destination);

                    return count;
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    return 0;
                }
            }
        }

        #endregion

        public void Dispose()
        {
            lock (sync) StopLocked();
        }
    }
}
