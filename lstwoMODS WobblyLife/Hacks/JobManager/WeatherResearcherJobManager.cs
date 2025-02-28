using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using lstwoMODS_Core;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class WeatherResearcherJobManager : BaseJobManager
    {
        private HacksUIHelper.LIBTrio moneyPerBalloon;
        private List<GameObject> objects = new();
        private QuickReflection<WeatherResearchJobMission> reflect;

        public override Type missionType => typeof(WeatherResearchJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Weather Researcher Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            moneyPerBalloon = ui.CreateLIBTrio("Set Money Per Balloon", "moneyPerDeliveredLIB", "10");
            moneyPerBalloon.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyPerBalloon.Button.OnClick = () => SetMoney(int.Parse(moneyPerBalloon.Input.Text));
            objects.Add(moneyPerBalloon.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((WeatherResearchJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyPerBalloon.Input.Text = ((int)reflect.GetField("moneyPerBalloon")).ToString();
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerBalloon", money);
            }
        }
    }
}
