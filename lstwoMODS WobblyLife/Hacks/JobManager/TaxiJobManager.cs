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
            
            var moneyPerDeliveredLIB = ui.CreateLIBTrio("Set Max Money Per Delivery", "moneyPerDeliveredLIB", "10");
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
