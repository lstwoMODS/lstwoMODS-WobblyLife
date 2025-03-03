using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.Utility;

namespace lstwoMODS_WobblyLife.Hacks;

public class WaterCustomizer : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.WaterCustomizer.OverrideWater", "Override Water Material", b =>
        {
            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.ShouldOverride = b;
                materialOverride.RefreshOverride();
            }
        });

        ui.AddSpacer(12);

        waterColorLIB = ui.CreateLIBTrio("Water Color", "lstwo.WaterCustomizer.WaterColor");
        waterColorLIB.Button.OnClick = () =>
        {
            var hex = waterColorLIB.Input.Text;

            if (!hex.StartsWith("#"))
            {
                hex = $"#{hex}";
            }

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return;
            }

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.Color = color;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        foamColorLIB = ui.CreateLIBTrio("Water Foam Color", "lstwo.WaterCustomizer.FoamColor");
        foamColorLIB.Button.OnClick = () =>
        {
            var hex = foamColorLIB.Input.Text;

            if (!hex.StartsWith("#"))
            {
                hex = $"#{hex}";
            }

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return;
            }

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.FoamColor = color;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        edgeColorLIB = ui.CreateLIBTrio("Water Edge Color", "lstwo.WaterCustomizer.EdgeColor");
        edgeColorLIB.Button.OnClick = () =>
        {
            var hex = edgeColorLIB.Input.Text;

            if (!hex.StartsWith("#"))
            {
                hex = $"#{hex}";
            }

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return;
            }

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.EdgeColor = color;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        deepWaterColorLIB = ui.CreateLIBTrio("Deep Water Color", "lstwo.WaterCustomizer.DeepWaterColor");
        deepWaterColorLIB.Button.OnClick = () =>
        {
            var hex = deepWaterColorLIB.Input.Text;

            if (!hex.StartsWith("#"))
            {
                hex = $"#{hex}";
            }

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return;
            }

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.DeepColor = color;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        foamDistanceLIB = ui.CreateLIBTrio("Foam Distance", "lstwo.WaterCustomizer.FoamDistance");
        foamDistanceLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        foamDistanceLIB.Button.OnClick = () =>
        {
            var value = float.Parse(foamDistanceLIB.Input.Text);

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.FoamDistance = value;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        waveMovementLIB = ui.CreateLIBTrio("Wave Movement", "lstwo.WaterCustomizer.WaveMovement");
        waveMovementLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        waveMovementLIB.Button.OnClick = () =>
        {
            var value = float.Parse(waveMovementLIB.Input.Text);

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.WaveMovement = value;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        noiseCutoffLIB = 
            ui.CreateLIBTrio("Water Surface Noise Cutoff", "lstwo.WaterCustomizer.WaterSurfaceNoiseCutoff");
        noiseCutoffLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        noiseCutoffLIB.Button.OnClick = () =>
        {
            var value = float.Parse(noiseCutoffLIB.Input.Text);

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.NoiseCutoff = value;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        depthLIB = ui.CreateLIBTrio("Water Depth", "lstwo.WaterCustomizer.WaterDepth");
        depthLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        depthLIB.Button.OnClick = () =>
        {
            var value = float.Parse(depthLIB.Input.Text);

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.WaterDepth = value;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);

        deepDepthLIB = ui.CreateLIBTrio("Water Deep Depth", "lstwo.WaterCustomizer.WaterDeepDepth");
        deepDepthLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        deepDepthLIB.Button.OnClick = () =>
        {
            var value = float.Parse(deepDepthLIB.Input.Text);

            foreach (var materialOverride in waterMaterials)
            {
                materialOverride.OverrideValues.WaterDeepDepth = value;
                materialOverride.RefreshOverride();
            }
        };

        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
        var renderers = Object.FindObjectsOfType<Renderer>();

        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader.name != "WobblyLife/Custom/WobblyWater" || waterMaterials.Any(materialOverride => materialOverride.Material == mat))
                {
                    continue;
                }

                waterMaterials.Add(new(mat));
            }
        }

        var waterMaterial = waterMaterials[0];

        waterColorLIB.Input.Text = waterMaterial.OverrideValues.Color.ToHex();
        foamColorLIB.Input.Text = waterMaterial.OverrideValues.FoamColor.ToHex();
        edgeColorLIB.Input.Text = waterMaterial.OverrideValues.EdgeColor.ToHex();
        deepWaterColorLIB.Input.Text = waterMaterial.OverrideValues.DeepColor.ToHex();
        foamDistanceLIB.Input.Text = waterMaterial.OverrideValues.FoamDistance.ToString();
        waveMovementLIB.Input.Text = waterMaterial.OverrideValues.WaveMovement.ToString();
        noiseCutoffLIB.Input.Text = waterMaterial.OverrideValues.NoiseCutoff.ToString();
        depthLIB.Input.Text = waterMaterial.OverrideValues.WaterDepth.ToString();
        deepDepthLIB.Input.Text = waterMaterial.OverrideValues.WaterDeepDepth.ToString();
    }

    public override string Name => "Water Customizer";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ClientHacksTab;

    private static List<WaterMaterialOverride> waterMaterials = new();

    private HacksUIHelper.LIBTrio waterColorLIB;
    private HacksUIHelper.LIBTrio foamColorLIB;
    private HacksUIHelper.LIBTrio edgeColorLIB;
    private HacksUIHelper.LIBTrio deepWaterColorLIB;
    private HacksUIHelper.LIBTrio foamDistanceLIB;
    private HacksUIHelper.LIBTrio waveMovementLIB;
    private HacksUIHelper.LIBTrio noiseCutoffLIB;
    private HacksUIHelper.LIBTrio depthLIB;
    private HacksUIHelper.LIBTrio deepDepthLIB;

    private class WaterMaterialOverride
    {
        public Material Material;
        public WaterMaterialValues OverrideValues;
        public bool ShouldOverride;

        private WaterMaterialValues originalValues;

        public WaterMaterialOverride(Material material)
        {
            Material = material;

            originalValues = new(material);
            OverrideValues = new(material);
        }

        public void RefreshOverride()
        {
            if (!Material)
            {
                return;
            }

            if (ShouldOverride)
            {
                OverrideValues.ApplyValues(Material);
            }
            else
            {
                originalValues.ApplyValues(Material);
            }
        }
    }

    private struct WaterMaterialValues
    {
        public Color Color;
        public Color FoamColor;
        public Color EdgeColor;
        public Color DeepColor;
        public float FoamDistance;
        public float WaveMovement;
        public float NoiseCutoff;
        public float WaterDepth;
        public float WaterDeepDepth;

        public WaterMaterialValues(Material material)
        {
            Color = material.GetColor("_WaterColour");
            FoamColor = material.GetColor("_FoamColour");
            EdgeColor = material.GetColor("_EdgeColour");
            DeepColor = material.GetColor("_DeepWaterColour");
            FoamDistance = material.GetFloat("_FoamDistance");
            WaveMovement = material.GetFloat("_WaveMovement");
            NoiseCutoff = material.GetFloat("_SurfaceNoiseCutoff");
            WaterDepth = material.GetFloat("_WaterDepth");
            WaterDeepDepth = material.GetFloat("_WaterDeepDepth");
        }

        public void ApplyValues(Material material)
        {
            material.SetColor("_WaterColour", Color);
            material.SetColor("_FoamColour", FoamColor);
            material.SetColor("_EdgeColour", EdgeColor);
            material.SetColor("_DeepColour", DeepColor);
            material.SetFloat("_FoamDistance", FoamDistance);
            material.SetFloat("_WaveMovement", WaveMovement);
            material.SetFloat("_SurfaceNoiseCutoff", NoiseCutoff);
            material.SetFloat("_WaterDepth", WaterDepth);
            material.SetFloat("_WaterDeepDepth", WaterDeepDepth);
        }
    }
}