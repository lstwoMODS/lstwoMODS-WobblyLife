using System;
using System.Collections.Generic;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS.ImGui.Shared;
using lstwoMODS_Core.Hotkeys;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife;
using UnityEngine;
using WLProxChat.Audio;
using WLProxChat.Transport;
using CorePlugin = lstwoMODS_Core.Plugin;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat;

public class VoiceChatMod : BaseMod
{
    public override string Name => "Voice Chat";

    public override string Description =>
        $"Proximity voice chat with its own microphone capture and Opus compression. "
        + $"Hold {VoiceChatSettings.PushToTalkKey} to talk, press {VoiceChatSettings.MuteKey} to mute yourself.";

    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    protected override void OnStaticInit()
    {
        base.OnStaticInit();

        try
        {
            VoiceNetworking.Initialize();
        }
        catch (Exception e)
        {
            Plugin.LogSource.LogError($"[VoiceChat] voice networking failed to initialise: {e}");
        }
    }

    private static VoiceChatManager _runtime;

    private static void EnsureRuntime()
    {
        if (_runtime != null) return;

        var go = new GameObject("lstwoMODS_VoiceChat");
        UnityEngine.Object.DontDestroyOnLoad(go);

        _runtime = go.AddComponent<VoiceChatManager>();
    }

    protected override void Awake()
    {
        base.Awake();

        try
        {
            EnsureRuntime();
        }
        catch (Exception e)
        {
            Plugin.LogSource.LogError($"[VoiceChat] could not start the voice chat runtime: {e}");
        }

        BindData(EnableVoiceChat,       nameof(EnableVoiceChat),       false);
        BindData(MicrophoneMode,        nameof(MicrophoneMode),        VoiceChatMode.PushToTalk);
        BindData(EnableHearingYourself, nameof(EnableHearingYourself), false);
        BindData(MutedIndicator,        nameof(MutedIndicator),        MutedIndicatorMode.InGame);

        BindData(Volume,        nameof(Volume),        1f);
        BindData(SpacialBlend,  nameof(SpacialBlend),  1f);
        BindData(MinDistance,   nameof(MinDistance),   10f);
        BindData(MaxDistance,   nameof(MaxDistance),   250f);
        BindData(VoiceRolloff,  nameof(VoiceRolloff),  VoiceRolloffMode.LinearSquare);

        BindData(Muffle,       nameof(Muffle),       true);
        BindData(MuffleCutoff, nameof(MuffleCutoff), 700f);

        BindData(Latency,         nameof(Latency),         VoiceLatencyMode.Normal);
        BindData(DirectVoicePort, nameof(DirectVoicePort), 8081);

        BindData(InputGain,     nameof(InputGain),     0f);
        BindData(NoiseGate,     nameof(NoiseGate),     true);
        BindData(GateThreshold, nameof(GateThreshold), 9f);
        BindData(AutoGain,      nameof(AutoGain),      true);
        BindData(Bitrate,       nameof(Bitrate),       24);

        BindData(VoiceChatSettings.MicrophoneDeviceRef, "MicrophoneDevice", "");
        BindData(VoiceChatSettings.PushToTalkKeyRef, "PushToTalkKey", "T");
        BindData(VoiceChatSettings.MuteKeyRef,       "MuteKey",       "N");
    }

    #region Microphone

    [ModSetting(Label = "Enable Voice Chat", Order = 0, Description = "Turns voice chat off entirely: nothing is recorded, sent, or played back.")]
    public static readonly Ref<bool> EnableVoiceChat = VoiceChatSettings.EnabledRef;

    [ModSetting(Label = "Microphone Mode", Order = 10, Description = "Off records nothing. PushToTalk transmits while T is held. "
                                                                     + "AlwaysOn transmits continuously. N mutes you in any mode.")]
    public static readonly Ref<VoiceChatMode> MicrophoneMode = VoiceChatSettings.ModeRef;

    [ModSetting(Label = "Enable Hearing Yourself", Order = 20, Description = "Loops your own voice back to you locally. Useful for checking your mic, "
                                                                             + "distracting to leave on.")]
    public static readonly Ref<bool> EnableHearingYourself = VoiceChatSettings.HearYourselfRef;

