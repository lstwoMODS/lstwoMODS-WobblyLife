using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks.Paid
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

            ui.CreateToggle("hide minimap", "Hide Minimap", (b) => PlayerUtils.GetMyPlayer().SetMinimapDisabled(this, b));

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
