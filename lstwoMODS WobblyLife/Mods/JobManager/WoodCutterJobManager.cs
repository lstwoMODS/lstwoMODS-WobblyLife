using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class WoodCutterJobManager : BaseJobManager
{
    private Ref<int> moneyPerPlank = new(5);

    public override Type missionType => typeof(WoodCutterJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<WoodCutterJobMission, int>("setMoney", "Set Money per Plank", "Money", (m, v) => m.moneyPerPlank = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Plank").WithValue(moneyPerPlank),
                WithMacroMenu(new Button("Set Money per Plank", () => SetMoney(moneyPerPlank.Value)).WithContentWidth(), "setMoney", "Set Money per Plank")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerPlank.Value = (int)((WoodCutterJobMission)Mission).moneyPerPlank;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((WoodCutterJobMission)Mission).moneyPerPlank = money;
    }
}
