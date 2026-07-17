using System;
using System.Collections.Generic;
using System.Linq;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public class ResolvedInput
{
    /// <summary>The whole input string starting with '/'. Empty if no input.</summary>
    public string Raw = "";

    /// <summary>Tokens after splitting on spaces, including the command name token.</summary>
    public string[] Tokens = System.Array.Empty<string>();

    /// <summary>The index of the token the user is currently typing (always <c>Tokens.Length - 1</c>).</summary>
    public int CurrentTokenIndex;

    /// <summary>The partial text of the current token.</summary>
    public string CurrentToken = "";

    /// <summary>Resolved command, or null when the user is still typing the command name itself.</summary>
    public Command Command;

    /// <summary>Per-arg position index into <see cref="Command.Args"/>. -1 when on the name token.</summary>
    public int ArgIndex = -1;

    /// <summary>Candidate completion strings for the current token, sorted, deduplicated.</summary>
    public List<string> Candidates = new();
}

public static class CommandRegistry
{
    private static readonly List<Command> _commands = new();
    private static readonly Dictionary<string, Command> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Command> All => _commands;

    /// <summary>
    /// Fires after any registered command executes (client-local, or host-side for a forwarded
    /// server command). The chat-command macro trigger listens to this to fire matching macros.
    /// </summary>
    public static event Action<Command, CommandContext> Invoked;

    /// <summary>Raise <see cref="Invoked"/>. Called by the dispatcher once a command has run.</summary>
    public static void NotifyInvoked(Command cmd, CommandContext ctx)
    {
        if (cmd != null) Invoked?.Invoke(cmd, ctx);
    }

    public static void Register(Command cmd)
    {
        if (cmd == null || string.IsNullOrEmpty(cmd.Name))
        {
            throw new ArgumentException("Command must have a non-empty Name");
        }

        if (_byName.ContainsKey(cmd.Name))
        {
            _commands.RemoveAll(c => string.Equals(c.Name, cmd.Name, StringComparison.OrdinalIgnoreCase));
        }

        _commands.Add(cmd);
        _byName[cmd.Name] = cmd;

        foreach (var alias in cmd.Aliases ?? System.Array.Empty<string>())
        {
            _byName[alias] = cmd;
        }
    }

    public static bool TryGet(string name, out Command cmd) => _byName.TryGetValue(name ?? "", out cmd);

    /// <summary>
    /// Remove a command (and all of its alias keys) by name. No-op if the name is unknown.
    /// Used by the custom-command store when a user-defined command is renamed or deleted.
    /// </summary>
    public static void Unregister(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (_byName.TryGetValue(name, out var cmd))
        {
            // Drop every key (canonical name + aliases) that resolves to this command.
            foreach (var key in _byName.Where(kv => kv.Value == cmd).Select(kv => kv.Key).ToList())
                _byName.Remove(key);
            _commands.Remove(cmd);
        }
        else
        {
            _byName.Remove(name);
        }
    }

    /// <summary>
    /// Parse and analyze a raw input string. The result is always non-null and tells the UI
    /// what to show in the suggestion popup.
    /// </summary>
    public static ResolvedInput Resolve(string raw)
    {
        var r = new ResolvedInput { Raw = raw ?? "" };

        if (string.IsNullOrEmpty(raw) || raw[0] != '/')
            return r;

        var body = raw.Substring(1);

        // Decoded tokens; a trailing empty token is appended when the user has just
        // typed a separator, so we know they've started a new (still empty) arg.
        var tokenList = CommandTokenizer.TokenizeForCompletion(body);
        if (tokenList.Count == 0)
            tokenList.Add("");
        var tokens = tokenList.ToArray();

        r.Tokens = tokens;
        r.CurrentTokenIndex = tokens.Length - 1;
        r.CurrentToken = tokens[r.CurrentTokenIndex] ?? "";

        if (tokens.Length == 1)
        {
            r.Candidates = _commands
                .Where(c => c.Name.StartsWith(r.CurrentToken, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            if (_byName.TryGetValue(r.CurrentToken, out var named))
                r.Command = named;

            return r;
        }

        if (!_byName.TryGetValue(tokens[0], out var cmd))
            return r;

        r.Command = cmd;
        r.ArgIndex = r.CurrentTokenIndex - 1;

        var declaredIndex = Math.Min(r.ArgIndex, cmd.Args.Count - 1);
        
        if (declaredIndex >= 0 && declaredIndex < cmd.Args.Count)
        {
            var arg = cmd.Args[declaredIndex];

            r.Candidates = arg.Suggest(r.CurrentToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
        }

        return r;
    }

    /// <summary>
    /// Compute the result of pressing Tab: returns the *new* full input string after
    /// substituting <paramref name="completion"/> into the current token position.
    /// </summary>
    public static string ApplyCompletion(ResolvedInput resolved, string completion)
    {
        if (resolved == null || string.IsNullOrEmpty(completion)) return resolved?.Raw ?? "";

        var tokens = resolved.Tokens.ToArray();
        tokens[resolved.CurrentTokenIndex] = completion;

        var more = resolved.Command != null && resolved.ArgIndex + 1 < resolved.Command.Args.Count;

        if (resolved.CurrentTokenIndex == 0 && _byName.TryGetValue(completion, out var cmd) && cmd.Args.Count > 0)
        {
            more = true;
        }

        // Re-encode each token so values containing spaces/quotes round-trip (the
        // command-name token at index 0 never needs quoting and is left verbatim).
        var rebuilt = tokens.Select((t, i) => i == 0 ? t : CommandTokenizer.Quote(t));
        return "/" + string.Join(" ", rebuilt) + (more ? " " : "");
    }

    public static void Clear() => _commands.Clear();
}
