using System.Collections.Generic;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace lstwoMODS_WobblyLife.Mods;

public class PostProcessingModifier : BaseMod
{
    public const string HarmonyId = "lstwo.lstwoMODS_WobblyLife.PostProcessingModifier";

    private const float VolumePriority = 1000f;

    private const string HostName = "lstwoMODS_PostProcessing";

    public enum GradingModeOverride
    {
        Vanilla,
        LowDefinitionRange,
        HighDefinitionRange,
    }

    public enum TonemapperOverride
    {
        Vanilla,
        None,
        Neutral,
        ACES,
    }



    [ModSetting(
        Label = "Disable Water Post Processing",
        SeparatorText = "Water",
        Description = "Stops GameplayCameraWater from handing its profile to the post process "
                    + "volume, so the camera keeps the normal surface look while submerged. Toxic "
                    + "water rides the same code path, so its tint goes away too. The underwater "
                    + "ambience and the water's character colour override are on separate paths "
                    + "and are unaffected.",
        Order = 10)]
    public static readonly Ref<bool> DisableWaterPostProcessing = new();

    [ModSetting(
        Label = "Force Water Post Processing",
        Description = "Keeps the last water profile the camera entered applied everywhere, so you "
                    + "can walk around town with the underwater tint on. Needs the camera to have "
                    + "dipped under water once this session so there is a profile to remember. "
                    + "Ignored while Disable Water Post Processing is on.",
        Order = 20)]
    public static readonly Ref<bool> ForceWaterPostProcessing = new();



    [ModSetting(
        Label = "Enable Overrides",
        SeparatorText = "Overrides",
        Description = "Adds a high priority global post process volume to every gameplay camera. "
                    + "Only the groups you switch on below are overridden, everything else falls "
                    + "through to the game's own profile. Note that enabling a group takes that "
                    + "effect over completely: the sliders start at neutral values, not at "
                    + "whatever the game shipped.",
        Order = 100)]
    public static readonly Ref<bool> Enabled = new();



    [ModSetting(
        Label = "Color Grading",
        SeparatorText = "Color Grading",
        Description = "Change how the colors of the game look to create effects like sepia, night vision, etc.",
        Order = 200)]
    public static readonly Ref<bool> GradingEnabled = new();

    [ModSetting(
        Label = "Grading Mode",
        Description = "Vanilla leaves the game's pipeline choice alone, which is the safe default. "
                    + "LDR enables the Brightness slider, HDR enables Exposure and the Tonemapper. ",
        Order = 210)]
    public static readonly Ref<GradingModeOverride> ColorGradingMode = new(GradingModeOverride.Vanilla);

    [ModSetting(
        Label = "Tonemapper",
        Description = "Only read by the HDR grading pipeline. ACES is the filmic curve, "
                    + "Neutral is the flat one.",
        Order = 220)]
    public static readonly Ref<TonemapperOverride> Tonemapping = new(TonemapperOverride.Vanilla);

    [ModSetting(
        Label = "Exposure",
        Min = -5f, Max = 5f, Format = "%.2f EV",
        Description = "Post-exposure in EV. HDR grading mode only.",
        Order = 230)]
    public static readonly Ref<float> Exposure = new(0f);

    [ModSetting(
        Label = "Brightness",
        Min = -100f, Max = 100f, Format = "%.0f",
        Description = "LDR grading mode only. Use Exposure instead in HDR mode.",
        Order = 240)]
    public static readonly Ref<float> Brightness = new(0f);

    [ModSetting(
        Label = "Contrast",
        Min = -100f, Max = 100f, Format = "%.0f",
        Order = 250)]
    public static readonly Ref<float> Contrast = new(0f);

    [ModSetting(
        Label = "Saturation",
        Min = -100f, Max = 100f, Format = "%.0f",
        Description = "-100 is fully greyscale.",
        Order = 260)]
    public static readonly Ref<float> Saturation = new(0f);

    [ModSetting(
        Label = "Hue Shift",
        Min = -180f, Max = 180f, Format = "%.0f deg",
        Description = "Rotates every colour around the wheel.",
        Order = 270)]
    public static readonly Ref<float> HueShift = new(0f);

