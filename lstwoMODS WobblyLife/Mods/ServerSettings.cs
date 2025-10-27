using HarmonyLib;
using HawkNetworking;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection; 
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks;

public class ServerSettings : BaseHack
{
    public override string Name => "Server Settings";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.ServerHacksTab;

    public override void ConstructUI(GameObject root)
    {
        new Harmony(typeof(CustomGamemodePatch).FullName).PatchAll(typeof(CustomGamemodePatch));
        new Harmony(typeof(MountainBaseTelephoneBoxPatch).FullName).PatchAll(typeof(MountainBaseTelephoneBoxPatch));
        new Harmony(typeof(PlayerVehicleDestructablePatch).FullName).PatchAll(typeof(PlayerVehicleDestructablePatch));
        new Harmony(typeof(PlayerVehicleRoadMovementPatch).FullName).PatchAll(typeof(PlayerVehicleRoadMovementPatch));
        new Harmony(typeof(TelephoneBoxPatch).FullName).PatchAll(typeof(TelephoneBoxPatch));

        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.ServerSettings.IsRespawningAllowed", "Is Respawning Allowed", (b) =>
        {
            respawningAllowed = b;

        }, true);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.ServerSettings.AllowVehicleSpawning", "Allow Vehicle Spawning", (b) =>
        {
            enableVehicles = b;

            try
            {
                WobblyServerUtilCompat.SetSettingsManagerValue("enableVehicles", b);
            }
            catch { }

        }, true);
        ui.CreateToggle("lstwo.ServerSettings.AllowVehicleDamage", "Allow Vehicle Damage", (b) =>
        {
            enableVehicleDamage = b;

            try
            {
                WobblyServerUtilCompat.SetSettingsManagerValue("enableVehicleDamage", b);
            }
            catch { }

        }, true);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.ServerSettings.AllowTankSpawning", "Allow Tank Spawning", (b) =>
        {
            enableVehicleTank = b;

            try
            {
                WobblyServerUtilCompat.SetSettingsManagerValue("enableVehicleTank", b);
            }
            catch { }

        }, true);
        ui.CreateToggle("lstwo.ServerSettings.AllowUFOSpawning", "Allow UFO Spawning", (b) =>
        {
            enableVehicleUFO = b;

            try
            {
                WobblyServerUtilCompat.SetSettingsManagerValue("enableVehicleUFO", b);
            }
            catch { }

        }, true);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.ServerSettings.AllowVehicleBoost", "Allow Vehicle Boost", (b) =>
        {
            enableVehicleBoost = b;

            try
            {
                WobblyServerUtilCompat.SetSettingsManagerValue("enableVehicleBoost", b);
            }
            catch { }

        }, true);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.ServerSettings.PreventPlayerDrowningToggle", "Prevent Player Drowning", (b) =>
        {
            preventPlayerDrowning = b;
        });

        ui.CreateToggle("lstwo.ServerSettings.PreventVehicleDrowningToggle", "Prevent Vehicle Drowning", (b) =>
        {
            preventVehicleDrowning = b;
        });

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
    }

    public override void Update()
    {
    }

    public static bool respawningAllowed = true;

    public static bool enableVehicles = true;
    public static bool enableVehicleDamage = true;

    public static bool enableVehicleTank = true;
    public static bool enableVehicleUFO = true;

    public static bool enableVehicleBoost = true;

    public static bool preventPlayerDrowning = false;
    public static bool preventVehicleDrowning = false;

    public class CustomGamemodePatch
    {
        [HarmonyPatch(typeof(FreemodeGamemode), "IsRespawningAllowed")]
        [HarmonyPrefix]
        public static bool IsRespawningAllowed()
        {
            return respawningAllowed;
        }
    }

    public class MountainBaseTelephoneBoxPatch
    {
        [HarmonyPatch(typeof(MountainBaseTelephoneBoxUFO), "IsAllowedToInteract")]
        [HarmonyPrefix]
        public static bool IsAllowedToInteractPrefix(ref bool __result)
        {
            if (!enableVehicleUFO)
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

    public class PlayerVehicleDestructablePatch
    {
        [HarmonyPatch(typeof(PlayerVehicleDestructable), "ServerDamage")]
        [HarmonyPrefix]
        public static bool ServerDamagePrefix(short damage, bool bLimit = true, bool bForceSend = false)
        {
            return enableVehicleDamage;
        }
    }

    public class PlayerVehicleRoadMovementPatch
    {
        public static Dictionary<PlayerVehicleRoadMovement, bool> boostEnabled = new();

        [HarmonyPatch(typeof(PlayerVehicleRoadMovement), "SimulateVehicleInput")]
        [HarmonyPrefix]
        public static bool SimulateVehicleInput(ref PlayerVehicleRoadMovement __instance, VehicleRoadInput input)
        {
            if (!boostEnabled.ContainsKey(__instance))
            {
                return true;
            }
                
            var field = typeof(PlayerVehicleRoadMovement).GetField("bAllowBoost", Plugin.Flags);
            field.SetValue(__instance, enableVehicleBoost && boostEnabled[__instance]);

            return true;
        }
    }

    public class TelephoneBoxPatch
    {
        [HarmonyPatch(typeof(TelephoneBox), "ServerSpawnVehicle")]
        [HarmonyPrefix()]
        [HarmonyPriority(Priority.VeryHigh)]
        private static bool ServerSpawnVehiclePrefix(ref TelephoneBox __instance, ref HawkNetReader reader, ref HawkRPCInfo info)
        {
            if (!enableVehicles) return false;

            var t = typeof(TelephoneBox);

            var actionInteract = (global::ActionEnterExitInteract)t.GetField("actionInteract", Plugin.Flags)?.GetValue(__instance);
            var avaliableVehiclesData = (VehiclesScriptableObject)t.GetField("avaliableVehiclesData", Plugin.Flags)?.GetValue(__instance);
            var vehicleSpawnTransform = (Transform)t.GetField("vehicleSpawnTransform", Plugin.Flags)?.GetValue(__instance);

            var playerController = UnitySingleton<GameInstance>.Instance.GetPlayerControllerByNetworkID(reader.ReadUInt32());
                
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

            if (!playerController || !avaliableVehiclesData)
            {
                return false;
            }
                
            var assetReference = avaliableVehiclesData.Find(guid);

            if (assetReference == null)
            {
                return false;
            }
                
            var array = Physics.OverlapSphere(vehicleSpawnTransform.position, 5f, LayerMask.GetMask(new string[] { "Vehicle" }));
                
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

                if (x.GetComponent<PlayerVehicleRoadMovement>() != null &&
                    !PlayerVehicleRoadMovementPatch.boostEnabled.ContainsKey(x.GetComponent<PlayerVehicleRoadMovement>()))
                {
                    var field = typeof(PlayerVehicleRoadMovement).GetField("bAllowBoost", Plugin.Flags);
                    PlayerVehicleRoadMovementPatch.boostEnabled.Add(x.GetComponent<PlayerVehicleRoadMovement>(),
                        (bool)field.GetValue(x.GetComponent<PlayerVehicleRoadMovement>()));
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
            }, vehicleSpawnTransform.position, vehicleSpawnTransform.rotation, null, false, false, false, true);

            return false;
        }

        private static bool AllowSpawnVehicle(GameObject obj)
        {
            if (obj.GetComponent<PlayerTank>() != null && !enableVehicleTank) return false;
            if (obj.GetComponent<PlayerUFO>() != null && !enableVehicleUFO) return false;
            return true;
        }

        [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateWater")]
        [HarmonyPrefix]
        private static bool SimulateWaterPrefix(ref PlayerCharacterMovement __instance)
        {
            var r = new QuickReflection<PlayerCharacterMovement>(__instance, Plugin.Flags);

            var water = (Water)r.GetField("water");
            var head = (RagdollPart)r.GetField("head");
            var playerCharacter = (PlayerCharacter)r.GetField("playerCharacter");
            var characterCustomize = (CharacterCustomize)r.GetField("characterCustomize");
            var bDrowning = (bool)r.GetField("bDrowning");
            var timeDrowning = (float)r.GetField("timeDrowning");
            var hipRigidbody = (Rigidbody)r.GetField("hipRigidbody");

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
                
            if (water.IsDeep() && num >= 1f && !preventPlayerDrowning)
            {
                if (!bDrowning)
                {
                    r.SetField("bDrowning", true);
                    r.SetField("timeDrowning", Time.time);
                }
            }
            else
            {
                r.SetField("bDrowning", false);
            }

            if (!bDrowning || !(Time.time - timeDrowning >= 3f) || preventPlayerDrowning)
            {
                return false;
            }
                
            var num2 = (waterAnchorTransform.position.y - hipRigidbody.transform.position.y) / 2f;
            playerCharacter.Kill(num2 + 2f);
            __instance.StartCoroutine((IEnumerator)r.GetMethod("SimulateDrowning", waterAnchorTransform));

            return false;
        }

        [HarmonyPatch(typeof(PlayerVehicleMovement), "ShouldDestroyWhenUnderWaterTooLong")]
        [HarmonyPrefix]
        private static bool ShouldDestroyWhenUnderWaterTooLongPrefix(ref bool __result)
        {
            __result = false;
            return !preventVehicleDrowning;
        }
    }
}