using HarmonyLib;
using NotAzzamods.UI.TabMenus;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public class PlayerCharacterCutoffToggler : BaseHack
    {
        public override string Name => "Player Character Cutoff Toggler";
        public override string Description => "";
        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        private static bool characterCutoff = true;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("lstwo.NotAzzamods.CharacterManager").PatchAll(typeof(HarmonyPatches));
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("cutoff", "Allow Player Character Cutoff", (b) => characterCutoff = b, true);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public static class HarmonyPatches
        {
            [HarmonyPatch(typeof(CameraFocusPlayerCharacter), "UpdateCamera")]
            [HarmonyPostfix]
            [HarmonyPriority(1)]
            static void PostfixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
            {
                __instance.SetUsingCharacterCutoff(characterCutoff);
            }
        }
    }
}
