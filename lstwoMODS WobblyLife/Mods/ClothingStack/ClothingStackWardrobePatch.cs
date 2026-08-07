using HarmonyLib;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

/// <summary>
/// Reroutes home-wardrobe clicks into the clothing stack while the mod is enabled. The bottom of each
/// slot's stack is kept as a real clothing piece in the actual vanilla slot (so it saves to the Wobbly
/// Life save file and syncs to everyone): the first pick for an empty slot falls through to vanilla,
/// and only further picks in that slot become stacked layers. Clicking the empty "None" option clears
/// every stacked layer of that slot and falls through to vanilla so the base slot empties too.
/// The active wardrobe is tracked so the stack can be mirrored onto its separate preview character.
/// </summary>
[HarmonyPatch(typeof(Wardrobe))]
internal static class ClothingStackWardrobePatch
{
    [HarmonyPatch(nameof(Wardrobe.TryOn))]
    [HarmonyPrefix]
    private static bool TryOnPrefix(Wardrobe __instance, ClothingAssetReference clothingPiece)
    {
        if (!ClothingStackMod.StackingEnabled || clothingPiece == null) return true;

        // The "None" option is one of the game's invisible slot pieces (e.g. _Hat_None), which carries
        // a real GUID and its slot type. Clearing must go by that, not by an empty GUID.
        if (ClothingStackMod.WardrobeIsClearPiece(clothingPiece.clothingGuid))
        {
            ClothingStackMod.WardrobeClearSlot(clothingPiece.selectionType);
            return true; // let vanilla put the invisible None piece on the preview's base slot
        }

        // The lowest piece in a slot must be a real, saved clothing piece. If this slot has no real
        // piece yet, let vanilla put this one in the actual clothing slot (it saves and syncs to every
        // player, modded or not). Only once a base piece exists do further picks stack on top.
        if (ClothingStackMod.WardrobeSlotBaseEmpty(__instance, clothingPiece.selectionType))
            return true;

        // Capture the same per-piece colour vanilla TryOn would apply (the player's saved wardrobe
        // colour for this piece) so the stacked layer shows the right colour instead of whatever the
        // pooled piece happened to carry from a previous use.
        ClothingStackMod.WardrobeAddLayer(clothingPiece.clothingGuid, WardrobeColorFor(__instance, clothingPiece));
        return false; // skip vanilla: don't switch the base slot, just layer on top
    }

    private static Color? WardrobeColorFor(Wardrobe wardrobe, ClothingAssetReference clothingPiece)
    {
        var unlocker = wardrobe.GetDriverController()?.GetPlayerControllerUnlocker();
        var data = unlocker?.GetClothingData(clothingPiece);
        return data.HasValue ? data.Value.clothingPrimaryColor : (Color?)null;
    }

    [HarmonyPatch(nameof(Wardrobe.StartCustomize))]
    [HarmonyPostfix]
    private static void StartCustomizePostfix(Wardrobe __instance)
        => ClothingStackMod.SetActiveWardrobe(__instance);

    [HarmonyPatch(nameof(Wardrobe.StopCustomize))]
    [HarmonyPostfix]
    private static void StopCustomizePostfix(Wardrobe __instance)
        => ClothingStackMod.ClearActiveWardrobe(__instance);
}
