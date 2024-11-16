using HarmonyLib;
using NotAzzamods.Hacks.Paid;
using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.Custom
{
    public class CharacterManager : BaseHack
    {
        public override string Name => "Character Manager";

        public override string Description => "Change Properties about your Player Character!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public bool PlayerCamEnabled
        {
            get
            {
                if(Player != null)
                {
                    return Player.Character.IsPlayerCamUIAllowed();
                }

                return false;
            }
            set
            {
                if (Player != null)
                {
                    Player.Character.SetPlayerCamUIAllowed(value);
                }
            }
        }

        public Color PlayerColor
        {
            get
            {
                return Player.Character.GetPlayerCharacterCustomize().GetCharacterColor();
            }
            set
            {
                // Colors
                var colors = playerColorApplyBtn.GameObject.GetComponent<Button>().colors;
                var color = value;

                color.a = 1;
                colors.normalColor = color;

                // UI
                playerColorSliderR.value = color.r;
                playerColorSliderG.value = color.g;
                playerColorSliderB.value = color.b;

                playerColorApplyBtn.GameObject.GetComponent<Button>().colors = colors;
                playerColorApplyBtn.GameObject.GetComponent<Image>().color = color;
                playerColorApplyBtn.GameObject.GetComponentInChildren<Text>().color = InvertColor(color);
            }
        }


        private static bool characterCutoff = true;

        private ButtonRef playerColorApplyBtn;
        private ColorBlock playerColorBtnColor;
        private Slider playerColorSliderR, playerColorSliderG, playerColorSliderB;

        private Toggle playerCutoffToggle;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("lstwo.NotAzzamods.CharacterManager").PatchAll(typeof(HarmonyPatches));

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Kill Player", "KillPlayer", () => KillPlayer(1), "Quick Kill Player", () => KillPlayer(0), "Respawn Player");

            ui.AddSpacer(6);

            var akpLib = ui.CreateLIBTrio("Advanced Kill Player", "AdvancedKillPlayer", "Knockout Time in Seconds", null, "Kill");
            akpLib.Button.OnClick = () => KillPlayer(float.Parse(akpLib.Input.Text));
            akpLib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            ui.CreateLabel("<b>Set Player Character Color</b>");

            ui.AddSpacer(6);

            playerColorSliderR = ui.CreateSlider("playerColorSliderR");
            playerColorSliderR.onValueChanged.AddListener(SetPlayerColorBtnR);

            playerColorSliderG = ui.CreateSlider("playerColorSliderR");
            playerColorSliderG.onValueChanged.AddListener(SetPlayerColorBtnG);

            playerColorSliderB = ui.CreateSlider("playerColorSliderR");
            playerColorSliderB.onValueChanged.AddListener(SetPlayerColorBtnB);

            ui.AddSpacer(6);

            playerColorApplyBtn = ui.CreateButton("Apply", () => SetPlayerColor(PlayerColor, true, 0), color: new(0, .35f, 0));
            playerColorBtnColor = playerColorApplyBtn.GameObject.GetComponent<Button>().colors;

            ui.AddSpacer(6);

            ui.CreateToggle("cutoff", "Allow Player Character Cutoff", (b) => characterCutoff = b, true);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            if (Player == null) return;

            PlayerColor = PlayerColor;

        }

        public override void Update() { }

        public void KillPlayer(float time = 1)
        {
            if(Player != null)
            {
                Player.Character.Kill(time);
            }
        }

        public void SetPlayerColor(Color color, bool smoothTransition = true, float timeTillDefault = 0)
        {
            if (Player != null)
            {
                Player.Character.GetPlayerCharacterCustomize().SetCharacterColor(color, smoothTransition, timeTillDefault);
            }
        }

        private void SetPlayerColorBtnR(float r)
        {
            var color = PlayerColor;
            color.r = r;
            PlayerColor = color;
        }

        private void SetPlayerColorBtnG(float g)
        {
            var color = PlayerColor;
            color.g = g;
            PlayerColor = color;
        }

        private void SetPlayerColorBtnB(float b)
        {
            var color = PlayerColor;
            color.b = b;
            PlayerColor = color;
        }

        public static Color InvertColor(Color color)
        {
            color.r = -color.r + 1;
            color.g = -color.g + 1;
            color.b = -color.b + 1;
            return color;
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
