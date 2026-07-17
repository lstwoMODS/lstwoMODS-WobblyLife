using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class ActionEnterExitInteractModifier : PlayerBasedMod
{
    public override string Name => "Enter Exit Interact Modifier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.VehicleModsWindow;

    [ModSetting(Order = 10)]
    public bool ShouldKnockoutIfGoingFast
    {
        get => Action?.bKnockoutPlayerIfGoingFast ?? false;
        set => Action?.SetShouldKnockoutbPlayerIfGoingFast(value);
    }

    [ModSetting(Order = 20)]
    public bool Locked
    {
        get => Action?.IsLocked(Player?.Controller) ?? false;
        set => Action?.SetLocked(value);
    }

    [ModSetting(Order = 30)]
    public bool Interactable
    {
        get => Action?.bInteractable ?? false;
        set => Action?.SetInteractable(value);
    }

    public ActionEnterExitInteract Action => (ActionEnterExitInteract) Player?.Controller?.GetPlayerControllerInteractor()?.GetEnteredAction();

    private Ref<bool> disableOptions = new();

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new UIText("No Action Info", "Player has no entered Action Enter Exit Interact").WithVisible(disableOptions),
            base.BuildPanel(id).WithDisabled(disableOptions),
            
            new Button("Inspect \"Action Enter Exit Interact\" Component", () =>
            {
                if (Action)
                {
                    InspectorManager.Inspect(Action);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth().WithDisabled(disableOptions)
        );
    }

    public override void RefreshUI()
    {
        disableOptions.Value = Action == null;
        base.RefreshUI();
    }
}