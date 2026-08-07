using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class MinerJobManager : BaseJobManager
{
    private Ref<int> money = new(0);

    public override Type missionType => typeof(MinerJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<MinerJobMission, int>("setMoney", "Set Current Job Money", "Money", (m, v) => m.totalEarnt = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Current Job Money").WithValue(money),
                WithMacroMenu(new Button("Set Current Job Money", () => SetMoney(money.Value)).WithContentWidth(), "setMoney", "Set Current Job Money")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            money.Value = (int)((MinerJobMission)Mission).totalEarnt;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((MinerJobMission)Mission).totalEarnt = money;
    }
}
