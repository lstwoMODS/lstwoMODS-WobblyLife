using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class ControllerManager : PlayerBasedHack
    {
        public override string Name => "Player Controller Manager";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        private Toggle clothingAbilitiesToggle;
        private Toggle allowRespawningToggle;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            clothingAbilitiesToggle = ui.CreateToggle("ClothingAbilitiesToggle", "Enable Clothing Abilities", SetClothingAbilitiesEnabled, true);
            allowRespawningToggle = ui.CreateToggle("AllowRespawning", "Allow Respawning", (b) => Player.Controller.SetAllowedToRespawn(this, b));

            ui.AddSpacer(6);

            ui.CreateButton("Inspect \"Player Controller\" Component", () =>
            {
                if (Player != null && Player.Controller)
                {
                    InspectorManager.Inspect(Player.Controller);
                    UIManager.ShowMenu = true;
                }
            }, "inspect", null, 256 * 3 + 32 * 2, 32);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            if (Player != null)
            {
                clothingAbilitiesToggle.isOn = (bool)typeof(PlayerController).GetField("bServerAllowedCustomsClothingAbilities", Plugin.Flags).GetValue(Player.Controller);
                allowRespawningToggle.isOn = Player.Controller.IsAllowedToRespawn();
            }
        }

        public override void Update()
        {
        }

        public void SetClothingAbilitiesEnabled(bool enabled)
        {
            if (Player != null)
                Player.Controller.ServerSetAllowedCustomClothingAbilities(enabled);
        }
    }
}
