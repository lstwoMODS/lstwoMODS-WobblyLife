using System.Collections.Generic;
using System.Text;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>
/// Splits a command body into argument tokens with shell-like quoting.
///
/// Rules:
/// <list type="bullet">
///   <item>Tokens are separated by runs of spaces (a run counts as one separator).</item>
///   <item>Double quotes group a run of text into a single token, so spaces inside
///         <c>"..."</c> are literal (e.g. <c>/msg "John Doe" hi</c>).</item>
///   <item><c>\</c> escapes the next character literally, inside or outside quotes.
///         Use <c>\"</c> for a literal quote, <c>\\</c> for a literal backslash,
///         <c>\ </c> for a literal space without quoting.</item>
/// </list>
/// The tokens returned are fully decoded (quotes stripped, escapes resolved).
/// <see cref="Quote"/> is the inverse, it re-encodes a value so it round-trips.
/// A trailing <see cref="CommandArg.IsGreedy"/> arg is exempt: it keeps the raw
/// remainder of the line verbatim so quotes typed inside a message stay literal.
/// </summary>
public static class CommandTokenizer
{
    /// <summary>One decoded token plus where it began in the original body.</summary>
    private readonly struct Token
    {
        public readonly string Value;
        public readonly int Start;
        public Token(string value, int start) { Value = value; Start = start; }
    }

    /// <summary>
    /// Tokenize <paramref name="body"/> into decoded argument values. Empty separator
    /// runs are collapsed; an explicit empty token (<c>""</c>) is preserved.
    /// </summary>
    public static string[] Tokenize(string body)
    {
        var tokens = Scan(body ?? "", out _, out _);
        var result = new string[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            result[i] = tokens[i].Value;
        return result;
    }

    /// <summary>
    /// Build the argument array (excluding the command-name token) for executing
    /// <paramref name="cmd"/>. If the command declares a greedy trailing arg, everything
    /// from that arg's first token onward is taken as a single raw token, spaces and
    /// quotes preserved exactly as typed.
    /// </summary>
    public static string[] BuildExecArgs(string body, Command cmd)
    {
        body ??= "";
        var tokens = Scan(body, out _, out _);

        var greedyArg = -1;
        if (cmd != null)
        {
            for (var i = 0; i < cmd.Args.Count; i++)
            {
                if (cmd.Args[i].IsGreedy) { greedyArg = i; break; }
            }
        }

        var args = new List<string>();
        for (var i = 1; i < tokens.Count; i++)
        {
            // arg index for token i (name token is 0) is i - 1.
            if (greedyArg >= 0 && i - 1 == greedyArg)
            {
                args.Add(body.Substring(tokens[i].Start));
                break;
            }
            args.Add(tokens[i].Value);
        }

        return args.ToArray();
    }

    /// <summary>
    /// Like <see cref="Tokenize"/>, but tailored for live completion: when the input
    /// ends on a separator outside quotes, a trailing empty token is appended so the
    /// caller knows the user has started a fresh (still empty) token.
    /// </summary>
    public static List<string> TokenizeForCompletion(string body)
    {
        var tokens = Scan(body ?? "", out var endedOnSeparator, out var inQuote);
        var result = new List<string>(tokens.Count + 1);
        foreach (var t in tokens)
            result.Add(t.Value);
        if (endedOnSeparator && !inQuote)
            result.Add("");
        return result;
    }

    /// <summary>
    /// Re-encode <paramref name="value"/> so that tokenizing the result yields it back.
    /// Wraps in quotes and escapes only when the value contains a space, quote or
    /// backslash; otherwise returns it unchanged.
    /// </summary>
    public static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        if (value.IndexOfAny(new[] { ' ', '"', '\\' }) < 0)
            return value;

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c == '"' || c == '\\')
                sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static List<Token> Scan(string s, out bool endedOnSeparator, out bool inQuoteAtEnd)
    {
        var tokens = new List<Token>();
        var sb = new StringBuilder();
        var inToken = false;
        var inQuote = false;
        var tokenStart = 0;
        endedOnSeparator = false;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (c == '\\')
            {
                if (!inToken) { inToken = true; tokenStart = i; }
                // Escape the following char literally; a trailing '\' is kept as-is.
                if (i + 1 < s.Length)
                {
                    sb.Append(s[i + 1]);
                    i++;
                }
                else
                {
                    sb.Append('\\');
                }
                endedOnSeparator = false;
                continue;
            }

            if (c == '"')
            {
                if (!inToken) { inToken = true; tokenStart = i; }
                inQuote = !inQuote; // an explicit "" is still a token
                endedOnSeparator = false;
                continue;
            }

            if (c == ' ' && !inQuote)
            {
                if (inToken)
                {
                    tokens.Add(new Token(sb.ToString(), tokenStart));
                    sb.Clear();
                    inToken = false;
                }
                endedOnSeparator = true;
                continue;
            }

            if (!inToken) { inToken = true; tokenStart = i; }
            sb.Append(c);
            endedOnSeparator = false;
        }

        if (inToken)
            tokens.Add(new Token(sb.ToString(), tokenStart));

        inQuoteAtEnd = inQuote;
        return tokens;
    }
}
