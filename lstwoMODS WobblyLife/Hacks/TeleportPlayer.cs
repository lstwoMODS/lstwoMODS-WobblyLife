using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class TeleportPlayer : PlayerBasedHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        var exitToggle = ui.CreateToggle("lstwo.TeleportAllPlayers.ForceExit", "Force Exit (Vehicles, Telephone Boxes, etc.)");

        ui.CreateLDBTrio("Destination (Player to teleport to)", "net.lstwo.teleportallplayers.destinationldb", "");

        ui.AddSpacer(6);
    }
    
    public void Teleport(bool forceExit, PlayerController destination)
    {
        if (Player == null || !GameInstance.InstanceExists) return;

        var pos = destination.GetPlayerCharacter().GetPlayerPosition();

        if (forceExit)
        {
            Player.ControllerInteractor.ForceRequestExit();
        }

        Player.Character.SetPlayerPosition(pos);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Teleport Player";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.PlayerHacksTab;
}