using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class ControllerManager : PlayerBasedMod
{
    public override string Name => "Player Controller Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    [ModSetting(ApplyButton = true, Order = 10)]
    public string PlayerName
    {
        get => Player?.Controller?.GetPlayerName() ?? "";
        set => Player?.Controller?.SetServerPlayerName(value);
    }

    [ModSetting(Order = 20)]
    public bool EnableClothingAbilities
    {
        get => Player?.Controller?.bServerAllowedCustomsClothingAbilities ?? false;
        set => Player?.Controller?.ServerSetAllowedCustomClothingAbilities(value);
    }

    [ModSetting(Order = 30)]
    public bool AllowRespawning
    {
        get => Player?.Controller?.IsAllowedToRespawn() ?? false;
        set => Player?.Controller?.SetAllowedToRespawn(this, value);
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            AutoUIBuilder.Build(this, id),
            
            new Button("Inspect \"Player Controller\" Component", () =>
            {
                if (Player != null && Player.Controller)
                {
                    InspectorManager.Inspect(Player.Controller);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }
}