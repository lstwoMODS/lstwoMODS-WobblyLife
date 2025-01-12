using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Panels;

namespace NotAzzamods.Hacks.JobManager
{
    public abstract class BaseJobManager
    {
        public HacksUIHelper ui;
        public GameObject root;

        public PlayerController Controller { get; private set; }
        public JobMission Mission { get; private set; }

        public abstract Type missionType { get; }

        public BaseJobManager()
        {
            JobHacks.jobManagers.Add(this);
        }

        public virtual void ConstructUI()
        {
        }

        public abstract void RefreshUI();

        public virtual void SetPlayerController(PlayerController controller)
        {
            Controller = controller;
            Mission = controller.GetPlayerControllerEmployment().GetActiveJob();
        }

        public bool CheckMission()
        {
            if (Mission == null) return false;

            object result = null;

            try
            {
                result = Convert.ChangeType(Mission, missionType);
            }
            catch
            {
            }

            return result != null;
        }
    }
}
