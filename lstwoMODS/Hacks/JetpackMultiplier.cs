using HarmonyLib;
using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NotAzzamods.Hacks
{
    public class JetpackMultiplier : BaseHack
    {
        public override string Name => "Jetpack Multiplier";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        private static bool fuelEnabled = true;
        private static bool allPlayers = true;
        private static float fuelTime = 3.5f;
        private static float speed = 6f;

        private HacksUIHelper.LIBTrio fuelTimeLIB;
        private HacksUIHelper.LIBTrio speedLIB;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("lstwo.NotAzzamods.JetpackMultiplier").PatchAll(typeof(Patches));

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("forAllPlayer", "Enable for All Players", (b) => allPlayers = b, true);

            ui.AddSpacer(6);

            fuelTimeLIB = ui.CreateLIBTrio("Jetpack Fuel Time", "fuelTime", "Fuel Time in Seconds");
            fuelTimeLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            fuelTimeLIB.Button.OnClick = () =>
            {
                fuelTime = float.Parse(fuelTimeLIB.Input.Text);
            };

            ui.AddSpacer(6);

            ui.CreateToggle("enableJetpackFuel", "Enable Jetpack Fuel (Toggle Off for Infinite Time)", (b) => fuelEnabled = b, true);

            ui.AddSpacer(6);

            speedLIB = ui.CreateLIBTrio("Jetpack Speed", "speed");
            speedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            speedLIB.Button.OnClick = () =>
            {
                speed = float.Parse(speedLIB.Input.Text);
            };

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            fuelTimeLIB.Input.Text = fuelTime.ToString();
            speedLIB.Input.Text = speed.ToString();
        }

        public override void Update()
        {
        }

        public class Patches
        {
            [HarmonyPatch(typeof(ClothingJetpack), "OnPostMovement")]
            [HarmonyPrefix]
            public static bool OnPostMovementPatch(ref ClothingJetpack __instance, ref PlayerCharacterMovement movement, Rigidbody rigidbody)
            {
                var baseReflect = new QuickReflection<ClothingCustom>(__instance, Plugin.Flags);
                var player = (PlayerController)baseReflect.GetField("playerController");

                if (player && player.networkObject != null && !player.networkObject.IsOwner() && !allPlayers)
                {
                    return true;
                }

                if (!__instance.IsAllowedCustom())
                {
                    return false;
                }
                if (__instance.GetCustomBit(0))
                {
                    PlayerBody playerBody = movement.GetPlayerBody();
                    if (playerBody)
                    {
                        playerBody.SetRagdollVelocityLerpY(speed, Time.fixedDeltaTime * 5f);
                    }
                }

                return false;
            }

            [HarmonyPatch(typeof(ClothingJetpack), "Update")]
            [HarmonyPrefix]
            public static bool UpdatePatch(ref ClothingJetpack __instance)
            {
                try
                {
                    var i = 0;
                    var baseReflect = new QuickReflection<ClothingCustom>(__instance, Plugin.Flags);

                    var player = (PlayerController)baseReflect.GetField("playerController");

                    if (player && player.networkObject != null && !player.networkObject.IsOwner() && !allPlayers)
                    {
                        return true;
                    }

                    var reflect = new QuickReflection<ClothingJetpack>(__instance, Plugin.Flags);

                    float num = 0f;

                    if (__instance.IsAllowedCustom())
                    {
                        if (__instance.GetCustomBit(0) && fuelEnabled)
                        {
                            reflect.SetField("fuelTime", (float)reflect.GetField("fuelTime") - Time.deltaTime);
                            reflect.SetField("bRefueling", false);
                        }

                        else
                        {
                            reflect.SetField("fuelTime", (float)reflect.GetField("fuelTime") + Time.deltaTime * 1f);
                            reflect.SetField("bRefueling", true);
                        }

                        reflect.SetField("fuelTime", Mathf.Clamp((float)reflect.GetField("fuelTime"), 0f, fuelTime));
                        num = (float)reflect.GetField("fuelTime") / fuelTime;
                    }

                    if ((Material)reflect.GetField("fuelIndicatorMaterial"))
                    {
                        var fuelIndicatorMaterial = (Material)reflect.GetField("fuelIndicatorMaterial");
                        fuelIndicatorMaterial.SetFloat((int)typeof(ClothingJetpack).GetField("Shader_Progress_ID", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), num);
                    }

                    return false;
                }
                catch (Exception e)
                {
                    Plugin.LogSource.LogError(e.Message + e.StackTrace);
                    return true;
                }
            }
        }
    }
}
