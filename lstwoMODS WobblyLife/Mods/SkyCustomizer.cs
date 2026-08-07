using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.UI;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class SkyCustomizer : BaseMod
{
    [ModSetting(Order = 10)]
    public static readonly Ref<bool> OverrideSkyColor = new();
    [ModSetting(Widget = WidgetType.Color3, Order = 20)]
    public static readonly Ref<Color> SkyColor = new();

    [ModSetting(Order = 30)]
    public static readonly Ref<bool> OverrideStarIntensity = new();
    [ModSetting(Order = 40)]
    public static readonly Ref<float> StarIntensity = new();

    public override string Name => "Sky Customizer";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    public static Material Skybox;

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.SkyCustomizer").PatchAll(typeof(Patches));
    }

    public override void RefreshUI()
    {
        if (RenderSettings.skybox?.shader?.name != "WobblyLife/Custom/WobblySkybox") return;

        Skybox = RenderSettings.skybox;
    }

    public class Patches
    {
        // Postfix, not prefix: let the game's own Lerp run untouched so the skybox light
        // matrix, fog, ambient and sun flare are all exactly vanilla. Then layer only the
        // two override values on top. The previous full-replacement prefix was an old
        // pre-1.0 copy of Lerp that drifted from the shipped method (e.g. it fed the light
        // matrix inverted), which desynced the skybox sun/moon from the actual light.
        [HarmonyPatch(typeof(DayNightCycle), "Lerp")]
        [HarmonyPostfix]
        public static void LerpPostfix(ref DayNightCycle __instance)
        {
            var material = __instance.skyBoxMaterial;
            if (material == null) return;

            if (OverrideStarIntensity.Value)
            {
                __instance.starVisibility = StarIntensity.Value;
                material.SetFloat(DayNightCycle.SkyboxStarsVisibility, StarIntensity.Value);
            }

            if (OverrideSkyColor.Value)
            {
                var tint = new Color(SkyColor.Value.r, SkyColor.Value.g, SkyColor.Value.b, 1f);
                __instance.currentSkyboxTint = tint;
                material.SetColor(DayNightCycle.SkyboxTintid, tint);
            }
        }
    }
}