    [ModSetting(
        Label = "Temperature",
        Min = -100f, Max = 100f, Format = "%.0f",
        Description = "Negative is cold and blue, positive is warm and orange.",
        Order = 280)]
    public static readonly Ref<float> Temperature = new(0f);

    [ModSetting(
        Label = "Tint",
        Min = -100f, Max = 100f, Format = "%.0f",
        Description = "The green to magenta axis, perpendicular to Temperature.",
        Order = 290)]
    public static readonly Ref<float> Tint = new(0f);

    [ModSetting(
        Label = "Color Filter",
        Widget = WidgetType.Color3,
        Description = "Multiplied over the whole image. White is neutral.",
        Order = 300)]
    public static readonly Ref<Col> ColorFilter = new(Color.white);



    [ModSetting(
        Label = "Bloom",
        SeparatorText = "Bloom",
        Description = "Blurs bright surfaces to make light look brighter",
        Order = 400)]
    public static readonly Ref<bool> BloomEnabled = new();

    [ModSetting(
        Label = "Intensity",
        Min = 0f, Max = 20f, Format = "%.2f",
        Order = 410)]
    public static readonly Ref<float> BloomIntensity = new(1f);

    [ModSetting(
        Label = "Threshold",
        Min = 0f, Max = 5f, Format = "%.2f",
        Description = "How bright a pixel has to be before it blooms. Drop this toward 0 and the "
                    + "whole screen glows.",
        Order = 420)]
    public static readonly Ref<float> BloomThreshold = new(1f);

    [ModSetting(
        Label = "Soft Knee",
        Min = 0f, Max = 1f, Format = "%.2f",
        Description = "How gradually pixels fade into the threshold. 0 is a hard cutoff.",
        Order = 430)]
    public static readonly Ref<float> BloomSoftKnee = new(0.5f);

    [ModSetting(
        Label = "Diffusion",
        Min = 1f, Max = 10f, Format = "%.2f",
        Description = "How far the glow spreads.",
        Order = 440)]
    public static readonly Ref<float> BloomDiffusion = new(7f);

    [ModSetting(
        Label = "Anamorphic Ratio",
        Min = -1f, Max = 1f, Format = "%.2f",
        Description = "Squashes the glow. Positive stretches it vertically, negative gives the "
                    + "stretches horizontal",
        Order = 450)]
    public static readonly Ref<float> BloomAnamorphicRatio = new(0f);

    [ModSetting(
        Label = "Tint",
        Widget = WidgetType.Color3,
        Description = "Tints the glow without touching the rest of the image.",
        Order = 460)]
    public static readonly Ref<Col> BloomColor = new(Color.white);



    [ModSetting(
        Label = "Vignette",
        SeparatorText = "Vignette",
        Description = "Darkens the screen edges.",
        Order = 500)]
    public static readonly Ref<bool> VignetteEnabled = new();

    [ModSetting(
        Label = "Intensity",
        Min = 0f, Max = 1f, Format = "%.2f",
        Order = 510)]
    public static readonly Ref<float> VignetteIntensity = new(0.45f);

    [ModSetting(
        Label = "Smoothness",
        Min = 0.01f, Max = 1f, Format = "%.2f",
        Description = "How soft the falloff into the darkened edge is.",
        Order = 520)]
    public static readonly Ref<float> VignetteSmoothness = new(0.2f);

    [ModSetting(
        Label = "Roundness",
        Min = 0f, Max = 1f, Format = "%.2f",
        Description = "1 is a circle, 0 follows the screen aspect ratio.",
        Order = 530)]
    public static readonly Ref<float> VignetteRoundness = new(1f);

    [ModSetting(
        Label = "Color",
        Widget = WidgetType.Color3,
        Order = 540)]
    public static readonly Ref<Col> VignetteColor = new(Color.black);



    [ModSetting(
        Label = "Depth of Field",
        SeparatorText = "Depth of Field",
        Description = "Blurs objects too close or too far from the camera.",
        Order = 600)]
    public static readonly Ref<bool> DepthOfFieldEnabled = new();

