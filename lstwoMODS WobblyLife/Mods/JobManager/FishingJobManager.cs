using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class FishingJobManager : BaseJobManager
{
    private Ref<int> baseMoney = new(20);

    public override Type missionType => typeof(FishingJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<FishingJobMission, int>("setMoney", "Set Base Job Money", "Money", (m, v) => m.baseMoney = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            new HStack("money",
                new DragInt("##Base Job Money").WithValue(baseMoney),
                WithMacroMenu(new Button("Set Base Job Money", () => SetMoney(baseMoney.Value)).WithContentWidth(), "setMoney", "Set Base Job Money")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            baseMoney.Value = (int)((FishingJobMission)Mission).baseMoney;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((FishingJobMission)Mission).baseMoney = money;
    }
}
