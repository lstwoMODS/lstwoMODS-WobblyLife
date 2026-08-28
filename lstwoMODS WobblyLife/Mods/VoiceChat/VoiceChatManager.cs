using System;
using System.Collections.Generic;
using HawkNetworking;
using lstwoMODS.ImGui.Shared;
using lstwoMODS_Core.Hotkeys;
using UnityEngine;
using WLProxChat;
using WLProxChat.Audio;
using WLProxChat.Transport;
using Color = UnityEngine.Color;
using Plugin = lstwoMODS_WobblyLife.Plugin;

/// <summary>
/// Drives voice chat: owns the capture and codec pipeline, hands the frames it produces to whichever
/// <see cref="IVoiceTransport"/> is running, and plays what comes back through one
/// <see cref="FmodVoiceStream"/> per speaker.
/// <para>
/// Nothing here knows how audio is captured or compressed, and nothing here knows how frames
/// travel. It is the piece in the middle: hotkeys and settings in, packets out, packets in, spatial
/// playback out.
/// </para>
/// </summary>
public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance;

    /// <summary>Stands in for our own speaker id on the loopback stream.</summary>
    private const uint LoopbackSpeakerId = VoiceIdentity.Loopback;

    private VoicePipeline pipeline;

    private bool isMuted;

    /// <summary>One stream per speaker, keyed by <see cref="VoiceIdentity"/> speaker id.</summary>
    private readonly Dictionary<uint, VoicePlayer> speakers = new();

    /// <summary>
    /// One remote speaker: their playback stream, and where in the world it is coming from.
    /// <para>
    /// The lock exists for exactly one race. Frames are decoded on the voice worker and enqueued
    /// from there, while the stream itself is created and destroyed on the game thread as players
    /// come and go. Everything else about <see cref="FmodVoiceStream"/> is already single producer.
    /// </para>
    /// </summary>
    private class VoicePlayer
    {
        public uint speakerId;
        public FmodVoiceStream stream;
        public PlayerController controller;
        public Vector3 position;

        /// <summary>False until we have located this speaker's body at least once.</summary>
        public bool hasPosition;

        public bool isLocal;
        public int packetsReceived;

        private readonly object sync = new object();
        private bool disposed;

        /// <summary>Worker thread. Feeds decoded PCM to the stream.</summary>
        public void Enqueue(byte[] pcm, int count)
        {
            lock (sync)
            {
                if (!disposed) stream.Enqueue(pcm, count);
            }
        }

        /// <summary>Game thread.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;

                disposed = true;
                stream.Dispose();
            }
        }
    }

    void Awake()
    {
        Instance = this;

        pipeline = new VoicePipeline();
        pipeline.Start();

        VoiceTransport.PacketReceived += OnVoicePacket;
        VoiceTransport.Refresh();

        Plugin.LogSource.LogInfo("[VoiceChat] runtime started");
    }

    void Update()
    {
        UpdateMicrophone();

        // Sending happens here rather than on the worker because that is where the transports
        // insist on being called from.
        pipeline.DrainOutbound(SendFrame);

        UpdateStreams();

        // Pushed every frame rather than on the toggle: the indicator also has to follow the
        // mode setting changing and ToggleMute being called from outside (macros, commands).
        VoiceChatMod.SetMuteIndicator(isMuted);
    }

    #region Capture + Send

    private void UpdateMicrophone()
    {
        if (Pressed(MuteBinding()))
            isMuted = !isMuted;

        var enabled = VoiceChatSettings.Enabled;
        var mode = VoiceChatSettings.Mode;

        var pushToTalk = mode == VoiceChatMode.PushToTalk && Held(PushToTalkBinding());

        // Recording stops entirely while muted or switched off, rather than being captured and
        // thrown away. It costs the noise floor tracker a second to settle again afterwards, which
        // is a fair price for the microphone light going out when someone mutes themselves.
        pipeline.EnsureCapture(enabled && !isMuted && mode != VoiceChatMode.Off,
            VoiceChatSettings.MicrophoneDevice);

        pipeline.ForceOpenGate = pushToTalk;

        pipeline.TransmitArmed = enabled && !isMuted
                                 && (mode == VoiceChatMode.AlwaysOn
                                     || (mode == VoiceChatMode.PushToTalk && pushToTalk));

        var preprocessor = pipeline.Preprocessor;

        preprocessor.InputGainDb = VoiceChatSettings.InputGain;
        preprocessor.NoiseGateEnabled = VoiceChatSettings.NoiseGate;
        preprocessor.GateThresholdDb = VoiceChatSettings.GateThreshold;
        preprocessor.AutoGainEnabled = VoiceChatSettings.AutoGain;

        pipeline.Bitrate = VoiceChatSettings.Bitrate * 1000;

        // Monitoring off: retire the loopback stream rather than leaving a silent one behind.
        if (!VoiceChatSettings.HearYourself && speakers.ContainsKey(LoopbackSpeakerId))
            RemoveSpeaker(LoopbackSpeakerId);
    }

    private void SendFrame(byte[] data, int length)
    {
        // No transport delivers a frame back to its sender, so hearing yourself is a local loopback
        // rather than a round trip. Feeding it back through the decoder rather than straight to the
        // stream means the monitor is genuinely what everyone else hears, codec and all, and it
        // works with nobody else connected, which is exactly when you want to test your microphone.
        if (VoiceChatSettings.HearYourself)
        {
            if (!speakers.TryGetValue(LoopbackSpeakerId, out var loopback))
            {
                loopback = CreateSpeaker(LoopbackSpeakerId, LocalController());
                loopback.isLocal = true;
            }

            loopback.packetsReceived++;
            pipeline.SubmitInbound(LoopbackSpeakerId, data, 0, length);
        }

        VoiceTransport.Broadcast(data, length);
    }

    #endregion

    #region Receive

    private void OnVoicePacket(VoicePacket packet)
    {
        if (!VoiceChatSettings.Enabled) return;

        if (!speakers.TryGetValue(packet.SpeakerId, out var vp))
            vp = CreateSpeaker(packet.SpeakerId, VoiceIdentity.Resolve(packet.SpeakerId));

        vp.packetsReceived++;

        // The transport's buffer is only valid for this call, so the pipeline copies it.
        pipeline.SubmitInbound(packet.SpeakerId, packet.Data, packet.Offset, packet.Length);
    }

    #endregion

    #region Speakers

    private VoicePlayer CreateSpeaker(uint speakerId, PlayerController controller)
    {
        var vp = new VoicePlayer
        {
            speakerId = speakerId,
            stream = new FmodVoiceStream("Voice_" + speakerId, VoiceFormat.SampleRate),
            controller = controller
        };

        speakers[speakerId] = vp;
        pipeline.RegisterSpeaker(speakerId, vp.Enqueue);

        return vp;
    }

    private static PlayerController LocalController()
        => GameInstance.InstanceExists ? GameInstance.Instance.GetFirstLocalPlayerController() : null;

    public void RemoveSpeaker(uint speakerId)
    {
        if (!speakers.TryGetValue(speakerId, out var vp))
            return;

        // Decoding stops before the stream goes away, so nothing is left mid frame with a disposed
        // sink to write into.
        pipeline.RemoveSpeaker(speakerId);

        vp.Dispose();
        speakers.Remove(speakerId);
    }

    private void UpdateStreams()
    {
        var settings = VoiceChatSettings.ForStream();

        foreach (var vp in speakers.Values)
        {
            // Keep retrying: the player object often shows up after the first frame does.
            if (vp.controller == null)
                vp.controller = vp.isLocal ? LocalController() : VoiceIdentity.Resolve(vp.speakerId);

            var body = vp.controller == null
                ? null
                : vp.controller.GetPlayerCharacter()?.GetPlayerBody();

            if (body != null)
            {
                vp.position = body.transform.position;
                vp.hasPosition = true;
            }

            // A speaker we cannot place would otherwise sit at the world origin, where 3D falloff
            // makes them silent for no visible reason. Park them on the listener until we can.
            vp.stream.UpdateSpatial(vp.hasPosition ? vp.position : FmodVoiceStream.ListenerPosition(), settings);
        }
    }

    public void ToggleMute() => isMuted = !isMuted;

    void OnDestroy()
    {
        VoiceTransport.PacketReceived -= OnVoicePacket;

        // Nothing ticks Update() any more, so the badge would otherwise hang around.
        VoiceChatMod.SetMuteIndicator(false);

        // Stops the worker before anything it writes into is torn down.
        pipeline?.Dispose();
        pipeline = null;

        foreach (var vp in speakers.Values)
            vp.Dispose();

        speakers.Clear();

        if (Instance == this) Instance = null;
    }

    #endregion

    #region Hotkeys

    // Parsed on change rather than every frame. Game thread only, so no locking needed.
    private string pushToTalkText;
    private string muteText;
    private HotkeyBinding pushToTalk = new(KeyCode.T, HotkeyModifiers.None);
    private HotkeyBinding mute = new(KeyCode.N, HotkeyModifiers.None);

    private HotkeyBinding PushToTalkBinding()
    {
        var text = VoiceChatSettings.PushToTalkKey;
        if (text == pushToTalkText) return pushToTalk;

        pushToTalkText = text;
        pushToTalk = Parse(text, KeyCode.T);
        return pushToTalk;
    }

    private HotkeyBinding MuteBinding()
    {
        var text = VoiceChatSettings.MuteKey;
        if (text == muteText) return mute;

        muteText = text;
        mute = Parse(text, KeyCode.N);
        return mute;
    }

    private static HotkeyBinding Parse(string text, KeyCode fallback)
        => HotkeyBinding.TryParse(text, out var binding)
            ? binding
            : new HotkeyBinding(fallback, HotkeyModifiers.None);

    /// <summary>True while the binding's key and every one of its modifiers are held down.</summary>
    private static bool Held(HotkeyBinding binding)
        => binding.Key != KeyCode.None && Input.GetKey(binding.Key) && ModifiersHeld(binding.Modifiers);

    /// <summary>True on the frame the binding's key goes down with its modifiers held.</summary>
    private static bool Pressed(HotkeyBinding binding)
        => binding.Key != KeyCode.None && Input.GetKeyDown(binding.Key) && ModifiersHeld(binding.Modifiers);

    private static bool ModifiersHeld(HotkeyModifiers modifiers)
    {
        if ((modifiers & HotkeyModifiers.Ctrl) != 0
            && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            return false;

        if ((modifiers & HotkeyModifiers.Shift) != 0
            && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return false;

        if ((modifiers & HotkeyModifiers.Alt) != 0
            && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            return false;

        return true;
    }

    #endregion

    #region Levels

    /// <summary>Microphone level as a 0..1 meter reading, mapped across a 60 dB window.</summary>
    public float InputMeter => ToMeter(pipeline?.Preprocessor?.Level ?? 0f);

    /// <summary>Where the gate's open threshold currently sits, on the same scale as the meter.</summary>
    public float GateMeter
    {
        get
        {
            var preprocessor = pipeline?.Preprocessor;
            if (preprocessor == null) return 0f;

            return ToMeter(preprocessor.NoiseFloor * Mathf.Pow(10f, VoiceChatSettings.GateThreshold / 20f));
        }
    }

    /// <summary>True while audio is actually being encoded and sent.</summary>
    public bool IsTransmitting => pipeline != null && pipeline.IsSending;

    public bool IsMuted => isMuted;

    public bool CaptureRunning => pipeline != null && pipeline.Capture.IsRunning;

    public string CaptureDevice => pipeline?.Capture.DeviceName ?? "";

    /// <summary>Linear RMS onto a 0..1 bar, using dB because that is how loudness is heard.</summary>
    private static float ToMeter(float rms)
    {
        if (rms <= 0.0001f) return 0f;

        var db = 20f * Mathf.Log10(rms);
        return Mathf.Clamp01((db + 60f) / 60f);
    }

    #endregion

    #region Debug

    private void OnGUI()
    {
        // The overlay variant is a UI element built in VoiceChatMod, not drawn from here.
        if (isMuted && VoiceChatSettings.MutedIndicator == MutedIndicatorMode.InGame)
        {
            GUI.contentColor = Color.red;
            GUI.Label(new Rect(10, 10, 100, 20), "Muted");
            GUI.contentColor = Color.white;
        }

        if (VoiceChatSettings.ShowDebug)
            DrawDebugWindow();
    }

    private void DrawDebugWindow()
    {
        GUILayout.BeginArea(new Rect(10, 40, 520, 700), "Voice Chat Debug", GUI.skin.window);
        GUILayout.Space(16);

        GUILayout.Label($"Enabled: {VoiceChatSettings.Enabled}    Mode: {VoiceChatSettings.Mode}    Muted: {isMuted}");
        GUILayout.Label($"Hear Yourself: {VoiceChatSettings.HearYourself}    Latency: {VoiceChatSettings.Latency}");

        GUILayout.Space(8);
        GUILayout.Label("--- Transport ---");
        GUILayout.Label($"Active: {VoiceTransport.ActiveMode}    Manager: {HawkNetworkManager.DefaultInstance?.GetType().Name ?? "none"}");
        GUILayout.Label(VoiceTransport.Status);

        var me = HawkNetworkManager.DefaultInstance?.GetMe();
        GUILayout.Label($"My connection id: {(me == null ? "n/a" : me.Id.ToString())}    Host: {HawkNetworkManager.DefaultInstance?.IsServer()}");

        GUILayout.Space(8);
        GUILayout.Label("--- Microphone ---");

        var capture = pipeline?.Capture;
        var preprocessor = pipeline?.Preprocessor;

        if (capture == null || preprocessor == null)
        {
            GUILayout.Label("pipeline is not running");
        }
        else
        {
            GUILayout.Label(capture.IsRunning
                ? $"\"{capture.DeviceName}\" at {capture.DeviceSampleRate} Hz, {capture.DeviceChannels} ch"
                  + $" -> {VoiceFormat.SampleRate} Hz mono"
                : "not recording");

            GUILayout.Label($"Gate: {(preprocessor.IsOpen ? "open" : "shut")}    Sending: {pipeline.IsSending}"
                          + $"    Overruns: {capture.Overruns}");

            GUILayout.Label($"Level: {Db(preprocessor.Level)}    Noise floor: {Db(preprocessor.NoiseFloor)}"
                          + $"    Auto gain: x{preprocessor.AutoGain:0.00}");

            GUILayout.Label($"Codec: {pipeline.CodecName} at {VoiceChatSettings.Bitrate} kbps");
            GUILayout.Label($"Encoded: {pipeline.FramesEncoded}    Sent: {pipeline.FramesSent}"
                          + $"    Dropped: {pipeline.FramesDropped}    Last: {pipeline.LastFrameBytes} B");
            GUILayout.Label($"Outgoing: {pipeline.BytesSent / 1024f:0.0} KiB total"
                          + $"    Unrouted: {pipeline.FramesUnrouted}");
        }

        GUILayout.Space(8);
        GUILayout.Label($"--- Streams ({speakers.Count}) ---");

        if (speakers.Count == 0)
        {
            GUILayout.Label("No streams yet. Nothing has been received or looped back.");
        }
        else
        {
            var listener = FmodVoiceStream.ListenerPosition();

            foreach (var vp in speakers.Values)
            {
                var stream = vp.stream;
                var receiver = pipeline?.GetReceiver(vp.speakerId);

                var label = vp.isLocal
                    ? "you (loopback)"
                    : vp.speakerId == VoiceIdentity.Unknown
                        ? "unplaced speaker"
                        : $"speaker {vp.speakerId}";

                GUILayout.Space(4);
                GUILayout.Label($"[{label}] packets: {vp.packetsReceived}");

                if (receiver != null)
                    GUILayout.Label($"  decoded: {receiver.FramesDecoded}   concealed: {receiver.FramesConcealed}"
                                  + $"   recovered: {receiver.FramesRecovered}   late: {receiver.FramesLate}"
                                  + $"   rejected: {receiver.FramesRejected}");

                GUILayout.Label($"  valid: {stream.IsValid}   playing: {stream.IsPlaying}   priming: {stream.IsPriming}");
                GUILayout.Label($"  buffered: {stream.BufferedSeconds * 1000f:0} ms   underruns: {stream.Underruns}");
                GUILayout.Label($"  queued: {stream.BytesQueued} B   dropped: {stream.BytesDropped} B");
                GUILayout.Label($"  placed: {vp.hasPosition}   distance: {Vector3.Distance(vp.position, listener):0.0} m");
            }
        }

        var error = pipeline?.LastError;

        if (!string.IsNullOrEmpty(error))
        {
            GUILayout.Space(8);
            GUI.contentColor = Color.red;
            GUILayout.Label($"Last error: {error}");
            GUI.contentColor = Color.white;
        }

        GUILayout.EndArea();
    }

    private static string Db(float rms)
        => rms <= 0.00001f ? "silent" : $"{20f * Mathf.Log10(rms):0.0} dB";

    #endregion
}
