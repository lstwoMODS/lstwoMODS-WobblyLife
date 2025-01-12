using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public class SmitePlayer : BaseHack
    {
        public override string Name => "Smite Player";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Spawn Lightning at Players Position", "", () =>
            {
                var data = WeatherSystem.Instance.GetCurrentWeatherData();
                var index = WeatherSystem.Instance.GetAllWeatherData().ToList().IndexOf(data);

                WeatherSystem.Instance.ServerSetWeatherByIndex(4);
                WeatherSystem.Instance.ServerLightingStrike(Player.Character.GetPlayerPosition());
                WeatherSystem.Instance.ServerSetWeatherByIndex(index);
            }, "Spawn Lightning");

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
