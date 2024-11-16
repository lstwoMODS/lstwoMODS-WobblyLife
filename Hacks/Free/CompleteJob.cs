using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks.Free
{
    internal class CompleteJob : BaseHack
    {
        public override string Name => "Job Completer";

        public override string Description => "Complete any Job instantly!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Complete Job", "CompleteJob", execute, "Complete");

            ui.AddSpacer(6);

            ui.CreateLBDuo("Fail Job", "FailJob", Fail, "Fail");
        }

        public void execute()
        {
            if (Player != null && Player.Controller.GetPlayerControllerEmployment().GetActiveJob() != null)
                Player.Controller.GetPlayerControllerEmployment().GetActiveJob().ServerJobCompleted();
        }

        public void Fail()
        {
            Player.Employment.GetActiveJob().ServerJobFailed("Host requested job failure using NotAzzamods");
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
