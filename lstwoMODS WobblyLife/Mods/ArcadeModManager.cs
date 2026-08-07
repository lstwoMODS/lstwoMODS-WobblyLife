using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using ModWobblyLife;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class ArcadeModManager : BaseMod
{
    public override string Name => "Arcade Mod Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModAction]
    public void OpenPlayAgainOrReturnToLobbyPopup()
    {
        ModInstance.Instance?.ServerPlayAgainOrReturnToLobby();
    }
    
    [ModAction]
    public void RestartMod()
    {
        ModInstance.Instance?.ServerRestartLevel();
    }
    
    [ModAction(ShowInUI = false)]
    public void ReturnToLobby()
    {
        ModInstance.Instance?.ServerReturnToLobby();
    }
}