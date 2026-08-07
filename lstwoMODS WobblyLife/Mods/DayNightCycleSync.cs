using System;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class DayNightCycleSync : BaseMod
{
    public override string Name => "Sync In-Game Time to Real Time";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;
    
    [ModSetting]
    public static bool Enabled = false;

    protected override void OnStaticInit()
    {
        new Harmony(GetType().ToString()).PatchAll(typeof(Patches));
    }

    private static void UpdateReplacement(ref DayNightCycle __instance)
    {
        if (__instance.networkManager.IsConnected() && (__instance.networkObject == null || !__instance.networkObject.IsServer()))
        {
            return;
        }
        
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        var hour = now.Hour + now.Minute / 60f + now.Second / 3600f;
        
        var timeOfDayDegrees = hour / 24f * 360f - 90f;
        
        if (timeOfDayDegrees < 0f)
        {
            timeOfDayDegrees += 360f;
        }

        __instance.timeTickTok = timeOfDayDegrees;
        
        if (Time.frameCount % 2 == 0)
        {
            __instance.UpdateTimeOfDay(__instance.timeTickTok);
        }
        
        if (__instance.timeOfDay >= 360f)
        {
            __instance.timeTickTok = 0f;
        }
        
        if (__instance.timeOfDay >= 270f)
        {
            if (__instance.bNextDaySent)
            {
                return;
            }
            
            __instance.bNextDaySent = true;
            __instance.ServerTriggerNextDay();
        }
        else
        {
            __instance.bNextDaySent = false;
        }
    }
    
    public static class Patches
    {
        [HarmonyPatch(typeof(DayNightCycle), "Update")]
        [HarmonyPrefix]
        public static bool DayNightCycle_Update_Prefix(ref DayNightCycle __instance)
        {
            if (!Enabled)
            {
                return true;
            }
            
            UpdateReplacement(ref __instance);
            return false;
        }
    }
}