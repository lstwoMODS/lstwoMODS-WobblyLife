using System.Collections.Generic;
using System.Linq;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>
/// Describes one positional argument of a command. Each arg supplies its display hint
/// (rendered inline in the suggestion popup, "&lt;player&gt;" etc.) and a candidate set
/// that powers Tab-completion.
/// </summary>
public abstract class CommandArg
{
    /// <summary>The display label, e.g. "&lt;player&gt;" or "[amount]".</summary>
    public string Hint { get; protected set; } = "";

    /// <summary>Whether the arg can be omitted.</summary>
    public bool Optional { get; protected set; }

    /// <summary>
    /// Whether this arg greedily consumes the raw remainder of the line (spaces and
    /// quotes preserved verbatim). Only meaningful on the last declared arg.
    /// </summary>
    public virtual bool IsGreedy => false;

    /// <summary>
    /// Candidate strings the user might want to fill in. <paramref name="typedSoFar"/> is the
    /// partial token they've already typed (before the cursor), implementations should filter
    /// case-insensitively. Empty string means "show all candidates".
    /// </summary>
    public abstract IEnumerable<string> Suggest(string typedSoFar);

    /// <summary>Whether <paramref name="text"/> is a valid completed value for this arg.</summary>
    public virtual bool Validate(string text) => !string.IsNullOrEmpty(text);
}

/// <summary>Free-form string. Suggestions empty unless explicitly given.</summary>
public class StringArg : CommandArg
{
    private readonly string[] _candidates;

    public StringArg(string hint, bool optional = false, params string[] candidates)
    {
        Hint = hint;
        Optional = optional;
        _candidates = candidates ?? System.Array.Empty<string>();
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
        => _candidates.Where(c => c.StartsWith(typedSoFar ?? "", System.StringComparison.OrdinalIgnoreCase));
}

/// <summary>Integer; no suggestions (free-form numeric).</summary>
public class IntArg : CommandArg
{
    public IntArg(string hint, bool optional = false)
    {
        Hint = hint;
        Optional = optional;
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
        => System.Linq.Enumerable.Empty<string>();

    public override bool Validate(string text) => int.TryParse(text, out _);
}

/// <summary>Float; no suggestions.</summary>
public class FloatArg : CommandArg
{
    public FloatArg(string hint, bool optional = false)
    {
        Hint = hint;
        Optional = optional;
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
        => System.Linq.Enumerable.Empty<string>();

    public override bool Validate(string text)
        => float.TryParse(text, System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out _);
}

/// <summary>
/// Suggests other players currently in the lobby. Resolves connection at execute-time.
/// </summary>
public class PlayerArg : CommandArg
{
    public PlayerArg(string hint = "<player>", bool optional = false)
    {
        Hint = hint;
        Optional = optional;
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
    {
        var partial = typedSoFar ?? "";
        return ChatNetworking.AllPlayerNames()
            .Where(n => n.StartsWith(partial, System.StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Closed set of string values (e.g. enum / mode list).</summary>
public class EnumArg : CommandArg
{
    private readonly string[] _values;

    public EnumArg(string hint, string[] values, bool optional = false)
    {
        Hint = hint;
        Optional = optional;
        _values = values ?? System.Array.Empty<string>();
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
        => _values.Where(v => v.StartsWith(typedSoFar ?? "", System.StringComparison.OrdinalIgnoreCase));

    public override bool Validate(string text)
        => _values.Any(v => string.Equals(v, text, System.StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Greedy string, eats every remaining token. Use for trailing message bodies
/// (e.g. /msg &lt;player&gt; &lt;message&gt;...).
/// </summary>
public class GreedyStringArg : CommandArg
{
    public override bool IsGreedy => true;

    public GreedyStringArg(string hint = "<message>", bool optional = false)
    {
        Hint = hint;
        Optional = optional;
    }

    public override IEnumerable<string> Suggest(string typedSoFar)
        => System.Linq.Enumerable.Empty<string>();

    public override bool Validate(string text) => true;
}
