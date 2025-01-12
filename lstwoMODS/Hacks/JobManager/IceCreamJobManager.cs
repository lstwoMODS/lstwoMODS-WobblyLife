using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.JobManager
{
    public class IceCreamJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<DeliveryJobMission> reflect;

        public override Type missionType => typeof(DeliveryJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Ice Cream Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Money per Order", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("5", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoneyPerOrder(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((DeliveryJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("moneyPerOrder")).ToString();
            }
        }

        public void SetMoneyPerOrder(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerOrder", money);
            }
        }
    }
}
