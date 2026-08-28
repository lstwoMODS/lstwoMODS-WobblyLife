namespace WLProxChat
{
    /// <summary>Where the "you are muted" reminder is drawn while your mic is toggled off.</summary>
    public enum MutedIndicatorMode
    {
        /// <summary>A plain Unity IMGUI label drawn straight onto the game window.</summary>
        InGame,

        /// <summary>A badge on the lstwoMODS overlay window. Takes no input, so it never steals clicks.</summary>
        Overlay
    }
}
