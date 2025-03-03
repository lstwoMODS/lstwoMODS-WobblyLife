using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class JobStarter : PlayerBasedHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);

        startJobLDB = ui.CreateLDBTrio("Start Job", "lstwo.JobStarter.StartJob", "", 16, null, null, "Start");
        startJobLDB.Button.OnClick = () =>
        {
            var selectedIndex = startJobLDB.Dropdown.value;

            if (selectedIndex < jobDispensors.Count)
            {
                jobDispensors[selectedIndex]?.StartJob(Player.Controller, _ => {});
            }
        };
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
        jobDispensors = (List<JobDispensorBehaviour>) typeof(JobDispensorManager).GetField("jobDispensors", Plugin.Flags)?.GetValue(JobDispensorManager.Instance);
        startJobLDB.Dropdown.ClearOptions();

        if (jobDispensors == null)
        {
            startJobLDB.Dropdown.RefreshShownValue();
            return;
        }
        
        foreach (var dispenser in jobDispensors)
        {
            startJobLDB.Dropdown.options.Add(new(dispenser.gameObject.name));
        }
        
        startJobLDB.Dropdown.RefreshShownValue();
    }

    public override string Name => "Job Starter";
    public override string Description => "";
    public override HacksTab HacksTab => null;

    private HacksUIHelper.LDBTrio startJobLDB;
    private List<JobDispensorBehaviour> jobDispensors = new();
}