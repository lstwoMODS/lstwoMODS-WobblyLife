using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_WobblyLife.Mods;

namespace lstwoMODS_WobblyLife.Mods;

public class JetpackMultiplier : PlayerBasedMod
{
    private class PlayerSettings
    {
        public bool FuelEnabled = true;
        public float FuelTime = 3.5f;
        public float Speed = 6f;
    }
    
    public override string Name => "Jetpack Multiplier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    private PlayerSettings Current => GetPlayerSettings<PlayerSettings>(Player?.Controller);

    [ModSetting(Order = 10)]
    public bool EnableFuel
    {
        get => Current?.FuelEnabled ?? false;
        set => Current?.FuelEnabled = value;
    }

    [ModSetting(Order = 20)]
    public float FuelTime
    {
        get => Current?.FuelTime ?? 0f;
        set => Current?.FuelTime = value;
    }

    [ModSetting(Order = 30)]
    public float Speed
    {
        get => Current?.Speed ?? 0f;
        set => Current?.Speed = value;
    }

    protected override void OnStaticInit()
    {
        new Harmony(Id).PatchAll(typeof(Patches));
    }

    public class Patches
    {
        [HarmonyPatch(typeof(ClothingJetpack), "OnPostMovement")]
        [HarmonyPrefix]
        public static bool OnPostMovementPatch(ref ClothingJetpack __instance, ref PlayerCharacterMovement movement, Rigidbody rigidbody)
        {
            if (!__instance.IsAllowedCustom() || !__instance.GetCustomBit(0))
            {
                return false;
            }
            
            var settings = GetPlayerSettings<PlayerSettings>(typeof(JetpackMultiplier), __instance.playerController);
            movement.GetPlayerBody()?.SetRagdollVelocityLerpY(settings.Speed, Time.fixedDeltaTime * 5f);

            return false;
        }

        [HarmonyPatch(typeof(ClothingJetpack), "Update")]
        [HarmonyPrefix]
        public static bool UpdatePatch(ref ClothingJetpack __instance)
        {
            var settings = GetPlayerSettings<PlayerSettings>(typeof(JetpackMultiplier), __instance.playerController) ?? new();
            var num = 0f;
            
            if (__instance.IsAllowedCustom())
            {
                if (__instance.GetCustomBit(0) && settings.FuelEnabled)
                {
                    __instance.fuelTime -= Time.deltaTime;
                    __instance.bRefueling = false;
                }
                else
                {
                    __instance.fuelTime += Time.deltaTime * 1f;
                    __instance.bRefueling = true;
                }
                
                __instance.fuelTime = Mathf.Clamp(__instance.fuelTime, 0f, settings.FuelTime);
                num = __instance.fuelTime / settings.FuelTime;
            }
            
            if (__instance.fuelIndicatorMaterial)
            {
                __instance.fuelIndicatorMaterial.SetFloat(ClothingJetpack.Shader_Progress_ID, num);
            }

            return false;
        }
    }
}