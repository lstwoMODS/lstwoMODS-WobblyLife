using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
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
            
            var moneyLIB = ui.CreateLIBTrio("Set Money Per Delivery", "moneyLIB", "5");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoneyPerDelivery(int.Parse(moneyInput.Text));
            objects.Add(moneyLIB.Root);
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
