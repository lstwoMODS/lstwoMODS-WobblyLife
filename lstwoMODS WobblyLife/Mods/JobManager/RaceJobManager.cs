using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

public class RaceJobManager : BaseJobManager
{
    private InputFieldRef moneyInput;
    private List<GameObject> objects = new();
    private QuickReflection<RaceJobMission> reflect;

    public override Type missionType => typeof(RaceJobMission);

    public override void ConstructUI()
    {
        base.ConstructUI();

        var title = ui.CreateLabel("Race Job", "title", fontSize: 18);
        objects.Add(title.gameObject);
            
        var lapsLIB = ui.CreateLIBTrio("Set Laps", "lapsLIB", "1");
        lapsLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        lapsLIB.Button.OnClick = () => SetLaps(int.Parse(lapsLIB.Input.Text));
        objects.Add(lapsLIB.Root);
    }

    public override void RefreshUI()
    {
        bool b = CheckMission();

        root.SetActive(b);

        if (b)
        {
            reflect = new((RaceJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

            moneyInput.Text = ((int)reflect.GetField("laps")).ToString();
        }
    }

    public void SetLaps(int laps)
    {
        if (CheckMission())
        {
            reflect.SetField("laps", laps);
        }
    }
}