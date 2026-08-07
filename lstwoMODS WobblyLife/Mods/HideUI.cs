using System.Collections.Generic;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class HideUI : BaseMod
{
    public override string Name => "Hide UI";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    private PlayerRef player => GameInstance.Instance?.GetFirstLocalPlayerController();

    [ModSetting] public readonly Ref<bool> hideAllUI = new();
    [ModSetting] public readonly Ref<bool> hideMinimap = new();
    [ModSetting] public readonly Ref<bool> hideJobUI = new();
    [ModSetting] public readonly Ref<bool> hideJobComponents = new();
    [ModSetting] public readonly Ref<bool> hideInputHints = new();
        
    private List<Canvas> hiddenCanvases = [];
    private bool prevHideAllUI;

    public HideUI()
    {
        hideAllUI.Changed         += _ => RefreshHiddenElements();
        hideMinimap.Changed       += b => player.Controller.SetMinimapDisabled(this, b);
        hideMinimap.Changed       += _ => RefreshHiddenElements();
        hideJobUI.Changed         += _ => RefreshHiddenElements();
        hideJobComponents.Changed += _ => RefreshHiddenElements();
        hideInputHints.Changed    += _ => RefreshHiddenElements();
    }

    [ModAction]
    public void RefreshHiddenElements()
    {
        if (!player?.PlayerBasedUI || !player?.ControllerUI)
        {
            return;
        }

        player.Controller.SetMinimapVisible(!hideMinimap.Value);

        player.PlayerBasedUI.GetUIJobCanvas().gameObject.GetComponent<Canvas>().enabled = !hideJobUI.Value;
        player.PlayerBasedUI.GetUIJobCanvas().GetUIJobComponentCanvas().gameObject.GetComponent<Canvas>().enabled = !hideJobComponents.Value;
        
        player.PlayerBasedUI.GetUIGameplayCanvas().GetGameCanvas().GetInputHintCanvas().gameObject.GetComponent<Canvas>().enabled = !hideInputHints.Value;

        if (hideAllUI.Value == prevHideAllUI)
        {
            return;
        }
        
        if (hideAllUI.Value)
        {
            hiddenCanvases.Clear();
            
            foreach (var canvas in player.PlayerBasedUI.GetComponentsInChildren<Canvas>(false))
            {
                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }
        else
        {
            foreach (var canvas in hiddenCanvases)
            {
                canvas.enabled = true;
            }

            hiddenCanvases.Clear();
        }

        prevHideAllUI = hideAllUI.Value;
    }
}