using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

public class ConstructionBuildingJobManager : BaseJobManager
{
    public override Type missionType => typeof(ConstructionBuildingJobMission);

    private List<GameObject> objects = new List<GameObject>();
    private QuickReflection<ConstructionBuildingJobMission> reflect;

    public override void ConstructUI()
    {
        base.ConstructUI();

        var title = ui.CreateLabel("Construction Building Job", "title", fontSize: 18);
        objects.Add(title.gameObject);

        var moneyLIB = ui.CreateLIBTrio("Set Current Job Money", "SetMoney", "0");
        moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        moneyLIB.Button.OnClick = () => SetMoney(int.Parse(moneyLIB.Input.Text));
        objects.Add(moneyLIB.Root);

        objects.Add(ui.AddSpacer(12));

        var spawnResourcesBtn = ui.CreateLBDuo("Spawn Resources", "SpawnResources", SpawnResources, "Spawn", "SpawnResourcesButton");
        objects.Add(spawnResourcesBtn.Root);

        objects.Add(ui.AddSpacer(6));

        var spawnHammersBtn = ui.CreateLBDuo("Spawn Hammers", "SpawnHammers", SpawnHammers, "Spawn", "SpawnHammersButton");
        objects.Add(spawnHammersBtn.Root);
            
        objects.Add(ui.AddSpacer(6));
            
        var moneyPerPieceLIB = ui.CreateLIBTrio("Set Current Job Money Per Building Piece", "moneyPerPieceLIB", "2");
        moneyPerPieceLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        moneyPerPieceLIB.Button.OnClick = () => SetMoneyPerBuildingPiece(int.Parse(moneyPerPieceLIB.Input.Text));
        objects.Add(moneyPerPieceLIB.Root);
    }

    public override void RefreshUI()
    {
        bool b = CheckMission();

        root.SetActive(b);

        if (b)
        {
            reflect = new((ConstructionBuildingJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    public void SpawnResources()
    {
        if (CheckMission())
        {
            Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("ServerSpawnResources"));
        }
    }

    public void SpawnHammers()
    {
        if (CheckMission())
        {
            Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("ServerSpawnHammers"));
        }
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
        {
            reflect.SetField("money", money);
        }
    }

    public void SetMoneyPerBuildingPiece(int money)
    {
        if (CheckMission())
        {
            reflect.SetField("moneyPerBuildingPiece", money);
        }
    }
}