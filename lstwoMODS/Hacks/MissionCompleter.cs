using IngameDebugConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using NotAzzamods.UI.TabMenus;

namespace NotAzzamods.Hacks
{
    internal class MissionComplete : BaseHack
    {
        public override string Name => "Mission Completer";

        public override string Description => "Completes all Active Missions";

        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        public void CompleteMissions()
        {
            WorldMission[] list = new WorldMission[WorldMissionManager.Instance.GetActiveMissions().Count];
            WorldMissionManager.Instance.GetActiveMissions().CopyTo(list);
            foreach (WorldMission mission in list)
            {
                mission.CompleteMission();
            }
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Complete active Missions", "CompleteMissions", CompleteMissions, "Complete All");

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
