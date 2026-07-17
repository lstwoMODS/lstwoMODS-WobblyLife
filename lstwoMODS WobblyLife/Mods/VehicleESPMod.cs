using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using lstwoMODS_WobblyLife.Mods.ESP;

namespace lstwoMODS_WobblyLife.Mods;

public class VehicleESPMod : BaseMod
{
    public override string Name => "Vehicle ESP";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;
    
    [ModSetting(Order = 10)]
    public bool EnableVehicleESP
    {
        get => ESPManager.vehicleTracker.draw;
        set => ESPManager.vehicleTracker.draw = value;
    }

    [ModSetting(Order = 20)]
    public bool DrawLines
    {
        get => ESPManager.vehicleTracker.drawLines;
        set => ESPManager.vehicleTracker.drawLines = value;
    }

    [ModSetting(Order = 30)]
    public bool DrawBoxes
    {
        get => ESPManager.vehicleTracker.drawBoxes;
        set => ESPManager.vehicleTracker.drawBoxes = value;
    }

    [ModSetting(Order = 40)]
    public bool DrawText
    {
        get => ESPManager.vehicleTracker.drawText;
        set => ESPManager.vehicleTracker.drawText = value;
    }

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.VehicleESP").PatchAll(typeof(Patches));
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