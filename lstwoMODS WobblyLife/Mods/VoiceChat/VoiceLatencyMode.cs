namespace WLProxChat
{
    /// <summary>
    /// How much audio a voice stream buffers before playing it. Trades delay against robustness
    /// to packet jitter: the higher the setting, the later a late packet can arrive and still
    /// be heard instead of dropping out.
    /// </summary>
    public enum VoiceLatencyMode
    {
        /// <summary>~40 ms of slack. Snappiest, but drops out on an unstable connection.</summary>
        Low,

        /// <summary>~80 ms of slack. Sensible default.</summary>
        Normal,

        /// <summary>~200 ms of slack. Noticeably delayed but rides out bad connections.</summary>
        High
    }
}
