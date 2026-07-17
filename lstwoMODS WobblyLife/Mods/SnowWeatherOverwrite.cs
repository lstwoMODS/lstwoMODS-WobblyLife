using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class SnowWeatherOverwrite : BaseMod
{
    private static Ref<int> mode = new();

    protected override void OnStaticInit()
    {
        new Harmony("net.lstwo.lstwoMODS_WobblyLife.SnowWeatherOverwrite").PatchAll(typeof(Patches));
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            new RadioButton("Don't Overwrite", "Don't Overwrite", mode, 0),
            new SameLine("SameLine"),
            new RadioButton("Snow Everywhere", "Snow Everywhere", mode, 1),
            new SameLine("SameLine"),
            new RadioButton("Rain Everywhere", "Rain Everywhere", mode, 2)
        );
    }

    public override string Name => "Snow Weather Overwrite";
    public override string Description => "Make it snow everywhere (or rain)";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    public static class Patches
    {
        [HarmonyPatch(typeof(WeatherTrigger), nameof(WeatherTrigger.GetWeatherRainingType))]
        [HarmonyPrefix]
        public static bool WeatherTrigger_GetWeatherRainingType_Prefix(ref WeatherRainingType __result)
        {
            return Patch(ref __result);
        }
        
        [HarmonyPatch(typeof(WeatherSystem), nameof(WeatherSystem.GetDefaultRainingType))]
        [HarmonyPrefix]
        public static bool WeatherSystem_GetDefaultRainingType_Prefix(ref WeatherRainingType __result)
        {
            return Patch(ref __result);
        }

        private static bool Patch(ref WeatherRainingType __result)
        {
            if (mode.Value is not (1 or 2))
            {
                return true;
            }
            
            __result = mode.Value == 1 ? WeatherRainingType.Snow : WeatherRainingType.Rain;
            return false;
        }

        /*[HarmonyPatch(typeof(WobblyHolidayScriptableObject), nameof(WobblyHolidayScriptableObject.GetCurrentHoliday))]
        [HarmonyPrefix]
        public static bool WobblyHolidayScriptableObject_GetCurrentHoliday_Prefix(ref WobblyHoliday __result)
        {
            __result = WobblyHoliday.Christmas;
            return false;
        }*/
    }
}