using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class CharacterManager : PlayerBasedMod
{
    public override string Name => "Character Manager";
    public override string Description => "Change Properties about your Player Character!";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    private Ref<Vector3> coordinates = new();

    private bool editingPosition;

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            base.BuildPanel(id),

            new SeparatorText("Other", "###separator"),
            
            new DragFloat3("Player Position", onValueChanged: _ => editingPosition = true).WithValue(coordinates),
            new Button("Apply Position", () =>
            {
                PlayerPosition = coordinates.Value;
                editingPosition = false;
            }).WithContentWidth(),
            
            new Separator("sp"),

            new Button("Inspect \"Player Character\" Component", () =>
            {
                if (Player != null && Player.Character)
                {
                    InspectorManager.Inspect(Player.Character);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        base.RefreshUI();

        editingPosition = false;
    }

    public override void Update()
    {
        if (Player?.Character == null) return;
        if (editingPosition) return;
        coordinates.Value = Player?.Character?.GetPlayerBody()?.transform?.position ?? Vector3.zero;
    }

    [ModSetting(Order = 10)]
    public bool PlayerCamEnabled
    {
        get
        {
            if (Player != null)
            {
                return Player?.Character?.IsPlayerCamUIAllowed() ?? false;
            }

            return false;
        }
        set => Player?.Character.SetPlayerCamUIAllowed(value);
    }

    [ModSetting(ShowInUI = false)]
    public Vector3 PlayerPosition
    {
        get => Player?.Body?.transform.position ?? Vector3.zero;
        set => Player?.Body?.transform.position = value;
    }

    [ModAction(SeparatorText = "Kill Player", Order = 20)]
    public void KillPlayer(float time = 1)
    {
        Player?.Character.Kill(time);
    }

    [ModAction(SeparatorText = "Player Color", Order = 30)]
    public void SetPlayerColor([ModActionParam(WidgetType.Color3)] Color color, bool smoothTransition = true, [ModActionParam(WidgetType.Drag, Speed = 0.025f)] float timeTillDefault = 0)
    {
        Player?.Character.GetPlayerCharacterCustomize().SetCharacterColor(color, smoothTransition, timeTillDefault);
    }
}