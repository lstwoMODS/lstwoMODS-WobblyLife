using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks;

public class SmitePlayer : PlayerBasedHack
{
    public override string Name => "Smite Player";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.PlayerHacksTab;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateLBDuo("Spawn Lightning at Players Position", "lstwo.SmitePlayer.Smite", () =>
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