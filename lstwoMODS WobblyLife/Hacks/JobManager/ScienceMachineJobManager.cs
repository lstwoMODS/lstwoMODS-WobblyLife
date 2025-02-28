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
    public class ScienceMachineJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<ScienceMachineJobMission> reflect;

        public override Type missionType => typeof(ScienceMachineJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Science Machine Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            var moneyPerDeliveredLIB = ui.CreateLIBTrio("Set Money Per Delivered Pipe", "moneyPerDeliveredLIB", "10");
            moneyPerDeliveredLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyPerDeliveredLIB.Button.OnClick = () => SetMoney(int.Parse(moneyPerDeliveredLIB.Input.Text));
            objects.Add(moneyPerDeliveredLIB.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((ScienceMachineJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("moneyPerDelivered")).ToString();
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerDelivered", money);
            }
        }
    }
}
