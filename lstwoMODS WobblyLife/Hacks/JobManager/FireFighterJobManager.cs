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
    public class FireFighterJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<FireFighterJobMission> reflect;

        public override Type missionType => typeof(FireFighterJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Fire Fighter Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            var moneyLIB = ui.CreateLIBTrio("Set Money Per Flame", "moneyLIB", "5");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoneyPerFlame(int.Parse(moneyLIB.Input.Text));
            objects.Add(moneyLIB.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((FireFighterJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("moneyPerFlame")).ToString();
            }
        }

        public void SetMoneyPerFlame(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerFlame", money);
            }
        }
    }
}
