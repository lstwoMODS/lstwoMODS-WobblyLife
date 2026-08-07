using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HawkNetworking;
using lstwoMODS_Core.Macros;
using lstwoMODS_Core.UI;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>
/// Registers the "Chat Command" macro trigger (<c>wl.chatCommand</c>) with core's
/// <see cref="MacroTriggerRegistry"/>, so any macro can be fired by running a chat command and
/// provides <see cref="CreateMacroForCommand"/>, the one-click "make a macro for this command"
/// action, mirroring the right-click "Create hotkey" flow.
/// </summary>
public static class ChatMacroTrigger
{
    public const string TriggerId = "wl.chatCommand";

    private const string CommandKey  = "command";
    private const string PlayerOut   = "player";
    private const string ArgsOut     = "args";
    private const string ArgCountOut = "argCount";

    /// <summary>
    /// How many positional parameters are surfaced as their own outputs (<c>arg1</c>..<c>argN</c>).
    /// A command with more parameters than this still exposes the overflow through <see cref="ArgsOut"/>.
    /// </summary>
    private const int MaxArgOutputs = 8;

    private static string ArgKey(int oneBased) => "arg" + oneBased;

    /// <summary>Register the trigger. Call once at startup (see Plugin.Start).</summary>
    public static void Register()
    {
        MacroTriggerRegistry.Register(new MacroTriggerDescriptor
        {
            Id    = TriggerId,
            Label = "Chat Command",
            Params = new[]
            {
                new MacroTriggerParam
                {
                    Key = CommandKey, Label = "Command", Type = typeof(string), Default = "",
                    Tooltip = "Fire when this chat command runs (the name without the leading slash). "
                            + "For a server command, this fires on the host where the command runs.",
                },
            },
            Outputs = BuildFallbackOutputs(),
            DynamicOutputs = DynamicOutputsFor,
            Arm = ctx =>
            {
                var wanted = (ctx.GetString(CommandKey) ?? "").Trim().TrimStart('/');
                if (string.IsNullOrEmpty(wanted)) return null;

                void Handler(Command cmd, CommandContext cctx)
                {
                    if (!string.Equals(cmd.Name, wanted, StringComparison.OrdinalIgnoreCase)) return;
                    ctx.Fire(BuildFireValues(cmd, cctx));
                }

                CommandRegistry.Invoked += Handler;
                return new CallbackDisposable(() => CommandRegistry.Invoked -= Handler);
            },
        });
    }

    
    private static MacroTriggerOutput PlayerOutput() => new()
    {
        Key = PlayerOut, Label = "Player", Type = typeof(PlayerRef),
        Tooltip = "The player who ran the command. For a server command this is the sender, resolved "
                + "on the host. Empty when their controller can't be resolved (e.g. not yet spawned).",
    };

    private static MacroTriggerOutput ArgsOutput() => new()
    {
        Key = ArgsOut, Label = "Arguments", Type = typeof(string),
        Tooltip = "The full argument string the command was run with (everything after the name).",
    };

    private static MacroTriggerOutput ArgCountOutput() => new()
    {
        Key = ArgCountOut, Label = "Argument count", Type = typeof(int),
        Tooltip = "How many arguments the command was run with.",
    };

    /// <summary>Generic positional outputs, shown when the configured command name isn't recognised.</summary>
    private static MacroTriggerOutput[] BuildFallbackOutputs()
    {
        var outputs = new List<MacroTriggerOutput> { PlayerOutput(), ArgsOutput(), ArgCountOutput() };
        for (var i = 1; i <= MaxArgOutputs; i++)
        {
            outputs.Add(new MacroTriggerOutput
            {
                Key = ArgKey(i), Label = $"Parameter {i}", Type = typeof(string),
                Tooltip = $"The value of parameter {i} (empty when run with fewer arguments).",
            });
        }
        return outputs.ToArray();
    }

    /// <summary>
    /// The exact outputs for the configured command: base values plus one per declared parameter,
    /// keyed by the parameter hint and typed to the parameter kind. Null when the command name isn't
    /// a registered command (so the static fallback outputs are used instead).
    /// </summary>
    private static MacroTriggerOutput[] DynamicOutputsFor(MacroTrigger trigger)
    {
        var name = (trigger.GetString(CommandKey) ?? "").Trim().TrimStart('/');
        if (string.IsNullOrEmpty(name) || !CommandRegistry.TryGet(name, out var cmd))
            return null;

        var outputs = new List<MacroTriggerOutput> { PlayerOutput(), ArgsOutput(), ArgCountOutput() };
        foreach (var (key, arg) in DeriveParams(cmd))
        {
            outputs.Add(new MacroTriggerOutput
            {
                Key = key, Label = arg.Hint, Type = OutputTypeFor(arg),
                Tooltip = $"Parameter \"{arg.Hint}\" of /{name}.",
            });
        }
        return outputs.ToArray();
    }

