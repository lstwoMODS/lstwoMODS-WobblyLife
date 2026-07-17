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
