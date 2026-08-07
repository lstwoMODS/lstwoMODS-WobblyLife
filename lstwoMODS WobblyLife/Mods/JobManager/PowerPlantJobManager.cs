using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class PowerPlantJobManager : BaseJobManager
{
    private Ref<int> moneyPerBarrel = new(10);

    public override Type missionType => typeof(PowerPlantJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<PowerPlantJobMission, int>("setMoneyPerBarrel", "Set Money per Barrel", "Money", (m, v) => m.perBarrelMoney = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Barrel").WithValue(moneyPerBarrel),
                WithMacroMenu(new Button("Set Money per Barrel", () => SetMoneyPerBarrel(moneyPerBarrel.Value)).WithContentWidth(), "setMoneyPerBarrel", "Set Money per Barrel")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerBarrel.Value = (int)((PowerPlantJobMission)Mission).perBarrelMoney;
    }

    public void SetMoneyPerBarrel(int money)
    {
        if (CheckMission())
            ((PowerPlantJobMission)Mission).perBarrelMoney = money;
    }
}
