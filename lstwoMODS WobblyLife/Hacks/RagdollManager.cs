using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityEngine.Rendering.PostProcessing;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class RagdollManager : PlayerBasedHack
    {
        public override string Name => "Ragdoll Manager";

        public override string Description => "Change Properties of your Ragdoll";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Lock Ragdoll State", onClick1: LockRagdoll, onClick2: UnlockRagdoll, buttonText1: "Lock", buttonText2: "Unlock");

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Ragdoll Player", onClick1: Ragdoll, onClick2: KnockoutPlayer, buttonText1: "Ragdoll", buttonText2: "Knockout");

            ui.AddSpacer(6);

            ui.CreateButton("Inspect \"Ragdoll Controller\" Component", () =>
            {
                if (Player != null && Player.RagdollController)
                {
                    InspectorManager.Inspect(Player.RagdollController);
                    UIManager.ShowMenu = true;
                }
            }, "inspect", null, 256 * 3 + 32 * 2, 32);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public void LockRagdoll()
        {
            if (Player != null)
                Player.RagdollController.LockRagdollState();
        }

        public void UnlockRagdoll()
        {
            if (Player != null)
                Player.RagdollController.UnlockRagdollState();
        }

        public void KnockoutPlayer()
        {
            if (Player != null)
                Player.RagdollController.Knockout();
        }

        public void Ragdoll()
        {
            if (Player != null)
                Player.RagdollController.Ragdoll();
        }
    }
}
