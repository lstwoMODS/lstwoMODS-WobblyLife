using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class FarmSeedingJobManager : BaseJobManager
{
    private Ref<int> money = new(20);

    public override Type missionType => typeof(FarmSeedingJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<FarmSeedingJobMission, int>("setMoney", "Set Job Money", "Money", (m, v) => m.money = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            new HStack("money",
                new DragInt("##Job Money").WithValue(money),
                WithMacroMenu(new Button("Set Job Money", () => SetMoney(money.Value)).WithContentWidth(), "setMoney", "Set Job Money")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            money.Value = (int)((FarmSeedingJobMission)Mission).money;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((FarmSeedingJobMission)Mission).money = money;
    }
}
