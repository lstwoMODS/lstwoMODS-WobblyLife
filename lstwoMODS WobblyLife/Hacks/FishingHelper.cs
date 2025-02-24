using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using ShadowLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace lstwoMODS_WobblyLife.Hacks;

public class FishingHelper : BaseHack
{
    public static void UnlockFishCatchable(FishCatchableScriptableObject fishCatchable)
    {
        if (!fishCatchable)
        {
            return;
        }
        
        var controller = PlayerUtils.GetMyPlayer();

        if (!controller || !controller.IsLocal())
        {
            return;
        }
            
        var playerControllerUnlocker = controller.GetPlayerControllerUnlocker();
            
        if (playerControllerUnlocker)
        {
            playerControllerUnlocker.PromptFishCaught(fishCatchable);
        }
            
        var firstActiveMissionByType = UnitySingleton<WorldMissionManager>.Instance.GetFirstActiveMissionByType<WorldMissionFishing>();
            
        if (firstActiveMissionByType)
        {
            firstActiveMissionByType.IncrementCaughtCount(fishCatchable.GetAssetid(), controller);
        }
    }
    
    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.FishingHelper").PatchAll(typeof(Patches));
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.FishingHelper.InstantFishBite", "Instant Fish Bite", (b) => bInstantFishBite = b);
        
        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.FishingHelper.ForceFishArea", "Force Fish Area", (b) => bForceFishArea = b);

        ui.AddSpacer(6);

        forcedFishAreaLDB = ui.CreateLDBTrio("Forced Fish Area", "lstwo.FishingHelper.ForcedFishArea");
        forcedFishAreaLDB.Button.OnClick = () =>
        {
            var selectedIndex = forcedFishAreaLDB.Dropdown.value;
            
            if (selectedIndex < fishAreas?.Count)
            {
                forcedFishArea = fishAreas[selectedIndex];
            }
        };
        
        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.FishingHelper.ForceFishRarity", "Force Fish Rarity", (b) => bForceFishRarity = b);

        ui.AddSpacer(6);

        forcedFishRarityLDB = ui.CreateLDBTrio("Forced Fish Rarity", "lstwo.FishingHelper.ForcedFishRarity");
        forcedFishRarityLDB.Button.OnClick = () =>
        {
            var selectedIndex = forcedFishRarityLDB.Dropdown.value;
            
            if (selectedIndex < fishRarities?.Count)
            {
                forcedFishRarity = fishRarities[selectedIndex];
            }
        };
        
        ui.AddSpacer(6);

        unlockFishLDB = ui.CreateLDBTrio("Unlock Fish", "lstwo.FishingHelper.UnlockFish");
        unlockFishLDB.Button.OnClick = () =>
        {
            var selectedIndex = unlockFishLDB.Dropdown.value;

            if (selectedIndex < allFishCatchables?.Count)
            {
                UnlockFishCatchable(allFishCatchables[selectedIndex]);
            }
        };
        
        ui.AddSpacer(6);
        
        ui.CreateLBDuo("Unlock All Fish", "lstwo.FishingHelper.UnlockAllFish", () =>
        {
            foreach (var fishCatchable in allFishCatchables)
            {
                UnlockFishCatchable(fishCatchable);
            }
        }, "Unlock", "lstwo.FishingHelper.UnlockAllFishButton");
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
        fishAreas = FishingSystem.Instance.GetAllFishingArea().ToList();
        fishRarities = (Enum.GetValues(typeof(FishCatchableRarity)) as FishCatchableRarity[])?.ToList();
        allFishCatchables = new();
        
        forcedFishAreaLDB.Dropdown.ClearOptions();
        forcedFishRarityLDB.Dropdown.ClearOptions();
        unlockFishLDB.Dropdown.ClearOptions();

        foreach (var area in fishAreas)
        {
            forcedFishAreaLDB.Dropdown.options.Add(new(area.GetTitleText()));

            foreach (var fish in area.GetAllCatchables())
            {
                if (allFishCatchables.Contains(fish))
                {
                    continue;
                }
                
                allFishCatchables.Add(fish);
                unlockFishLDB.Dropdown.options.Add(new(fish.GetFishTitle()));
            }
        }

        if (fishRarities != null)
        {
            foreach (var fishRarity in fishRarities)
            {
                forcedFishRarityLDB.Dropdown.options.Add(new(fishRarity.ToString()));
            }
        }

