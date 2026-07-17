using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class NewsRoundManager : BaseJobManager
{
    private Ref<int> moneyPerPlayer = new(30);

    public override Type missionType => typeof(NewsRoundJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<NewsRoundJobMission, int>("setMoney", "Set Money per Player", "Money", (m, v) => m.currentMoneyPerPlayer = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            new HStack("money",
                new DragInt("##Money Per Player").WithValue(moneyPerPlayer),
                WithMacroMenu(new Button("Set Money per Player", () => SetMoney(moneyPerPlayer.Value)).WithContentWidth(), "setMoney", "Set Money per Player")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerPlayer.Value = (int)((NewsRoundJobMission)Mission).currentMoneyPerPlayer;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((NewsRoundJobMission)Mission).currentMoneyPerPlayer = money;
    }
}
