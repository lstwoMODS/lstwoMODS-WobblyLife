using System;
using System.Reflection;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class DayNightCycleSync : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);
        
        

        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Sync In-Game Time to Real Time";
    public override string Description => "";
    public override HacksTab HacksTab { get; }
    
    public static bool enabled = false;

    private static void UpdateReplacement(ref DayNightCycle __instance)
    {
        var qr = new QuickReflection<DayNightCycle>(__instance, Plugin.Flags);
        
        if (((HawkNetworkManager)qr.GetField("networkManager")).IsConnected() && (__instance.networkObject == null || !__instance.networkObject.IsServer()))
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

        qr.SetField("timeTickTok", timeOfDayDegrees);
        
        if (Time.frameCount % 2 == 0)
        {
            __instance.UpdateTimeOfDay((float)qr.GetField("timeTickTok"));
        }
        
        if ((float)qr.GetField("timeOfDay") >= 360f)
        {
            qr.SetField("timeTickTok", 0f);
        }
        
        if ((float)qr.GetField("timeOfDay") >= 270f)
        {
            if ((bool)qr.GetField("bNextDaySent"))
            {
                return;
            }
            
            qr.SetField("bNextDaySent", true);
            qr.GetMethod("ServerTriggerNextDay");
        }
        else
        {
            qr.SetField("bNextDaySent", false);
        }
    }
    
    public static class Patches
    {
        [HarmonyPatch(typeof(DayNightCycle), "Update")]
        [HarmonyPrefix]
        public static bool DayNightCycle_Update_Prefix(ref DayNightCycle __instance)
        {
            if (!enabled)
            {
                return true;
            }
            
            UpdateReplacement(ref __instance);
            return false;
        }
    }
}