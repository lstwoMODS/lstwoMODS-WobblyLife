using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class TaxiJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<TaxiJobMission> reflect;

        public override Type missionType => typeof(TaxiJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Taxi Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Max Money Per Delivery", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("10", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply", () => SetMoney(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((TaxiJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("maxMoneyPerDelivery")).ToString();
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("maxMoneyPerDelivery", money);
            }
        }
    }
}
