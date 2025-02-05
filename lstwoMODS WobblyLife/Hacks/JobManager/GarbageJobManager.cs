using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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

            var moneyLabel = ui.CreateLabel("Set Money Earnt", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("0", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoneyEarnt(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var moneyPerBagLabel = ui.CreateLabel("Set Money Per Bag", "moneyLabel");
            objects.Add(moneyPerBagLabel.gameObject);

            moneyPerBagInput = ui.CreateInputField("5", "moneyInput");
            objects.Add(moneyPerBagInput.GameObject);

            var moneyPerBagBtn = ui.CreateButton("Apply Money", () => SetMoneyEarnt(int.Parse(moneyPerBagInput.Text)));
            objects.Add(moneyPerBagBtn.GameObject);
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

        public void SetMoneyEarnt(int money)
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
