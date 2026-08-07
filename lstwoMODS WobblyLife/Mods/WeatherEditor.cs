using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;
using UnityExplorer;
using Random = UnityEngine.Random;

namespace lstwoMODS_WobblyLife.Mods;

public class WeatherEditor : BaseMod
{
    public override string Name => "Weather Editor";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    private WeatherData[] weatherDatas;
    private Ref<string[]> weatherComboItems = new();
    private Ref<int> weatherComboSelection = new();

    private Ref<string> weatherTitleInput = new("");
    private Ref<float> fogDistanceInput = new();
    private Ref<float> pickWeightInput = new();
    private Ref<float> transitionTimeInput = new();
    private Ref<bool> isStormToggle = new();

    private Ref<int> rainStateDropdown = new();

    private Ref<Vector2> lightningStrikeFrequency = new();
    private Ref<Vector2> lightningSkyFrequency = new();
    private Ref<float> chanceToGetHit = new();
    
    [ModSetting(ShowInUI = false, Label = "Lock Weather",
        Description = "Hold the current weather indefinitely by pushing its expiry out of reach. Host only.")]
    public static Ref<bool> LockWeather = new();

    private Ref<float> minWeatherTimeInput = new();
    private Ref<float> maxWeatherTimeInput = new();

    private WeatherData customWeather;

    /// <summary>Lower bound on strike frequency: the game rolls <c>Random.Range(min, max)</c> as the
    /// delay until the next strike, so 0 spawns a lightning particle every single frame.</summary>
    private const float MinStrikeFrequency = 0.1f;

    private static BaseParticle[] cachedGroundParticles;

