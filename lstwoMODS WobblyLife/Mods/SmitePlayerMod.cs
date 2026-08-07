using System.Linq;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class SmitePlayerMod : PlayerBasedMod
{
    public override string Name => "Smite Player";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    [ModAction("Spawn Lightning At Players Position")]
    public void SmitePlayer()
    {
        var data = WeatherSystem.Instance.GetCurrentWeatherData();
        var index = WeatherSystem.Instance.GetAllWeatherData().ToList().IndexOf(data);

        WeatherSystem.Instance.ServerSetWeatherByIndex(4);
        WeatherSystem.Instance.ServerLightingStrike(Player.Character.GetPlayerPosition());
        WeatherSystem.Instance.ServerSetWeatherByIndex(index);
    }
}