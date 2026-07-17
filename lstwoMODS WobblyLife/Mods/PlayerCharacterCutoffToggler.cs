using HarmonyLib;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class PlayerCharacterCutoffToggler : BaseMod
{
    public override string Name => "Player Character Cutoff Toggler";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.CharacterManager").PatchAll(typeof(HarmonyPatches));
    }

    [ModSetting(Label = "Character Fade Out Enabled", Description = "Sets whether the player character should fade out when the camera gets too close.")]
    public static bool CharacterCutoffEnabled = false;

    public static class HarmonyPatches
    {
        [HarmonyPatch(typeof(CameraFocusPlayerCharacter), "UpdateCamera")]
        [HarmonyPostfix]
        [HarmonyPriority(100)]
        private static void PostfixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
        {
            __instance.SetUsingCharacterCutoff(CharacterCutoffEnabled);
        }
    }
}
