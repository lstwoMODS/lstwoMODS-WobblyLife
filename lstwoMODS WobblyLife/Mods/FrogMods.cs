using UnityEngine;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.Elements;
using UnityExplorer.UI;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;

namespace lstwoMODS_WobblyLife.Mods;

public class FrogMods : PlayerBasedMod
{
    public override string Name => "Frog Mods";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    private PlayerController _lastPlayer;

    public PlayerFrog Frog
    {
        get
        {
            if (Player == null) return null;

            if (field != null && _lastPlayer == Player.Controller) return field;
            _lastPlayer = Player.Controller;

            foreach (var frog in UnityEngine.Object.FindObjectsOfType<PlayerFrog>())
            {
                if (frog.GetPlayerController() == Player.Controller)
                {
                    field = frog;
                }
            }

            return null;
        }
    }

    [ModSetting(Speed = 0.05f)]
    public float MaxSpeed
    {
        get => Frog?.GetComponent<PlayerFrogMovement>()?.maxSpeed ?? 0;
        set => Frog?.GetComponent<PlayerFrogMovement>()?.maxSpeed = value;
    }

    [ModSetting(Speed = 0.05f)]
    public float MovementSpeed
    {
        get => Frog?.GetComponent<PlayerFrogMovement>()?.movementSpeed ?? 0;
        set => Frog?.GetComponent<PlayerFrogMovement>()?.movementSpeed = value;
    }

    [ModSetting(Speed = 0.05f)]
    public float JumpForce
    {
        get => Frog?.GetComponent<PlayerFrogMovement>()?.jumpForce ?? 0;
        set => Frog?.GetComponent<PlayerFrogMovement>()?.jumpForce = value;
    }

    [ModSetting]
    public Color FrogColor
    {
        get => Frog?.GetComponent<PropColour>()?.GetPrimaryColor() ?? Color.clear;
        set => Frog?.GetComponent<PropColour>()?.SetPrimaryColour(value);
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            base.BuildPanel(id),
            
            new Button("Inspect \"Player Frog\" Component", () =>
            {
                if (Frog)
                {
                    InspectorManager.Inspect(Frog);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }
}