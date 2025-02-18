using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    internal class CompleteJob : PlayerBasedHack
    {
        public override string Name => "Job Completer";

        public override string Description => "Complete any Job instantly!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Complete Job", "lstwo.CompleteJob.CompleteJob", execute, "Complete");

            ui.AddSpacer(6);

            ui.CreateLBDuo("Fail Job", "lstwo.CompleteJob.FailJob", Fail, "Fail");
        }

        public void execute()
        {
            if (Player != null && Player.Controller.GetPlayerControllerEmployment().GetActiveJob() != null)
                Player.Controller.GetPlayerControllerEmployment().GetActiveJob().ServerJobCompleted();
        }

        public void Fail()
        {
            Player.ControllerEmployment.GetActiveJob().ServerJobFailed("Host requested job failure using lstwoMODS_WobblyLife");
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
