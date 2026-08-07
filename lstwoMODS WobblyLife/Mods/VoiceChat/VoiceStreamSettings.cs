namespace WLProxChat
{
    /// <summary>
    /// Per frame playback settings for a <see cref="FmodVoiceStream"/>. Callers keep their own flat
    /// settings (so they can be bound to UI) and pack them into one of these once per frame.
    /// </summary>
    public struct VoiceStreamSettings
    {
        /// <summary>Linear amplitude. 1 is unity gain, above that is boosted.</summary>
        public float Volume;

        /// <summary>0 is fully 2D (same in both ears everywhere), 1 is fully positional.</summary>
        public float SpatialBlend;

        /// <summary>Distance out to which the voice stays at full volume, in metres.</summary>
        public float MinDistance;

        /// <summary>Distance at which attenuation bottoms out, in metres.</summary>
        public float MaxDistance;

        /// <summary>Shape of the attenuation curve between min and max distance.</summary>
        public VoiceRolloffMode Rolloff;

        /// <summary>Size of the jitter buffer.</summary>
        public VoiceLatencyMode Latency;

        /// <summary>Whether distant voices get progressively low-passed.</summary>
        public bool Muffle;

        /// <summary>Low-pass cutoff in Hz reached at <see cref="MaxDistance"/>. Lower is more muffled.</summary>
        public float MuffleCutoff;

        public static VoiceStreamSettings Default => new VoiceStreamSettings
        {
            Volume = 1f,
            SpatialBlend = 1f,
            MinDistance = 10f,
            MaxDistance = 250f,
            Rolloff = VoiceRolloffMode.LinearSquare,
            Latency = VoiceLatencyMode.Normal,
            Muffle = true,
            MuffleCutoff = 700f
        };
    }
}
