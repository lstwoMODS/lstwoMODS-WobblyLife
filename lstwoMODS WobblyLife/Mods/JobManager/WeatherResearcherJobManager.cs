using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class WeatherResearcherJobManager : BaseJobManager
{
    private Ref<int> moneyPerBalloon = new(10);

    public override Type missionType => typeof(WeatherResearchJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<WeatherResearchJobMission, int>("setMoney", "Set Money per Balloon", "Money", (m, v) => m.moneyPerBalloon = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Money Per Balloon").WithValue(moneyPerBalloon),
                WithMacroMenu(new Button("Set Money per Balloon", () => SetMoney(moneyPerBalloon.Value)).WithContentWidth(), "setMoney", "Set Money per Balloon")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            moneyPerBalloon.Value = (int)((WeatherResearchJobMission)Mission).moneyPerBalloon;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((WeatherResearchJobMission)Mission).moneyPerBalloon = money;
    }
}