    [ModSetting(
        Label = "Focus Distance",
        Min = 0.1f, Max = 100f, Format = "%.2f m",
        Order = 610)]
    public static readonly Ref<float> FocusDistance = new(10f);

    [ModSetting(
        Label = "Aperture",
        Min = 0.05f, Max = 32f, Format = "f/%.2f",
        Description = "Smaller f-number means a shallower, blurrier depth of field.",
        Order = 620)]
    public static readonly Ref<float> Aperture = new(5.6f);

    [ModSetting(
        Label = "Focal Length",
        Min = 1f, Max = 300f, Format = "%.0f mm",
        Description = "Longer lenses compress the scene and blur the background harder.",
        Order = 630)]
    public static readonly Ref<float> FocalLength = new(50f);

    [ModSetting(
        Label = "Bokeh Quality",
        Description = "Blur kernel size. Larger costs more but smooths out the bokeh.",
        Order = 640)]
    public static readonly Ref<KernelSize> BokehQuality = new(KernelSize.Medium);



    [ModSetting(
        Label = "Lens Distortion",
        SeparatorText = "Lens Distortion",
        Description = "Distorts the edges of the view.",
        Order = 700)]
    public static readonly Ref<bool> LensDistortionEnabled = new();

    [ModSetting(
        Label = "Intensity",
        Min = -100f, Max = 100f, Format = "%.0f",
        Description = "Negative pinches inward, positive bulges outward.",
        Order = 710)]
    public static readonly Ref<float> LensDistortionIntensity = new(0f);

    [ModSetting(
        Label = "Scale",
        Min = 0.01f, Max = 5f, Format = "%.2f",
        Description = "Zooms the distorted image. Useful for hiding the stretched screen edges a "
                    + "strong negative intensity exposes.",
        Order = 720)]
    public static readonly Ref<float> LensDistortionScale = new(1f);



    [ModSetting(
        Label = "Chromatic Aberration",
        SeparatorText = "Chromatic Aberration",
        Description = "Splits colour channels toward the screen edges.",
        Order = 800)]
    public static readonly Ref<bool> ChromaticAberrationEnabled = new();

    [ModSetting(
        Label = "Intensity",
        Min = 0f, Max = 1f, Format = "%.2f",
        Order = 810)]
    public static readonly Ref<float> ChromaticAberrationIntensity = new(0.25f);



    [ModSetting(
        Label = "Film Grain",
        SeparatorText = "Film Grain",
        Order = 900)]
    public static readonly Ref<bool> GrainEnabled = new();

    [ModSetting(
        Label = "Intensity",
        Min = 0f, Max = 1f, Format = "%.2f",
        Order = 910)]
    public static readonly Ref<float> GrainIntensity = new(0.5f);

    [ModSetting(
        Label = "Size",
        Min = 0.3f, Max = 3f, Format = "%.2f",
        Order = 920)]
    public static readonly Ref<float> GrainSize = new(1f);

    [ModSetting(
        Label = "Colored",
        Description = "Off gives monochrome grain, which usually reads as more filmic.",
        Order = 930)]
    public static readonly Ref<bool> GrainColored = new(true);



    [ModSetting(
        Label = "Motion Blur",
        SeparatorText = "Motion Blur",
        Description = "Blurs along per-pixel motion vectors.",
        Order = 1000)]
    public static readonly Ref<bool> MotionBlurEnabled = new();

    [ModSetting(
        Label = "Shutter Angle",
        Min = 0f, Max = 360f, Format = "%.0f deg",
        Description = "How long the virtual shutter stays open. 0 is no blur, 360 is a full frame "
                    + "of smear.",
        Order = 1010)]
    public static readonly Ref<float> ShutterAngle = new(270f);

    [ModSetting(
        Label = "Sample Count",
        Min = 4, Max = 32, Format = "%d",
        Description = "More samples means smoother blur and more cost.",
        Order = 1020)]
    public static readonly Ref<int> MotionBlurSamples = new(10);


    public override string Name => "Post Processing Modifier";

    public override string Description =>
        "Colour grading, bloom, vignette, depth of field, lens distortion, chromatic aberration, "
        + "grain and motion blur, plus control over the underwater tint.";

    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;
    
