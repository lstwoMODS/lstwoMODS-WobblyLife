using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.JobManager
{
    public class FarmPlowingJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<FarmPlowingJobMission> reflect;

        public override Type missionType => typeof(FarmPlowingJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Farm Plowing Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Job Money", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("20", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoney(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((FarmPlowingJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("money")).ToString();
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
}
