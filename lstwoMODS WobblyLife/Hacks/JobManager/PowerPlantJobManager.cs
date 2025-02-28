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
    public class PowerPlantJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<PowerPlantJobMission> reflect;

        public override Type missionType => typeof(PowerPlantJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Power Plant Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            var moneyLIB = ui.CreateLIBTrio("Set Money Per Barrel", "moneyLIB", "10");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoneyPerBarrel(int.Parse(moneyLIB.Input.Text));
            objects.Add(moneyLIB.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((PowerPlantJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("perBarrelMoney")).ToString();
            }
        }

        public void SetMoneyPerBarrel(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("perBarrelMoney", money);
            }
        }
    }
}