    private static readonly List<CameraBinding> Bindings = new();

    private static PostProcessingProfileData lastWaterProfile;

    protected override void OnStaticInit()
    {
        BindData(DisableWaterPostProcessing, nameof(DisableWaterPostProcessing), false);
        BindData(ForceWaterPostProcessing, nameof(ForceWaterPostProcessing), false);

        BindData(Enabled, nameof(Enabled), false);

        BindData(GradingEnabled, nameof(GradingEnabled), false);
        BindData(ColorGradingMode, nameof(ColorGradingMode), GradingModeOverride.Vanilla);
        BindData(Tonemapping, nameof(Tonemapping), TonemapperOverride.Vanilla);
        BindData(Exposure, nameof(Exposure), 0f);
        BindData(Brightness, nameof(Brightness), 0f);
        BindData(Contrast, nameof(Contrast), 0f);
        BindData(Saturation, nameof(Saturation), 0f);
        BindData(HueShift, nameof(HueShift), 0f);
        BindData(Temperature, nameof(Temperature), 0f);
        BindData(Tint, nameof(Tint), 0f);
        BindData(ColorFilter, nameof(ColorFilter), Color.white);

        BindData(BloomEnabled, nameof(BloomEnabled), false);
        BindData(BloomIntensity, nameof(BloomIntensity), 1f);
        BindData(BloomThreshold, nameof(BloomThreshold), 1f);
        BindData(BloomSoftKnee, nameof(BloomSoftKnee), 0.5f);
        BindData(BloomDiffusion, nameof(BloomDiffusion), 7f);
        BindData(BloomAnamorphicRatio, nameof(BloomAnamorphicRatio), 0f);
        BindData(BloomColor, nameof(BloomColor), Color.white);

        BindData(VignetteEnabled, nameof(VignetteEnabled), false);
        BindData(VignetteIntensity, nameof(VignetteIntensity), 0.45f);
        BindData(VignetteSmoothness, nameof(VignetteSmoothness), 0.2f);
        BindData(VignetteRoundness, nameof(VignetteRoundness), 1f);
        BindData(VignetteColor, nameof(VignetteColor), Color.black);

        BindData(DepthOfFieldEnabled, nameof(DepthOfFieldEnabled), false);
        BindData(FocusDistance, nameof(FocusDistance), 10f);
        BindData(Aperture, nameof(Aperture), 5.6f);
        BindData(FocalLength, nameof(FocalLength), 50f);
        BindData(BokehQuality, nameof(BokehQuality), KernelSize.Medium);

        BindData(LensDistortionEnabled, nameof(LensDistortionEnabled), false);
        BindData(LensDistortionIntensity, nameof(LensDistortionIntensity), 0f);
        BindData(LensDistortionScale, nameof(LensDistortionScale), 1f);

        BindData(ChromaticAberrationEnabled, nameof(ChromaticAberrationEnabled), false);
        BindData(ChromaticAberrationIntensity, nameof(ChromaticAberrationIntensity), 0.25f);

        BindData(GrainEnabled, nameof(GrainEnabled), false);
        BindData(GrainIntensity, nameof(GrainIntensity), 0.5f);
        BindData(GrainSize, nameof(GrainSize), 1f);
        BindData(GrainColored, nameof(GrainColored), true);

        BindData(MotionBlurEnabled, nameof(MotionBlurEnabled), false);
        BindData(ShutterAngle, nameof(ShutterAngle), 270f);
        BindData(MotionBlurSamples, nameof(MotionBlurSamples), 10);

        new Harmony(HarmonyId).PatchAll(typeof(Patches));
    }

    public override void Update()
    {
        PruneBindings();

        if (!Enabled.Value)
        {
            ReleaseAll();
            return;
        }

        if (!UnitySingleton<GameplayCameraManager>.InstanceExists)
        {
            return;
        }

        var manager = UnitySingleton<GameplayCameraManager>.Instance;

        if (!manager)
        {
            return;
        }

        manager.IterateGameplayCameras(camera =>
        {
            var binding = Find(camera) ?? Create(camera);

            if (binding != null)
            {
                Apply(binding);
            }
        });
    }



