using System;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public enum ChatMessageKind : byte
{
    /// <summary>Normal player chat broadcast to everyone.</summary>
    PlayerSay,
    /// <summary>Private message received from another player.</summary>
    PrivateFrom,
    /// <summary>Outgoing private message to another player (local echo).</summary>
    PrivateTo,
    /// <summary>Server / system notice.</summary>
    System,
    /// <summary>Local echo of a command the user typed.</summary>
    CommandEcho,
    /// <summary>Reply produced by a command (treated as system text).</summary>
    CommandReply,
    /// <summary>Command produced an error.</summary>
    Error,
}

public class ChatMessage
{
    public ChatMessageKind Kind;
    public ulong SenderSteamId;
    public string SenderName = "";
    public string RecipientName = "";
    public string Text = "";
    public DateTime ReceivedAt = DateTime.UtcNow;

    /// <summary>
    /// The sender's remote endpoint ("ip:port") when we hold their direct/LAN connection ourselves.
    /// Never travels over the wire (a peer's address is not ours to hand to the rest of the lobby),
    /// so it is empty for messages the host relayed on someone else's behalf. Steam-transport
    /// messages identify by <see cref="SenderSteamId"/> instead and leave this empty.
    /// </summary>
    public string SenderAddress = "";

    /// <summary>
    /// The sender's chosen (or auto-assigned) name color, used to tint just the name of a normal
    /// chat line. Null for kinds that don't show a player name in a colorable position.
    /// </summary>
    public Color? NameColor;

    /// <summary>
    /// True when this line renders the sender's name as a separately colored segment (see
    /// <see cref="RenderName"/> / <see cref="RenderBody"/>). Only normal player chat does this.
    /// </summary>
    public bool HasColoredName =>
        Kind == ChatMessageKind.PlayerSay && NameColor.HasValue && !string.IsNullOrEmpty(SenderName);

    /// <summary>The name segment (colored with <see cref="NameColor"/>). Only meaningful when
    /// <see cref="HasColoredName"/>.</summary>
    public string RenderName() => Esc(SenderName);

    /// <summary>The rest of the line, colored with <see cref="Color"/>, that follows the name segment.
    /// Only meaningful when <see cref="HasColoredName"/>.</summary>
    public string RenderBody() => $": {Esc(Text)}";

    /// <summary>How the sender is identified on the transport we received them over, for logging:
    /// a Steam ID under the Steam transport, an "ip:port" endpoint on a direct/LAN connection.</summary>
    public string SenderIdentity() =>
        !string.IsNullOrEmpty(SenderAddress) ? $"IP: {SenderAddress}"
        : SenderSteamId != 0UL              ? $"Steam ID: {SenderSteamId}"
        :                                     "unknown sender";

    public Color Color => Kind switch
    {
        ChatMessageKind.PlayerSay     => Color.white,
        ChatMessageKind.PrivateFrom   => new Color(1f, 0.6f, 1f),
        ChatMessageKind.PrivateTo     => new Color(0.7f, 0.7f, 1f),
        ChatMessageKind.System        => new Color(1f, 1f, 0.4f),
        ChatMessageKind.CommandEcho   => new Color(0.6f, 0.6f, 0.6f),
        ChatMessageKind.CommandReply  => new Color(0.7f, 1f, 0.7f),
        ChatMessageKind.Error         => new Color(1f, 0.3f, 0.3f),
        _ => Color.white,
    };

    public string Render() => Kind switch
    {
        ChatMessageKind.PlayerSay     => $"{Esc(SenderName)}: {Esc(Text)}",
        ChatMessageKind.PrivateFrom   => $"[{Esc(SenderName)} -> me] {Esc(Text)}",
        ChatMessageKind.PrivateTo     => $"[me -> {Esc(RecipientName)}] {Esc(Text)}",
        ChatMessageKind.System        => $"[System] {Esc(Text)}",
        ChatMessageKind.CommandEcho   => $"> {Esc(Text)}",
        ChatMessageKind.CommandReply  => Esc(Text),
        ChatMessageKind.Error         => $"[Error] {Esc(Text)}",
        _ => Esc(Text),
    };

    /// <summary>
    /// Escapes '%' so the rendered string is safe to hand to ImGui's printf-style text APIs
    /// (e.g. <c>ImGui.TextColored</c> maps to native <c>igTextColored(col, fmt, ...)</c>). A remote
    /// peer controls sender/message text, so an unescaped "%s%n" would crash/corrupt the overlay.
    /// Double-escaping a locally-produced '%' is harmless, "%%" renders as a single literal '%'.
    /// </summary>
    private static string Esc(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("%", "%%");
}
