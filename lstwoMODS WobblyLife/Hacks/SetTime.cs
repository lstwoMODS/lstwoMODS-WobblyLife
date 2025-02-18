using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityEngine.Rendering.PostProcessing;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class SetTime : BaseHack
    {
        public override string Name => "Set Time of Day";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ServerHacksTab;

        private InputFieldRef timeSpeedInput;
        private Text dayNumLabel;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var timeCatLabel = ui.CreateLabel("<b>Time of Day Settings (Might NOT fully work in Multiplayer)</b>");
            dayNumLabel = ui.CreateLabel("");

            ui.AddSpacer(6);

            var timeSpeedLib = ui.CreateLIBTrio("Set Time Speed", "lstwo.SetTime.speed", "1", () => SetTimeOfDaySpeed(float.Parse(timeSpeedInput.Text)));
            timeSpeedInput = timeSpeedLib.Input;

            ui.AddSpacer(6);

            var setMorningBtn = ui.CreateButton("Set Time to Morning", SetTimeMorning, "lstwo.SetTime.Morning");
            ui.AddSpacer(6);
            var setMiddayBtn = ui.CreateButton("Set Time to Midday", SetTimeMidday, "lstwo.SetTime.Midday");
            ui.AddSpacer(6);
            var setEveningBtn = ui.CreateButton("Set Time to Evening", SetTimeEvening, "lstwo.SetTime.Evening");
            ui.AddSpacer(6);
            var setMidnightBtn = ui.CreateButton("Set Time to Midnight", SetTimeMidnight, "lstwo.SetTime.Midnight");

            ui.AddSpacer(6);

            ui.CreateButton("Inspect \"Day Night Cycle\" Component", () =>
            {
                if (DayNightCycle.InstanceExists)
                {
                    InspectorManager.Inspect(DayNightCycle.Instance);
                    UIManager.ShowMenu = true;
                }
            }, "lstwo.SetTime.inspect", null, 256 * 3 + 32 * 2, 32);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
            var dnc = DayNightCycle.Instance;

            if (dnc != null && dayNumLabel != null && dayNumLabel.gameObject.activeInHierarchy)
            {
                var time = dnc.GetTimeString();
                var day = dnc.GetDayNum();

                dayNumLabel.text = $"Time: {time}, Day: {day}";
            }
        }

        public void SetTimeOfDaySpeed(float speed)
        {
            DayNightCycle.Instance.SetSpeed(speed);
        }

        public void SetTimeMorning()
        {
            DayNightCycle.Instance.SetMorning();
        }

        public void SetTimeMidday()
        {
            DayNightCycle.Instance.SetMidday();
        }

        public void SetTimeEvening()
        {
            DayNightCycle.Instance.SetEvening();
        }

        public void SetTimeMidnight()
        {
            DayNightCycle.Instance.SetMidnight();
        }
    }
}