    protected override void OnStaticInit()
    {
        base.OnStaticInit();
        
        new Harmony(Id).PatchAll(typeof(Patches));
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("Change Weather", "Change Weather"),
            
            new Combo("Select Weather", [], 0, index =>
            {
                if (index < 0 || index >= weatherDatas.Length) return;

                var data = weatherDatas[index];
                RefreshInputs(data);
                
            }).WithItems(weatherComboItems).WithSelectedIndex(weatherComboSelection),
            
            new Button("Change Weather", () => ApplyWeatherIndex(weatherComboSelection.Value))
                .WithContentWidth(),
            
            new Checkbox("Lock Weather").WithValue(LockWeather),

            new TreeNode("Weather Change Speed", "Weather Change Speed",

                new DragFloat("Min Seconds Between Changes", 240, 1, 1, float.MaxValue, onValueChanged: SetMinWeatherTime)
                    .WithTooltip("Shortest time a weather can last before the game randomly picks a new one (server only).")
                    .WithValue(minWeatherTimeInput),
                new DragFloat("Max Seconds Between Changes", 600, 1, 1, float.MaxValue, onValueChanged: SetMaxWeatherTime)
                    .WithTooltip("Longest time a weather can last before the game randomly picks a new one (server only).")
                    .WithValue(maxWeatherTimeInput)
            ),

            new SeparatorText("Edit / Create Weather", "Edit / Create Weather"),
            new TreeNode("Base Weather Info", "Base Weather Info",
                
                new InputText("Weather Title").WithValue(weatherTitleInput),
                new DragFloat("Pick Chance", 0, 0.001f, 0, 1, "%.4f").WithValue(pickWeightInput),
                new DragFloat("Transition Time", 0, 0.01f, 1).WithValue(transitionTimeInput),
                new DragFloat("Fog Distance", 0, .1f).WithValue(fogDistanceInput)
            ),
            
            new TreeNode("Rain Info", "Rain Info",
                
                new Combo("Rain State", Enum.GetNames(typeof(WeatherIntensity))).WithSelectedIndex(rainStateDropdown),
                new Checkbox("Is Storm?").WithValue(isStormToggle)
            ),
            
            new TreeNode("Thunder Info", "Thunder Info",

                new DragFloat2("Lightning Strike Frequency", default, 0.01f, 0, float.MaxValue)
                    .WithTooltip("Min / max seconds in between lightning strikes. Needs \"Is Storm?\" on, only the host spawns strikes, and they only start once the transition has finished.")
                    .WithValue(lightningStrikeFrequency),
                new DragFloat2("Lightning Sky Frequency", default, 0.01f, 0, float.MaxValue)
                    .WithTooltip("Unused by the game: it rolls a sky lightning timer on every weather change but never fires one.")
                    .WithValue(lightningSkyFrequency),
                new DragFloat("Chance to Get Hit", 0, .001f, 0, 1)
                    .WithTooltip("Chance a strike lands directly on a player instead of somewhere within 50m of them.")
                    .WithValue(chanceToGetHit),

                new Button("Strike Lightning Now", StrikeLightningNow)
                    .WithTooltip("Fire one strike at you right now, ignoring the storm flag, the transition and the timer. If this is invisible the particles are missing, not the timing.")
                    .WithContentWidth()
            ),
            
            new HStack("Apply Buttons",
                
                new Button("Apply Custom Weather", () =>
                {
                    if (!WeatherSystem.InstanceExists) return;

                    var index = customWeather == null
                        ? -1
                        : Array.IndexOf(WeatherSystem.Instance.GetAllWeatherData(), customWeather);

                    if (index < 0)
                    {
                        customWeather = MakeWeatherData();
                        customWeather.data.title = "Custom";
                        AddNewWeather(customWeather);
                        index = WeatherSystem.Instance.GetAllWeatherData().Length - 1;
                    }
                    else
                    {
                        weatherTitleInput.Value = "Custom";
                        customWeather = SetWeather(index) ?? customWeather;
                    }

                    RefreshWeatherDatas();
                    ApplyWeatherIndex(index);
                }),

                new Button("Add Weather to List", () =>
                {
                    AddNewWeather(MakeWeatherData());
                    RefreshWeatherDatas();
                }),

                new Button("Overwrite Selected Weather", () =>
                {
                    if (SetWeather(weatherComboSelection.Value) == null) return;
                    RefreshWeatherDatas();
                })
                
            ).WithContentWidth(),
            
            new SeparatorText("Separator", ""),
            
            new Button("Inspect \"Weather System\" Component", () =>
            {
                if(WeatherSystem.InstanceExists)
                {
                    InspectorManager.Inspect(WeatherSystem.Instance);
                    UnityExplorer.UI.UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }

    [ModAction(Label = "Set Weather", Description = "Set the current weather to the chosen preset.", ShowInUI = false)]
    public static void SetCurrentWeather(WeatherData weather)
    {
        if (weather != null && WeatherSystem.InstanceExists)
            WeatherSystem.Instance.SetWeather(weather);
    }

    #region Macro surface

    private static WeatherDataScriptableObject CurrentWeatherData()
    {
        if (!WeatherSystem.InstanceExists) return null;

        var current = WeatherSystem.Instance.GetCurrentWeatherData();
        return current != null && current.data != null ? current.data : null;
    }

    private static ThunderData CurrentThunder() => CurrentWeatherData()?.thunderData;

    private static ThunderData EnsureCurrentThunder()
    {
        RepairCurrentThunder();
        return CurrentThunder();
    }

    private static void RerollNextStrike(ThunderData thunder)
    {
        if (!WeatherSystem.InstanceExists) return;

        WeatherSystem.Instance.timeTillNextStrikeLighting =
            Random.Range(thunder.minLightingStrikeFrequency, thunder.maxLightingStrikeFrequency);
    }

    [ModSetting(ShowInUI = false, Label = "Storm",
        Description = "Whether the active weather counts as a storm. Thunder does not run at all without this.")]
    public static bool StormEnabled
    {
        get => WeatherSystem.InstanceExists && WeatherSystem.Instance.IsStorm();
        set
        {
            if (!WeatherSystem.InstanceExists) return;

            var current = WeatherSystem.Instance.GetCurrentWeatherData();
            if (current == null || current.data == null) return;

            current.data.bIsStorm = value;
            if (value) RepairCurrentThunder();
        }
    }

    [ModSetting(ShowInUI = false, Label = "Min Seconds Between Strikes", Speed = 0.1f, Min = MinStrikeFrequency,
        Description = "Shortest gap between lightning strikes on the active weather. Lower = strikes faster.")]
    public static float StrikeFrequencyMin
    {
        get => CurrentThunder()?.minLightingStrikeFrequency ?? 0f;
        set
        {
            var thunder = EnsureCurrentThunder();
            if (thunder == null) return;

            thunder.minLightingStrikeFrequency = Mathf.Max(value, MinStrikeFrequency);
            RerollNextStrike(thunder);
        }
    }

    [ModSetting(ShowInUI = false, Label = "Max Seconds Between Strikes", Speed = 0.1f, Min = MinStrikeFrequency,
        Description = "Longest gap between lightning strikes on the active weather. Lower = strikes faster.")]
    public static float StrikeFrequencyMax
    {
        get => CurrentThunder()?.maxLightingStrikeFrequency ?? 0f;
        set
        {
            var thunder = EnsureCurrentThunder();
            if (thunder == null) return;

            thunder.maxLightingStrikeFrequency = Mathf.Max(value, MinStrikeFrequency);
            RerollNextStrike(thunder);
        }
    }

    [ModSetting(ShowInUI = false, Label = "Chance to Get Hit", Min = 0f, Max = 1f,
        Description = "Chance a strike lands directly on a player instead of somewhere within 50m of them.")]
    public static float StrikeChanceToGetHit
    {
        get => CurrentThunder()?.chanceToGetHit ?? 0f;
        set
        {
            var thunder = EnsureCurrentThunder();
            if (thunder != null) thunder.chanceToGetHit = Mathf.Clamp01(value);
        }
    }

    [ModAction(Label = "Strike Lightning Now", ShowInUI = false,
        Description = "Fire one lightning strike at the local player, ignoring the storm flag, the transition and the strike timer.")]
    public static void MacroStrikeLightning() => StrikeLightningNow();

    [ModAction(Label = "Strike Lightning At Player", ShowInUI = false,
        Description = "Fire one lightning strike at the chosen player. Host only.")]
    public static void MacroStrikeLightningAt(PlayerRef player)
    {
        if (!WeatherSystem.InstanceExists) return;

        var character = player?.Character;
        if (character == null) return;

        var ws = WeatherSystem.Instance;
        if (ws.networkObject == null || !ws.networkObject.IsServer())
        {
            Plugin.LogSource.LogWarning("[Thunder] Only the host spawns lightning strikes.");
            return;
        }

        if (!RepairCurrentThunder())
            Plugin.LogSource.LogWarning("[Thunder] Current weather has no ground lightning particles, this strike will be invisible.");

        ws.ServerLightingStrike(character.GetPlayerPosition());
    }

    
    [ModSetting(ShowInUI = false, Label = "Auto Weather",
        Description = "The game's own switch for rolling a new weather once the current one expires. Off stops the rotation outright, unlike Lock Weather which just pushes the expiry away. Host only.")]
    public static bool AutoWeather
    {
        get => WeatherSystem.InstanceExists && WeatherSystem.Instance.bAuto;
        set { if (WeatherSystem.InstanceExists) WeatherSystem.Instance.bAuto = value; }
    }

    [ModSetting(ShowInUI = false, Label = "Min Seconds Between Weather Changes", Speed = 1f, Min = 1f,
        Description = "Shortest a weather lasts before the game rolls a new one. Host only.")]
    public float MinWeatherTime
    {
        get => WeatherSystem.InstanceExists ? WeatherSystem.Instance.minWeatherTime : 0f;
        set
        {
            if (!WeatherSystem.InstanceExists) return;

            minWeatherTimeInput.Value = Mathf.Max(value, 1f);
            SetMinWeatherTime(minWeatherTimeInput.Value);
        }
    }

    [ModSetting(ShowInUI = false, Label = "Max Seconds Between Weather Changes", Speed = 1f, Min = 1f,
        Description = "Longest a weather lasts before the game rolls a new one. Host only.")]
    public float MaxWeatherTime
    {
        get => WeatherSystem.InstanceExists ? WeatherSystem.Instance.maxWeatherTime : 0f;
        set
        {
            if (!WeatherSystem.InstanceExists) return;

            maxWeatherTimeInput.Value = Mathf.Max(value, 1f);
            SetMaxWeatherTime(maxWeatherTimeInput.Value);
        }
    }

    [ModAction(Label = "Random Weather", ShowInUI = false,
        Description = "Roll a new weather now, weighted by each preset's Pick Chance. Host only.")]
    public static void MacroRandomWeather()
    {
        if (WeatherSystem.InstanceExists) WeatherSystem.Instance.NextRndWeatherType();
    }

    [ModAction(Label = "Reset Weather", ShowInUI = false,
        Description = "Go back to the first preset in the list and restart the change timer. Host only.")]
    public static void MacroResetWeather()
    {
        if (WeatherSystem.InstanceExists) WeatherSystem.Instance.ResetWeather();
    }

    // ---- look of the active weather ----

    [ModSetting(ShowInUI = false, Label = "Fog Distance", Speed = 1f,
        Description = "Fog distance of the active weather. Lower = thicker fog.")]
    public static float CurrentFogDistance
    {
        get => CurrentWeatherData()?.fogEndDistance ?? 0f;
        set
        {
            var data = CurrentWeatherData();
            if (data != null) data.fogEndDistance = value;
        }
    }

    [ModSetting(ShowInUI = false, Label = "Rain Intensity",
        Description = "Rain state of the active weather. Takes effect on the next frame once the weather has finished transitioning.")]
    public static WeatherIntensity CurrentRainIntensity
    {
        get => CurrentWeatherData()?.rainState ?? WeatherIntensity.None;
        set
        {
            var data = CurrentWeatherData();
            if (data != null) data.rainState = value;
        }
    }

    [ModSetting(ShowInUI = false, Label = "Transition Time", Speed = 0.1f, Min = 1f,
        Description = "How long the active weather takes to blend in. Thunder does not start until the transition has finished.")]
    public static float CurrentTransitionTime
    {
        get => CurrentWeatherData()?.transitionTime ?? 0f;
        set
        {
            var data = CurrentWeatherData();
            if (data != null) data.transitionTime = Mathf.Max(value, 1f);
        }
    }

    [ModSetting(ShowInUI = false, Label = "Use Blend Fog Color",
        Description = "Whether the active weather tints the fog with its blend color.")]
    public static bool CurrentUseBlendFogColor
    {
        get => CurrentWeatherData()?.bUseBlendFogColor ?? false;
        set
        {
            var data = CurrentWeatherData();
            if (data != null) data.bUseBlendFogColor = value;
        }
    }

    [ModSetting(ShowInUI = false, Label = "Blend Fog Color",
        Description = "Fog tint of the active weather. Setting it also turns Use Blend Fog Color on, since a color on its own does nothing.")]
    public static Col CurrentBlendFogColor
    {
        get
        {
            var data = CurrentWeatherData();
            return data != null ? (Col)data.blendFogColor : default;
        }
        set
        {
            var data = CurrentWeatherData();
            if (data == null) return;

            data.blendFogColor = value;
            data.bUseBlendFogColor = true;
        }
    }

    // ---- readable state, for If / Switch steps ----
    //
    // These are actions rather than settings because ModRegistry skips get-only properties
    // (see ModRegistry.cs, `if (!isRef && !pi.CanWrite) continue`).

    [ModAction(Label = "Get Weather Title", ShowInUI = false, Description = "Title of the active weather preset.")]
    public static string MacroGetWeatherTitle() => CurrentWeatherData()?.title ?? "";

    [ModAction(Label = "Is Storm", ShowInUI = false, Description = "Whether the active weather counts as a storm.")]
    public static bool MacroIsStorm() => WeatherSystem.InstanceExists && WeatherSystem.Instance.IsStorm();

    [ModAction(Label = "Is Raining", ShowInUI = false, Description = "Whether the active weather has any rain at all.")]
    public static bool MacroIsRaining() => WeatherSystem.InstanceExists && WeatherSystem.Instance.IsRaining();

    [ModAction(Label = "Is Foggy", ShowInUI = false, Description = "Whether the active weather's fog distance is 500 or less, which is what the game calls foggy.")]
    public static bool MacroIsFoggy() => WeatherSystem.InstanceExists && WeatherSystem.Instance.IsFoggy();

    [ModAction(Label = "Get Rain Intensity", ShowInUI = false, Description = "Rain state of the active weather.")]
    public static WeatherIntensity MacroGetRainState()
        => WeatherSystem.InstanceExists ? WeatherSystem.Instance.GetRainState() : WeatherIntensity.None;

    [ModAction(Label = "Seconds Until Weather Change", ShowInUI = false,
        Description = "How long the active weather has left before the game rolls a new one. Huge while Lock Weather is on.")]
    public static float MacroSecondsUntilWeatherChange()
    {
        if (!WeatherSystem.InstanceExists) return 0f;

        var ws = WeatherSystem.Instance;
        var now = HawkNetworkManager.DefaultInstance.GetTimestep();
        var elapsed = now >= ws.changedTime ? (now - ws.changedTime) / 1000f : 0f;

        return Mathf.Max(ws.weatherExpire - elapsed, 0f);
    }

    // ---- per-player fog, the one part of this mod a guest can use ----
    //
    // The override is applied in OnCameraPreCull against RenderSettings, so it is purely local and
    // needs no host. The arrays behind it are indexed by local (split-screen) player id, so only a
    // controller on this machine has a slot.

    [ModAction(Label = "Set Personal Fog Distance", ShowInUI = false,
        Description = "Override the fog distance for one local player only. Nobody else sees it and it needs no host. Blend speed 0 uses the game's default of 2.")]
    public static void MacroSetPersonalFogDistance(PlayerRef player, float distance, float blendSpeed)
    {
        var controller = LocalControllerOf(player);
        if (controller == null) return;

        var ws = WeatherSystem.Instance;

        // Order matters: SetUseOverrideFogDistance resets the blend to 2, so it has to go first.
        ws.SetUseOverrideFogDistance(controller, true);
        ws.SetOverrideFogDistance(controller, distance, blendSpeed > 0f ? blendSpeed : 2f);
    }

    [ModAction(Label = "Clear Personal Fog Distance", ShowInUI = false,
        Description = "Hand the fog back to whatever the weather says.")]
    public static void MacroClearPersonalFogDistance(PlayerRef player)
    {
        var controller = LocalControllerOf(player);
        if (controller != null) WeatherSystem.Instance.SetUseOverrideFogDistance(controller, false);
    }

    /// <summary>The player's controller, but only when it is one of this machine's.</summary>
    private static PlayerController LocalControllerOf(PlayerRef player)
    {
        if (!WeatherSystem.InstanceExists || !GameInstance.InstanceExists) return null;

        var controller = player?.Controller;
        if (controller == null) return null;

        var locals = GameInstance.Instance.GetLocalPlayerControllers();
        return locals != null && locals.Contains(controller) ? controller : null;
    }

    #endregion

    /// <summary>
    /// Apply the weather at <paramref name="index"/> and point the dropdown at it.
    /// Goes through the index instead of <see cref="WeatherSystem.SetWeather"/>, which resolves
    /// the index by reference identity and silently does nothing when the instance it is handed
    /// is not the one currently sitting in the live array.
    /// </summary>
    private void ApplyWeatherIndex(int index)
    {
        if (!WeatherSystem.InstanceExists) return;
        if (index < 0 || index >= WeatherSystem.Instance.GetAllWeatherData().Length) return;

        WeatherSystem.Instance.ServerSetWeatherByIndex(index);
        weatherComboSelection.Value = index;

        // Vanilla presets other than the storm ship with an empty particle array, so switching to
        // one and expecting thunder gets you strikes nobody can see.
        if (WeatherSystem.Instance.IsStorm())
            RepairCurrentThunder();
    }

    /// <summary>Overwrite the weather at <paramref name="index"/> with the current inputs.
    /// Returns null (and changes nothing) when the index is out of range.</summary>
    private WeatherData SetWeather(int index)
    {
        if (!WeatherSystem.InstanceExists) return null;

        var datas = WeatherSystem.Instance.GetAllWeatherData();
        if (index < 0 || index >= datas.Length) return null;

        datas[index] = MakeWeatherData();
        WeatherSystem.Instance.weatherDatas = datas;
        return datas[index];
    }

    private void AddNewWeather(WeatherData data)
    {
        var datas = WeatherSystem.Instance.GetAllWeatherData();
        var newDatas = new WeatherData[datas.Length + 1];

        for (var i = 0; i < datas.Length; i++)
        {
            if (i < datas.Length)
            {
                newDatas[i] = datas[i];
            }
        }

        newDatas[datas.Length] = data;

        WeatherSystem.Instance.weatherDatas = newDatas;
    }

    private WeatherData MakeWeatherData()
    {
        var data = new WeatherData
        {
            pickWeight = pickWeightInput.Value,
            data = ScriptableObject.CreateInstance<WeatherDataScriptableObject>()
        };

        data.data.title = weatherTitleInput.Value;
        data.data.fogEndDistance = fogDistanceInput.Value;
        data.data.rainState = (WeatherIntensity)rainStateDropdown.Value;
        var transitionTime = transitionTimeInput;
        data.data.transitionTime = transitionTime.Value < 1 ? 1 : transitionTime.Value;
        data.data.bIsStorm = isStormToggle.Value;
        data.data.thunderData = new ThunderData
        {
            // A frequency of 0 means "strike every frame", which is what an untouched thunder
            // section (or one copied off a non-storm preset) gives you.
            minLightingStrikeFrequency = Mathf.Max(lightningStrikeFrequency.Value.x, MinStrikeFrequency),
            maxLightingStrikeFrequency = Mathf.Max(lightningStrikeFrequency.Value.y, MinStrikeFrequency),
            minLightingSkyFrequency = lightningSkyFrequency.Value.x,
            maxLightingSkyFrequency = lightningSkyFrequency.Value.y,
            chanceToGetHit = chanceToGetHit.Value,
            groundLightingParticles = GetGroundParticles()
        };

        return data;
    }

    /// <summary>
    /// Ground lightning particles to hand a weather.
    ///
    /// Playing one of these is the <em>only</em> thing a lightning strike does. Nothing in the game
    /// subscribes to <c>WeatherSystem.onLightingStrike</c>, there is no separate sky flash and no
    /// thunder clap, and <c>WeatherSystem.OnLighting</c> returns without doing anything when the
    /// array is null or empty. A weather with no particles therefore has thunder that fires on
    /// schedule and is completely invisible and silent, which is exactly what "thunder is broken"
    /// looks like.
    ///
    /// Vanilla only fills the array on storm presets, and every other preset serializes an empty
    /// (but non-null) array, so emptiness has to be checked, not just null.
    /// </summary>
    private static BaseParticle[] GetGroundParticles()
    {
        if (cachedGroundParticles is { Length: > 0 })
            return cachedGroundParticles;

        cachedGroundParticles = FindGroundParticles();
        return cachedGroundParticles;
    }

    private static BaseParticle[] FindGroundParticles()
    {
        if (WeatherSystem.InstanceExists)
        {
            var datas = WeatherSystem.Instance.GetAllWeatherData();
            var fromLevel = PickParticles(datas == null ? null : datas.Select(d => d?.data));

            if (fromLevel != null)
            {
                Plugin.LogSource.LogInfo($"[WeatherEditor] Ground lightning particles taken from this level's weather list ({fromLevel.Length}).");
                return fromLevel;
            }
        }

        // Weather assets other levels use are still loaded, and the storm ones carry the particles
        // even when this level's list does not.
        var fromAssets = PickParticles(Resources.FindObjectsOfTypeAll<WeatherDataScriptableObject>());
        if (fromAssets != null)
        {
            Plugin.LogSource.LogInfo($"[WeatherEditor] Ground lightning particles taken from a loaded weather asset ({fromAssets.Length}).");
            return fromAssets;
        }

        var prefab = FindLightningPrefab();
        if (prefab != null)
        {
            Plugin.LogSource.LogInfo($"[WeatherEditor] No weather asset carries ground lightning particles, borrowing prefab '{prefab.name}'.");
            return [prefab];
        }

        Plugin.LogSource.LogWarning("[WeatherEditor] Found no ground lightning particles at all, thunder will fire but stay invisible.");
        return null;
    }

    /// <summary>First non-empty particle array among <paramref name="datas"/>, storm presets first.</summary>
    private static BaseParticle[] PickParticles(IEnumerable<WeatherDataScriptableObject> datas)
    {
        if (datas == null) return null;

        BaseParticle[] fallback = null;

        foreach (var data in datas)
        {
            if (data == null || data.thunderData == null) continue;

            var particles = data.thunderData.groundLightingParticles;
            if (particles == null || particles.Length == 0 || particles[0] == null) continue;

            if (data.bIsStorm) return particles;
            fallback ??= particles;
        }

        return fallback;
    }

    /// <summary>Last resort: any lightning particle in memory. The game spells it "Lighting" about
    /// as often as "Lightning" (see <c>SwordStonePlatform.lightingStrikeParticle</c>), so match both.</summary>
    private static BaseParticle FindLightningPrefab()
    {
        BaseParticle sceneInstance = null;

        foreach (var particle in Resources.FindObjectsOfTypeAll<BaseParticle>())
        {
            if (particle == null) continue;

            var name = particle.name;
            if (name.IndexOf("lighting", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("lightning", StringComparison.OrdinalIgnoreCase) < 0) continue;

            // Prefer an asset over something already placed in the level: PopPlayPush clones
            // whatever it is handed, and cloning a live object drags its scene state along.
            if (!particle.gameObject.scene.IsValid()) return particle;
            sceneInstance ??= particle;
        }

        return sceneInstance;
    }

    /// <summary>
    /// Give the weather that is currently active ground lightning particles if it has none, so its
    /// strikes are actually visible. Returns whether it ended up with any.
    /// </summary>
    private static bool RepairCurrentThunder()
    {
        if (!WeatherSystem.InstanceExists) return false;

        var current = WeatherSystem.Instance.GetCurrentWeatherData();
        if (current == null || current.data == null) return false;

        current.data.thunderData ??= new ThunderData();

        var thunder = current.data.thunderData;
        if (thunder.groundLightingParticles is { Length: > 0 })
            return true;

        thunder.groundLightingParticles = GetGroundParticles();

        // Same reason as in MakeWeatherData: a zero frequency is a strike every frame.
        thunder.minLightingStrikeFrequency = Mathf.Max(thunder.minLightingStrikeFrequency, MinStrikeFrequency);
        thunder.maxLightingStrikeFrequency = Mathf.Max(thunder.maxLightingStrikeFrequency, MinStrikeFrequency);

        return thunder.groundLightingParticles is { Length: > 0 };
    }

    /// <summary>Fire one strike at the local player right now, bypassing the storm flag, the
    /// transition gate and the strike timer, so only the particle path is under test.</summary>
    private static void StrikeLightningNow()
    {
        var log = Plugin.LogSource;

        if (!WeatherSystem.InstanceExists)
        {
            log.LogWarning("[Thunder] No WeatherSystem in this level.");
            return;
        }

        var ws = WeatherSystem.Instance;

        if (ws.networkObject == null)
        {
            log.LogWarning("[Thunder] WeatherSystem has no network object, the game never spawns strikes.");
            return;
        }

        if (!ws.networkObject.IsServer())
        {
            log.LogWarning("[Thunder] Only the host spawns lightning strikes.");
            return;
        }

        var character = GameInstance.InstanceExists
            ? GameInstance.Instance.GetFirstLocalPlayerController()?.GetPlayerCharacter()
            : null;

        if (character == null)
        {
            log.LogWarning("[Thunder] No local player to strike.");
            return;
        }

        if (!RepairCurrentThunder())
            log.LogWarning("[Thunder] Current weather has no ground lightning particles, this strike will be invisible.");

        ws.ServerLightingStrike(character.GetPlayerPosition());
        log.LogInfo("[Thunder] Strike sent.");
    }

    /// <summary>Dump every condition <c>WeatherSystem</c> checks before a strike becomes visible.</summary>
    private static void LogThunderDiagnostics()
    {
        var log = Plugin.LogSource;

        if (!WeatherSystem.InstanceExists)
        {
            log.LogInfo("[Thunder] No WeatherSystem in this level.");
            return;
        }

        var ws = WeatherSystem.Instance;

        log.LogInfo("[Thunder] ---- diagnostics ----");
        log.LogInfo("[Thunder] network object: " + (ws.networkObject == null
            ? "null, so UpdateWeatherLerp never calls UpdateThunderAndLighting"
            : ws.networkObject.IsServer() ? "server" : "client, only the host spawns strikes"));

        var current = ws.GetCurrentWeatherData();
        if (current == null || current.data == null)
        {
            log.LogInfo("[Thunder] no current weather.");
        }
        else
        {
            var elapsed = (HawkNetworkManager.DefaultInstance.GetTimestep() - ws.changedTime) / 1000f;
            var progress = elapsed / Mathf.Max(current.data.transitionTime, 0.0001f);
            var thunder = current.data.thunderData;

            log.LogInfo($"[Thunder] current '{current.data.title}': isStorm={current.data.bIsStorm} (required), transition={progress:0.00} (must be >= 1)");
            log.LogInfo(thunder == null
                ? "[Thunder] current weather has no ThunderData."
                : $"[Thunder] every {thunder.minLightingStrikeFrequency}-{thunder.maxLightingStrikeFrequency}s, chanceToGetHit={thunder.chanceToGetHit}, particles={thunder.groundLightingParticles?.Length ?? 0} (0 means invisible strikes)");
        }

        // GetRandomPositionAroundPlayers bails unless the column above the player resolves to Rain.
        log.LogInfo($"[Thunder] default raining type: {ws.GetDefaultRainingType()} (anything but Rain skips every strike)");

        foreach (var data in ws.GetAllWeatherData())
        {
            if (data == null || data.data == null) continue;
            log.LogInfo($"[Thunder]   preset '{data.data.title}': isStorm={data.data.bIsStorm} particles={data.data.thunderData?.groundLightingParticles?.Length ?? 0}");
        }

        log.LogInfo("[Thunder] ---- end ----");
    }

    public override void RefreshUI()
    {
        RefreshWeatherDatas();
        RefreshDropdown();

        if (WeatherSystem.InstanceExists)
        {
            minWeatherTimeInput.Value = WeatherSystem.Instance.minWeatherTime;
            maxWeatherTimeInput.Value = WeatherSystem.Instance.maxWeatherTime;
        }

        var index = weatherComboSelection.Value;
        var data = index < weatherDatas.Length && index != -1 ? weatherDatas[index] : null;

        RefreshInputs(data);
    }

    private void SetMinWeatherTime(float value)
    {
        if (!WeatherSystem.InstanceExists) return;
        WeatherSystem.Instance.minWeatherTime = value;
        ReRollWeatherExpire();
    }

    private void SetMaxWeatherTime(float value)
    {
        if (!WeatherSystem.InstanceExists) return;
        WeatherSystem.Instance.maxWeatherTime = value;
        ReRollWeatherExpire();
    }

    private void ReRollWeatherExpire()
    {
        var ws = WeatherSystem.Instance;
        var min = Mathf.Min(ws.minWeatherTime, ws.maxWeatherTime);
        var max = Mathf.Max(ws.minWeatherTime, ws.maxWeatherTime);
        ws.weatherExpire = Random.Range(min, max);
    }

    private void RefreshWeatherDatas()
    {
        if (!WeatherSystem.Instance)
        {
            weatherDatas = [];
            weatherComboItems.Value = [];
            return;
        }

        weatherDatas = WeatherSystem.Instance.GetAllWeatherData();
        weatherComboItems.Value = weatherDatas.Select(x => x.data.title).ToArray();
    }

    private void RefreshDropdown()
    {
        if (!WeatherSystem.InstanceExists) return;

        weatherComboSelection.Value = weatherDatas.ToList().IndexOf(WeatherSystem.Instance.GetCurrentWeatherData());
    }

    private void RefreshInputs(WeatherData data)
    {
        if (data == null) return;

        weatherTitleInput.Value = data.data.title;
        pickWeightInput.Value = data.pickWeight;
        fogDistanceInput.Value = data.data.fogEndDistance;
        transitionTimeInput.Value = data.data.transitionTime;

        var thunder = data.data.thunderData;
        if (thunder != null)
        {
            lightningStrikeFrequency.Value = new(thunder.minLightingStrikeFrequency, thunder.maxLightingStrikeFrequency);
            lightningSkyFrequency.Value = new(thunder.minLightingSkyFrequency, thunder.maxLightingSkyFrequency);
            chanceToGetHit.Value = thunder.chanceToGetHit;
        }

        rainStateDropdown.Value = (int)data.data.rainState;
        isStormToggle.Value = data.data.bIsStorm;
    }

    private static bool wasLocked;

    public static class Patches
    {
        [HarmonyPatch(typeof(WeatherSystem), nameof(WeatherSystem.Update))]
        [HarmonyPrefix]
        public static void Prefix_WeatherSystem_Update(ref WeatherSystem __instance)
        {
            if (LockWeather.Value)
            {
                __instance.weatherExpire = float.MaxValue;
                wasLocked = true;
            }
            else if (wasLocked)
            {
                wasLocked = false;
                __instance.changedTime = HawkNetworkManager.DefaultInstance.GetTimestep();
                __instance.weatherExpire = Random.Range(__instance.minWeatherTime, __instance.maxWeatherTime);
            }
        }
    }
}