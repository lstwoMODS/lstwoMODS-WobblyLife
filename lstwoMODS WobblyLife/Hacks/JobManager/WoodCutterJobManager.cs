using System;
using System.Collections.Generic;
using System.Reflection;
using lstwoMODS_Core;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

public class WoodCutterJobManager : BaseJobManager
{
    private HacksUIHelper.LIBTrio moneyLIB;
    private List<GameObject> objects = new();
    private QuickReflection<WoodCutterJobMission> reflect;

    public override Type missionType => typeof(WoodCutterJobMission);

    public override void ConstructUI()
    {
        base.ConstructUI();

        var title = ui.CreateLabel("Woodcutter Job", "title", fontSize: 18);
        objects.Add(title.gameObject);
            
        moneyLIB = ui.CreateLIBTrio("Set Money Per Plank", "moneyPerDeliveredLIB", "5");
        moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        moneyLIB.Button.OnClick = () => SetMoney(int.Parse(moneyLIB.Input.Text));
        objects.Add(moneyLIB.Root);
    }

    public override void RefreshUI()
    {
        bool b = CheckMission();

        root.SetActive(b);

        if (b)
        {
            reflect = new((WoodCutterJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

            moneyLIB.Input.Text = ((int)reflect.GetField("moneyPerPlank")).ToString();
        }
    }

    public void SetMoney(int money)
    {
        if (CheckMission())
        {
            reflect.SetField("moneyPerPlank", money);
        }
    }
}