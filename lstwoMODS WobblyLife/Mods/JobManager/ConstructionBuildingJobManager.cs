using System;
using System.Collections;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class ConstructionBuildingJobManager : BaseJobManager
{
    private Ref<int> money = new(0);
    private Ref<int> moneyPerPiece = new(2);

    public override Type missionType => typeof(ConstructionBuildingJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<ConstructionBuildingJobMission, int>("setMoney", "Set Job Money", "Money", (m, v) => m.money = v);
        RegisterJobAction<ConstructionBuildingJobMission, int>("setMoneyPerPiece", "Set Money per Building Piece", "Money", (m, v) => m.moneyPerBuildingPiece = v);
        RegisterJobAction<ConstructionBuildingJobMission>("spawnResources", "Spawn Resources", m => Plugin._StartCoroutine(m.ServerSpawnResources()));
        RegisterJobAction<ConstructionBuildingJobMission>("spawnHammers", "Spawn Hammers", m => Plugin._StartCoroutine(m.ServerSpawnHammers()));
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("money",
                new DragInt("##Job Money").WithValue(money),
                WithMacroMenu(new Button("Set Job Money", () => SetMoney(money.Value)).WithContentWidth(), "setMoney", "Set Job Money")
            ).WithContentWidth(),

            new HStack("money-per-piece",
                new DragInt("##Money Per Building Piece").WithValue(moneyPerPiece),
                WithMacroMenu(new Button("Set Money per Building Piece", () => SetMoneyPerBuildingPiece(moneyPerPiece.Value)).WithContentWidth(), "setMoneyPerPiece", "Set Money per Building Piece")
            ).WithContentWidth(),

            new HStack("spawns",
                WithMacroMenu(new Button("Spawn Resources", SpawnResources), "spawnResources", "Spawn Resources"),
                WithMacroMenu(new Button("Spawn Hammers", SpawnHammers), "spawnHammers", "Spawn Hammers")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
    }

    public void SpawnResources()
    {
        if (CheckMission())
            Plugin._StartCoroutine(((ConstructionBuildingJobMission)Mission).ServerSpawnResources());
    }

    public void SpawnHammers()
    {
        if (CheckMission())
            Plugin._StartCoroutine(((ConstructionBuildingJobMission)Mission).ServerSpawnHammers());
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
            ((ConstructionBuildingJobMission)Mission).money = money;
    }

    public void SetMoneyPerBuildingPiece(int money)
    {
        if (CheckMission())
            ((ConstructionBuildingJobMission)Mission).moneyPerBuildingPiece = money;
    }
}
