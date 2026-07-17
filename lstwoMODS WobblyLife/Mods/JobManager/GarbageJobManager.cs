using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class GarbageJobManager : BaseJobManager
{
    private Ref<int> moneyEarned = new(0);
    private Ref<int> moneyPerBag = new(5);

    public override Type missionType => typeof(GarbageJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<GarbageJobMission, int>("setMoneyEarned", "Set Money Earned", "Money", (m, v) => m.moneyEarnt = v);
        RegisterJobAction<GarbageJobMission, int>("setMoneyPerBag", "Set Money per Bag", "Money", (m, v) => m.moneyPerBagDisposed = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money-earned",
                new DragInt("##Money Earned").WithValue(moneyEarned),
                WithMacroMenu(new Button("Set Money Earned", () => SetMoneyEarned(moneyEarned.Value)).WithContentWidth(), "setMoneyEarned", "Set Money Earned")
            ).WithContentWidth(),

            new HStack("money-per-bag",
                new DragInt("##Money Per Bag").WithValue(moneyPerBag),
                WithMacroMenu(new Button("Set", () => SetMoneyPerBag(moneyPerBag.Value)).WithContentWidth(), "setMoneyPerBag", "Set Money per Bag")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
        {
            var m = (GarbageJobMission)Mission;
            moneyEarned.Value = (int)m.moneyEarnt;
            moneyPerBag.Value = (int)m.moneyPerBagDisposed;
        }
    }

    public void SetMoneyEarned(int money)
    {
        if (CheckMission())
            ((GarbageJobMission)Mission).moneyEarnt = money;
    }

    public void SetMoneyPerBag(int money)
    {
        if (CheckMission())
            ((GarbageJobMission)Mission).moneyPerBagDisposed = money;
    }
}
