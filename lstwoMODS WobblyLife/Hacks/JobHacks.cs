using System;
using System.Collections.Generic;
using lstwoMODS_WobblyLife.Hacks.JobManager;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks;

public class JobHacks : PlayerBasedHack
{
    public override string Name => "Job Mods";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.PlayerHacksTab;

    public JobHacks()
    {
        new ArtStudioJobManager();
    }

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        foreach (var manager in jobManagers)
        {
            try
            {
                var newRoot = ui.CreateVerticalGroup(manager.GetType().Name, false, true, false, true, bgColor: new(0, 0, 0, 0));
                var newUI = new HacksUIHelper(newRoot);
                manager.ui = newUI;
                manager.root = newRoot;
                manager.ConstructUI();
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError("Error while Constructing Job Manager " + manager.GetType().Name + ": " + e.Message + e.StackTrace);
            }
        }

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        if (Player == null) return;

        currentJobMission = Player.ControllerEmployment.GetActiveJob() ? Player.ControllerEmployment.GetActiveJob() : null;

        foreach (var manager in jobManagers)
        {
            try
            {
                manager.SetPlayerController(Player.Controller);
                manager.RefreshUI();
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError("Error while Refreshing Job Manager: " + e.Message + e.StackTrace);
            }
        }
    }

    public override void Update()
    {
    }

    public static List<BaseJobManager> jobManagers = new();

    public Dictionary<Type, GameObject[]> jobControls = new();
    public JobMission currentJobMission;
}