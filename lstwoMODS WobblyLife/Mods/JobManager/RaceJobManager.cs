using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class RaceJobManager : BaseJobManager
{
    private Ref<int> laps = new(1);

    public override Type missionType => typeof(RaceJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<RaceJobMission, int>("setLaps", "Set Laps", "Laps", (m, v) => m.laps = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("laps",
                new DragInt("##Laps").WithValue(laps),
                WithMacroMenu(new Button("Set Laps", () => SetLaps(laps.Value)).WithContentWidth(), "setLaps", "Set Laps")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            laps.Value = (int)((RaceJobMission)Mission).laps;
    }

    public void SetLaps(int laps)
    {
        if (CheckMission())
            ((RaceJobMission)Mission).laps = laps;
    }
}
