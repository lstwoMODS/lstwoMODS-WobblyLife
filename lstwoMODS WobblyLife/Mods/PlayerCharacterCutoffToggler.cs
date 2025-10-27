using HarmonyLib;
using ImGuiNET;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.Hacks.ModActions;

namespace lstwoMODS_WobblyLife.Hacks;

public class PlayerCharacterCutoffToggler : BaseMod
{
    public override string Name => "Player Character Cutoff Toggler";
    public override string Description => "";
    public override ModsTab ModsTab => Plugin.ClientModsTab;

    private static bool characterCutoff = true;

    public PlayerCharacterCutoffToggler()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.CharacterManager").PatchAll(typeof(HarmonyPatches));
    }

    public override void RenderUI()
    {
        ImGui.Checkbox("Allow Player Character Cutoff", ref characterCutoff);
    }

    public override void RefreshUI()
    {
    }

    public override void Update()
    {
    }

    [ModAction(Name = "Toggle Character Cutoff", Tooltip = "Sets whether the player character should fade out when the camera gets too close.")]
    public static void ToggleCharacterCutoff(bool b)
    {
        characterCutoff = b;
    }

    public static class HarmonyPatches
    {
        [HarmonyPatch(typeof(CameraFocusPlayerCharacter), "UpdateCamera")]
        [HarmonyPostfix]
        [HarmonyPriority(100)]
        static void PostfixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
        {
            __instance.SetUsingCharacterCutoff(characterCutoff);
        }
    }
}