    [ModSetting(Label = "Muted Indicator", Order = 25, Description = "Where the reminder that you are muted is drawn. InGame paints a plain label on the "
                                                                     + "game itself. Overlay draws a badge on the lstwoMODS overlay window instead, which "
                                                                     + "looks nicer but is only visible while the overlay is running.")]
    public static readonly Ref<MutedIndicatorMode> MutedIndicator = VoiceChatSettings.MutedIndicatorRef;

    #endregion

    #region Input processing

    [ModSetting(Label = "Input Gain", Min = -20f, Max = 20f, Format = "%.0f dB", SeparatorText = "Input", Order = 26,
        Description = "Fixed boost or cut applied to your microphone before anything else. Leave it "
                    + "at 0 unless the meter above barely moves when you talk, or pins at the top.")]
    public static readonly Ref<float> InputGain = VoiceChatSettings.InputGainRef;

    [ModSetting(Label = "Noise Gate", Order = 27,
        Description = "Silences your microphone until you actually speak, measured against the "
                    + "background noise it hears the rest of the time. In Always On mode it also "
                    + "stops you transmitting at all, so a fan or a keyboard is not everyone else's "
                    + "problem. Push to talk always opens the gate while the key is held.")]
    public static readonly Ref<bool> NoiseGate = VoiceChatSettings.NoiseGateRef;

    [ModSetting(Label = "Noise Gate Sensitivity", Min = 3f, Max = 30f, Format = "%.0f dB", Order = 28,
        Description = "How far above the background noise you have to be for the gate to open. "
                    + "Lower opens more easily and lets more of the room through, higher needs a "
                    + "clearer voice. The marker on the meter above is where the threshold sits.")]
    public static readonly Ref<float> GateThreshold = VoiceChatSettings.GateThresholdRef;

    [ModSetting(Label = "Automatic Gain", Order = 29,
        Description = "Evens out how loud you come across, so a quiet talker is lifted and a loud "
                    + "one is brought down instead of clipping. Turn it off if you already run "
                    + "your microphone through something that does this.")]
    public static readonly Ref<bool> AutoGain = VoiceChatSettings.AutoGainRef;

    #endregion

    #region Muted indicator

    /// <summary>
    /// Open state of the overlay badge. The runtime pushes the live mute state in through
    /// <see cref="SetMuteIndicator"/> every frame; this ref is what actually shows and hides
    /// the window on the overlay side.
    /// </summary>
    private static readonly Ref<bool> MutedBadgeVisible = new(false);

    /// <summary>
    /// Called from <see cref="VoiceChatManager"/> with the live mute state. Cheap to call every
    /// frame: the ref is only written when the resolved visibility actually changes, so no
    /// element update goes over IPC while nothing has moved.
    /// </summary>
    public static void SetMuteIndicator(bool muted)
    {
        var show = muted && VoiceChatSettings.MutedIndicator == MutedIndicatorMode.Overlay;
        if (MutedBadgeVisible.Value != show)
            MutedBadgeVisible.Value = show;
    }

    /// <summary>
    /// Builds the overlay badge. Wired to <c>LstwoModsOverlay.OnConstructUI</c> from the plugin,
    /// so it lives at the overlay root rather than in the mods menu and is drawn whether or not
    /// the menu is open. Everything here is input-inert: <see cref="RequireInputMode.False"/>
    /// keeps the overlay window in pass-through, and NoInputs keeps ImGui itself from claiming
    /// the mouse over the badge.
    /// </summary>
    public static void BuildMutedIndicatorUI()
    {
        CorePlugin.Window.AddElement(
            new GuiWindow("voice-muted-indicator", "##voice-muted-indicator",
                new TextColored("voice-muted-indicator-text", $"{Lucide.MicOff} Muted", 1f, 0.35f, 0.35f)
                    .WithRequireInput(false)
            )
            .WithOpen(MutedBadgeVisible)
            .WithRequireInput(false)
            .WithNoClose()
            .WithFlags(
                ImGuiWindowFlags.NoDecoration          |
                ImGuiWindowFlags.NoMove                |
                ImGuiWindowFlags.NoInputs              |
                ImGuiWindowFlags.NoSavedSettings       |
                ImGuiWindowFlags.NoFocusOnAppearing    |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoDocking             |
                ImGuiWindowFlags.AlwaysAutoResize
            )
            .WithStyleVar(ImGuiStyleVar.WindowRounding, 6f)
            .WithStyleVar(ImGuiStyleVar.WindowPadding, 10f, 6f)
            .WithStyleColorAlpha(ImGuiCol.WindowBg, 0.55f)
            // Offset from the main viewport's top-left, cleared of the main menu bar so the
            // badge is still readable with the mods menu open.
            .PinToMainViewport()
            .WithPosition(12f, 40f, ImGuiCond.Always)
        );
    }

