using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class FireFighterJobManager : BaseJobManager
{
    private Ref<int> moneyPerFlame = new(5);

    public override Type missionType => typeof(FireFighterJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<FireFighterJobMission, int>("setMoneyPerFlame", "Set Money per Flame", "Money", (m, v) => m.moneyPerFlame = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            new HStack("money",
                new DragInt("##Money Per Flame").WithValue(moneyPerFlame),
                WithMacroMenu(new Button("Set Money per Flame", () => SetMoneyPerFlame(moneyPerFlame.Value)).WithContentWidth(), "setMoneyPerFlame", "Set Money per Flame")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerFlame.Value = (int)((FireFighterJobMission)Mission).moneyPerFlame;
    }

    public void SetMoneyPerFlame(int money)
    {
        if (CheckMission())
            ((FireFighterJobMission)Mission).moneyPerFlame = money;
    }
}
