using System.Collections.Generic;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>
/// A user-defined chat command created through the Chat mod UI. It carries no behaviour yet,
/// registering it makes the name resolve and tab-complete, routes it client- or server-side per
/// <see cref="Scope"/>, and (for <see cref="CommandScope.Server"/>) enforces its per-command
/// <see cref="Whitelist"/>. The execute body is a placeholder until real actions are wired up.
/// </summary>
public class CustomChatCommand
{
    /// <summary>The command word typed after '/'. No leading slash, no whitespace.</summary>
    public string Name { get; set; } = "";

    /// <summary>Free-form description shown by /help.</summary>
    public string Description { get; set; } = "";

    /// <summary>Whether the command runs locally (<see cref="CommandScope.Client"/>) or is forwarded to the host (<see cref="CommandScope.Server"/>).</summary>
    public CommandScope Scope { get; set; } = CommandScope.Client;

    /// <summary>Positional parameters, in order. Drives the hint string and tab-completion.</summary>
    public List<CustomCommandParam> Params { get; set; } = new();

    /// <summary>
    /// When true (server commands only), the <see cref="Whitelist"/> is enforced: host + whitelisted
    /// players only. When false, the whitelist is ignored and every player may run the command.
    /// </summary>
    public bool WhitelistEnabled { get; set; } = true;

    /// <summary>
    /// Steam profiles permitted to run this command when it is <see cref="CommandScope.Server"/>-scoped
    /// and <see cref="WhitelistEnabled"/> is true. Empty means host-only (matching the built-in default).
    /// The host is always allowed regardless. Ignored entirely for client-scoped commands.
    /// </summary>
    public List<SteamProfile> Whitelist { get; set; } = new();
}