    #endregion

    #region Playback

    [ModSetting(Label = "Volume", Min = 0f, Max = 3.5f, Format = "%.2f", SeparatorText = "Playback", Order = 30,
        Description = "Playback volume for everyone else. 1.00 is unchanged, above that is boosted "
                    + "and can clip on loud speakers.")]
    public static readonly Ref<float> Volume = VoiceChatSettings.VolumeRef;

    [ModSetting(Label = "Spatial (3D) Blend", Min = 0f, Max = 1f, Format = "%.2f", Order = 40,
        Description = "0 plays every voice flat at the same volume wherever the speaker is. "
                    + "1 is fully positional: direction and distance both matter.")]
    public static readonly Ref<float> SpacialBlend = VoiceChatSettings.SpatialBlendRef;

    #endregion

    #region Distance

    [ModSetting(Label = "Full Volume Distance", Min = 1f, Max = 50f, Format = "%.0f m", SeparatorText = "Distance", Order = 50,
        Description = "Voices stay at full volume out to this range, then start falling off. This is "
                    + "the main loudness control for nearby players: raise it if people standing "
                    + "next to you sound too quiet.")]
    public static readonly Ref<float> MinDistance = VoiceChatSettings.MinDistanceRef;

    [ModSetting(Label = "Max Distance", Min = 50f, Max = 1000f, Format = "%.0f m", Order = 60,
        Description = "Range at which a voice has faded out completely. With the Logarithmic curve "
                    + "it is where fading stops rather than where silence begins.")]
    public static readonly Ref<float> MaxDistance = VoiceChatSettings.MaxDistanceRef;

    [ModSetting(Label = "Proximity Volume Rolloff Mode", Order = 70,
        Description = "Shape of the fade between the two distances. Logarithmic is gentle and never "
                    + "quite silent, Linear fades evenly to nothing, LinearSquare stays loud up close "
                    + "then drops off fast.")]
    public static readonly Ref<VoiceRolloffMode> VoiceRolloff = VoiceChatSettings.RolloffRef;

    #endregion

    #region Muffle

    [ModSetting(Label = "Muffle Distant Voices", SeparatorText = "Muffle", Order = 80,
        Description = "Progressively filters the treble out of far away voices, the way distance "
                    + "muffles a real one. Makes it much easier to tell who is close.")]
    public static readonly Ref<bool> Muffle = VoiceChatSettings.MuffleRef;

    [ModSetting(Label = "Muffle Strength", Min = 200f, Max = 5000f, Format = "%.0f Hz", Order = 90,
        Description = "Cutoff frequency reached at max distance. Lower is more muffled: around "
                    + "500 Hz sounds like a voice through a wall, 3000 Hz is barely noticeable.")]
    public static readonly Ref<float> MuffleCutoff = VoiceChatSettings.MuffleCutoffRef;

    #endregion

    #region Network

    [ModSetting(Label = "Buffer Size", SeparatorText = "Network", Order = 100,
        Description = "How much audio is buffered before it plays. Low is the most responsive but "
                    + "breaks up on an unstable connection, High adds noticeable delay but rides out "
                    + "packet loss. Raise this if voices sound choppy.")]
    public static readonly Ref<VoiceLatencyMode> Latency = VoiceChatSettings.LatencyRef;

