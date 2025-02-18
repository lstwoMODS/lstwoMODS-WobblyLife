using HarmonyLib;
using HawkNetworking;
using lstwoMODS_WobblyLife.UI.TabMenus;
using ShadowLib;
using System;
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

namespace lstwoMODS_WobblyLife.Hacks
{
    public class ServerSettings : BaseHack
    {
        public override string Name => "Server Settings";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ServerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var h = new QuickHarmony("lstwo.NotAzza.ServerSettings");

            h.Init(typeof(CustomGamemodePatch));
            h.Init(typeof(MountainBaseTelephoneBoxPatch));
            h.Init(typeof(PlayerVehicleDestructablePatch));
            h.Init(typeof(PlayerVehicleRoadMovementPatch));
            h.Init(typeof(TelephoneBoxPatch));

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

            ui.CreateToggle("lstwo.ServerSettings.PreventPlayerDrowningToggle", "Prevent Player Drowing", (b) =>
            {
                activePlayer = b;
            });

            ui.CreateToggle("lstwo.ServerSettings.PreventVehicleDrowningToggle", "Prevent Vehicle Drowing", (b) =>
            {
                activeVehicle = b;
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
        public static bool enableCarDrowning = true;

        public static bool activePlayer = false;
        public static bool activeVehicle = false;

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
                if (boostEnabled.ContainsKey(__instance))
                {
                    var field = typeof(PlayerVehicleRoadMovement).GetField("bAllowBoost", Plugin.Flags);
                    if (enableVehicleBoost)
                        field.SetValue(__instance, boostEnabled[__instance]);
                    else
                        field.SetValue(__instance, false);
                }

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

                var actionInteract = (global::ActionEnterExitInteract)t.GetField("actionInteract", Plugin.Flags).GetValue(__instance);
                var avaliableVehiclesData = (VehiclesScriptableObject)t.GetField("avaliableVehiclesData", Plugin.Flags).GetValue(__instance);
                var vehicleSpawnTransform = (Transform)t.GetField("vehicleSpawnTransform", Plugin.Flags).GetValue(__instance);

                PlayerController playerController = UnitySingleton<GameInstance>.Instance.GetPlayerControllerByNetworkID(reader.ReadUInt32());
                if (playerController && actionInteract)
                {
                    PlayerControllerEmployment employment = playerController.GetPlayerControllerEmployment();
                    if (playerController == actionInteract.GetDriverController() && employment)
                    {
                        actionInteract.RequestExit(playerController);
                        Guid guid = reader.ReadGUID();
                        if (playerController && avaliableVehiclesData)
                        {
                            AssetReference assetReference = avaliableVehiclesData.Find(guid);
                            if (assetReference != null)
                            {
                                Collider[] array = Physics.OverlapSphere(vehicleSpawnTransform.position, 5f, LayerMask.GetMask(new string[] { "Vehicle" }));
                                for (int i = 0; i < array.Length; i++)
                                {
                                    PlayerVehicle componentElseParent = array[i].GetComponentElseParent<PlayerVehicle>();
                                    if (componentElseParent && componentElseParent.networkObject != null)
                                    {
                                        componentElseParent.EvacuateAll();
                                        VanishComponent.VanishAndDestroy(componentElseParent.gameObject);
                                    }
                                }
                                NetworkPrefab.SpawnNetworkPrefab(assetReference, delegate (HawkNetworkBehaviour x)
                                {
                                    if (!AllowSpawnVehicle(x.gameObject)) VanishComponent.VanishAndDestroy(x.gameObject);

                                    if (x.GetComponent<PlayerVehicleRoadMovement>() != null &&
                                        !PlayerVehicleRoadMovementPatch.boostEnabled.ContainsKey(x.GetComponent<PlayerVehicleRoadMovement>()))
                                    {
                                        FieldInfo field = typeof(PlayerVehicleRoadMovement).GetField("bAllowBoost", Plugin.Flags);
                                        PlayerVehicleRoadMovementPatch.boostEnabled.Add(x.GetComponent<PlayerVehicleRoadMovement>(),
                                            (bool)field.GetValue(x.GetComponent<PlayerVehicleRoadMovement>()));
                                    }

                                    PlayerVehicle playerVehicle = x as PlayerVehicle;
                                    if (playerVehicle)
                                    {
                                        global::ActionEnterExitInteract actionEnterExitInteract = playerVehicle.GetActionEnterExitInteract();
                                        if (actionEnterExitInteract != null)
                                        {
                                            actionEnterExitInteract.RequestEnter(playerController);
                                        }
                                        employment.SetPersonalVehicle(playerVehicle);
                                    }
                                }, new Vector3?(vehicleSpawnTransform.position), new Quaternion?(vehicleSpawnTransform.rotation), null, false, false, false, true);
                            }
                        }
                    }
                }

                return false;
            }

            private static bool AllowSpawnVehicle(GameObject obj)
            {
                if (obj.GetComponent<PlayerTank>() != null && !enableVehicleTank) return false;
                if (obj.GetComponent<PlayerUFO>() != null && !enableVehicleUFO) return false;
                return true;
            }

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
