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
    public class NewsRoundManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<NewsRoundJobMission> reflect;

        public override Type missionType => typeof(NewsRoundJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("News Round Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            var moneyLIB = ui.CreateLIBTrio("Set Current Money Per Player", "moneyLIB", "30");
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
                reflect = new((NewsRoundJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("currentMoneyPerPlayer")).ToString();
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("currentMoneyPerPlayer", money);
            }
        }
    }
}
