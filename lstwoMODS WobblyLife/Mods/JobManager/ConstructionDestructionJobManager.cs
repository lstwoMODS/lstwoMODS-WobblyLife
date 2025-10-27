using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

public class ConstructionDestructionJobManager : BaseJobManager
{
    public override Type missionType => typeof(ConstructionDestructionJobMission);

    private List<GameObject> objects = new List<GameObject>();
    private QuickReflection<ConstructionDestructionJobMission> reflect;

    public override void ConstructUI()
    {
        base.ConstructUI();

        var title = ui.CreateLabel("Construction Destruction Job", "title", fontSize: 18);
        objects.Add(title.gameObject);

        var moneyLIB = ui.CreateLIBTrio("Set Current Job Money", "moneyLIB", "0");
        moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        moneyLIB.Button.OnClick = () => SetMoney(int.Parse(moneyLIB.Input.Text));
        objects.Add(moneyLIB.Root);

        objects.Add(ui.AddSpacer(10));

        var spawnTools = ui.CreateLBDuo("Spawn Tools", "SpawnTools", SpawnTools, "Spawn", "SpawnToolsButton");
        objects.Add(spawnTools.Root);

        objects.Add(ui.AddSpacer(5));

        var destroyTools = ui.CreateLBDuo("Destroy Tools", "DestroyTools", DestroyTools, "Destroy", "DestroyToolsButton");
        objects.Add(destroyTools.Root);
    }

    public override void RefreshUI()
    {
        bool b = CheckMission();

        root.SetActive(b);

        if (b)
        {
            reflect = new((ConstructionDestructionJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    public void SpawnTools()
    {
        if (CheckMission())
        {
            Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("SpawnTools"));
        }
    }

    public void DestroyTools()
    {
        if (CheckMission())
        {
            reflect.GetMethod("DestroyAllTools");
        }
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
        {
            reflect.SetField("money", money);
        }
    }
}