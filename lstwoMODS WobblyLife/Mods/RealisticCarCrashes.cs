using HarmonyLib;
using System.Collections;
using UnityEngine;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class RealisticCarCrashes : BaseMod
{
    public override string Name => "Realistic Car Crashes";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    [ModSetting]
    public static bool Enabled;
    
    [ModSetting]
    private static float ExplosionForce = 1500f, ExplosionRadius = 500f, ExplosionUpwardsModifier = 5f;

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.CatCrash").PatchAll(typeof(Patches));
    }

    public class Patches
    {
        [HarmonyPatch(typeof(PlayerVehicleRoadDestructable), "OnUpdatedDestructionStage")]
        [HarmonyPrefix]
        public static void OnUpdatedDestructionStage(ref PlayerVehicleRoadDestructable __instance, RoadDestructableStage stage)
        {
            if (stage != RoadDestructableStage.Fine && Enabled)
            {
                Plugin._StartCoroutine(Coroutine(__instance));
            }
        }

        private static IEnumerator Coroutine(PlayerVehicleRoadDestructable __instance)
        {
            var movement = __instance.roadMovement;
            var pos = __instance.transform.position;

            __instance.GetComponent<IOnVehicleDestroy>().OnVehicleDestroyed(movement.GetPlayerVehicleRoad());

            foreach (var destroy in __instance.GetComponentsInChildren<IOnVehicleDestroy>())
            {
                destroy.OnVehicleDestroyed(movement.GetPlayerVehicleRoad());
            }

            yield return null;

            movement.GetPlayerVehicleRoad().IterateControllersInVehicle(pc =>
            {
                pc.GetPlayerControllerInteractor().ForceRequestExit();
                pc.GetPlayerCharacter().GetRagdollController().Knockout();
                pc.GetPlayerCharacter().GetHipRigidbody().AddExplosionForce(ExplosionForce, pos, ExplosionRadius, ExplosionUpwardsModifier, ForceMode.Impulse);
            });
        }
    }
}