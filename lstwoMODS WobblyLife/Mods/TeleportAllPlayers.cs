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

namespace lstwoMODS_WobblyLife.Hacks;

public class TeleportAllPlayers : PlayerBasedMod
{
    public override string Name => "Teleport All Players";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.PlayerHacksTab;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        var exitToggle = ui.CreateToggle("lstwo.TeleportAllPlayers.ForceExit", "Force Exit (Vehicles, Telephone Boxes, etc.)");

        ui.CreateLBDuo("Teleport All Players to Selected Player", "lstwo.TeleportAllPlayers.Teleport", () => TeleportPlayers(exitToggle.isOn), "Teleport", 
            "lstwo.TeleportAllPlayers.TeleportButton");

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