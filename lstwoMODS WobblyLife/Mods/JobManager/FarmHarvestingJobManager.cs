using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class FarmHarvestingJobManager : BaseJobManager
{
    private Ref<int> money = new(5);

    public override Type missionType => typeof(FarmHarvestingJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<FarmHarvestingJobMission, int>("setMoneyPerDelivery", "Set Money per Delivery", "Money", (m, v) => m.moneyPerDelivery = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Delivery").WithValue(money),
                WithMacroMenu(new Button("Set Money per Delivery", () => SetMoneyPerDelivery(money.Value)).WithContentWidth(), "setMoneyPerDelivery", "Set Money per Delivery")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
    }

    public void SetMoneyPerDelivery(int money)
    {
        if (CheckMission())
            ((FarmHarvestingJobMission)Mission).moneyPerDelivery = money;
    }
}
