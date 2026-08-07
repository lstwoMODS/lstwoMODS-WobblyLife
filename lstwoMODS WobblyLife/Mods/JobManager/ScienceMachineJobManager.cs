using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class ScienceMachineJobManager : BaseJobManager
{
    private Ref<int> moneyPerDelivered = new(10);

    public override Type missionType => typeof(ScienceMachineJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<ScienceMachineJobMission, int>("setMoney", "Set Money per Delivered Pipe", "Money", (m, v) => m.moneyPerDelivered = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Delivered Pipe").WithValue(moneyPerDelivered),
                WithMacroMenu(new Button("Set Money per Delivered Pipe", () => SetMoney(moneyPerDelivered.Value)).WithContentWidth(), "setMoney", "Set Money per Delivered Pipe")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerDelivered.Value = (int)((ScienceMachineJobMission)Mission).moneyPerDelivered;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((ScienceMachineJobMission)Mission).moneyPerDelivered = money;
    }
}
