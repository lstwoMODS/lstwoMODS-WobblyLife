using IngameDebugConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    internal class MissionManager : BaseHack
    {
        public override string Name => "Mission Manager";
        public override string Description => "Completes all Active Missions";
        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        private HacksUIHelper.LDBTrio completeMissionLDB;
        private HacksUIHelper.LDBTrio uncompleteMissionLDB;

        private List<WorldMission> missions;

        public void CompleteActiveMissions()
        {
            var missions = WorldMissionManager.Instance.GetActiveMissions();

            foreach (var mission in missions)
            {
                mission.CompleteMission();
            }
        }

        public void CompleteAllMissions()
        {
            var missions = WorldMissionManager.Instance.GetAllMissions();

            foreach (var mission in missions)
            {
                mission.CompleteMission();
            }
        }

        public void UncompleteAllMissions()
        {
            var missions = WorldMissionManager.Instance.GetAllMissions();

            foreach (var mission in missions)
            {
                SaveGameManager.Instance.GetSaveMissionData().Uncomplete(mission.GetGuid());
            }
        }

        public void CompleteMission(WorldMission mission)
        {
            mission.CompleteMission();
        }

        public void UncompleteMission(WorldMission mission)
        {
            SaveGameManager.Instance.GetSaveMissionData().Uncomplete(mission.GetGuid());
        }

        // NOTE: Uncompleting makes them just get re-completed after reloading
        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Complete Missions", "lstwo.MissionManager.CompleteMissions", CompleteActiveMissions, "Complete Active", "lstwo.MissionManager.CompleteActive",
                CompleteAllMissions, "Complete All", "lstwo.MissionManager.CompleteAll");

            //ui.AddSpacer(6);

            //ui.CreateLBDuo("Uncomplete Missions", "lstwo.MissionManager.UncompleteMissions", UncompleteAllMissions, "Uncomplete All Missions");

            ui.AddSpacer(6);

            completeMissionLDB = ui.CreateLDBTrio("Complete Mission", "lstwo.MissionManager.CompleteMission", "Select Mission", buttonText: "Complete");
            completeMissionLDB.Button.OnClick = () =>
            {
                if(missions.Count > 0 && completeMissionLDB.Dropdown.value < missions.Count)
                {
                    CompleteMission(missions[completeMissionLDB.Dropdown.value]);
                }
            };

            ui.AddSpacer(6);

            //uncompleteMissionLDB = ui.CreateLDBTrio("Uncomplete Mission", "lstwo.MissionManager.UncompleteMission", "Select Mission", buttonText: "Uncomplete");
            //uncompleteMissionLDB.Button.OnClick = () =>
            //{
            //    if (missions.Count > 0 && uncompleteMissionLDB.Dropdown.value < missions.Count)
            //    {
            //        CompleteMission(missions[uncompleteMissionLDB.Dropdown.value]);
            //    }
            //};

            //ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            missions = WorldMissionManager.Instance.GetAllMissions().ToList();

            completeMissionLDB.Dropdown.ClearOptions();
            //uncompleteMissionLDB.Dropdown.ClearOptions();

            foreach (var mission in missions)
            {
                completeMissionLDB.Dropdown.options.Add(new(mission.GetWorldMissionInfo().GetMissionTitle()));
                //uncompleteMissionLDB.Dropdown.options.Add(new(mission.GetWorldMissionInfo().GetMissionTitle()));
            }

            completeMissionLDB.Dropdown.RefreshShownValue();
            //uncompleteMissionLDB.Dropdown.RefreshShownValue();
        }

        public override void Update()
        {
        }
    }
}
