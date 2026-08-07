using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class IceCreamJobManager : BaseJobManager
{
    private Ref<int> moneyPerOrder = new(5);

    public override Type missionType => typeof(IceCreamJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<IceCreamJobMission, int>("setMoneyPerOrder", "Set Money per Order", "Money", (m, v) => m.moneyPerOrder = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Order").WithValue(moneyPerOrder),
                WithMacroMenu(new Button("Set Money per Order", () => SetMoneyPerOrder(moneyPerOrder.Value)).WithContentWidth(), "setMoneyPerOrder", "Set Money per Order")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerOrder.Value = (int)((IceCreamJobMission)Mission).moneyPerOrder;
    }

    public void SetMoneyPerOrder(int money)
    {
        if (CheckMission())
            ((IceCreamJobMission)Mission).moneyPerOrder = money;
    }
}
