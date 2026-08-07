using System.Collections.Generic;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>The kind of a user-defined command parameter, mapped to a concrete <see cref="CommandArg"/> at registration.</summary>
public enum CustomParamType
{
    String,
    Int,
    Float,
    Player,
    Enum,
    Greedy,
}

/// <summary>
/// A serializable description of one positional parameter on a <see cref="CustomChatCommand"/>.
/// Converted to a live <see cref="CommandArg"/> (which drives the hint + tab-completion) by
/// <see cref="ToArg"/>.
/// </summary>
public class CustomCommandParam
{
    /// <summary>Display label shown in the suggestion popup, e.g. "&lt;player&gt;" or "[amount]".</summary>
    public string Hint { get; set; } = "<arg>";

    public CustomParamType Type { get; set; } = CustomParamType.String;

    public bool Optional { get; set; }

    /// <summary>Allowed values when <see cref="Type"/> is <see cref="CustomParamType.Enum"/>.</summary>
    public List<string> EnumValues { get; set; } = new();

    public CommandArg ToArg()
    {
        var hint = string.IsNullOrEmpty(Hint) ? "<arg>" : Hint;
        return Type switch
        {
            CustomParamType.Int    => new IntArg(hint, Optional),
            CustomParamType.Float  => new FloatArg(hint, Optional),
            CustomParamType.Player => new PlayerArg(hint, Optional),
            CustomParamType.Enum   => new EnumArg(hint, EnumValues?.ToArray() ?? System.Array.Empty<string>(), Optional),
            CustomParamType.Greedy => new GreedyStringArg(hint, Optional),
            _                      => new StringArg(hint, Optional),
        };
    }
}
