using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>What <see cref="SnowWeatherOverwrite"/> forces every weather trigger in the world to
/// report. Values match the radio buttons' option values, which is what <c>mode</c> stores.</summary>
public enum WeatherOverwriteMode
{
    DontOverwrite = 0,
    SnowEverywhere = 1,
    RainEverywhere = 2,
}

public class SnowWeatherOverwrite : BaseMod
{
    private static Ref<int> mode = new();

    /// <summary>Macro/hotkey view of the radio buttons. Writing it goes through <c>mode</c>, so the
    /// panel follows along.</summary>
    [ModSetting(ShowInUI = false, Label = "Overwrite Mode",
        Description = "Force every weather trigger in the world to report Snow or Rain. Client side, so it works as a guest. " +
                      "Snow also stops lightning entirely: the game only spawns strikes over Rain.")]
    public static WeatherOverwriteMode Mode
    {
        get => (WeatherOverwriteMode)mode.Value;
        set => mode.Value = (int)value;
    }

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
            if (Mode == WeatherOverwriteMode.DontOverwrite)
            {
                return true;
            }

            __result = Mode == WeatherOverwriteMode.SnowEverywhere ? WeatherRainingType.Snow : WeatherRainingType.Rain;
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