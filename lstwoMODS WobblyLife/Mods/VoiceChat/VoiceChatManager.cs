using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HawkNetworking;
using lstwoMODS.ImGui.Shared;
using lstwoMODS_Core.Hotkeys;
using UnityEngine;
using Steamworks;
using WLProxChat;
using WLProxChat.Transport;
using Color = UnityEngine.Color;
using Plugin = lstwoMODS_WobblyLife.Plugin;

/// <summary>
/// Drives voice chat: captures from Steam, hands frames to whichever <see cref="IVoiceTransport"/>
/// is running, and plays what comes back through one <see cref="FmodVoiceStream"/> per speaker.
/// Knows nothing about how frames actually travel.
/// </summary>
public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance;

    /// <summary>Stands in for our own connection id on the loopback stream.</summary>
    private const int LoopbackConnectionId = -2;

    private int sampleRate;
    private bool isMuted;
    private bool running;

    /// <summary>
    /// True once Steam's voice interface has answered. It is not necessarily up when the mod
    /// initialises, so this is re-probed rather than decided once: latching a single early failure
    /// disables capture for the whole session, with the mic silently never arming.
    /// </summary>
    private bool steamVoiceAvailable;

    private float nextSteamProbe;

    /// <summary>One stream per speaker, keyed by Hawk connection id.</summary>
    private readonly Dictionary<int, VoicePlayer> speakers = new();

    private class VoicePlayer
    {
        public int connectionId;
        public FmodVoiceStream stream;
        public PlayerController controller;
        public Vector3 position;

        /// <summary>False until we have located this speaker's body at least once.</summary>
        public bool hasPosition;

        public bool isLocal;
        public int packetsReceived;
    }

    void Awake()
    {
        Instance = this;

        // Placeholder until the first successful probe; no stream is built before then.
        sampleRate = 24000;

        VoiceTransport.PacketReceived += OnVoicePacket;
        VoiceTransport.Refresh();

        running = true;
        StartCoroutine(CaptureLoop());

        Plugin.LogSource.LogInfo("[VoiceChat] runtime started, waiting for steam voice");
    }

    /// <summary>
    /// Pin the decode rate so the FMOD streams' frequency matches what DecompressVoice produces.
    /// Retried on a slow timer: Steam's voice interface is routinely not up yet when mods
    /// initialise, and everything here (capture, decompress) is unusable until it answers.
    /// </summary>
    private bool EnsureSteamVoice()
    {
        if (steamVoiceAvailable) return true;

        var now = Time.realtimeSinceStartup;
        if (now < nextSteamProbe) return false;
        nextSteamProbe = now + 1f;

        try
        {
            if (!SteamClient.IsValid) return false;

            var optimal = SteamUser.OptimalSampleRate;

            // Facepunch throws out of the SampleRate setter outside this range, and a zero here just
            // means the interface answered before it was ready.
            if (optimal < 11025 || optimal > 48000) return false;

            SteamUser.SampleRate = optimal;
            sampleRate = (int)optimal;
            steamVoiceAvailable = true;

            Plugin.LogSource.LogInfo($"[VoiceChat] steam voice ready at {sampleRate} Hz");
            return true;
        }
        catch (Exception e)
        {
            lastError = $"steam voice probe: {e.Message}";
            return false;
        }
    }

    private void SetSteamVoiceRecord()
    {
        if (!steamVoiceAvailable) return;

        if (Pressed(MuteBinding()))
        {
            isMuted = !isMuted;
        }

        if (VoiceChatSettings.Enabled && !isMuted)
        {
            if (VoiceChatSettings.Mode == VoiceChatMode.Off)
            {
                SteamUser.VoiceRecord = false;
            }
            else if (VoiceChatSettings.Mode == VoiceChatMode.PushToTalk)
            {
                SteamUser.VoiceRecord = Held(PushToTalkBinding());
            }
            else if (VoiceChatSettings.Mode == VoiceChatMode.AlwaysOn)
            {
                SteamUser.VoiceRecord = true;
            }
        }
        else
        {
            SteamUser.VoiceRecord = false;
        }
    }

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

    void Update()
    {
        EnsureSteamVoice();
        SetSteamVoiceRecord();
        UpdateStreams();
    }

    #region Capture + Send

    private IEnumerator CaptureLoop()
    {
        var wait = new WaitForSeconds(0.02f);

        while (running)
        {
            yield return wait;

            if (!steamVoiceAvailable || !SteamUser.HasVoiceData || !VoiceChatSettings.Enabled || isMuted)
                continue;

            var data = SteamUser.ReadVoiceDataBytes();
            if (data == null || data.Length == 0)
                continue;

            packetsCaptured++;
            lastCapturedSize = data.Length;

            // No transport delivers a frame back to its sender, so hearing yourself is a local
            // loopback rather than a round trip. It also means this works with nobody else
            // connected, which is exactly when you want to test your mic.
            if (VoiceChatSettings.HearYourself)
                Loopback(data);

            VoiceTransport.Broadcast(data, data.Length);
        }
    }

    /// <summary>Feed our own captured voice straight back into a local stream.</summary>
    private void Loopback(byte[] compressed)
    {
        if (!speakers.TryGetValue(LoopbackConnectionId, out var vp))
        {
            vp = CreateSpeaker(LoopbackConnectionId, LocalController());
            vp.isLocal = true;
        }

        HandleVoicePacket(vp, compressed, 0, compressed.Length);
    }

    #endregion

    #region Receive

    private void OnVoicePacket(VoicePacket packet)
    {
        // DecompressVoice goes through the same interface capture does, so an inbound frame is just
        // as unusable until it is up.
        if (!VoiceChatSettings.Enabled || !steamVoiceAvailable) return;

        if (!speakers.TryGetValue(packet.ConnectionId, out var vp))
            vp = CreateSpeaker(packet.ConnectionId, ResolveController(packet.ConnectionId));

        HandleVoicePacket(vp, packet.Data, packet.Offset, packet.Length);
    }

    private MemoryStream decompressStream = new MemoryStream(1024 * 32);

    private void HandleVoicePacket(VoicePlayer vp, byte[] compressed, int offset, int length)
    {
        if (length <= 0) return;

        // DecompressVoice consumes the whole array it is handed, so a framed payload (or a shared
        // receive buffer) has to be lifted into an exactly sized one first.
        if (offset != 0 || length != compressed.Length)
        {
            var exact = new byte[length];
            Buffer.BlockCopy(compressed, offset, exact, 0, length);
            compressed = exact;
        }

        // Reset stream without reallocating
        decompressStream.Position = 0;
        decompressStream.SetLength(0);

        int written;

        try
        {
            written = SteamUser.DecompressVoice(compressed, decompressStream);
        }
        catch (Exception e)
        {
            lastError = e.Message;
            return;
        }

        lastDecompressedSize = written;

        if (written <= 0)
            return;

        vp.packetsReceived++;

        // IMPORTANT: do NOT call ToArray()
        vp.stream.Enqueue(decompressStream.GetBuffer(), written);
    }

    #endregion

    #region Speakers

    private VoicePlayer CreateSpeaker(int connectionId, PlayerController controller)
    {
        var vp = new VoicePlayer
        {
            connectionId = connectionId,
            stream = new FmodVoiceStream("Voice_" + connectionId, sampleRate),
            controller = controller
        };

        speakers[connectionId] = vp;
        return vp;
    }

    private static PlayerController LocalController()
        => GameInstance.InstanceExists ? GameInstance.Instance.GetFirstLocalPlayerController() : null;

    /// <summary>
    /// Hawk connection id to the player it owns. Goes through the replicated controllers rather than
    /// the manager's connection list, which only holds the full roster on the host. Returns null
    /// rather than throwing: a frame can easily arrive before the player object exists.
    /// </summary>
    private static PlayerController ResolveController(int connectionId)
    {
        try
        {
            if (!GameInstance.InstanceExists || connectionId < 0)
                return null;

            return GameInstance.Instance.GetPlayerControllers()
                .FirstOrDefault(c => c != null && c.networkObject?.GetOwner()?.Id == connectionId);
        }
        catch (Exception e)
        {
            Plugin.LogSource.LogWarning($"[VoiceChat] could not resolve controller for connection {connectionId}: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Utility

    private void UpdateStreams()
    {
        var settings = VoiceChatSettings.ForStream();

        foreach (var vp in speakers.Values)
        {
            // Keep retrying: the player object often shows up after the first frame does.
            if (vp.controller == null)
                vp.controller = vp.isLocal ? LocalController() : ResolveController(vp.connectionId);

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

    public void RemoveSpeaker(int connectionId)
    {
        if (!speakers.TryGetValue(connectionId, out var vp))
            return;

        vp.stream.Dispose();
        speakers.Remove(connectionId);
    }

    public void ToggleMute() => isMuted = !isMuted;

    void OnDestroy()
    {
        running = false;
        SteamUser.VoiceRecord = false;

        VoiceTransport.PacketReceived -= OnVoicePacket;

        foreach (var vp in speakers.Values)
            vp.stream.Dispose();

        speakers.Clear();

        if (Instance == this) Instance = null;
    }

    #endregion

    #region Debug

    private int packetsCaptured;
    private int lastCapturedSize;
    private int lastDecompressedSize;
    private string lastError = "";

    private void OnGUI()
    {
        if (isMuted)
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
        GUILayout.BeginArea(new Rect(10, 40, 480, 640), "Voice Chat Debug", GUI.skin.window);
        GUILayout.Space(16);

        GUILayout.Label($"Enabled: {VoiceChatSettings.Enabled}    Mode: {VoiceChatSettings.Mode}    Muted: {isMuted}");
        GUILayout.Label($"Hear Yourself: {VoiceChatSettings.HearYourself}");
        GUILayout.Label($"Sample Rate: {sampleRate} Hz    Latency: {VoiceChatSettings.Latency}");
        GUILayout.Label($"Steam voice ready: {steamVoiceAvailable}    SteamClient valid: {SteamClient.IsValid}");

        GUILayout.Space(8);
        GUILayout.Label("--- Transport ---");
        GUILayout.Label($"Active: {VoiceTransport.ActiveMode}    Manager: {HawkNetworkManager.DefaultInstance?.GetType().Name ?? "none"}");
        GUILayout.Label(VoiceTransport.Status);

        var me = HawkNetworkManager.DefaultInstance?.GetMe();
        GUILayout.Label($"My connection id: {(me == null ? "n/a" : me.Id.ToString())}    Host: {HawkNetworkManager.DefaultInstance?.IsServer()}");

        GUILayout.Space(8);
        GUILayout.Label("--- Microphone ---");
        GUILayout.Label($"VoiceRecord: {SteamUser.VoiceRecord}    HasVoiceData: {SteamUser.HasVoiceData}");
        GUILayout.Label($"Captured: {packetsCaptured}    Last: {lastCapturedSize} B compressed / {lastDecompressedSize} B pcm");

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
                var label = vp.isLocal ? "you (loopback)" : $"conn {vp.connectionId}";

                GUILayout.Space(4);
                GUILayout.Label($"[{label}] packets: {vp.packetsReceived}");
                GUILayout.Label($"  valid: {stream.IsValid}   playing: {stream.IsPlaying}   priming: {stream.IsPriming}");
                GUILayout.Label($"  buffered: {stream.BufferedSeconds * 1000f:0} ms   underruns: {stream.Underruns}");
                GUILayout.Label($"  queued: {stream.BytesQueued} B   dropped: {stream.BytesDropped} B");
                GUILayout.Label($"  placed: {vp.hasPosition}   distance: {Vector3.Distance(vp.position, listener):0.0} m");
            }
        }

        if (!string.IsNullOrEmpty(lastError))
        {
            GUILayout.Space(8);
            GUI.contentColor = Color.red;
            GUILayout.Label($"Last error: {lastError}");
            GUI.contentColor = Color.white;
        }

        GUILayout.EndArea();
    }

    #endregion
}
