using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

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
            
        var timeLIB = ui.CreateLIBTrio("Set Current Job Timer", "timeLIB", "60");
        timeLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        timeLIB.Button.OnClick = () => SetTimerInSeconds(ulong.Parse(timeLIB.Input.Text));
        objects.Add(timeLIB.Root);

        objects.Add(ui.AddSpacer(5));

        var bRunningToggle = ui.CreateToggle(label: "Is Running", defaultState: true, onValueChanged: (b) => { });
        objects.Add(bRunningToggle.gameObject);

        var bResetTimerToggle = ui.CreateToggle(label: "Reset Timer", onValueChanged: (b) => { });
        objects.Add(bResetTimerToggle.gameObject);

        var setRunningBtn = ui.CreateLBDuo("Set Running", "SetRunning", () => SetTimerRunning(bRunningToggle.isOn, bResetTimerToggle.isOn), "Apply", "SetIsRunningButton");
        objects.Add(setRunningBtn.Root);
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