    [ModSetting(Label = "Bitrate", Min = 8f, Max = 64f, Format = "%d kbps", Order = 105,
        Description = "How much bandwidth your voice is given. 24 kbps is clear speech; raising it "
                    + "helps most on a good microphone and costs everyone else download for every "
                    + "person talking. Lower it if voice is competing with the game for bandwidth.")]
    public static readonly Ref<int> Bitrate = VoiceChatSettings.BitrateRef;

    public static readonly Ref<int> DirectVoicePort = VoiceChatSettings.DirectVoicePortRef;

    private static readonly Ref<bool> PortDisabled = new(true);
    private static readonly Ref<string> PortNote = new("");

    #endregion

    #region Microphone device and level

    private static readonly Ref<string[]> DeviceItems = new(new[] { DefaultDeviceLabel });
    private static readonly Ref<int> DeviceIndex = new(0);
    private static MicrophoneCapture.DeviceInfo[] _devices = Array.Empty<MicrophoneCapture.DeviceInfo>();

    private const string DefaultDeviceLabel = "System Default";

    private static readonly Ref<float> InputLevel = new(0f);
    private static readonly Ref<string> InputLevelText = new("");

    private static float _nextMeterUpdate;

    /// <summary>
    /// Re-reads the device list. Not on a timer: asking FMOD for every driver's details is not free,
    /// and a microphone appearing mid game is rare enough to be worth a button.
    /// </summary>
    private static void RefreshDevices()
    {
        _devices = MicrophoneCapture.Enumerate();

        var items = new string[_devices.Length + 1];
        items[0] = DefaultDeviceLabel;

        for (var i = 0; i < _devices.Length; i++)
            items[i + 1] = _devices[i].IsConnected ? _devices[i].Name : _devices[i].Name + " (unavailable)";

        DeviceItems.Value = items;
        DeviceIndex.Value = IndexOfDevice(VoiceChatSettings.MicrophoneDevice);
    }

    private static int IndexOfDevice(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;

        for (var i = 0; i < _devices.Length; i++)
            if (_devices[i].Name == name)
                return i + 1;

        return 0;
    }

    private static void OnDeviceSelected(int index)
    {
        VoiceChatSettings.MicrophoneDeviceRef.Value =
            index <= 0 || index > _devices.Length ? "" : _devices[index - 1].Name;
    }

    /// <summary>
    /// Pushes the live input level into the meter. Rate limited and quantised because this runs
    /// every frame whether the panel is open or not, and every changed Ref is an element update
    /// travelling to the overlay process.
    /// </summary>
    private static void UpdateMeter()
    {
        if (Time.realtimeSinceStartup < _nextMeterUpdate) return;
        _nextMeterUpdate = Time.realtimeSinceStartup + 0.05f;

        var runtime = VoiceChatManager.Instance;
        var level = Mathf.Round((runtime?.InputMeter ?? 0f) * 50f) / 50f;

        if (!Mathf.Approximately(InputLevel.Value, level))
            InputLevel.Value = level;

        var text = !VoiceChatSettings.Enabled ? "voice chat is off"
            : runtime == null ? "starting"
            : runtime.IsMuted ? "muted"
            : !runtime.CaptureRunning ? "no microphone"
            : runtime.IsTransmitting ? "sending" : "listening";

        SetIf(InputLevelText, text);
    }

    #endregion

    #region Debug

    public static readonly Ref<bool> ShowDebug = VoiceChatSettings.ShowDebugRef;

    private static ModNetworkIndicator _netStatus;

