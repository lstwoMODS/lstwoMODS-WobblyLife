using lstwoMODS_Core.UI;
using WLProxChat.Transport;

namespace WLProxChat
{
    public static class VoiceChatSettings
    {
        #region Microphone

        public static readonly Ref<bool> EnabledRef = new(false);
        public static readonly Ref<VoiceChatMode> ModeRef = new(VoiceChatMode.PushToTalk);
        public static readonly Ref<bool> HearYourselfRef = new(false);

        /// <summary>
        /// Held to transmit in PushToTalk mode, as a <c>HotkeyBinding</c> text form ("T", "Ctrl+V").
        /// Steam's own push-to-talk setting governs Steam's voice chat only and is not readable
        /// through Steamworks, so the binding has to live here.
        /// </summary>
        public static readonly Ref<string> PushToTalkKeyRef = new("T");

        /// <summary>Pressed to toggle your mic off entirely, in any mode.</summary>
        public static readonly Ref<string> MuteKeyRef = new("N");

        /// <summary>Where the muted reminder is drawn while the mic is toggled off.</summary>
        public static readonly Ref<MutedIndicatorMode> MutedIndicatorRef = new(MutedIndicatorMode.InGame);

        /// <summary>
        /// Recording device, by name rather than by index. Indices shuffle the moment another
        /// microphone is plugged in or removed; the name is what the user actually chose. Empty
        /// means whatever the system default is.
        /// </summary>
        public static readonly Ref<string> MicrophoneDeviceRef = new("");

        /// <summary>Fixed gain applied to the microphone before anything else, in decibels.</summary>
        public static readonly Ref<float> InputGainRef = new(0f);

        public static readonly Ref<bool> NoiseGateRef = new(true);

        /// <summary>How far above the tracked noise floor speech has to rise to open the gate.</summary>
        public static readonly Ref<float> GateThresholdRef = new(9f);

        public static readonly Ref<bool> AutoGainRef = new(true);

        public static bool Enabled => EnabledRef.Value;
        public static VoiceChatMode Mode => ModeRef.Value;
        public static bool HearYourself => HearYourselfRef.Value;
        public static string PushToTalkKey => PushToTalkKeyRef.Value;
        public static string MuteKey => MuteKeyRef.Value;
        public static MutedIndicatorMode MutedIndicator => MutedIndicatorRef.Value;
        public static string MicrophoneDevice => MicrophoneDeviceRef.Value;
        public static float InputGain => InputGainRef.Value;
        public static bool NoiseGate => NoiseGateRef.Value;
        public static float GateThreshold => GateThresholdRef.Value;
        public static bool AutoGain => AutoGainRef.Value;

        #endregion

        #region Playback

        public static readonly Ref<float> VolumeRef = new(1f);
        public static readonly Ref<float> SpatialBlendRef = new(1f);
        public static readonly Ref<float> MinDistanceRef = new(10f);
        public static readonly Ref<float> MaxDistanceRef = new(200f);
        public static readonly Ref<VoiceRolloffMode> RolloffRef = new(VoiceRolloffMode.LinearSquare);
        public static readonly Ref<bool> MuffleRef = new(true);
        public static readonly Ref<float> MuffleCutoffRef = new(400f);

        public static float Volume => VolumeRef.Value;
        public static float SpatialBlend => SpatialBlendRef.Value;
        public static float MinDistance => MinDistanceRef.Value;
        public static float MaxDistance => MaxDistanceRef.Value;
        public static VoiceRolloffMode Rolloff => RolloffRef.Value;
        public static bool Muffle => MuffleRef.Value;
        public static float MuffleCutoff => MuffleCutoffRef.Value;

        #endregion

        #region Network

        public static readonly Ref<VoiceLatencyMode> LatencyRef = new(VoiceLatencyMode.Normal);

        /// <summary>
        /// Opus bitrate in kilobits per second. 24 is comfortably clear for speech; the range exists
        /// because a busy lobby multiplies whatever you pick by the number of people talking.
        /// </summary>
        public static readonly Ref<int> BitrateRef = new(24);

        public static readonly Ref<int> DirectVoicePortRef = new(8081);

        public static VoiceLatencyMode Latency => LatencyRef.Value;
        public static int Bitrate => BitrateRef.Value;
        public static int DirectVoicePort => DirectVoicePortRef.Value;

        #endregion

        public static readonly Ref<bool> ShowDebugRef = new(false);

        public static bool ShowDebug => ShowDebugRef.Value;

        /// <summary>Packs the playback settings for <see cref="FmodVoiceStream.UpdateSpatial"/>.</summary>
        public static VoiceStreamSettings ForStream() => new VoiceStreamSettings
        {
            Volume = Volume,
            SpatialBlend = SpatialBlend,
            MinDistance = MinDistance,
            MaxDistance = MaxDistance,
            Rolloff = Rolloff,
            Latency = Latency,
            Muffle = Muffle,
            MuffleCutoff = MuffleCutoff
        };
    }
}
