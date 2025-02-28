using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class GarbageJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private InputFieldRef moneyPerBagInput;
        private List<GameObject> objects = new();
        private QuickReflection<GarbageJobMission> reflect;

        public override Type missionType => typeof(GarbageJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Garbage Job", "title", fontSize: 18);
            objects.Add(title.gameObject);
            
            var moneyLIB = ui.CreateLIBTrio("Set Money Earned", "moneyLIB", "0");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoneyEarned(int.Parse(moneyLIB.Input.Text));
            objects.Add(moneyLIB.Root);

            objects.Add(ui.AddSpacer(5));
            
            var moneyPerBagLIB = ui.CreateLIBTrio("Set Money Per Bag", "moneyPerBagLIB", "5");
            moneyPerBagLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyPerBagLIB.Button.OnClick = () => SetMoneyPerBag(int.Parse(moneyPerBagLIB.Input.Text));
            objects.Add(moneyPerBagLIB.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((GarbageJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("moneyEarnt")).ToString();
                moneyPerBagInput.Text = ((int)reflect.GetField("moneyPerBagDisposed")).ToString();
            }
        }

        public void SetMoneyEarned(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyBagReward", money);
            }
        }

        public void SetMoneyPerBag(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerBagDisposed", money);
            }
        }
    }
}
