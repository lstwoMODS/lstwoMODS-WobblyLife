using HarmonyLib;
using HawkNetworking;
using System.Collections.Generic;
using UnityEngine;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class ServerSettings : BaseMod
{
    public override string Name => "Server Settings";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    protected override void OnStaticInit()
    {
        new Harmony(typeof(CustomGamemodePatch).FullName).PatchAll(typeof(CustomGamemodePatch));
        new Harmony(typeof(MountainBaseTelephoneBoxPatch).FullName).PatchAll(typeof(MountainBaseTelephoneBoxPatch));
        new Harmony(typeof(PlayerVehicleDestructablePatch).FullName).PatchAll(typeof(PlayerVehicleDestructablePatch));
        new Harmony(typeof(PlayerVehicleRoadMovementPatch).FullName).PatchAll(typeof(PlayerVehicleRoadMovementPatch));
        new Harmony(typeof(TelephoneBoxPatch).FullName).PatchAll(typeof(TelephoneBoxPatch));
    }

    [ModSetting] public static bool RespawningAllowed = true;

    [ModSetting] public static bool EnableVehicles = true;
    [ModSetting] public static bool EnableVehicleDamage = true;

    [ModSetting] public static bool EnableVehicleTank = true;
    [ModSetting] public static bool EnableVehicleUfo = true;

    [ModSetting] public static bool EnableVehicleBoost = true;

    [ModSetting] public static bool PreventPlayerDrowning = false;
    [ModSetting] public static bool PreventVehicleDrowning = false;

    public class CustomGamemodePatch
    {
        [HarmonyPatch(typeof(FreemodeGamemode), "IsRespawningAllowed")]
        [HarmonyPrefix]
        public static bool IsRespawningAllowed()
        {
            return RespawningAllowed;
        }
    }

    public class MountainBaseTelephoneBoxPatch
    {
        [HarmonyPatch(typeof(MountainBaseTelephoneBoxUFO), "IsAllowedToInteract")]
        [HarmonyPrefix]
        public static bool IsAllowedToInteractPrefix(ref bool __result)
        {
            if (EnableVehicleUfo)
            {
                return true;
            }
            
            __result = false;
            return false;
        }
    }

    public class PlayerVehicleDestructablePatch
    {
        [HarmonyPatch(typeof(PlayerVehicleDestructable), "ServerDamage")]
        [HarmonyPrefix]
        public static bool ServerDamagePrefix(short damage, bool bLimit = true, bool bForceSend = false)
        {
            return EnableVehicleDamage;
        }
    }

    public class PlayerVehicleRoadMovementPatch
    {
        public static Dictionary<PlayerVehicleRoadMovement, bool> BoostEnabled = new();

        [HarmonyPatch(typeof(PlayerVehicleRoadMovement), "SimulateVehicleInput")]
        [HarmonyPrefix]
        public static bool SimulateVehicleInput(ref PlayerVehicleRoadMovement __instance, VehicleRoadInput input)
        {
            if (!BoostEnabled.TryGetValue(__instance, out var boostEnabled))
            {
                return true;
            }
            
            __instance.bAllowBoost = EnableVehicleBoost && boostEnabled;
            return true;
        }
    }

    public class TelephoneBoxPatch
    {
        [HarmonyPatch(typeof(TelephoneBox), "ServerSpawnVehicle")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)]
        private static bool ServerSpawnVehiclePrefix(ref TelephoneBox __instance, ref HawkNetReader reader, ref HawkRPCInfo info)
        {
            if (!EnableVehicles) return false;

            var actionInteract = __instance.actionInteract;
            var availableVehiclesData = __instance.avaliableVehiclesData;
            var vehicleSpawnTransform = __instance.vehicleSpawnTransform;

            var playerController = GameInstance.Instance.GetPlayerControllerByNetworkID(reader.ReadUInt32());
                
            if (!playerController || !actionInteract)
            {
                return false;
            }
                
            var employment = playerController.GetPlayerControllerEmployment();

            if (playerController != actionInteract.GetDriverController() || !employment)
            {
                return false;
            }
                
            actionInteract.RequestExit(playerController);
            var guid = reader.ReadGUID();

            if (!playerController || !availableVehiclesData)
            {
                return false;
            }
                
            var assetReference = availableVehiclesData.Find(guid);

            if (assetReference == null)
            {
                return false;
            }
                
            var array = Physics.OverlapSphere(vehicleSpawnTransform.position, 5f, LayerMask.GetMask("Vehicle"));
                
            foreach (var overlap in array)
            {
                var componentElseParent = overlap.GetComponentElseParent<PlayerVehicle>();
                    
                if (!componentElseParent || componentElseParent.networkObject == null)
                {
                    continue;
                }
                    
                componentElseParent.EvacuateAll();
                VanishComponent.VanishAndDestroy(componentElseParent.gameObject);
            }
                
            NetworkPrefab.SpawnNetworkPrefab(assetReference, delegate (HawkNetworkBehaviour x)
            {
                if (!AllowSpawnVehicle(x.gameObject))
                {
                    VanishComponent.VanishAndDestroy(x.gameObject);
                }

                var vehicleRoadMovement = x.GetComponent<PlayerVehicleRoadMovement>();

                if (vehicleRoadMovement != null && !PlayerVehicleRoadMovementPatch.BoostEnabled.ContainsKey(vehicleRoadMovement))
                {
                    PlayerVehicleRoadMovementPatch.BoostEnabled.Add(vehicleRoadMovement, vehicleRoadMovement.bAllowBoost);
                }

                var playerVehicle = x as PlayerVehicle;

                if (!playerVehicle)
                {
                    return;
                }
                    
                var actionEnterExitInteract = playerVehicle.GetActionEnterExitInteract();
                    
                if (actionEnterExitInteract != null)
                {
                    actionEnterExitInteract.RequestEnter(playerController);
                }
                    
                employment.SetPersonalVehicle(playerVehicle);
            }, vehicleSpawnTransform.position, vehicleSpawnTransform.rotation, null, false);

            return false;
        }

        private static bool AllowSpawnVehicle(GameObject obj)
        {
            if (obj.GetComponent<PlayerTank>() != null && !EnableVehicleTank) return false;
            if (obj.GetComponent<PlayerUFO>() != null && !EnableVehicleUfo) return false;
            return true;
        }

        [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateWater")]
        [HarmonyPrefix]
        private static bool SimulateWaterPrefix(ref PlayerCharacterMovement __instance)
        {
            var water = __instance.water;
            var head = __instance.head;
            var playerCharacter = __instance.playerCharacter;
            var characterCustomize = __instance.characterCustomize;
            var bDrowning = __instance.bDrowning;
            var timeDrowning = __instance.timeDrowning;
            var hipRigidbody = __instance.hipRigidbody;

            if (!__instance.IsInWater() || !head || !playerCharacter || !characterCustomize)
            {
                return false;
            }
                
            var waterDataScriptableObject = water.GetWaterDataScriptableObject();
                
            if (waterDataScriptableObject.IsOverrideCharacterColor())
            {
                characterCustomize.SetCharacterColor(waterDataScriptableObject.GetOverrideCharacterColor(), true, 5f);
            }
                
            var waterAnchorTransform = water.GetWaterAnchorTransform();
                
            if (playerCharacter.IsDead())
            {
                return false;
            }
                
            var num = waterAnchorTransform.position.y - head.transform.position.y;
                
            if (water.IsDeep() && num >= 1f && !PreventPlayerDrowning)
            {
                if (!bDrowning)
                {
                    __instance.bDrowning = true;
                    __instance.timeDrowning = Time.time;
                }
            }
            else
            {
                __instance.bDrowning = false;
            }

            if (!bDrowning || !(Time.time - timeDrowning >= 3f) || PreventPlayerDrowning)
            {
                return false;
            }
                
            var num2 = (waterAnchorTransform.position.y - hipRigidbody.transform.position.y) / 2f;
            playerCharacter.Kill(num2 + 2f);
            __instance.StartCoroutine(__instance.SimulateDrowning(waterAnchorTransform));

            return false;
        }

        [HarmonyPatch(typeof(PlayerVehicleMovement), "ShouldDestroyWhenUnderWaterTooLong")]
        [HarmonyPrefix]
        private static bool ShouldDestroyWhenUnderWaterTooLongPrefix(ref bool __result)
        {
            __result = false;
            return !PreventVehicleDrowning;
        }
    }
}