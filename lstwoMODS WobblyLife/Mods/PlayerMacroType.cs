using System;
using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.Macros;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>
/// Registers <see cref="PlayerRef"/> as a macro object type: player parameters on macro
/// steps get Local Player / First Player / By Name selection modes, string values (from
/// constants, expressions or step outputs) resolve by player name, and detached mod
/// instances are cached per PlayerController.
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
                    Id = "byName", Label = "By Name",
                    Param = new MacroParam { Name = "name", Type = typeof(string) },
                    Resolve = args => FindByName((string)args[0]),
                    Choices = () => Controllers().Select(c => c.GetPlayerName()).ToArray(),
                },
            },
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