    public override Container BuildPanel(string id)
    {
        _netStatus = new ModNetworkIndicator("voice-net-status", () => VoiceTransport.IsReady);

        RefreshDevices();

        var elements = new List<BaseUIElement>
        {
            _netStatus,

            new SeparatorText(id + ".MicrophoneSeparator", "Microphone"),

            new HStack(id + ".MicrophoneRow",
                    new Combo("##" + id + "-mic-device", Array.Empty<string>(), 0, OnDeviceSelected)
                        .WithItems(DeviceItems)
                        .WithSelectedIndex(DeviceIndex)
                        .WithTooltip("Which microphone to record from. Stored by name, so it follows "
                                   + "the device rather than its position in the list."),
                    new Button("Refresh##" + id + "-mic-refresh", RefreshDevices))
                .WithProportions(3f, 1f),

            new ProgressBar(id + ".MicrophoneLevel", sizeY: 18f)
                .WithValue(InputLevel)
                .WithOverlay(InputLevelText)
                .WithTooltip("Live input level. Talk normally: it should move well clear of where it "
                           + "sits when you are quiet, without pinning at the far end."),

            AutoUIBuilder.Build(this, id),

            new SeparatorText(id + ".KeysSeparator", "Keys"),
            KeyRow(id, "Push To Talk", VoiceChatSettings.PushToTalkKeyRef, KeyCode.T),
            KeyRow(id, "Toggle Mute", VoiceChatSettings.MuteKeyRef, KeyCode.N),

            new InputInt("Direct Voice Port##" + id + "-voice-port")
                .WithValue(DirectVoicePort)
                .WithDisabled(PortDisabled),

            new TextDisabled(id + ".PortNote", "").WithText(PortNote)
        };

        if (CorePlugin.DeveloperModeEntry?.Value == true)
        {
            elements.Add(new SeparatorText(id + ".DebugSeparator", "Debug"));
            elements.Add(new Checkbox("Show Voice Debug Overlay##" + id)
                .WithTooltip("Draws capture and per-stream diagnostics over the game: whether Steam "
                           + "is recording, packet counts, buffered audio, underruns, and how far "
                           + "away each speaker is being placed.")
                .WithValue(ShowDebug));
        }

        return new Container(id + ".Root", elements.ToArray());
    }

    private static BaseUIElement KeyRow(string id, string label, Ref<string> binding, KeyCode fallback)
    {
        var capture = new KeyCapture(id + "." + label + ".Capture");

        capture.WithInlineLabel(label);
        capture.WithDisplay(Describe(binding.Value, fallback));

        capture.OnCaptured += (key, modifiers) =>
        {
            var captured = new HotkeyBinding(VoiceKeyMap.ToKeyCode(key), modifiers);

            if (captured.Key == KeyCode.None)
            {
                Plugin.LogSource.LogWarning(
                    $"[VoiceChat] {key} cannot be used as a hotkey: Unity's input has no equivalent. "
                    + "Keeping the previous binding.");

                capture.Reset(Describe(binding.Value, fallback));
                return;
            }

            binding.Value = captured.ToString();
            capture.Reset(captured.ToString());
        };

        return capture;
    }

    private static string Describe(string stored, KeyCode fallback)
        => HotkeyBinding.TryParse(stored, out var parsed)
            ? parsed.ToString()
            : new HotkeyBinding(fallback, HotkeyModifiers.None).ToString();

    #endregion

    public override void Update()
    {
        _netStatus?.Tick();
        UpdatePortRow();
        UpdateMeter();
    }

    private static void UpdatePortRow()
    {
        var direct = HawkNetworkManager.InstanceExists
                     && HawkNetworkManager.DefaultInstance is LiteNetworkManager;

        var locked = VoiceTransport.PortLocked;

        if (locked && VoiceTransport.ActivePort > 0 && DirectVoicePort.Value != VoiceTransport.ActivePort)
            DirectVoicePort.Value = VoiceTransport.ActivePort;

        SetIf(PortDisabled, !direct || locked);

        SetIf(PortNote, !direct
            ? GameBuild.IsCrossplay
                ? "Only used for LAN / direct IP games. Online lobbies carry voice over the game's own "
                  + "channel, which needs the host to be running the mod."
                : "Only used for LAN / direct IP games. Steam lobbies carry voice over Steam itself."
            : locked
                ? $"In use on port {VoiceTransport.ActivePort}. Close the lobby to change it."
                : "Forward this alongside the game port, then host from the LAN Multiplayer mod. "
                + "Clients are sent it automatically when they join. 0 picks any free port.");
    }

    private static void SetIf(Ref<bool> r, bool v) { if (r.Value != v) r.Value = v; }
    private static void SetIf(Ref<string> r, string v) { if (r.Value != v) r.Value = v; }
}
