using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class WaterCustomizer : BaseMod
{
    public override string Name => "Water Customizer";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    private static List<WaterMaterialOverride> waterMaterials = new();

    [ModSetting(Order = 10)]
    public bool OverrideWaterMaterials
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.ShouldOverride = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Order = 20)]
    public Color WaterColor
    {
        get;
        set
        {
            field = value;
            
            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.Color = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Order = 30)]
    public Color FoamColor
    {
        get;
        set
        {
            field = value;
            
            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.FoamColor = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Order = 40)]
    public Color EdgeColor
    {
        get;
        set
        {
            field = value;
            
            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.EdgeColor = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Order = 50)]
    public Color DeepWaterColor
    {
        get;
        set
        {
            field = value;
            
            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.DeepColor = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 60)]
    public float FoamDistance
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.FoamDistance = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 70)]
    public float WaveMovement
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.WaveMovement = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 80)]
    public float NoiseCutoff
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.NoiseCutoff = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 90)]
    public float WaterDepth
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.WaterDepth = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 100)]
    public float WaterDeepDepth
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.WaterDeepDepth = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 110)]
    public float ReflectionMultiplier
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.ReflectionMultiplier = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 120)]
    public float ReflectionNoiseScale
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.ReflectionNoiseScale = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 130)]
    public float ReflectionNoisePan
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.ReflectionNoisePan = value;
                waterOverride.RefreshOverride();
            }
        }
    }

    [ModSetting(Speed = 0.05f, Order = 140)]
    public float ReflectionNoiseStrength
    {
        get;
        set
        {
            field = value;

            foreach (var waterOverride in waterMaterials)
            {
                waterOverride.OverrideValues.ReflectionNoiseStrength = value;
                waterOverride.RefreshOverride();
            }
        }
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

        if (waterMaterials.Count == 0)
        {
            base.RefreshUI();
            return;
        }

        var waterMaterial = waterMaterials[0];

        WaterColor = waterMaterial.OverrideValues.Color;
        FoamColor = waterMaterial.OverrideValues.FoamColor;
        EdgeColor = waterMaterial.OverrideValues.EdgeColor;
        DeepWaterColor = waterMaterial.OverrideValues.DeepColor;
        FoamDistance = waterMaterial.OverrideValues.FoamDistance;
        WaveMovement = waterMaterial.OverrideValues.WaveMovement;
        NoiseCutoff = waterMaterial.OverrideValues.NoiseCutoff;
        WaterDepth = waterMaterial.OverrideValues.WaterDepth;
        WaterDeepDepth = waterMaterial.OverrideValues.WaterDeepDepth;
        ReflectionMultiplier = waterMaterial.OverrideValues.ReflectionMultiplier;
        ReflectionNoiseScale = waterMaterial.OverrideValues.ReflectionNoiseScale;
        ReflectionNoisePan = waterMaterial.OverrideValues.ReflectionNoisePan;
        ReflectionNoiseStrength = waterMaterial.OverrideValues.ReflectionNoiseStrength;
        
        base.RefreshUI();
    }
    
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
        public float ReflectionMultiplier;
        public float ReflectionNoiseScale;
        public float ReflectionNoisePan;
        public float ReflectionNoiseStrength;

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
            ReflectionMultiplier = material.GetFloat("_ReflectionMul");
            ReflectionNoiseScale = material.GetFloat("_ReflectionNoiseScale");
            ReflectionNoisePan = material.GetFloat("_ReflectionNoisePan");
            ReflectionNoiseStrength = material.GetFloat("_ReflectionNoiseStrength");
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
            material.SetFloat("_ReflectionMul", ReflectionMultiplier);
            material.SetFloat("_ReflectionNoiseScale", ReflectionNoiseScale);
            material.SetFloat("_ReflectionNoisePan", ReflectionNoisePan);
            material.SetFloat("_ReflectionNoiseStrength", ReflectionNoiseStrength);
        }
    }
}