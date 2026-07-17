using System;
using HawkNetworking;
using lstwoMODS_Core.Macros;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Wobbly Life specific macro triggers, registered with the core <see cref="MacroTriggerRegistry"/>
/// through the same public API the built-in Manual/Hotkey/Interval triggers use. Called once at
/// startup (see Plugin.Start).
///
/// Triggers that fire off a local-player event (money, presents, spawn) go through
/// <see cref="PlayerMacroTrigger"/>, which gives each a Player filter and a <c>player</c> output for
/// free and survives respawns / joining-leaving a session; the rest hook a static game event directly.
/// </summary>
public static class WLMacroTriggers
{
    private const string SceneKey     = "scene";
    private const string AllScenesKey = "allScenes";
    private const string LoadModeKey  = "loadMode";
    private const string MinKey       = "min";

    // Output keys: the values a trigger hands the run, usable in the macro's expressions as bare
    // variables (or via trigger("key")). See docs/custom-macro-triggers.md. The player-based
    // triggers' "player" output is added by PlayerMacroTrigger.
    private const string SceneOut    = "scene";
    private const string AdditiveOut = "additive";
    private const string AmountOut   = "amount";
    private const string TotalOut    = "total";
    private const string PresentOut  = "present";

    public static void Register()
    {
        RegisterSceneLoaded();
        RegisterGotMoney();
        RegisterPresentCollected();
        RegisterPlayerSpawned();
        RegisterPlayerJoined();
        RegisterJoinedGame();
    }

    /// <summary>Fires when the game finishes loading a scene. "All scenes" ignores the Scene pick
    /// and fires on any load; otherwise it's gated to the chosen <see cref="LoadScene"/> (a plain
    /// enum, so the editor renders it as a dropdown automatically). "Load mode" picks Single (a full
    /// scene switch  the usual case) or Additive (a scene layered on top of the current one).</summary>
    private static void RegisterSceneLoaded()
    {
        MacroTriggerRegistry.Register(new MacroTriggerDescriptor
        {
            Id    = "wl.sceneLoaded",
            Label = "Scene Loaded",
            Params = new[]
            {
                new MacroTriggerParam
                {
                    Key = AllScenesKey, Label = "All scenes", Type = typeof(bool), Default = false,
                    Tooltip = "Fire on any scene load, ignoring the Scene pick below.",
                },
                new MacroTriggerParam
                {
                    Key = SceneKey, Label = "Scene", Type = typeof(LoadScene),
                    Default = LoadScene.WobblyIsland,
                    Tooltip = "Fire this macro when the game finishes loading this scene. Ignored when \"All scenes\" is on.",
                },
                new MacroTriggerParam
                {
                    Key = LoadModeKey, Label = "Load mode", Type = typeof(LoadSceneMode),
                    Default = LoadSceneMode.Single,
                    Tooltip = "Single = a full scene switch (the usual case). Additive = a scene layered on top of the current one.",
                },
            },
            Outputs = new[]
            {
                new MacroTriggerOutput { Key = SceneOut, Label = "Scene", Type = typeof(LoadScene),
                    Tooltip = "Which scene finished loading (useful with \"All scenes\" on)." },
                new MacroTriggerOutput { Key = AdditiveOut, Label = "Additive", Type = typeof(bool),
                    Tooltip = "True when the scene was layered on additively rather than switched to." },
            },
            Arm = ctx =>
            {
                var all    = ctx.GetBool(AllScenesKey);
                var wanted = ctx.GetEnum<LoadScene>(SceneKey);
                var mode   = ctx.GetEnum<LoadSceneMode>(LoadModeKey);

                if (mode == LoadSceneMode.Single)
                {
                    void Handler(LoadScene loaded)
                    {
                        if (all || loaded == wanted) ctx.Fire((SceneOut, loaded), (AdditiveOut, false));
                    }
                    GameInstance.onSceneLoaded += Handler;
                    return new CallbackDisposable(() => GameInstance.onSceneLoaded -= Handler);
                }

                void HawkHandler(LoadUnloadSceneData data)
                {
                    if (data == null || data.loadMode != LoadSceneMode.Additive) return;
                    var gi = GameInstance.Instance;
                    var loaded = gi != null ? gi.GetLoadSceneFromLoadUnloadSceneData(data) : wanted;
                    if (!all && (gi == null || loaded != wanted)) return;
                    ctx.Fire((SceneOut, loaded), (AdditiveOut, true));
                }
                HawkSceneManager.onSceneLoaded += HawkHandler;
                return new CallbackDisposable(() => HawkSceneManager.onSceneLoaded -= HawkHandler);
            },
        });
    }

