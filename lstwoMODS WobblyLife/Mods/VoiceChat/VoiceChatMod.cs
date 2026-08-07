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
using WLProxChat.Transport;
using CorePlugin = lstwoMODS_Core.Plugin;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat;

public class VoiceChatMod : BaseMod
{
    public override string Name => "Voice Chat";

    public override string Description =>
        $"Proximity voice chat over Steam. Hold {VoiceChatSettings.PushToTalkKey} to talk, "
        + $"press {VoiceChatSettings.MuteKey} to mute yourself.";

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

        BindData(Volume,        nameof(Volume),        1f);
        BindData(SpacialBlend,  nameof(SpacialBlend),  1f);
        BindData(MinDistance,   nameof(MinDistance),   10f);
        BindData(MaxDistance,   nameof(MaxDistance),   250f);
        BindData(VoiceRolloff,  nameof(VoiceRolloff),  VoiceRolloffMode.LinearSquare);

        BindData(Muffle,       nameof(Muffle),       true);
        BindData(MuffleCutoff, nameof(MuffleCutoff), 700f);

        BindData(Latency,         nameof(Latency),         VoiceLatencyMode.Normal);
        BindData(DirectVoicePort, nameof(DirectVoicePort), 8081);

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

    public static readonly Ref<int> DirectVoicePort = VoiceChatSettings.DirectVoicePortRef;

    private static readonly Ref<bool> PortDisabled = new(true);
    private static readonly Ref<string> PortNote = new("");

    #endregion

    #region Debug

    public static readonly Ref<bool> ShowDebug = VoiceChatSettings.ShowDebugRef;

    private static ModNetworkIndicator _netStatus;

    public override Container BuildPanel(string id)
    {
        _netStatus = new ModNetworkIndicator("voice-net-status", () => VoiceTransport.IsReady);

        var elements = new List<BaseUIElement>
        {
            _netStatus,

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
            ? "Only used for LAN / direct IP games. Steam lobbies carry voice over Steam itself."
            : locked
                ? $"In use on port {VoiceTransport.ActivePort}. Close the lobby to change it."
                : "Forward this alongside the game port, then host from the LAN Multiplayer mod. "
                + "Clients are sent it automatically when they join. 0 picks any free port.");
    }

    private static void SetIf(Ref<bool> r, bool v) { if (r.Value != v) r.Value = v; }
    private static void SetIf(Ref<string> r, string v) { if (r.Value != v) r.Value = v; }
}
