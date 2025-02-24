using lstwoMODS_WobblyLife.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class HideUI : BaseHack
    {
        public override string Name => "Hide UI";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        private PlayerBasedUI playerUI;
        private PlayerControllerUI controllerUI;
        private PlayerController player;

        private bool hideAllUI;
        private bool hideMinimap;
        private bool hideJobUI;
        private bool hideJobComponents;
        private bool hideInputHints;
        
        private List<Canvas> hiddenCanvases = new List<Canvas>();
        private bool prevHideAllUI;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.HideUI.HideAllUI", "Hide All UI", (b) =>
            {
                hideAllUI = b;
                RefreshHiddenElements();
            });

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.HideUI.DisableMinimap", "Disable Minimap", (b) =>
            {
                player.SetMinimapDisabled(this, b);
            });

            ui.CreateToggle("lstwo.HideUI.HideMinimap", "Hide Minimap", (b) =>
            {
                hideMinimap = b;
                RefreshHiddenElements();
            });

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.HideUI.HideJobUI", "Hide Job UI", (b) =>
            {
                hideJobUI = b;
                RefreshHiddenElements();
            });

            ui.CreateToggle("lstwo.HideUI.HideJobComponents", "Hide Job Components Only", (b) =>
            {
                hideJobComponents = b;
                RefreshHiddenElements();
            });

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.HideUI.HideInputHints", "Hide Input Hints", (b) =>
            {
                hideInputHints = b;
                RefreshHiddenElements();
            });
            
            ui.AddSpacer(6);

            ui.CreateButton("Refresh Hidden Elements", RefreshHiddenElements, "lstwo.HideUI.RefreshButton");
            
            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            player = PlayerUtils.GetMyPlayer();
            playerUI = player.GetPlayerBasedUI();
            controllerUI = player.GetPlayerControllerUI();
        }

        public override void Update()
        {
        }

        private void RefreshHiddenElements()
        {
            if (!playerUI)
            {
                return;
            }

            player.SetMinimapVisible(!hideMinimap);

            playerUI.GetUIJobCanvas().gameObject.GetComponent<Canvas>().enabled = !hideJobUI;
            playerUI.GetUIJobCanvas().GetUIJobComponentCanvas().gameObject.GetComponent<Canvas>().enabled = !hideJobComponents;
            
            playerUI.GetUIGameplayCanvas().GetGameCanvas().GetInputHintCanvas().gameObject.GetComponent<Canvas>().enabled = !hideInputHints;

            if (hideAllUI == prevHideAllUI)
            {
                return;
            }
            
            if (hideAllUI)
            {
                hiddenCanvases.Clear();
                    
                foreach (var canvas in playerUI.GetComponentsInChildren<Canvas>(false))
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
                
            prevHideAllUI = hideAllUI;
        }
    }
}