        forcedFishAreaLDB.Dropdown.RefreshShownValue();
        forcedFishRarityLDB.Dropdown.RefreshShownValue();
        unlockFishLDB.Dropdown.RefreshShownValue();
    }

    public override string Name => "Fishing Helper";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.SaveHacksTab;

    private static bool bInstantFishBite;
    private static bool bForceFishArea;
    private static FishAreaScriptableObject forcedFishArea;
    private static bool bForceFishRarity;
    private static FishCatchableRarity forcedFishRarity;
    
    private HacksUIHelper.LDBTrio forcedFishAreaLDB;
    private HacksUIHelper.LDBTrio forcedFishRarityLDB;
    private HacksUIHelper.LDBTrio unlockFishLDB;

    private List<FishAreaScriptableObject> fishAreas;
    private List<FishCatchableRarity> fishRarities;
    private List<FishCatchableScriptableObject> allFishCatchables;

    public class Patches
    {
        [HarmonyPatch(typeof(FishingSystem), "UpdateAwaitCatchable")]
        [HarmonyPrefix]
        public static bool UpdateAwaitCatchablePrefix(float currentTime, ref FishingAwaitingCatchable catchable)
        {
            if (catchable.bFoundCatchable)
            {
                if (!(currentTime - catchable.timeSinceEvent >= 3f))
                {
                    return false;
                }
                
                catchable.timeSinceEvent = currentTime;
                catchable.bFoundCatchable = false;
                var onMissed = catchable.onMissed;

                onMissed?.Invoke();
                return false;
            }

            if (!(currentTime - catchable.timeSinceEvent >= catchable.secondsTillNextFind) && !bInstantFishBite)
            {
                return false;
            }
            
            catchable.RefreshSecondsTillNextFind();
            FishCatchableScriptableObject fishCatchableScriptableObject = null;
            
            if (catchable != null && catchable.fishArea)
            {
                fishCatchableScriptableObject = catchable.fishArea.GetRandomFishCatchable();
            }

            if (fishCatchableScriptableObject == null)
            {
                return false;
            }
            
            var onCatchable = catchable.onCatchable;
            
            onCatchable?.Invoke(catchable.fishArea, fishCatchableScriptableObject);
            catchable.bFoundCatchable = true;

            return false;
        }

        [HarmonyPatch(typeof(FishAreaScriptableObject), "GetRandomFishCatchable")]
        [HarmonyPrefix]
        public static bool GetRandomFishCatchablePrefix(ref FishCatchableScriptableObject __result, ref FishAreaScriptableObject __instance)
        {
            var sr = new QuickReflection<FishAreaScriptableObject>(null, BindingFlags.Static | BindingFlags.NonPublic);
            var r = new QuickReflection<FishAreaScriptableObject>(__instance, Plugin.Flags);

            foreach (var fishCatchableRarity in (FishCatchableRarity[]) Enum.GetValues(typeof(FishCatchableRarity)))
            {
                if (((Dictionary<FishCatchableRarity, float>)sr.GetField("RarityPercent")).TryGetValue(fishCatchableRarity, out var chance))
                {
                    if (chance == 0f)
                    {
                        continue;
                    }
                    
                    var value = Random.value;

                    Debug.Log(bForceFishRarity);
                    Debug.Log(forcedFishRarity);
                    Debug.Log(fishCatchableRarity);

                    if (!bForceFishRarity && chance < value || (bForceFishRarity && forcedFishRarity != fishCatchableRarity))
                    {
                        continue;
                    }
                    
                    var randomFishCatchable_Internal = (FishCatchableScriptableObject) r.GetMethod("GetRandomFishCatchable_Internal", fishCatchableRarity);

                    if (!randomFishCatchable_Internal)
                    {
                        continue;
                    }
                    
                    __result = randomFishCatchable_Internal;
                    return false;
                }

                Debug.LogError(fishCatchableRarity + " doesn't have a RarityPercent");
            }
            
            __result = null;
            return false;
        }

        [HarmonyPatch(typeof(FishingSystem), "SampleArea")]
        [HarmonyPrefix]
        public static bool SampleAreaPrefix(ref FishAreaScriptableObject __result)
        {
            if (bForceFishArea)
            {
                __result = forcedFishArea;
                return false;
            }

            return true;
        }
    }
}