    /// <summary>Fires when a local player gains money (a job payout, a sale, ...). The "Min amount"
    /// config gates it to gains of at least that much in one go  handy for challenges ("fire when I
    /// earn 500+ at once"); 1 means any gain. Player-based (see <see cref="PlayerMacroTrigger"/>): it
    /// gets a Player filter and a <c>player</c> output for free.</summary>
    private static void RegisterGotMoney()
    {
        PlayerMacroTrigger.Register("wl.gotMoney", "Got Money",
            (ctx, pc, fire) =>
            {
                var employment = pc.GetPlayerControllerEmployment();
                if (employment == null) return null;
                var min = ctx.GetInt(MinKey);
                void OnMoney(int amount, int money)
                {
                    if (amount >= min) fire((AmountOut, amount), (TotalOut, money));
                }
                employment.onLocalMoneyChanged += OnMoney;
                return new CallbackDisposable(() => employment.onLocalMoneyChanged -= OnMoney);
            },
            extraParams: new[]
            {
                new MacroTriggerParam
                {
                    Key = MinKey, Label = "Min amount", Type = typeof(int), Default = 1,
                    Tooltip = "Fire when the money gained in one go is at least this. 1 = any gain.",
                },
            },
            extraOutputs: new[]
            {
                new MacroTriggerOutput { Key = AmountOut, Label = "Amount", Type = typeof(int),
                    Tooltip = "How much money was gained in this one payout." },
                new MacroTriggerOutput { Key = TotalOut, Label = "Total", Type = typeof(int),
                    Tooltip = "The player's new money balance after the gain." },
            });
    }

    /// <summary>Fires when a local player collects a wizard present. Player-based: gets a Player
    /// filter and a <c>player</c> output for free.</summary>
    private static void RegisterPresentCollected()
    {
        PlayerMacroTrigger.Register("wl.presentCollected", "Present Collected",
            (ctx, pc, fire) =>
            {
                var unlocker = pc.GetPlayerControllerUnlocker();
                if (unlocker == null) return null;
                void OnPresent(PlayerController controller, Guid presentGuid) => fire((PresentOut, presentGuid.ToString()));
                unlocker.onPresentCollected += OnPresent;
                return new CallbackDisposable(() => unlocker.onPresentCollected -= OnPresent);
            },
            extraOutputs: new[]
            {
                new MacroTriggerOutput { Key = PresentOut, Label = "Present id", Type = typeof(string),
                    Tooltip = "The collected present's unique id." },
            });
    }

    /// <summary>Fires when a local player's character (re)spawns  the handy place to re-apply speed,
    /// noclip, cosmetics and the like after a death or scene change. Player-based: gets a Player
    /// filter and a <c>player</c> output for free.</summary>
    private static void RegisterPlayerSpawned()
    {
        PlayerMacroTrigger.Register("wl.playerSpawned", "Player Spawned",
            (ctx, pc, fire) =>
            {
                void OnSpawn(PlayerController controller, PlayerCharacter character) => fire();
                pc.onPlayerSpawned += OnSpawn;
                return new CallbackDisposable(() => pc.onPlayerSpawned -= OnSpawn);
            });
    }

    /// <summary>Host-side: fires when another player connects to your server  good for content
    /// (announce joiners, hand out a welcome kit, ...).</summary>
    private static void RegisterPlayerJoined()
    {
        MacroTriggerRegistry.Register(new MacroTriggerDescriptor
        {
            Id    = "wl.playerJoined",
            Label = "Player Joined (host)",
            Arm = ctx =>
            {
                void OnConnected(HawkConnection connection) => ctx.Fire();
                WobblyNetworkManager.onServerPlayerConnected += OnConnected;
                return new CallbackDisposable(() => WobblyNetworkManager.onServerPlayerConnected -= OnConnected);
            },
        });
    }

    /// <summary>Fires when you connect to a game (join a lobby or start hosting).</summary>
    private static void RegisterJoinedGame()
    {
        MacroTriggerRegistry.Register(new MacroTriggerDescriptor
        {
            Id    = "wl.joinedGame",
            Label = "Joined a Game",
            Arm = ctx =>
            {
                void OnConnected() => ctx.Fire();
                WobblyNetworkManager.onClientConnected += OnConnected;
                return new CallbackDisposable(() => WobblyNetworkManager.onClientConnected -= OnConnected);
            },
        });
    }
}
