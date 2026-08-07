using System;
using System.Collections;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class ConstructionDestructionJobManager : BaseJobManager
{
    private Ref<int> money = new(0);

    public override Type missionType => typeof(ConstructionDestructionJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<ConstructionDestructionJobMission, int>("setMoney", "Set Job Money", "Money", (m, v) => m.money = v);
        RegisterJobAction<ConstructionDestructionJobMission>("spawnTools", "Spawn Tools", m => Plugin._StartCoroutine(m.SpawnTools()));
        RegisterJobAction<ConstructionDestructionJobMission>("destroyTools", "Destroy Tools", m => m.DestroyAllTools());
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Job Money").WithValue(money),
                WithMacroMenu(new Button("Set Job Money", () => SetMoney(money.Value)).WithContentWidth(), "setMoney", "Set Job Money")
            ).WithContentWidth(),

            new HStack("tools",
                WithMacroMenu(new Button("Spawn Tools", SpawnTools), "spawnTools", "Spawn Tools"),
                WithMacroMenu(new Button("Destroy Tools", DestroyTools), "destroyTools", "Destroy Tools")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
    }

    public void SpawnTools()
    {
        if (CheckMission())
            Plugin._StartCoroutine(((ConstructionDestructionJobMission)Mission).SpawnTools());
    }

    public void DestroyTools()
    {
        if (CheckMission())
            ((ConstructionDestructionJobMission)Mission).DestroyAllTools();
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((ConstructionDestructionJobMission)Mission).money = money;
    }
}
