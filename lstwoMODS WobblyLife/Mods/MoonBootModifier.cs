using System.Collections;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class MoonBootModifier : PlayerBasedMod
{
    public override string Name => "Moon Boots Modifier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    private PlayerSettings Current => GetPlayerSettings<PlayerSettings>(Player?.Controller);

    [ModSetting(Order = 5)]
    public bool ChangeJumpForce
    {
        get => Current?.ChangeJumpForce ?? false;
        set => Current?.ChangeJumpForce = value;
    }

    [ModSetting(Label = "Jump Force", Order = 10)]
    public float JumpForce
    {
        get => Current?.JumpForce ?? 0;
        set => Current?.JumpForce = value;
    }

    [ModSetting(Order = 15, SeparatorText = "")]
    public bool ChangeCooldown
    {
        get => Current?.ChangeCooldown ?? false;
        set => Current?.ChangeCooldown = value;
    }

    [ModSetting(Label = "Cooldown Duration", Order = 20, Description = "< 0 = use particle duration")]
    public float CooldownDuration
    {
        get => Current?.CooldownDuration ?? 0;
        set => Current?.CooldownDuration = value;
    }

    [ModSetting(Label = "Infinite Double Jumps", Order = 30, SeparatorText = "")]
    public bool InfiniteDoubleJumps
    {
        get => Current?.InfiniteDoubleJumps ?? false;
        set => Current?.InfiniteDoubleJumps = value;
    }

    protected override void OnStaticInit()
    {
        new Harmony("lstwoMODS.MoonBootModifier").PatchAll(typeof(Patches));
    }

    private class PlayerSettings
    {
        public bool ChangeJumpForce;
        public float JumpForce = 15f;
        public bool ChangeCooldown;
        public float CooldownDuration;
        public bool InfiniteDoubleJumps;
    }

    public class Patches
    {
        [HarmonyPatch(typeof(ClothingMoonBoots), "OnJump")]
        [HarmonyPrefix]
        public static void OnJumpPrefix(ClothingMoonBoots __instance)
        {
            if (!__instance.movement.networkObject.IsOwner()) return;

            var settings = GetPlayerSettings<PlayerSettings>(typeof(MoonBootModifier), __instance.movement.playerCharacter.GetPlayerController());

            if (settings.ChangeJumpForce)
            {
                __instance.jumpForce = settings.JumpForce;
            }
        }

        [HarmonyPatch(typeof(ClothingMoonBoots), "Cooldown")]
        [HarmonyPrefix]
        public static bool CooldownPrefix(ClothingMoonBoots __instance, ref IEnumerator __result)
        {
            if (!__instance.movement.playerCharacter.GetPlayerController().networkObject.IsOwner()) return true;

            var settings = GetPlayerSettings<PlayerSettings>(typeof(MoonBootModifier), __instance.movement.playerCharacter.GetPlayerController());

            if (!settings.ChangeCooldown)
            {
                return true;
            }

            __result = CustomCooldown(__instance, settings.CooldownDuration);
            return false;
        }

        private static IEnumerator CustomCooldown(ClothingMoonBoots instance, float duration)
        {
            var startTime = Time.time;

            while (Time.time - startTime <= duration)
            {
                yield return null;
            }

            instance.cooldownCoroutine = null;
            instance.SetCustomBit(0, false);
        }

        [HarmonyPatch(typeof(ClothingMoonBoots), "JumpCheck")]
        [HarmonyPrefix]
        public static bool JumpCheckPrefix(ClothingMoonBoots __instance, ref IEnumerator __result, PlayerCharacterMovement movement, Rigidbody rigidbody)
        {
            if (!__instance.movement.networkObject.IsOwner()) return true;

            var settings = GetPlayerSettings<PlayerSettings>(typeof(MoonBootModifier),__instance.movement.playerCharacter.GetPlayerController());
            if (!settings.InfiniteDoubleJumps) return true;

            __result = InfiniteJumpCheck(__instance, movement);
            return false;
        }

        private static IEnumerator InfiniteJumpCheck(ClothingMoonBoots instance, PlayerCharacterMovement movement)
        {
            var timeSinceJump = Time.time;
            var bDoubleJump = false;
            var bInputOff = false;

            while (movement && (!movement.IsGrounded() || !movement.CanJump()) && instance.IsAllowedCustom())
            {
                yield return new WaitForFixedUpdate();
                var num = Time.time - timeSinceJump;

                var latestInput = instance.input.GetLatestInput();
                var bJump = latestInput.bJump;

                if (!bInputOff)
                {
                    if (!bJump) bInputOff = true;
                }
                else if (bJump)
                {
                    bDoubleJump = true;
                }

                if (!(num >= 0.1f) || instance.cooldownCoroutine != null || !bDoubleJump) continue;
                
                var playerBody = movement.GetPlayerBody();
                
                if (!playerBody) continue;
                
                playerBody.SetRagdollVelocityY(instance.jumpForce);
                instance.SetCustomBit(0, true);
                instance.cooldownCoroutine = instance.StartCoroutine(instance.Cooldown());
                bDoubleJump = false;
                bInputOff = false;
                timeSinceJump = Time.time;
            }

            instance.jumpCoroutine = null;
        }
    }
}