    private static CameraBinding Find(GameplayCamera camera)
    {
        foreach (var binding in Bindings)
        {
            if (binding.Camera == camera)
            {
                return binding;
            }
        }

        return null;
    }

    private static CameraBinding Create(GameplayCamera camera)
    {
        if (!camera)
        {
            return null;
        }

        var layer = camera.GetComponent<PostProcessLayer>();

        var host = new GameObject(HostName);
        host.transform.SetParent(camera.transform, false);
        host.layer = ResolveLayer(camera, layer);

        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

        // Keeps Resources.UnloadUnusedAssets from collecting it across scene loads.
        profile.hideFlags = HideFlags.HideAndDontSave;

        var volume = host.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = VolumePriority;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        var binding = new CameraBinding
        {
            Camera = camera,
            Layer = layer,
            Host = host,
            Volume = volume,
            Profile = profile,
            Grading = profile.AddSettings<ColorGrading>(),
            BloomFx = profile.AddSettings<Bloom>(),
            VignetteFx = profile.AddSettings<Vignette>(),
            DepthOfFieldFx = profile.AddSettings<DepthOfField>(),
            LensDistortionFx = profile.AddSettings<LensDistortion>(),
            ChromaticAberrationFx = profile.AddSettings<ChromaticAberration>(),
            GrainFx = profile.AddSettings<Grain>(),
            MotionBlurFx = profile.AddSettings<MotionBlur>(),
        };

        Bindings.Add(binding);
        return binding;
    }

    private static int ResolveLayer(GameplayCamera camera, PostProcessLayer layer)
    {
        var cameraLayer = camera.gameObject.layer;
        var mask = layer ? layer.volumeLayer.value : 0;

        if (mask == 0 || (mask & (1 << cameraLayer)) != 0)
        {
            return cameraLayer;
        }

        for (var i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                return i;
            }
        }

