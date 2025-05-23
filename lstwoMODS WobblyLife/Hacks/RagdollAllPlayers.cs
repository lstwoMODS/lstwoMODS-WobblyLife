﻿using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class RagdollAllPlayers : BaseHack
    {
        public override string Name => "Ragdoll All Players";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ServerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Ragdoll All Players", "lstwo.RagdollAllPlayers.RagdollAll", onClick1: Ragdoll, onClick2: KnockoutPlayer, buttonText1: "Ragdoll", buttonText2: "Knockout", 
                buttonName1: "lstwo.RagdollAllPlayers.RagdollAllButton", buttonName2: "lstwo.RagdollAllPlayers.KnockOutAllButton");

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Kill All Players", "lstwo.RagdollAllPlayers.KillPlayer", () => KillPlayer(1), "Quick Kill", "lstwo.RagdollAllPlayers.QuickKillButton", () => KillPlayer(0), 
                "Respawn", "lstwo.RagdollAllPlayers.RespawnButton");

            ui.AddSpacer(6);

            var akpLib = ui.CreateLIBTrio("Advanced Kill All Players", "lstwo.RagdollAllPlayers.AdvancedKillPlayer", "Knockout Time in Seconds", null, "Kill");
            akpLib.Button.OnClick = () => KillPlayer(float.Parse(akpLib.Input.Text));
            akpLib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);
        }

        public void KillPlayer(float time = 1)
        {
            if (!GameInstance.InstanceExists) return;

            foreach (var controller in GameInstance.Instance.GetPlayerControllers())
            {
                var Player = new PlayerRef();

                Player.SetPlayerController(controller);

                if (Player != null)
                {
                    Player.Character.Kill(time);
                }
            }
        }

        public void KnockoutPlayer()
        {
            if (!GameInstance.InstanceExists) return;

            foreach (var controller in GameInstance.Instance.GetPlayerControllers())
            {
                var Player = new PlayerRef();

                Player.SetPlayerController(controller);

                if (Player != null)
                    Player.RagdollController.Knockout();
            }
        }

        public void Ragdoll()
        {
            if (!GameInstance.InstanceExists) return;

            foreach (var controller in GameInstance.Instance.GetPlayerControllers())
            {
                var Player = new PlayerRef();

                Player.SetPlayerController(controller);

                if (Player != null)
                    Player.RagdollController.Ragdoll();
            }
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
