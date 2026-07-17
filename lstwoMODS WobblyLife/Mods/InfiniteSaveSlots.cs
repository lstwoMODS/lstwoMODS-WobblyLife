using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using Unity.Mathematics;

namespace lstwoMODS_WobblyLife.Mods;

public class InfiniteSaveSlots : BaseMod
{
    public override string Name => "Infinite Save Worlds";
    public override string Description => "Increase the amount of save slots (max 500).";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;
    
    [ModSetting(Widget = WidgetType.Input, Max = 500, Min = 1)]
    public static Ref<int> maxFileSlots = new(5);

    protected override void OnStaticInit()
    {
        var h = new Harmony(GetType().FullName);
        h.PatchAll(typeof(Patches));
        h.PatchAll(typeof(Patch_LoadNext_MaxSlots));

        maxFileSlots.Changed += value =>
        {
            if (value is < 1 or > 500) maxFileSlots.Value = math.clamp(value, 1, 500);
        };
        
        BindData(maxFileSlots, "MaxFileSlots", 5);
    }
    
    public static class Patches
    {
        [HarmonyPatch(typeof(SaveGameManager), "SaveSlot")]
        [HarmonyPrefix]
        public static bool SaveSlot(SaveGameManager __instance, SaveSlotInfoData infoData)
        {
            if (infoData != null)
            {
                var saveSlot = infoData.GetSaveSlot();
                
                if (saveSlot <= 0 || saveSlot > maxFileSlots.Value)
                {
                    Plugin.LogSource.LogError("Save slot not set ot out of range: " + saveSlot.ToString());
                    return false;
                }
                
                if (infoData.lastSelectedPlayerSlot == -1)
                {
                    Plugin.LogSource.LogWarning("Last Selected Player Slot not set - Not Saving!");
                    return false;
                }
                
                infoData.Save_Internal();
                __instance.Save(infoData, "SlotInfo", "/GameSaves/SaveSlot_" + infoData.GetSaveSlot().ToString() + "/", false);
            }

            return false;
        }

        [HarmonyPatch(typeof(SaveGameManager), "OnActiveSlotChanged")]
        [HarmonyPrefix]
        public static bool OnActiveSlotChanged(SaveGameManager __instance, SaveSlotInfoData activeSlotInfoData)
        {
            if (__instance.GetSaveInfoData() != null)
            {
                var saveSlot = activeSlotInfoData.GetSaveSlot();
                
                if (saveSlot <= 0 || saveSlot > maxFileSlots.Value)
                {
                    Plugin.LogSource.LogError("Something has gone wrong, active slot is out of range");
                    return false;
                }
                
                __instance.GetSaveInfoData().lastLoadedSlot = saveSlot;
            }

            return false;
        }
        
        [HarmonyPatch(typeof(SaveGameManager), "LoadSlot_Internal")]
        [HarmonyPrefix]
        public static bool LoadSlot_Internal(SaveGameManager __instance, Action<SaveSlotInfoData> onSlotLoaded, int saveSlot, bool bCreateIfNotAvaliable)
        {
            if (__instance.GetSaveInfoData() == null)
            {
                Plugin.LogSource.LogError("SaveInfo must be loaded");
                return false;
            }
            
            if (saveSlot > 0 && saveSlot <= maxFileSlots.Value)
            {
                __instance.Load(delegate(SaveSlotInfoData x)
                {
                    if (x != null)
                    {
                        if (x.lastSelectedPlayerSlot == -1)
                        {
                            x.lastSelectedPlayerSlot = 0;
                        }
                        x.SetSaveSlot(saveSlot);
                    }
                    
                    var onSlotLoaded3 = onSlotLoaded;
                    
                    if (onSlotLoaded3 == null)
                    {
                        return;
                    }
                    
                    onSlotLoaded3(x);
                }, "SlotInfo", "/GameSaves/SaveSlot_" + saveSlot + "/", false, bCreateIfNotAvaliable);
                
                return false;
            }
            
            Plugin.LogSource.LogError("Save slot is out of range");
            var onSlotLoaded2 = onSlotLoaded;
            
            if (onSlotLoaded2 == null)
            {
                return false;
            }
            
            onSlotLoaded2(null);

            return false;
        }
    }
    
    public static class SlotConfigHelper
    {
        public static int GetMaxSlots()
        {
            return maxFileSlots.Value;
        }
    }
    
    [HarmonyPatch(typeof(UISaveSlotSelector), "LoadNext")]
    class Patch_LoadNext_MaxSlots
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.Method(typeof(SlotConfigHelper), nameof(SlotConfigHelper.GetMaxSlots));

            foreach (var code in instructions)
            {
                if (code.opcode == OpCodes.Ldc_I4_5)
                {
                    yield return new CodeInstruction(OpCodes.Call, getter);
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}