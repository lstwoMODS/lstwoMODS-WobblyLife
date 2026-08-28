using System.Collections.Generic;
using System.Reflection;
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

    [HarmonyPatch]
    [HarmonyPriority(100)]
    public static class HarmonyPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var baseType = typeof(CameraFocus);

            foreach (var type in AccessTools.GetTypesFromAssembly(baseType.Assembly))
            {
                if (type.IsAbstract || type.IsInterface || !baseType.IsAssignableFrom(type))
                {
                    continue;
                }

                var method = AccessTools.DeclaredMethod(type, nameof(CameraFocus.UpdateCamera), [typeof(GameplayCamera)]);

                if (method != null && !method.IsAbstract)
                {
                    yield return method;
                }
            }
        }
        
        private static void Prefix(CameraFocus __instance, GameplayCamera camera)
        {
            __instance.SetUsingCharacterCutoff(CharacterCutoffEnabled);
        }
    }
}
