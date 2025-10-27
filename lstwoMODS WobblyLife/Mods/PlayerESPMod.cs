using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Hacks.ESP;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class PlayerESPMod : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.PlayerESPMod").PatchAll(typeof(Patches));
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.PlayerESP.Enabled", "Enable Player ESP", b => ESPManager.playerTracker.draw = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.PlayerESP.DrawLines", "Draw Lines", b => ESPManager.playerTracker.drawLines = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.PlayerESP.DrawBoxes", "Draw Boxes", b => ESPManager.playerTracker.drawBoxes = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.PlayerESP.DrawText", "Draw Text", b => ESPManager.playerTracker.drawText = b);
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Player ESP";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ClientHacksTab;
    
    public class Patches
    {
        [HarmonyPatch(typeof(PlayerBody), "Awake")]
        [HarmonyPostfix]
        private static void PlayerBodyAwakePatch(ref PlayerBody __instance)
        {
            ESPManager.playerTracker.AddTrackedObject(__instance);
        }
    }
}