    /// <summary>Map the sender and each declared parameter onto the outputs for a single fire.</summary>
    private static (string Key, object Value)[] BuildFireValues(Command cmd, CommandContext cctx)
    {
        var args = cctx?.Args ?? Array.Empty<string>();

        var values = new List<(string, object)>
        {
            (PlayerOut, ResolveCommandSender(cctx?.Sender)),
            (ArgsOut, string.Join(" ", args)),
            (ArgCountOut, args.Length),
        };

        var derived = DeriveParams(cmd);
        for (var i = 0; i < derived.Count; i++)
        {
            var token = i < args.Length ? args[i] : "";
            values.Add((derived[i].Key, ConvertArg(derived[i].Arg, token)));
        }

        return values.ToArray();
    }

    /// <summary>
    /// Assign each of the command's declared parameters a unique expression-identifier key derived
    /// from its hint (e.g. "&lt;player&gt;" → <c>player</c>, "[amount]" → <c>amount</c>), falling back
    /// to <c>argN</c> for hints with no usable identifier. Keys never collide with the base outputs
    /// or each other. Used by both the output declaration and the fire, so they always line up.
    /// </summary>
    private static List<(string Key, CommandArg Arg)> DeriveParams(Command cmd)
    {
        var result = new List<(string, CommandArg)>();
        if (cmd?.Args == null) return result;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlayerOut, ArgsOut, ArgCountOut };
        for (var i = 0; i < cmd.Args.Count; i++)
        {
            var arg = cmd.Args[i];
            result.Add((DeriveKey(arg?.Hint, i, used), arg));
        }
        return result;
    }

    private static string DeriveKey(string hint, int index, HashSet<string> used)
    {
        var sb = new StringBuilder();
        foreach (var c in hint ?? "")
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);

        var key = sb.ToString();
        if (key.Length == 0 || char.IsDigit(key[0]))
            key = ArgKey(index + 1);

        var baseKey = key;
        var n = 2;
        while (!used.Add(key)) key = baseKey + n++;
        return key;
    }

    private static Type OutputTypeFor(CommandArg arg) => arg switch
    {
        IntArg    => typeof(int),
        FloatArg  => typeof(float),
        PlayerArg => typeof(PlayerRef),
        _         => typeof(string),
    };

    private static object ConvertArg(CommandArg arg, string token)
    {
        switch (arg)
        {
            case IntArg:
                return int.TryParse(token, out var iv) ? iv : 0;
            case FloatArg:
                return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv) ? fv : 0f;
            case PlayerArg:
                return ResolveConnection(ChatNetworking.FindByName(token));
            default:
                return token ?? "";
        }
    }

    /// <summary>
    /// The player who ran the command, as a <see cref="PlayerRef"/>. A Steam sender maps to their
    /// in-game controller. A locally-run command (offline game, or a host whose connection isn't a
    /// <see cref="SteamConnection"/>) resolves to the local player, since the sender is us, without
    /// this, <c>player</c> is null offline even though the command was clearly run locally, and any
    /// step using it NREs. Only a genuine remote Steam sender we can't match yet stays null (falling
    /// back to local there would silently act on the host instead of the actual sender).
    /// </summary>
    private static PlayerRef ResolveCommandSender(HawkConnection sender)
    {
        var resolved = ResolveConnection(sender);
        if (resolved != null) return resolved;

        if (sender is not SteamConnection)
        {
            var local = GameInstance.InstanceExists ? GameInstance.Instance.GetFirstLocalPlayerController() : null;
            if (local != null) return local;
        }
        return null;
    }

    /// <summary>
    /// Map a connection to a <see cref="PlayerRef"/> strictly by Steam account. Returns null when
    /// there's no game, the connection isn't a Steam player, or no controller is owned by it yet.
    /// Used for the <c>&lt;player&gt;</c> argument lookup, where an unknown name must stay unresolved
    /// rather than fall back to the local player.
    /// </summary>
    private static PlayerRef ResolveConnection(HawkConnection sender)
    {
        if (sender is not SteamConnection sc) return null;
        var controllers = SteamProfileHelper.GetPlayerControllers(sc.steamId.Value);
        return controllers.Count > 0 ? controllers[0] : null;
    }

    /// <summary>
    /// Create a new macro pre-wired to fire on <paramref name="commandName"/> and jump to the Macros
    /// window to fill in its steps. The macro starts empty (no steps), the same way "Create hotkey"
    /// drops you into the editor to finish the setup.
    /// </summary>
    public static void CreateMacroForCommand(string commandName)
    {
        commandName = (commandName ?? "").Trim().TrimStart('/');
        if (string.IsNullOrEmpty(commandName)) return;

        var macro = MacroManager.Add($"On /{commandName}");
        macro.Trigger.TypeId = TriggerId;
        macro.Trigger.Set(CommandKey, commandName);
        MacroManager.NotifyTriggerChanged();

        LstwoModsPanels.RevealWindow(LstwoModsPanels.MacrosWindow);
    }
}
