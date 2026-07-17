using System.Linq;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityExplorer;

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

    private Ref<string[]> rainStates = new();
    private Ref<int> rainStateDropdown = new();

    private Ref<Vector2> lightningStrikeFrequency = new();
    private Ref<Vector2> lightningSkyFrequency = new();
    private Ref<float> chanceToGetHit = new();
    
    private static Ref<bool> lockWeather = new();

    private Ref<float> minWeatherTimeInput = new();
    private Ref<float> maxWeatherTimeInput = new();

    private WeatherData customWeather;

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
                if (index >= weatherDatas.Length) return;
                
                var data = weatherDatas[index];
                RefreshInputs(data);
                
            }).WithItems(weatherComboItems).WithSelectedIndex(weatherComboSelection),
            
            new Button("Change Weather", () =>
            {
                if (weatherComboSelection.Value >= weatherDatas.Length) return;
                WeatherSystem.Instance.SetWeather(weatherDatas[weatherComboSelection.Value]);
                
            }).WithContentWidth(),
            
            new Checkbox("Lock Weather").WithValue(lockWeather),

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
                
                new Combo("Rain State", []).WithItems(rainStates).WithSelectedIndex(rainStateDropdown),
                new Checkbox("Is Storm?").WithValue(isStormToggle)
            ),
            
            new TreeNode("Thunder Info (may be broken)", "Thunder Info (may be broken)",
                
                new DragFloat2("Lightning Strike Frequency", default, 0.01f, 0, float.MaxValue).WithTooltip("Min / max seconds in between lightning strikes.").WithValue(lightningStrikeFrequency),
                new DragFloat2("Lightning Sky Frequency", default, 0.01f, 0, float.MaxValue).WithTooltip("Min / max seconds in between lightning strikes in the sky.").WithValue(lightningSkyFrequency),
                new DragFloat("Chance to Get Hit", 0, .001f, 0, 1).WithValue(chanceToGetHit)
            ),
            
            new HStack("Apply Buttons",
                
                new Button("Apply Custom Weather", () =>
                {
                    if (customWeather == null)
                    {
                        customWeather = MakeWeatherData();
                        customWeather.data.title = "Custom";
                        AddNewWeather(customWeather);
                    }
                    else
                    {
                        weatherTitleInput.Value = "Custom";
                        customWeather = SetWeather(weatherDatas.ToList().IndexOf(customWeather));
                    }

                    RefreshWeatherDatas();

                    WeatherSystem.Instance.SetWeather(customWeather);
                }),
                
                new Button("Add Weather to List", () =>
                {
                    AddNewWeather(MakeWeatherData());
                    RefreshWeatherDatas();
                }),
                
                new Button("Overwrite Selected Weather", () =>
                {
                    SetWeather(weatherComboSelection.Value);
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

    private WeatherData SetWeather(int index)
    {
        var datas = WeatherSystem.Instance.GetAllWeatherData();
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
            minLightingStrikeFrequency = lightningStrikeFrequency.Value.x,
            maxLightingStrikeFrequency = lightningStrikeFrequency.Value.y,
            minLightingSkyFrequency = lightningSkyFrequency.Value.x,
            maxLightingSkyFrequency = lightningSkyFrequency.Value.y,
            chanceToGetHit = chanceToGetHit.Value,
            groundLightingParticles = GetGroundParticles()
        };

        return data;
    }

    private BaseParticle[] GetGroundParticles()
    {
        foreach (var data in WeatherSystem.Instance.GetAllWeatherData())
        {
            if (data != null && data.data != null && data.data.thunderData != null && data.data.thunderData.groundLightingParticles != null)
            {
                return data.data.thunderData.groundLightingParticles;
            }
        }

        return null;
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

    // Apply the new timing to the current weather period so the change is felt now instead of
    // only from the next weather onwards. Harmless while locked: the Update prefix re-pins
    // weatherExpire out of reach every frame, so the lock still holds.
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
        fogDistanceInput.Value = data.data.fogEndDistance;
        transitionTimeInput.Value = data.data.transitionTime;

        lightningStrikeFrequency.Value = new(data.data.thunderData.minLightingStrikeFrequency, data.data.thunderData.maxLightingStrikeFrequency);
        lightningSkyFrequency.Value = new(data.data.thunderData.minLightingSkyFrequency, data.data.thunderData.maxLightingSkyFrequency);
        chanceToGetHit.Value = data.data.thunderData.chanceToGetHit;

        rainStateDropdown.Value = (int)data.data.rainState;
        isStormToggle.Value = data.data.bIsStorm;
    }

    private static bool wasLocked;

    public static class Patches
    {
        // Let the game's own Update run (lerp, fog, thunder, networking all stay vanilla) and
        // only neutralise the auto-transition: while locked, push weatherExpire out of reach so
        // the server never rolls a new random weather. On unlock, restart the timer so the huge
        // elapsed time that built up while locked doesn't instantly expire and swap the weather.
        [HarmonyPatch(typeof(WeatherSystem), nameof(WeatherSystem.Update))]
        [HarmonyPrefix]
        public static void Prefix_WeatherSystem_Update(ref WeatherSystem __instance)
        {
            if (lockWeather.Value)
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