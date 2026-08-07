using System;
using BepInEx.Configuration;
using lstwoMODS_Core;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Global "don't touch my save file" switch.
///
/// Mods that permanently write to the game save (unlocks, achievements, missions, money, ...)
/// disable their widgets with <see cref="Guard{T}"/> and early-out of their actions with
/// <see cref="On"/>. The action-side check is the one that actually enforces the setting:
/// macros, hotkeys and chat commands invoke those methods without going through the panel.
/// </summary>
internal static class SaveGuard
{
    private const string NoticeText =
        "Save-file mods are disabled because \"Protect Save File\" is enabled in Settings.";

    private static ConfigEntry<bool> _entry;

    /// <summary>
    /// True while save-permanent mods are blocked. Callbacks fire on the IPC reader thread so
    /// the UI keeps updating while the game is frozen or loading; handlers must stay off Unity APIs.
    /// </summary>
    public static readonly Ref<bool> Blocked = new(false, runCallbacksOnMainThread: false);

    public static bool On => Blocked.Value;

    public static void Init()
    {
        _entry = Plugin.ConfigFile.Bind("Save File", "ProtectSaveFile", false,
            "Disable every mod and action that permanently modifies your save file " +
            "(unlocks, achievements, missions, money, ...).");

        Blocked.Value = _entry.Value;

        // Blocked changes on the IPC thread; the config write is Unity/BepInEx-side, so defer it.
        Blocked.Changed += v => MainThread.Enqueue(() => _entry.Value = v);
    }

    /// <summary>
    /// Grey out <paramref name="element"/> (and its children) whenever the guard is on.
    /// Works on the non-generic base type, so it also accepts <c>ActionMenu</c>/<c>SettingMenu</c>
    /// wrappers and auto-built panels.
    /// </summary>
    public static T Guard<T>(T element) where T : BaseUIElement
    {
        element.SetDisabled(Blocked.Value);

        // Weak so a panel rebuild doesn't pile up handlers on elements the overlay no longer renders.
        var weak = new WeakReference<BaseUIElement>(element);
        Action<bool> handler = null;
        handler = v =>
        {
            if (weak.TryGetTarget(out var target))
                target.SetDisabled(v);
            else
                Blocked.Changed -= handler;
        };
        Blocked.Changed += handler;

        return element;
    }

    /// <summary>Explanatory line shown at the top of a guarded panel, visible only while blocked.</summary>
    public static TextWrapped Notice(string name)
        => new TextWrapped(name, NoticeText).WithVisible(Blocked);
}
