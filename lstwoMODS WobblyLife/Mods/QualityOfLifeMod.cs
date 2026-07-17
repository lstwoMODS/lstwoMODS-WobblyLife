using System;
using System.Collections;
using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class QualityOfLifeMod : BaseMod
{
    public override string Name => "Quality of Life";
    public override string Description => "Fixes and improvements to the game";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    [ModSetting] public static Ref<bool> UnlockCursor = new();
    [ModSetting] public static Ref<bool> DisablePets = new();
    [ModSetting(Label = "Disable Loading Screen (Faster loading times)")] public static Ref<bool> DisableLoadingScreen = new();
    [ModSetting(Label = "Disable Splash Screen (Faster game start times)")] public static Ref<bool> DisableSplashScreen = new();

    public static void ApplyPatches(Harmony harmony)
    {
        harmony.PatchAll(typeof(Patch_LoadingMusic_Play));
        harmony.PatchAll(typeof(Patch_LoadingMusic_Stop));
        harmony.PatchAll(typeof(Patch_UILoadingGame_Show));
        harmony.PatchAll(typeof(SplashScreenPatch));
    }

    public static void EarlyLoadData()
    {
        var qolKey = typeof(QualityOfLifeMod).FullName;
        UnlockCursor.Value = DataStorage.Load<bool>(qolKey, "UnlockCursor");
        DisablePets.Value = DataStorage.Load<bool>(qolKey, "DisablePets");
        DisableLoadingScreen.Value = DataStorage.Load<bool>(qolKey, "DisableLoadingScreen");
        DisableSplashScreen.Value = DataStorage.Load<bool>(qolKey, "DisableSplashScreen");
    }

    protected override void OnStaticInit()
    {
        StartCoroutine(UnlockCursorCoroutine());
        GameInstance.onAssignedPlayerController += OnAssignedPlayerController;

        BindData(UnlockCursor, "UnlockCursor");
        BindData(DisablePets, "DisablePets");
        BindData(DisableLoadingScreen, "DisableLoadingScreen");
        BindData(DisableSplashScreen, "DisableSplashScreen");
    }
    
    private static IEnumerator UnlockCursorCoroutine()
    {
        while(true)
        {
            yield return new WaitForEndOfFrame();
            UpdateCursorControl();
        }
    }
    
    internal static void UpdateCursorControl()
    {
        if (!UnlockCursor.Value) return;
        
        if(Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    private void OnAssignedPlayerController(PlayerController controller)
    {
        StartCoroutine(DisablePet(controller));
    }

    private static IEnumerator DisablePet(PlayerController controller)
    {
        yield return new WaitUntil(() => controller?.GetPlayerControllerPet()?.GetActivePet() && DisablePets.Value);
        var pet = controller.GetPlayerControllerPet().GetActivePet().gameObject;
        pet.SetActive(false);
        
        var lastDisablePets = DisablePets.Value;

        while (!controller.IsDestroyed())
        {
            if (lastDisablePets != DisablePets.Value)
            {
                pet.SetActive(!DisablePets.Value);
                lastDisablePets = DisablePets.Value;
            }

            yield return null;
        }
    }
    
    [HarmonyPatch(typeof(UILoadingGame), "Show")]
    class Patch_UILoadingGame_Show
    {
        static void Postfix()
        {
            if (!DisableLoadingScreen.Value) return;

            var target = GameObject.Find("Loading-Canvas")
                ?? GameObject.Find("Loading-Canvas(Clone)");

            if (target == null)
            {
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (canvas.gameObject.name.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        target = canvas.gameObject;
                        break;
                    }
                }
            }

            if (target != null)
                target.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(LoadingMusicManager), "PlayLoadingMusic")]
    class Patch_LoadingMusic_Play
    {
        static bool Prefix()
        {
            return !DisableLoadingScreen.Value;
        }
    }
    
    [HarmonyPatch(typeof(LoadingMusicManager), "StopLoadingMusic")]
    class Patch_LoadingMusic_Stop
    {
        static bool Prefix()
        {
            return !DisableLoadingScreen.Value;
        }
    }
    
    [HarmonyPatch(typeof(LoadingScene), "SplashScreen")]
    public class SplashScreenPatch
    {
        static bool Prefix(LoadingScene __instance, ref IEnumerator __result)
        {
            if (!DisableSplashScreen.Value) return true;

            __instance.bSplashScreenDone = true;
            __result = Done();
            return false;
        }

        static IEnumerator Done() { yield break; }
    }
}