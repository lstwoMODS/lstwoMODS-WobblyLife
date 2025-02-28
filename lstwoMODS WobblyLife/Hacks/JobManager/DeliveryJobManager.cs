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

            var moneyLIB = ui.CreateLIBTrio("Set Job Money", "moneyLIB", "30");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoney(int.Parse(moneyLIB.Input.Text));
            objects.Add(moneyLIB.Root);
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
