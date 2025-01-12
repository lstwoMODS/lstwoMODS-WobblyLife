using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.JobManager
{
    public class JobTimerManager : BaseJobManager
    {
        public override Type missionType => null;
        private QuickReflection<JobMissionTimer> reflect;
        private List<GameObject> objects = new();
        private InputFieldRef moneyInput;

        private JobMissionTimer timer;

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Job Timer", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Current Job Timer", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("60 (seconds)", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply", () => SetTimerInSeconds(ulong.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var bRunningToggle = ui.CreateToggle(label: "Is Running", defaultState: true, onValueChanged: (b) => { });
            objects.Add(bRunningToggle.gameObject);

            var bResetTimerToggle = ui.CreateToggle(label: "Reset Timer", onValueChanged: (b) => { });
            objects.Add(bResetTimerToggle.gameObject);

            var setRunningBtn = ui.CreateButton("Set Running", () => SetTimerRunning(bRunningToggle.isOn, bResetTimerToggle.isOn));
            objects.Add(setRunningBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = Mission && Mission.TryGetComponent(out timer);

            if (b)
            {
                reflect = new(timer, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            root.SetActive(b);
        }

        public void SetTimerInSeconds(ulong seconds)
        {
            if (timer != null)
            {
                reflect.SetField("jobTimerInSeconds", seconds);
            }
        }

        public void SetTimerRunning(bool bRunning, bool bResetTimer)
        {
            if (timer != null)
            {
                timer.ServerSetRunning(bRunning, bResetTimer);
            }
        }
    }
}
