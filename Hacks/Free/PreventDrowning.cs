using HarmonyLib;
using NotAzzamods.UI.TabMenus;
using UnityEngine;

namespace NotAzzamods.Hacks.Free
{
    internal class PreventDrowning : BaseHack
    {
        public static bool activePlayer = false;
        public static bool activeVehicle = false;

        public override string Name => "Prevent Drowning";

        public override string Description => "Allow yourself to Walk Underwater!";
        public override HacksTab HacksTab => Plugin.ServerHacksTab;

        public void TogglePlayerDrownPrevention(bool b)
        {
            activePlayer = b;
        }

        public void ToggleVehicleDrownPrevention(bool b)
        {
            activeVehicle = b;
        }

        public override void ConstructUI(GameObject root)
        {
            new Harmony("NotAzza.PreventDrowning").PatchAll(typeof(PreventDrowningPatches));
            
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("PreventPlayerDrowningToggle", "Prevent Player Drowing", TogglePlayerDrownPrevention);
            ui.CreateToggle("PreventVehicleDrowningToggle", "Prevent Vehicle Drowing", ToggleVehicleDrownPrevention);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public static class PreventDrowningPatches
        {
            [HarmonyPatch(typeof(PlayerCharacterMovement), "IsInWater")]
            [HarmonyPrefix]
            private static bool SetIsInWaterPrefix(ref bool __result)
            {
                if (activePlayer)
                {
                    __result = false;
                    return false;
                }
                else
                {
                    return true;
                }
            }

            [HarmonyPatch(typeof(PlayerVehicleMovement), "IsInWater")]
            [HarmonyPrefix]
            private static bool SetIsInWaterVehiclePrefix(ref bool __result)
            {
                if (activeVehicle)
                {
                    __result = false;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
