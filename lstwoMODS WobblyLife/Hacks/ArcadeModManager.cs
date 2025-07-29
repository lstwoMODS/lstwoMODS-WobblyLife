using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using ModWobblyLife;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class ArcadeModManager : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);
        
        ui.CreateLBBTrio("Restart Or Return To Lobby", "restartOrReturnToLobbyLBB", 
            () => ModInstance.Instance?.ServerRestartLevel(), "Restart Mod", "restartModBtn",
            () => ModInstance.Instance?.ServerReturnToLobby(), "Return To Lobby", "returnToLobbyBtn");
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Arcade Mod Manager";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ServerHacksTab;
}