using System;
using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.Macros;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>
/// Registers <see cref="PlayerRef"/> as a macro object type: player parameters on macro
/// steps get Local Player / Host / First Player / Random Player / By Name selection modes,
/// string values (from constants, expressions or step outputs) resolve by player name, and
/// detached mod instances are cached per PlayerController.
///
/// The same selections are also exposed as standalone value-producing steps under the
/// "Player" category (Get Local Player, Get Host Player, ..., Get All Players, Get All Local
/// Players), so a player  or an array of players  can be piped via <c>prev</c> or stored
/// with Set Variable and read back later with <c>var(...)</c>.
/// </summary>
public static class PlayerMacroType
{
    public static void Register()
    {
        MacroTypes.Register(new MacroTypeDescriptor
        {
            Type = typeof(PlayerRef),
            DisplayName = "Player",
            DefaultModeId = "local",
            ContextCacheKey = v => (v as PlayerRef)?.Controller,
            ResolveFromString = FindByName,
            Modes =
            {
                new MacroTypeMode
                {
                    Id = "local", Label = "Local Player",
                    Resolve = _ => LocalPlayer(),
                },
                new MacroTypeMode
                {
                    Id = "host", Label = "Host",
                    Resolve = _ => HostPlayer(),
                },
                new MacroTypeMode
                {
                    Id = "first", Label = "First Player",
                    Resolve = _ => FirstPlayer(),
                },
                new MacroTypeMode
                {
                    Id = "random", Label = "Random Player",
                    Resolve = _ => RandomPlayer(),
                },
                new MacroTypeMode
                {
                    Id = "byName", Label = "By Name",
                    Param = new MacroParam { Name = "name", Type = typeof(string) },
                    Resolve = args => FindByName((string)args[0]),
                    Choices = () => Controllers().Select(c => c.GetPlayerName()).ToArray(),
                },
            },
        });

        RegisterSteps();
    }

    /// <summary>Value-producing macro steps mirroring the selection modes, plus the two set
    /// selections (All / All Local) that a single-value parameter mode can't express. Each
    /// returns its player (or player array) as the step output.</summary>
    private static void RegisterSteps()
    {
        Producer("player.getLocal",  "Get Local Player",  () => LocalPlayer());
        Producer("player.getHost",   "Get Host Player",   () => HostPlayer());
        Producer("player.getFirst",  "Get First Player",  () => FirstPlayer());
        Producer("player.getRandom", "Get Random Player", () => RandomPlayer());

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id = "player.getByName",
            Label = "Get Player By Name",
            Category = "Player",
            PickerLabel = "Get Player By Name",
            Parameters = new[] { new MacroParam { Name = "name", Type = typeof(string) } },
            ReturnType = typeof(PlayerRef),
            Execute = args => FindByName((string)MacroValues.Coerce(args[0], typeof(string))),
        });

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id = "player.getAll",
            Label = "Get All Players",
            Category = "Player",
            PickerLabel = "Get All Players",
            ReturnType = typeof(PlayerRef[]),
            Execute = _ => AllPlayers(),
        });

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id = "player.getAllLocal",
            Label = "Get All Local Players",
            Category = "Player",
            PickerLabel = "Get All Local Players",
            ReturnType = typeof(PlayerRef[]),
            Execute = _ => AllLocalPlayers(),
        });
    }

    /// <summary>Register a parameterless step under the "Player" category that returns one player.</summary>
    private static void Producer(string id, string label, Func<PlayerRef> resolve)
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id = id,
            Label = label,
            Category = "Player",
            PickerLabel = label,
            ReturnType = typeof(PlayerRef),
            Execute = _ => resolve(),
        });
    }

    private static List<PlayerController> Controllers()
    {
        var controllers = GameInstance.InstanceExists ? GameInstance.Instance.GetPlayerControllers() : null;
        if (controllers == null || controllers.Count == 0)
            throw new InvalidOperationException("No players available (no game running?).");
        return controllers;
    }

    /// <summary>The first local (owned) player controller.</summary>
    private static PlayerRef LocalPlayer()
    {
        var local = Controllers().FirstOrDefault(c => c.networkObject.IsOwner());
        if (local == null)
            throw new InvalidOperationException("No local player found.");
        return local;
    }

    /// <summary>The player hosting the session (the server's owned player).</summary>
    private static PlayerRef HostPlayer()
    {
        var host = Controllers().FirstOrDefault(c => c.networkObject?.GetOwner()?.IsHost == true);
        if (host == null)
            throw new InvalidOperationException("No host player found.");
        return host;
    }

    /// <summary>The first player in the game's controller list.</summary>
    private static PlayerRef FirstPlayer() => Controllers()[0];

    /// <summary>A uniformly random player currently in the game.</summary>
    private static PlayerRef RandomPlayer()
    {
        var controllers = Controllers();
        return controllers[UnityEngine.Random.Range(0, controllers.Count)];
    }

    /// <summary>Every player in the game, as a fresh array (safe to iterate while players join/leave).</summary>
    private static PlayerRef[] AllPlayers()
        => Controllers().Select(c => (PlayerRef)c).ToArray();

    /// <summary>Every local (owned) player on this machine, splitscreen included.</summary>
    private static PlayerRef[] AllLocalPlayers()
    {
        var locals = GameInstance.InstanceExists ? GameInstance.Instance.GetLocalPlayerControllers() : null;
        if (locals == null || locals.Count == 0)
            throw new InvalidOperationException("No local players found (no game running?).");
        return locals.Select(c => (PlayerRef)c).ToArray();
    }

    private static object FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name is empty.");

        var controllers = Controllers();
        var match = controllers.FirstOrDefault(c => string.Equals(c.GetPlayerName(), name, StringComparison.OrdinalIgnoreCase))
                 ?? controllers.FirstOrDefault(c => (c.GetPlayerName() ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        if (match == null)
            throw new ArgumentException(
                $"No player named '{name}'. Available: {string.Join(", ", controllers.Select(c => c.GetPlayerName()))}");
        return (PlayerRef)match;
    }
}
