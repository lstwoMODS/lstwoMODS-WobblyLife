using HarmonyLib;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks;

public class RealisticCarCrashes : BaseHack
{
    public override string Name => "Realistic Car Crashes";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ExtraHacksTab;

    private static bool enabled = false;
    private static float explosionForce = 1500f, explosionRadius = 500f, explosionUpwardsModifier = 5f;

    private InputFieldRef forceInput, upwardsInput;

    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.CatCrash").PatchAll(typeof(Patches));

        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.RealisticCarCrashes.enable", "Enable Mod", (b) => enabled = b);

        ui.AddSpacer(6);

        var forceLIB = ui.CreateLIBTrio("Explosion Force", "lstwo.RealisticCarCrashes.force", "1500.0");
        forceLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        forceLIB.Button.OnClick = () => explosionForce = float.Parse(forceLIB.Input.Text);

        forceInput = forceLIB.Input;

        ui.AddSpacer(6);

        var upwardsModifierLIB = ui.CreateLIBTrio("Upwards Modifier", "lstwo.RealisticCarCrashes.upwardsModifier", "5.0");
        upwardsModifierLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        upwardsModifierLIB.Button.OnClick = () => explosionForce = float.Parse(forceLIB.Input.Text);

        upwardsInput = upwardsModifierLIB.Input;

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        forceInput.Text = explosionForce.ToString();
        upwardsInput.Text = explosionUpwardsModifier.ToString();
    }

    public override void Update()
    {
    }

    public class Patches
    {
        [HarmonyPatch(typeof(PlayerVehicleRoadDestructable), "OnUpdatedDestructionStage")]
        [HarmonyPrefix]
        public static void OnUpdatedDestructionStage(ref PlayerVehicleRoadDestructable __instance, RoadDestructableStage stage)
        {
            if (stage != RoadDestructableStage.Fine && enabled)
            {
                Plugin._StartCoroutine(enumerator(__instance));
            }
        }

        private static IEnumerator enumerator(PlayerVehicleRoadDestructable __instance)
        {
            var r = new QuickReflection<PlayerVehicleRoadDestructable>(__instance, Plugin.Flags);
            var movement = (PlayerVehicleRoadMovement)r.GetField("roadMovement");
            var pos = __instance.transform.position;

            __instance.GetComponent<IOnVehicleDestroy>().OnVehicleDestroyed(movement.GetPlayerVehicleRoad());

            foreach (var destroy in __instance.GetComponentsInChildren<IOnVehicleDestroy>())
            {
                destroy.OnVehicleDestroyed(movement.GetPlayerVehicleRoad());
            }

            yield return null;

            movement.GetPlayerVehicleRoad().IterateControllersInVehicle((pc) =>
            {
                pc.GetPlayerControllerInteractor().ForceRequestExit();
                pc.GetPlayerCharacter().GetRagdollController().Knockout();
                pc.GetPlayerCharacter().GetHipRigidbody().AddExplosionForce(explosionForce, pos, explosionRadius, explosionUpwardsModifier, ForceMode.Impulse);
            });
        }
    }
}