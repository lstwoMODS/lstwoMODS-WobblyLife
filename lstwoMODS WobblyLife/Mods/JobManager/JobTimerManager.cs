using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class JobTimerManager : BaseJobManager
{
    private Ref<int> timerSeconds = new(60);
    private Ref<bool> isRunning = new(true);
    private Ref<bool> resetTimer = new(false);
    private JobMissionTimer timer;

    public override Type missionType => null;

    public override void RegisterMacros()
    {
        // The timer lives as a component on any job mission, so these resolve it from the
        // player's active job rather than a fixed mission type.
        RegisterJobAction<JobMission, int>("setTimer", "Set Timer (seconds)", "Seconds",
            (m, seconds) => { if (m.TryGetComponent(out JobMissionTimer t)) t.jobTimerInSeconds = (ulong)seconds; });
        RegisterJobAction<JobMission, bool, bool>("setTimerRunning", "Set Timer Running", "Running", "Reset",
            (m, running, reset) => { if (m.TryGetComponent(out JobMissionTimer t)) t.ServerSetRunning(running, reset); });
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("timer",
                new DragInt("##Timer (seconds)").WithValue(timerSeconds),
                WithMacroMenu(new Button("Set Timer (seconds)", () => SetTimerInSeconds((ulong)timerSeconds.Value)).WithContentWidth(), "setTimer", "Set Timer (seconds)")
            ).WithContentWidth(),

            new Checkbox("Enable Timer").WithValue(isRunning),
            new Checkbox("Reset Timer").WithValue(resetTimer),
            WithMacroMenu(new Button("Set Timer Running", () => SetTimerRunning(isRunning.Value, resetTimer.Value)), "setTimerRunning", "Set Timer Running")
        );
    }

    public override bool ShouldShow() => Mission != null && Mission.TryGetComponent(out timer);

    public override void RefreshUI() { }

    public void SetTimerInSeconds(ulong seconds)
    {
        if (timer != null)
            timer.jobTimerInSeconds = seconds;
    }

    public void SetTimerRunning(bool bRunning, bool bResetTimer)
    {
        if (timer != null)
            timer.ServerSetRunning(bRunning, bResetTimer);
    }
}
