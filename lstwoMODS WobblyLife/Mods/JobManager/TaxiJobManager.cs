using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class TaxiJobManager : BaseJobManager
{
    private Ref<int> maxMoneyPerDelivery = new(10);

    public override Type missionType => typeof(TaxiJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<TaxiJobMission, int>("setMoney", "Set Max Money per Delivery", "Money", (m, v) => m.maxMoneyPerDelivery = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Max Money Per Delivery").WithValue(maxMoneyPerDelivery),
                WithMacroMenu(new Button("Set Max Money per Delivery", () => SetMoney(maxMoneyPerDelivery.Value)).WithContentWidth(), "setMoney", "Set Max Money per Delivery")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            maxMoneyPerDelivery.Value = (int)((TaxiJobMission)Mission).maxMoneyPerDelivery;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((TaxiJobMission)Mission).maxMoneyPerDelivery = money;
    }
}
