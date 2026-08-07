using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using Random = UnityEngine.Random;

namespace lstwoMODS_WobblyLife.Mods;

public class FishingHelper : BaseMod
{
    public override string Name => "Fishing Helper";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    [ModSetting(ShowInUI = false)] public static readonly Ref<bool> EnableInstantFishBite = new();
    [ModSetting(ShowInUI = false)] public static readonly Ref<bool> ForceFishArea = new();
    [ModSetting(ShowInUI = false)] public static FishAreaScriptableObject ForcedFishArea;
    [ModSetting(ShowInUI = false)] public static readonly Ref<bool> ForceFishRarity = new();
    [ModSetting(ShowInUI = false)] public static FishCatchableRarity ForcedFishRarity;

    private static readonly Ref<int> ForcedFishAreaIndex = new();
    private static readonly Ref<int> ForcedFishRarityIndex = new();
    private static readonly Ref<int> UnlockFishIndex = new();

    public static List<FishAreaScriptableObject> FishAreas;
    public static List<FishCatchableRarity> FishRarities;
    public static List<FishCatchableScriptableObject> AllFishCatchables;

    private static readonly Ref<string[]> FishAreaDropdownItems = new();
    private static readonly Ref<string[]> FishRarityDropdownItems = new();
    private static readonly Ref<string[]> AllFishCatchableDropdownItems = new();
    
    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.FishingHelper").PatchAll(typeof(Patches));
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new SeparatorText("Fishing Help", "Fishing Help"),
        
            SettingMenu(new Checkbox("Instant Fish Bite").WithValue(EnableInstantFishBite), nameof(EnableInstantFishBite)),

            SettingMenu(new Checkbox("Force Fish Area").WithValue(ForceFishArea), nameof(ForceFishArea)),
            new Combo("Fish Area", [], 0, index => ForcedFishArea = FishAreas[index]).WithItems(FishAreaDropdownItems).WithSelectedIndex(ForcedFishAreaIndex),

            SettingMenu(new Checkbox("Force Fish Rarity").WithValue(ForceFishRarity), nameof(ForceFishRarity)),
            new Combo("Fish Rarity", [], 0, index => ForcedFishRarity = FishRarities[index]).WithItems(FishRarityDropdownItems).WithSelectedIndex(ForcedFishRarityIndex),

            new SeparatorText("Unlock Fish", "Unlock Fish"),

            SaveGuard.Notice("fishing-guard-notice"),

            new Combo("Fish to Unlock", []).WithItems(AllFishCatchableDropdownItems).WithSelectedIndex(UnlockFishIndex),
            SaveGuard.Guard(ActionMenu(new Button("Unlock", () => UnlockFishCatchable(AllFishCatchables[UnlockFishIndex.Value])).WithContentWidth(), nameof(UnlockFishCatchable))),

            SaveGuard.Guard(new Button("Unlock All Fish", () => AllFishCatchables.ForEach(UnlockFishCatchable)).WithContentWidth())
        );
    }

    public override void RefreshUI()
    {
        if (!FishingSystem.InstanceExists) return;
        
        FishAreas = FishingSystem.Instance.GetAllFishingArea().ToList();
        FishRarities = (Enum.GetValues(typeof(FishCatchableRarity)) as FishCatchableRarity[])?.ToList();
        AllFishCatchables = new();
        
        foreach (var fish in from area in FishAreas from fish in area.GetAllCatchables() where !AllFishCatchables.Contains(fish) select fish)
        {
            AllFishCatchables.Add(fish);
        }

        FishAreaDropdownItems.Value = FishAreas.Select(x => x.GetTitleText()).ToArray();
        FishRarityDropdownItems.Value = FishRarities.Select(x => x.ToString()).ToArray();
        AllFishCatchableDropdownItems.Value = AllFishCatchables.Select(x => x.GetFishTitle()).ToArray();
    }
    
    [ModAction(ShowInUI = false)]
    public static void UnlockFishCatchable(FishCatchableScriptableObject fishCatchable)
    {
        if (SaveGuard.On) return;

        if (!fishCatchable)
        {
            return;
        }
        
        var controller = GameInstance.Instance.GetFirstLocalPlayerController();

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

            if (!(currentTime - catchable.timeSinceEvent >= catchable.secondsTillNextFind) && !EnableInstantFishBite.Value)
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

                    if (!ForceFishRarity.Value && chance < value || (ForceFishRarity.Value && ForcedFishRarity != fishCatchableRarity))
                    {
                        continue;
                    }
                    
                    var randomFishCatchableInternal = (FishCatchableScriptableObject) r.GetMethod("GetRandomFishCatchable_Internal", fishCatchableRarity);

                    if (!randomFishCatchableInternal)
                    {
                        continue;
                    }
                    
                    __result = randomFishCatchableInternal;
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
            if (ForceFishArea.Value)
            {
                __result = ForcedFishArea;
                return false;
            }

            return true;
        }
    }
}