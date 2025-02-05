using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
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

            var moneyLabel = ui.CreateLabel("Set Laps", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("1", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply", () => SetLaps(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
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
}
