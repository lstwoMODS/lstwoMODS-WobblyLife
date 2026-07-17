using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class ArtStudioJobManager : BaseJobManager
{
    private Ref<int> money = new(30);

    public override Type missionType => typeof(ArtStudioJobMission);

    public override Container BuildContent(string id)
    {
        return new Container(id,

            new HStack("money",
                new DragInt("##Job Money").WithValue(money),
                WithMacroMenu(new Button("Set Job Money", () => SetMoney(money.Value)).WithContentWidth(), "setMoney", "Set Job Money")
            ).WithContentWidth(),

            WithMacroMenu(new Button("Spawn All Tools", SpawnAllTools), "spawnAllTools", "Spawn All Tools")
        );
    }

    public override void RegisterMacros()
    {
        RegisterJobAction<ArtStudioJobMission, int>("setMoney", "Set Job Money", "Money", (m, money) => m.money = money);
        RegisterJobAction<ArtStudioJobMission>("spawnAllTools", "Spawn All Tools", m => m.ServerSpawnAllTools());
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (b)
            money.Value = (int)((ArtStudioJobMission)Mission).money;
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((ArtStudioJobMission)Mission).money = money;
    }

    public void SpawnAllTools()
    {
        if (CheckMission())
            ((ArtStudioJobMission)Mission).ServerSpawnAllTools();
    }
}
