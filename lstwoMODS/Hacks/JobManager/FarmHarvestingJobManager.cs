using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.JobManager
{
    public class FarmHarvestingJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<FarmHarvestingJobMission> reflect;

        public override Type missionType => typeof(FarmHarvestingJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Farm Harvesting Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Money Per Delivery", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("5", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoneyPerDelivery(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((FarmHarvestingJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        public void SetMoneyPerDelivery(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerDelivery", money);
            }
        }
    }
}
