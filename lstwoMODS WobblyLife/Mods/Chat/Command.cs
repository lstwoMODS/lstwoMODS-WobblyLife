using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public enum CommandScope
{
    /// <summary>Runs locally on the typer's client. Never RPC'd.</summary>
    Client,
    /// <summary>Runs locally if you're the host, otherwise forwarded to the host.</summary>
    Server,
}

public class CommandContext
{
    /// <summary>The connection that invoked the command. Null = local invocation with no network sender.</summary>
    public HawkConnection Sender;

    /// <summary>The raw token array (excluding the command name itself).</summary>
    public string[] Args = Array.Empty<string>();

    /// <summary>Set to true by the dispatcher when this command was forwarded from a remote sender.</summary>
    public bool IsRemote;

    /// <summary>Set to true when the local invoker is the host (server-side dispatch path).</summary>
    public bool IsHost;

    /// <summary>The chat mod owns this, replies are routed to the appropriate destination.</summary>
    public Action<ChatMessage> Reply;

    public void ReplyText(string text, ChatMessageKind kind = ChatMessageKind.CommandReply)
        => Reply?.Invoke(new ChatMessage { Kind = kind, Text = text });

    public void Error(string text)
        => Reply?.Invoke(new ChatMessage { Kind = ChatMessageKind.Error, Text = text });

    public bool TryGetInt(int index, out int value)
    {
        value = 0;
        return index < Args.Length && int.TryParse(Args[index], out value);
    }

    public bool TryGetFloat(int index, out float value)
    {
        value = 0;
        return index < Args.Length && float.TryParse(Args[index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    public string GetString(int index, string fallback = "")
        => index < Args.Length ? Args[index] : fallback;

    /// <summary>Returns all args from <paramref name="startIndex"/> onward, joined with spaces.</summary>
    public string GetRest(int startIndex)
        => startIndex < Args.Length ? string.Join(" ", Args.Skip(startIndex)) : "";
}

public class Command
{
    public string Name = "";
    public string Description = "";
    public CommandScope Scope = CommandScope.Client;
    public List<CommandArg> Args = new();
    public Action<CommandContext> Execute;
    public string[] Aliases = Array.Empty<string>();

    /// <summary>
    /// Server-side permission predicate. When null (the default) only the host/owner may run the
    /// command; set an explicit predicate to opt in to public use.
    /// </summary>
    public Func<HawkConnection, bool> ServerAllow;

    /// <summary>The "/name &lt;arg1&gt; &lt;arg2&gt;..." display string used for hints.</summary>
    public string SignatureString =>
        string.IsNullOrEmpty(Name) ? "" :
        "/" + Name + (Args.Count > 0 ? " " + string.Join(" ", Args.Select(a => a.Hint)) : "");
}
