using System;
using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.Macros;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_WobblyLife.Mods;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public abstract class BaseJobManager
{
    public PlayerController Controller { get; private set; }
    public JobMission Mission { get; private set; }

    public abstract Type missionType { get; }

    public virtual string DisplayName => ModRegistry.NicifyName(GetType().Name.Replace("Manager", ""));

    public BaseJobManager()
    {
        JobMods.jobManagers.Add(this);
    }

    public abstract Container BuildContent(string id);

    public virtual void RefreshUI() { }

    public virtual void SetPlayerController(PlayerController controller)
    {
        Controller = controller;
        Mission = controller.GetPlayerControllerEmployment().GetActiveJob();
    }

    public virtual void SetPlayerController(PlayerController controller, JobMission missionOverride)
    {
        Controller = controller;
        Mission = missionOverride;
    }

    public bool CheckMission()
    {
        return Mission != null && missionType != null && missionType.IsInstanceOfType(Mission);
    }

    public virtual bool ShouldShow() => CheckMission();

    // --- Macro integration -------------------------------------------------
    //
    // Job actions are exposed to macros as plain MacroRegistry methods rather than
    // [ModAction]s: the managers aren't BaseMods, and a hand-registered method lets us
    // take an explicit Player parameter (rendered as the Local/By Name picker via
    // PlayerMacroType) and resolve the target job mission fresh at run time. Override
    // RegisterMacros to declare a manager's callable actions; JobMods calls it once at
    // startup for every manager.

    /// <summary>Declare this manager's macro-callable actions via <see cref="RegisterJobAction{TMission}(string,string,System.Action{TMission})"/>.
    /// Called once at startup. Wrap the matching UI button with <see cref="WithMacroMenu"/> so
    /// right-clicking it offers "Add to macro" / "Create hotkey".</summary>
    public virtual void RegisterMacros() { }

    /// <summary>Stable macro method id for one of this manager's actions.</summary>
    protected string MacroId(string action) => $"jobs.{GetType().Name}.{action}";

    /// <summary>Register a no-argument job action. It runs only when the resolved player's
    /// active job is a <typeparamref name="TMission"/>, mirroring the UI's mission gating.</summary>
    protected void RegisterJobAction<TMission>(string action, string label, Action<TMission> run)
        where TMission : JobMission
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id         = MacroId(action),
            Label      = label,
            Category   = $"Jobs/{DisplayName}",
            Parameters = new[] { PlayerParam() },
            Execute    = args =>
            {
                if (ResolveMission<TMission>(args[0] as PlayerRef) is { } mission) run(mission);
                return null;
            },
        });
    }

    /// <summary>Register a job action taking one extra argument (money, count, ...) shown as a
    /// step option labelled <paramref name="argLabel"/>.</summary>
    protected void RegisterJobAction<TMission, TArg>(string action, string label, string argLabel, Action<TMission, TArg> run)
        where TMission : JobMission
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id         = MacroId(action),
            Label      = label,
            Category   = $"Jobs/{DisplayName}",
            Parameters = new[] { PlayerParam(), new MacroParam { Name = argLabel, Type = typeof(TArg) } },
            Execute    = args =>
            {
                if (ResolveMission<TMission>(args[0] as PlayerRef) is { } mission) run(mission, (TArg)args[1]);
                return null;
            },
        });
    }

    /// <summary>Register a job action taking two extra arguments.</summary>
    protected void RegisterJobAction<TMission, TArg1, TArg2>(string action, string label,
        string argLabel1, string argLabel2, Action<TMission, TArg1, TArg2> run)
        where TMission : JobMission
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id         = MacroId(action),
            Label      = label,
            Category   = $"Jobs/{DisplayName}",
            Parameters = new[]
            {
                PlayerParam(),
                new MacroParam { Name = argLabel1, Type = typeof(TArg1) },
                new MacroParam { Name = argLabel2, Type = typeof(TArg2) },
            },
            Execute = args =>
            {
                if (ResolveMission<TMission>(args[0] as PlayerRef) is { } mission)
                    run(mission, (TArg1)args[1], (TArg2)args[2]);
                return null;
            },
        });
    }

    /// <summary>Wrap a UI widget so right-clicking it offers "Add to macro" / "Create hotkey" for
    /// the action registered under <paramref name="action"/>.</summary>
    protected ContextMenu WithMacroMenu(BaseUIElement trigger, string action, string label)
        => new($"{GetType().Name}-{action}-ctx", trigger,
            ModContextMenu.Items(MacroId(action), label).ToArray());

    private static MacroParam PlayerParam()
        => new() { Name = "Player", Type = typeof(PlayerRef), IsContext = true };

    private static TMission ResolveMission<TMission>(PlayerRef player) where TMission : JobMission
        => player?.ControllerEmployment?.GetActiveJob() as TMission;
}
