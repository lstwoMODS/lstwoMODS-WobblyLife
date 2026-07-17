using System;
using System.Collections.Generic;
using lstwoMODS_Core.Macros;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// A convenience layer over <see cref="MacroTriggerRegistry"/> for triggers that fire off a
/// per-local-player game event (money gained, present collected, respawn, ...). Register one with
/// <see cref="Register"/> and it automatically gains:
/// <list type="bullet">
///   <item>a <b>Player</b> filter config field  the same picker a step's Player parameter shows
///     (Any Player / Local Player / By Name), so a macro fires only for the player it names;</item>
///   <item>a <c>player</c> output (the <see cref="PlayerRef"/> it fired for), usable in the macro's
///     expressions as the bare variable <c>player</c> or via <c>trigger("player")</c>;</item>
///   <item>the per-controller hook/unhook plumbing (<see cref="ForEachLocalController"/>) so it
///     survives respawns and joining/leaving a session.</item>
/// </list>
/// Your <c>subscribe</c> only wires the one event and calls <c>fire(...)</c> with any extra values;
/// the filter and the <c>player</c> value are applied for you. The three built-in WL player triggers
/// (Got Money / Present Collected / Player Spawned) are registered through this same API.
/// </summary>
public static class PlayerMacroTrigger
{
    /// <summary>Config key of the auto-added player-name filter.</summary>
    public const string PlayerFilterKey = "playerFilter";

    /// <summary>Output key of the auto-added firing player.</summary>
    public const string PlayerOut = "player";

    /// <summary>Fire the trigger for the current player, passing this trigger's own extra values
    /// (the wrapper adds <c>player</c> and applies the name filter). Called from the event handler
    /// your <see cref="Register"/> <c>subscribe</c> wired up.</summary>
    public delegate void PlayerTriggerFire(params (string Key, object Value)[] extraValues);

    /// <summary>
    /// Register a player-based trigger. <paramref name="subscribe"/> is invoked once per local
    /// controller: hook the controller's event and return an <see cref="IDisposable"/> that unhooks
    /// it (or null to skip this controller), calling the supplied <see cref="PlayerTriggerFire"/>
    /// when the event fires. The <b>Player</b> filter field and the <c>player</c> output are added
    /// for you; pass only this trigger's own <paramref name="extraParams"/> / <paramref name="extraOutputs"/>.
    /// </summary>
    public static void Register(
        string id,
        string label,
        Func<MacroTriggerContext, PlayerController, PlayerTriggerFire, IDisposable> subscribe,
        MacroTriggerParam[] extraParams = null,
        MacroTriggerOutput[] extraOutputs = null)
    {
        var paramList = new List<MacroTriggerParam>();
        if (extraParams != null) paramList.AddRange(extraParams);
        paramList.Add(new MacroTriggerParam
        {
            // A registered macro type, so the editor renders PlayerRef's own modes here.
            Key = PlayerFilterKey, Label = "Player", Type = typeof(PlayerRef),
            Default = "", EmptyLabel = "Any Player",
            Tooltip = "Only fire for this player. \"Any Player\" fires for every local player.",
        });

        var outputList = new List<MacroTriggerOutput>();
        if (extraOutputs != null) outputList.AddRange(extraOutputs);
        outputList.Add(new MacroTriggerOutput
        {
            Key = PlayerOut, Label = "Player", Type = typeof(PlayerRef),
            Tooltip = "The local player this fired for.",
        });

        MacroTriggerRegistry.Register(new MacroTriggerDescriptor
        {
            Id     = id,
            Label  = label,
            Params = paramList.ToArray(),
            Outputs = outputList.ToArray(),
            Arm = ctx =>
            {
                // One handle per hooked controller so detach removes the exact subscription.
                var handles = new Dictionary<PlayerController, IDisposable>();
                return ForEachLocalController(
                    pc =>
                    {
                        void Fire((string Key, object Value)[] extra)
                        {
                            if (!PassesFilter(ctx, pc)) return;
                            var values = new (string, object)[(extra?.Length ?? 0) + 1];
                            values[0] = (PlayerOut, (PlayerRef)pc);
                            if (extra != null && extra.Length > 0)
                                Array.Copy(extra, 0, values, 1, extra.Length);
                            ctx.Fire(values);
                        }

                        var handle = subscribe(ctx, pc, Fire);
                        if (handle != null) handles[pc] = handle;
                    },
                    pc =>
                    {
                        if (!handles.TryGetValue(pc, out var handle)) return;
                        handles.Remove(pc);
                        handle.Dispose();
                    });
            },
        });
    }

    /// <summary>Whether <paramref name="pc"/> is the player the filter names ("Any Player" matches
    /// everyone). Resolved live at fire time, so a rename takes effect without a rearm; a filter
    /// naming someone who isn't here right now resolves to nothing and simply doesn't fire.</summary>
    private static bool PassesFilter(MacroTriggerContext ctx, PlayerController pc)
    {
        PlayerRef wanted;
        try { wanted = ctx.GetTyped<PlayerRef>(PlayerFilterKey); }
        catch { return false; }
        // Identity, not Unity's ==: FindByName hands back a fresh PlayerRef around the same
        // controller, so compare the controllers themselves.
        return wanted == null || ReferenceEquals(wanted.Controller, pc);
    }

    /// <summary>
    /// Attach a per-local-player event to every local <see cref="PlayerController"/>  the ones
    /// present now and any assigned later  and detach on rearm/disable/delete or when a controller
    /// is unassigned. The robust way to hook events that live on the player controller (its
    /// employment, unlocker, spawn callbacks), which come and go with respawns and session changes.
    /// </summary>
    internal static IDisposable ForEachLocalController(Action<PlayerController> attach, Action<PlayerController> detach)
    {
        var hooked = new List<PlayerController>();

        void Attach(PlayerController pc)
        {
            if (pc == null || hooked.Contains(pc)) return;
            try { if (!pc.IsLocal()) return; }
            catch { return; }
            hooked.Add(pc);
            try { attach(pc); }
            catch (Exception ex) { Plugin.LogSource.LogError($"[Macros] player trigger attach failed: {ex}"); }
        }

        void Detach(PlayerController pc)
        {
            if (pc == null || !hooked.Remove(pc)) return;
            try { detach(pc); } catch { /* controller tearing down; nothing to clean */ }
        }

        GameInstance.OnAssignedPlayerController   onAssigned   = Attach;
        GameInstance.OnUnassignedPlayerController onUnassigned = Detach;
        GameInstance.onAssignedPlayerController   += onAssigned;
        GameInstance.onUnassignedPlayerController += onUnassigned;

        // Hook controllers that already exist (macro armed mid-session).
        var existing = GameInstance.Instance?.GetPlayerControllers();
        if (existing != null)
            foreach (var pc in existing) Attach(pc);

        return new CallbackDisposable(() =>
        {
            GameInstance.onAssignedPlayerController   -= onAssigned;
            GameInstance.onUnassignedPlayerController -= onUnassigned;
            foreach (var pc in hooked.ToArray()) Detach(pc);
        });
    }
}
