using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public class TeleportAllPlayers : BaseHack
    {
        public override string Name => "Teleport All Players";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var exitToggle = ui.CreateToggle("ForceExit", "Force Exit (Vehicles, Telephone Boxes, etc.)");

            ui.CreateLBDuo("Teleport All Players to Selected Player", "Teleport", () => TeleportPlayers(exitToggle.isOn), "Teleport");

            ui.AddSpacer(6);
        }

        public void TeleportPlayers(bool forceExit)
        {
            if (Player == null || !GameInstance.InstanceExists) return;

            var pos = Player.Character.GetPlayerPosition();

            foreach (var player in GameInstance.Instance.GetPlayerControllers())
            {
                if (player == Player.Controller) continue;

                if (forceExit)
                {
                    player.GetPlayerControllerInteractor().ForceRequestExit();
                }

                player.GetPlayerCharacter().SetPlayerPosition(pos);
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
