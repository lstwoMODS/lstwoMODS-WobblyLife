using HarmonyLib;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks;

public class CharacterManager : PlayerBasedMod
{
    public override string Name => "Character Manager";

    public override string Description => "Change Properties about your Player Character!";

    public override HacksTab HacksTab => Plugin.PlayerHacksTab;

    public bool PlayerCamEnabled
    {
        get
        {
            if (Player != null)
            {
                return Player.Character.IsPlayerCamUIAllowed();
            }

            return false;
        }
        set
        {
            Player?.Character.SetPlayerCamUIAllowed(value);
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
            Player.Character.GetPlayerCharacterCustomize().SetCharacterColor(value, true, 0);
        }
    }

    public Color NewPlayerColor
    {
        get
        {
            return newPlayerColor;
        }
        set
        {
            // Colors
            newPlayerColor = value;
            var colors = playerColorApplyBtn.GameObject.GetComponent<Button>().colors;

            newPlayerColor.a = 1;
            colors.normalColor = newPlayerColor;

            // UI
            playerColorSliderR.value = newPlayerColor.r;
            playerColorSliderG.value = newPlayerColor.g;
            playerColorSliderB.value = newPlayerColor.b;

            playerColorApplyBtn.GameObject.GetComponent<Button>().colors = colors;
            playerColorApplyBtn.GameObject.GetComponent<Image>().color = newPlayerColor;
            playerColorApplyBtn.GameObject.GetComponentInChildren<Text>().color = InvertColor(newPlayerColor);
        }
    }

    private Color newPlayerColor;
    private ButtonRef playerColorApplyBtn;
    private Slider playerColorSliderR, playerColorSliderG, playerColorSliderB;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateLBBTrio("Kill Player", "KillPlayer", () => KillPlayer(1), "Quick Kill Player", "lstwo.CharacterManager.QuickKillPlayer", () => KillPlayer(0), "Respawn Player", 
            "lstwo.CharacterManager.RespawnPlayer");

        ui.AddSpacer(6);

        var akpLib = ui.CreateLIBTrio("Advanced Kill Player", "lstwo.CharacterManager.AdvancedKillPlayer", "Knockout Time in Seconds", null, "Kill");
        akpLib.Button.OnClick = () => KillPlayer(float.Parse(akpLib.Input.Text));
        akpLib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        ui.CreateLabel("<b>Set Player Character Color</b>");

        ui.AddSpacer(6);

        playerColorSliderR = ui.CreateSlider("lstwo.CharacterManager.playerColorSliderR");
        playerColorSliderR.onValueChanged.AddListener(SetPlayerColorBtnR);

        playerColorSliderG = ui.CreateSlider("lstwo.CharacterManager.playerColorSliderG");
        playerColorSliderG.onValueChanged.AddListener(SetPlayerColorBtnG);

        playerColorSliderB = ui.CreateSlider("lstwo.CharacterManager.playerColorSliderB");
        playerColorSliderB.onValueChanged.AddListener(SetPlayerColorBtnB);

        ui.AddSpacer(6);

        playerColorApplyBtn = ui.CreateButton("Apply", () => SetPlayerColor(NewPlayerColor, true, 0), "lstwo.CharacterManager.playerColorApplyBtn", new(0, .35f, 0));

        ui.AddSpacer(6);

        ui.CreateButton("Inspect \"Player Character\" Component", () =>
        {
            if (Player != null && Player.Character)
            {
                InspectorManager.Inspect(Player.Character);
                UIManager.ShowMenu = true;
            }
        }, "lstwo.CharacterManager.Inspect", null, 256 * 3 + 32 * 2, 32);

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        if (Player == null)
        {
            return;
        }

        NewPlayerColor = PlayerColor;
    }

    public override void Update() { }

    public void KillPlayer(float time = 1)
    {
        Player?.Character.Kill(time);
    }

    public void SetPlayerColor(Color color, bool smoothTransition = true, float timeTillDefault = 0)
    {
        Player?.Character.GetPlayerCharacterCustomize().SetCharacterColor(color, smoothTransition, timeTillDefault);
    }

    private void SetPlayerColorBtnR(float r)
    {
        var color = NewPlayerColor;
        color.r = r;
        NewPlayerColor = color;
    }

    private void SetPlayerColorBtnG(float g)
    {
        var color = NewPlayerColor;
        color.g = g;
        NewPlayerColor = color;
    }

    private void SetPlayerColorBtnB(float b)
    {
        var color = NewPlayerColor;
        color.b = b;
        NewPlayerColor = color;
    }

    public static Color InvertColor(Color color)
    {
        color.r = -color.r + 1;
        color.g = -color.g + 1;
        color.b = -color.b + 1;
        return color;
    }
}