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
        private PlayerController player;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.HideUI.HideMinimap", "Hide Minimap", (b) => PlayerUtils.GetMyPlayer().SetMinimapDisabled(this, b));

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            player = PlayerUtils.GetMyPlayer();
            playerUI = player.GetPlayerBasedUI();
        }

        public override void Update()
        {
        }
    }
}
