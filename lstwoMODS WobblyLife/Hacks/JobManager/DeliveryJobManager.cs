using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class DeliveryJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private List<GameObject> objects = new();
        private QuickReflection<DeliveryJobMission> reflect;

        public override Type missionType => typeof(DeliveryJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Delivery Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Job Money", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("30", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoney(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b;

            try
            {
                var delivery = (DeliveryJobMission)Mission;
                b = delivery != null;
            }

            catch
            {
                b = false;
            }

            root.SetActive(b);

            if (b)
            {
                reflect = new((DeliveryJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = ((int)reflect.GetField("moneyBagReward")).ToString();
            }
        }

        public void SetMoney(int money)
        {
            try
            {
                reflect.SetField("moneyBagReward", money);
            }
            catch { }
        }
    }
}
