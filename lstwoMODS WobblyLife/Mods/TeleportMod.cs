using System.Linq;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class TeleportMod : PlayerBasedMod
{
    public override string Name => "Teleport Players";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    private Ref<int> selectedSourcePlayerIndex = new();
    private Ref<bool> forceExit = new();
    private Ref<string> teleportOnePlayerTooltipText = new();

    private PlayerRef sourcePlayer => PlayerBasedModsWindow.players[selectedSourcePlayerIndex.Value];

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new Checkbox("Force Exit (Vehicles, Telephone Boxes, etc.)").WithValue(forceExit),
            
            new SeparatorText("Teleport All Players", "Teleport All Players"),
            ActionMenu(new Button("Teleport Everyone to Player", () => TeleportEveryoneToPlayer(forceExit.Value)).WithContentWidth(), nameof(TeleportEveryoneToPlayer)),
            
            new SeparatorText("Teleport One Player", "Teleport One Player"),
            new Combo("Player to Teleport", [], onChanged: UpdateTooltip).WithItems(PlayerBasedModsWindow.playerNames).WithSelectedIndex(selectedSourcePlayerIndex).WithTooltip(teleportOnePlayerTooltipText),
            new Button("Teleport to Player", () => TeleportPlayerToSelected(sourcePlayer, forceExit.Value)).WithContentWidth().WithTooltip(teleportOnePlayerTooltipText)
        );
    }

    public override void RefreshUI()
    {
        selectedSourcePlayerIndex.Value = 0;
        UpdateTooltip(0);
    }

    private void UpdateTooltip(int i)
    {
        if(Player == null || PlayerBasedModsWindow.players.Length <= i) return;
        teleportOnePlayerTooltipText.Value = $"{PlayerBasedModsWindow.players[i]?.Controller?.GetPlayerName()} -> {Player?.Controller?.GetPlayerName()}";
    }

    [ModAction(ShowInUI = false)]
    public void TeleportEveryoneToPlayer([ModActionParam(Label = "Force Exit (Vehicles, Telephone Boxes, etc.)")] bool forceExit)
    {
        if (Player?.Controller == null || !GameInstance.InstanceExists) return;

        foreach (var player in GameInstance.Instance.GetPlayerControllers().Where(player => player != Player.Controller))
        {
            TeleportPlayerToPlayer(player, Player, forceExit);
        }
    }

    public void TeleportPlayerToSelected(PlayerRef playerToTeleport, [ModActionParam(Label = "Force Exit (Vehicles, Telephone Boxes, etc.)")] bool forceExit)
    {
        if (Player?.Controller == null || !GameInstance.InstanceExists) return;
        TeleportPlayerToPlayer(playerToTeleport, Player, forceExit);
    }

    [ModAction(ShowInUI = false)]
    public static void TeleportPlayerToPlayer(PlayerRef sourcePlayer, PlayerRef destinationPlayer, [ModActionParam(Label = "Force Exit (Vehicles, Telephone Boxes, etc.)")] bool forceExit)
    {
        TeleportPlayerToPos(sourcePlayer, destinationPlayer.Character.GetPlayerPosition(), forceExit);
    }

    [ModAction(ShowInUI = false)]
    public static void TeleportPlayerToPos(PlayerRef player, Vector3 targetPosition, [ModActionParam(Label = "Force Exit (Vehicles, Telephone Boxes, etc.)")] bool forceExit)
    {
        if (forceExit)
        {
            player.Controller.GetPlayerControllerInteractor().ForceRequestExit();
        }

        player.Controller.GetPlayerCharacter().SetPlayerPosition(targetPosition);
    }
}