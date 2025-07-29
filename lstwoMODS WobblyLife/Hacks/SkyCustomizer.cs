using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.Utility;

namespace lstwoMODS_WobblyLife.Hacks;

public class SkyCustomizer : BaseHack
{
    public static Color overrideSkyTint;
    public static bool bOverrideSkyColor;

    public static float overrideStarVisibility;
    public static bool bOverrideStarIntensity;
        
    public override string Name => "Sky Customizer";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ClientHacksTab;

    private Material skybox;

    private HacksUIHelper.LIBTrio skyColorLIB;
    private HacksUIHelper.LIBTrio starIntensityLIB;

    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.SkyCustomizer").PatchAll(typeof(Patches));
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.SkyCustomizer.OverrideSkyColor", "Override Sky Color", (b) =>
        {
            bOverrideSkyColor = b;
        });

        ui.AddSpacer(6);

        skyColorLIB = ui.CreateLIBTrio("Sky Color", "lstwo.SkyCustomizer.SkyColor", "#3b9bff");
        skyColorLIB.Button.OnClick = () =>
        {
            var hex = skyColorLIB.Input.Text;

            if(!hex.StartsWith("#"))
            {
                hex = $"#{hex}";
            }

            ColorUtility.TryParseHtmlString(hex, out overrideSkyTint);
        };

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.SkyCustomizer.OverrideStarIntensity", "Override Star Intensity", (b) =>
        {
            bOverrideStarIntensity = b;
        });

        ui.AddSpacer(6);

        starIntensityLIB = ui.CreateLIBTrio("Star Intensity", "lstwo.SkyCustomizer.StarIntensity", "1");
        starIntensityLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        starIntensityLIB.Button.OnClick = () =>
        {
            overrideStarVisibility = float.Parse(starIntensityLIB.Input.Text);
        };

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        if(!skybox)
        {
            var _skybox = RenderSettings.skybox;

            if(_skybox.shader.name != "WobblyLife/Custom/WobblySkybox")
            {
                return;
            }

            skybox = _skybox;
        }

        skyColorLIB.Input.Text = ColorUtility.ToHtmlStringRGB(skybox.GetColor("_Tint"));
        starIntensityLIB.Input.Text = skybox.GetFloat("_StarsVisibility").ToString();
    }

    public override void Update()
    {
    }

    public class Patches
    {
        [HarmonyPatch(typeof(DayNightCycle), "Lerp")]
        [HarmonyPrefix]
        public static bool LerpPrefix(ref DayNightCycle __instance, ref DayNightSetting from, ref DayNightSetting to, ref float progress)
        {
            var r = new QuickReflection<DayNightCycle>(__instance, Plugin.Flags);
            var sr = new QuickReflection<DayNightCycle>(null, BindingFlags.NonPublic | BindingFlags.Static);

            var material = (Material)r.GetField("skyBoxMaterial");

            if (bOverrideStarIntensity)
            {
                r.SetField("starVisibility", overrideStarVisibility);
            }
            else
            {
                if (__instance.IsDayFast())
                {
                    r.SetField("starVisibility", Mathf.Lerp((float)r.GetField("starVisibility"), 0f, Time.deltaTime));
                }
                else
                {
                    r.SetField("starVisibility", Mathf.Lerp((float)r.GetField("starVisibility"), 1f, Time.deltaTime));
                }
            }

            if (from.lightCurve != null)
            {
                progress = from.lightCurve.Evaluate(progress);
            }

            if(bOverrideSkyColor)
            {
                r.SetField("currentSkyboxTint", overrideSkyTint);
            }
            else
            {
                var currentSkyboxTint = Color.Lerp(from.skyboxTint, to.skyboxTint, progress);
                currentSkyboxTint.a = 1f;
                r.SetField("currentSkyboxTint", currentSkyboxTint);
            }

            material.SetMatrix((int)sr.GetField("SkyboxLightLocalToWorldMatrix"), ((Transform)r.GetField("lightTransform")).transform.localToWorldMatrix.inverse);
            material.SetColor((int)sr.GetField("SkyboxTintid"), (Color)r.GetField("currentSkyboxTint"));
            material.SetFloat((int)sr.GetField("SkyboxStarsVisibility"), (float)r.GetField("starVisibility"));

            float num2;

            if (RenderSettings.fogEndDistance >= 200f)
            {
                float num = Mathf.Clamp(RenderSettings.fogEndDistance, 0f, 2000f);
                num2 = 1f - (num - 200f) / 1800f;
            }
            else
            {
                num2 = 1f;
            }

            material.SetFloat((int)sr.GetField("SkyboxFogFill"), num2);
            Color color = Color.Lerp(from.fogColour, to.fogColour, progress);

            if ((bool)r.GetField("bUseFogBlendColor"))
            {
                color = Color.Lerp(color, (Color)r.GetField("fogBlendColor"), 0.5f);
            }

            Color color2 = Color.Lerp(RenderSettings.fogColor, color, Time.deltaTime * 2f);
            RenderSettings.fogColor = color2;
            Color color3 = Color.Lerp(from.ambientLight, to.ambientLight, progress);

            if (Application.isPlaying && (LensFlare)r.GetField("sunFlare"))
            {
                var sunFlare = (LensFlare)r.GetField("sunFlare");
                sunFlare.brightness = (1f - num2) * (float)r.GetField("sunFlareMaxBrightness");
                sunFlare.color = color2;

                if ((bool)r.GetField("bSunFlareEnabled"))
                {
                    float num3 = Vector3.Dot(Vector3.up, -((Transform)r.GetField("lightTransform")).transform.forward);
                    sunFlare.enabled = num3 >= -0.1f;
                }
                else
                {
                    sunFlare.enabled = false;
                }
            }

            if ((PlayerAmbientManager)r.GetField("ambientManager"))
            {
                ((PlayerAmbientManager)r.GetField("ambientManager")).SetDefault(color3);
                return false;
            }

            RenderSettings.ambientSkyColor = color3;

            return false;
        }
    }
}