namespace WLProxChat
{
    /// <summary>
    /// Distance attenuation curve for a voice stream. Maps onto the FMOD 3D rolloff mode flags.
    /// </summary>
    public enum VoiceRolloffMode
    {
        /// <summary>Inverse rolloff. Smooth falloff that never quite reaches silence, the FMOD default.</summary>
        Logarithmic,

        /// <summary>Linear falloff, silent at max distance.</summary>
        Linear,

        /// <summary>Linear-squared falloff, silent at max distance but quieter up close to it.</summary>
        LinearSquare
    }
}