        return cameraLayer;
    }

    private static void PruneBindings()
    {
        for (var i = Bindings.Count - 1; i >= 0; i--)
        {
            var binding = Bindings[i];

            if (!binding.Camera || !binding.Volume || !binding.Host)
            {
                DestroyBinding(binding);
                Bindings.RemoveAt(i);
            }
        }
    }

    private static void ReleaseAll()
    {
        if (Bindings.Count == 0)
        {
            return;
        }

        foreach (var binding in Bindings)
        {
            DestroyBinding(binding);
        }

        Bindings.Clear();
    }

    private static void DestroyBinding(CameraBinding binding)
    {
        if (binding.Host)
        {
            Object.Destroy(binding.Host);
        }

        if (binding.Profile)
        {
            // Created with HideAndDontSave, so nothing else will ever clean it up.
            Object.Destroy(binding.Profile);
        }
    }




    private static void Apply(CameraBinding binding)
    {
        var layer = ResolveLayer(binding.Camera, binding.Layer);

        if (binding.Host.layer != layer)
        {
            binding.Host.layer = layer;
        }

        ApplyGrading(binding.Grading);
        ApplyBloom(binding.BloomFx);
        ApplyVignette(binding.VignetteFx);
        ApplyDepthOfField(binding.DepthOfFieldFx);
        ApplyLensDistortion(binding.LensDistortionFx);
        ApplyChromaticAberration(binding.ChromaticAberrationFx);
        ApplyGrain(binding.GrainFx);
        ApplyMotionBlur(binding.MotionBlurFx);
    }

    private static void ApplyGrading(ColorGrading fx)
    {
        var on = GradingEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        var mode = ColorGradingMode.Value;
        fx.gradingMode.overrideState = on && mode != GradingModeOverride.Vanilla;

        if (fx.gradingMode.overrideState)
        {
            fx.gradingMode.value = mode == GradingModeOverride.LowDefinitionRange
                ? GradingMode.LowDefinitionRange
                : GradingMode.HighDefinitionRange;
        }

        var tonemapper = Tonemapping.Value;
        fx.tonemapper.overrideState = on && tonemapper != TonemapperOverride.Vanilla;

        if (fx.tonemapper.overrideState)
        {
            fx.tonemapper.value = tonemapper switch
            {
                TonemapperOverride.None    => Tonemapper.None,
                TonemapperOverride.Neutral => Tonemapper.Neutral,
                _                          => Tonemapper.ACES,
            };
        }

        Set(fx.postExposure, on, Exposure.Value);
        Set(fx.brightness, on, Brightness.Value);
        Set(fx.contrast, on, Contrast.Value);
        Set(fx.saturation, on, Saturation.Value);
        Set(fx.hueShift, on, HueShift.Value);
        Set(fx.temperature, on, Temperature.Value);
        Set(fx.tint, on, Tint.Value);
        Set(fx.colorFilter, on, ColorFilter.Value);
    }

    private static void ApplyBloom(Bloom fx)
    {
        var on = BloomEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.intensity, on, BloomIntensity.Value);
        Set(fx.threshold, on, BloomThreshold.Value);
        Set(fx.softKnee, on, BloomSoftKnee.Value);
        Set(fx.diffusion, on, BloomDiffusion.Value);
        Set(fx.anamorphicRatio, on, BloomAnamorphicRatio.Value);
        Set(fx.color, on, BloomColor.Value);
    }

    private static void ApplyVignette(Vignette fx)
    {
        var on = VignetteEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.mode, on, VignetteMode.Classic);
        Set(fx.intensity, on, VignetteIntensity.Value);
        Set(fx.smoothness, on, VignetteSmoothness.Value);
        Set(fx.roundness, on, VignetteRoundness.Value);
        Set(fx.color, on, VignetteColor.Value);
    }

    private static void ApplyDepthOfField(DepthOfField fx)
    {
        var on = DepthOfFieldEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.focusDistance, on, FocusDistance.Value);
        Set(fx.aperture, on, Aperture.Value);
        Set(fx.focalLength, on, FocalLength.Value);
        Set(fx.kernelSize, on, BokehQuality.Value);
    }

    private static void ApplyLensDistortion(LensDistortion fx)
    {
        var on = LensDistortionEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.intensity, on, LensDistortionIntensity.Value);
        Set(fx.scale, on, LensDistortionScale.Value);
    }

    private static void ApplyChromaticAberration(ChromaticAberration fx)
    {
        var on = ChromaticAberrationEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.intensity, on, ChromaticAberrationIntensity.Value);
    }

    private static void ApplyGrain(Grain fx)
    {
        var on = GrainEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.intensity, on, GrainIntensity.Value);
        Set(fx.size, on, GrainSize.Value);
        Set(fx.colored, on, GrainColored.Value);
    }

    private static void ApplyMotionBlur(MotionBlur fx)
    {
        var on = MotionBlurEnabled.Value;

        fx.enabled.value = true;
        fx.enabled.overrideState = on;

        Set(fx.shutterAngle, on, ShutterAngle.Value);
        Set(fx.sampleCount, on, MotionBlurSamples.Value);
    }

    private static void Set<T>(ParameterOverride<T> parameter, bool on, T value)
    {
        parameter.overrideState = on;

        if (on)
        {
            parameter.value = value;
        }
    }


    public class Patches
    {
        [HarmonyPatch(typeof(GameplayCameraWater), nameof(GameplayCameraWater.GetWaterPostProcessing))]
        [HarmonyPostfix]
        public static void GetWaterPostProcessingPostfix(ref PostProcessingProfileData __result)
        {
            if (__result)
            {
                lastWaterProfile = __result;
            }

            if (DisableWaterPostProcessing.Value)
            {
                __result = null;
                return;
            }

            if (ForceWaterPostProcessing.Value && !__result && lastWaterProfile)
            {
                __result = lastWaterProfile;
            }
        }
    }

    private class CameraBinding
    {
        public GameplayCamera Camera;
        public PostProcessLayer Layer;
        public GameObject Host;
        public PostProcessVolume Volume;
        public PostProcessProfile Profile;

        public ColorGrading Grading;
        public Bloom BloomFx;
        public Vignette VignetteFx;
        public DepthOfField DepthOfFieldFx;
        public LensDistortion LensDistortionFx;
        public ChromaticAberration ChromaticAberrationFx;
        public Grain GrainFx;
        public MotionBlur MotionBlurFx;
    }
}
