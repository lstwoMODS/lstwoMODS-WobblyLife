using System.Collections.Generic;
using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using lstwoMODS_WobblyLife.Hacks.ESP;

namespace lstwoMODS_WobblyLife.Hacks;

public class VehicleESPMod : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.VehicleESP").PatchAll(typeof(Patches));
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.VehicleESP.Enabled", "Enable Vehicle ESP", b => ESPManager.vehicleTracker.draw = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.VehicleESP.DrawLines", "Draw Lines", b => ESPManager.vehicleTracker.drawLines = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.VehicleESP.DrawBoxes", "Draw Boxes", b => ESPManager.vehicleTracker.drawBoxes = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.VehicleESP.DrawText", "Draw Text", b => ESPManager.vehicleTracker.drawText = b);
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
        if (ESPManager.Instance == null)
        {
            new GameObject("ESP Behavior").AddComponent<ESPManager>();
        }
        else
        {
            ESPManager.Refresh();
        }
    }

    public override string Name => "Vehicle ESP";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ClientHacksTab;

    public class Patches
    {
        [HarmonyPatch(typeof(PlayerVehicle), "OnEnable")]
        [HarmonyPrefix]
        private static bool PlayerVehicleOnEnablePatch(ref PlayerVehicle __instance)
        {
            ESPManager.vehicleTracker.AddTrackedObject(__instance.gameObject);
            return true;
        }
        
        [HarmonyPatch(typeof(PlayerVehicle), "OnDestroy")]
        [HarmonyPrefix]
        private static bool PlayerVehicleOnDestroyPatch(ref PlayerVehicle __instance)
        {
            ESPManager.vehicleTracker.RemoveTrackedObject(__instance.gameObject);
            return true;
        }
    }
}