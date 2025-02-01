using HarmonyLib;
using IngameDebugConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Rewired;
using UnityEngine.UI;
using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class MovementManager : PlayerBasedHack
    {
        public static bool infiniteJump = false;
        public static bool multiJump = false;

        public override string Name => "Movement Manager";
        public override string Description => "The Managing Movement";
        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        private HacksUIHelper.LIBTrio moveSpeedLib, jumpHeightLib;
        private Toggle noclipToggle;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("NotAzza.Movement").PatchAll(typeof(MovementPatches));

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            moveSpeedLib = ui.CreateLIBTrio("Set Move Speed", "SetMoveSpeed", "Move Speed");
            moveSpeedLib.Button.OnClick = () => SetMoveSpeed(float.Parse(moveSpeedLib.Input.Text));
            moveSpeedLib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            jumpHeightLib = ui.CreateLIBTrio("Set Jump Height", "SetJumpHeight", "Jump Height");
            jumpHeightLib.Button.OnClick = () => SetJumpHeight(float.Parse(jumpHeightLib.Input.Text));
            jumpHeightLib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            ui.CreateToggle("AllowInfiniteJump", "Enable Infinite Jump Hack (Only for you)", SetInfiniteJump);

            ui.AddSpacer(6);

            ui.CreateToggle("AllowMultiJump", "Enable Multi Jump Hack (Only for you)", SetMultiJump);

            ui.AddSpacer(6);

            noclipToggle = ui.CreateToggle("Noclip", "Enable Noclip (Fly Mode)", SetNoclipEnabled);

            ui.AddSpacer(6);
        }

        public override void Update()
        {
            if (Player != null && Player.Character != null && Player.Character.GetRewiredPlayer().GetButtonDown("Jump") && multiJump)
            {
                PlayerCharacterMovement __instance = Player.CharacterMovement;
                FieldInfo canJumpField = typeof(PlayerCharacterMovement).GetField("bCanJump", Plugin.Flags);
                canJumpField.SetValue(__instance, true);

                FieldInfo groundedField = typeof(PlayerCharacterMovement).GetField("bIsGrounded", Plugin.Flags);
                groundedField.SetValue(__instance, true);
            }
        }

        public override void RefreshUI()
        {
            if (Player != null)
            {
                moveSpeedLib.Input.Text = Player.CharacterMovement.GetSpeedMultiplier().ToString();
                jumpHeightLib.Input.Text = Player.CharacterMovement.GetJumpMultiplier().ToString();
                noclipToggle.isOn = Player.CharacterMovement.IsNoClipEnabled();
            }
        }


        public void SetMoveSpeed(float speed)
        {
            if (Player != null)
                Player.CharacterMovement.SetSpeedMultiplier(speed);
        }

        public void SetJumpHeight(float height)
        {
            if (Player != null)
                Player.CharacterMovement.SetJumpMultiplier(height);
        }


        public void SetNoclipEnabled(bool b)
        {
            if (Player != null)
                Player.CharacterMovement.SetNoClipEnabled(b);
        }

        public void SetInfiniteJump(bool b)
        {
            infiniteJump = b;
            if (b == true) multiJump = false;
        }

        public void SetMultiJump(bool b)
        {
            multiJump = b;
            if (b == true) infiniteJump = false;
        }
    }

    public static class MovementPatches
    {
        [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateJump")]
        [HarmonyPrefix]
        public static void InfiniteJumpHack(PlayerCharacterMovement __instance, bool bJump)
        {
            if (MovementManager.infiniteJump == true && __instance.GetPlayerBody().GetPlayerCharacter().GetPlayerController().networkObject.IsOwner())
            {
                FieldInfo canJumpField = typeof(PlayerCharacterMovement).GetField("bCanJump", Plugin.Flags);
                canJumpField.SetValue(__instance, true);

                FieldInfo groundedField = typeof(PlayerCharacterMovement).GetField("bIsGrounded", Plugin.Flags);
                groundedField.SetValue(__instance, true);
            }
        }
    }
}
