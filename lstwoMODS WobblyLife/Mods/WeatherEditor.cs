using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityEngine.Rendering.PostProcessing;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks;

public class WeatherEditor : BaseHack
{
    public override string Name => "Weather Editor";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.ServerHacksTab;

    private HacksUIHelper.LDBTrio setWeatherLDB;
    private WeatherData[] weatherDatas;

    private InputFieldRef weatherTitleInput;
    private InputFieldRef fogDistanceInput;
    private InputFieldRef pickWeightInput;
    private Dropdown rainStateDropdown;
    private InputFieldRef transitionTimeInput;
    private Toggle isStormToggle;

    private InputFieldRef minLightingStrikeFrequency;
    private InputFieldRef maxLightingStrikeFrequency;
    private InputFieldRef minLightingSkyFrequency;
    private InputFieldRef maxLightingSkyFrequency;
    private InputFieldRef chanceToGetHit;

    private WeatherData customWeather;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        setWeatherLDB = ui.CreateLDBTrio("Set Current Weather", "lstwo.WeatherEditor.SetWeather", "- Select Weather -", onValueChanged: (index) =>
        {
            var data = weatherDatas[index];

            RefreshInputs(data);
        });

        setWeatherLDB.Button.OnClick = () =>
        {
            if (setWeatherLDB.Dropdown.value >= weatherDatas.Length) return;

            WeatherSystem.Instance.SetWeather(weatherDatas[setWeatherLDB.Dropdown.value]);
        };

        ui.AddSpacer(6);

        ui.CreateLabel("<b>Create new Weather!</b>");

        ui.CreateLabel("<b>Base Weather Information</b>");

        weatherTitleInput = ui.CreateLIDuo("Weather Title", "lstwo.WeatherEditor.Title", "lstwo.WeatherEditor.TitleInput", "Weather Title").Input;

        ui.AddSpacer(6);

        pickWeightInput = ui.CreateLIDuo("Pick Chance", "lstwo.WeatherEditor.PickChance", "lstwo.WeatherEditor.PickChanceInput", "0 (0%) - 1 (100%)").Input;
        pickWeightInput.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        transitionTimeInput = ui.CreateLIDuo("Transition Time", "lstwo.WeatherEditor.transitionTimeInput", "lstwo.WeatherEditor.TransitionTimeInput", "e.g. 1").Input;

        ui.AddSpacer(6);

        fogDistanceInput = ui.CreateLIDuo("Fog Distance", "lstwo.WeatherEditor.FogDistance", "lstwo.WeatherEditor.FogDistanceInput", "Fog Distance").Input;
        fogDistanceInput.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        ui.CreateLabel("<b>Rain Information</b>");

        var rainStateGroup = ui.CreateHorizontalGroup("rainStateGroup", true, true, true, true);

        UIFactory.SetLayoutElement(UIFactory.CreateLabel(rainStateGroup, "label", " Rain Intensity").gameObject, 256, 32, 0, 0);

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", rainStateGroup), 32, 32);

        var rainStateDropdownObj = UIFactory.CreateDropdown(rainStateGroup, "lstwo.WeatherEditor.RainStateDropdown", out rainStateDropdown, "", 16, null, Enum.GetNames(typeof(WeatherIntensity)));
        rainStateDropdown.image.sprite = HacksUIHelper.RoundedRect;
        UIFactory.SetLayoutElement(rainStateDropdownObj, 256, 32, 0, 0);

        ui.AddSpacer(6);

        isStormToggle = ui.CreateToggle("isStormToggle", "Is Storm", (b) => { });

        ui.AddSpacer(6);

        ui.CreateLabel("<b>Thunder Information</b>");

        ConstructThunderDataUI(root);

        ui.AddSpacer(6);

        var group = ui.CreateHorizontalGroup("applyGroup", true, true, true, true);

        var setCustomWeatherButton = UIFactory.CreateButton(group, "lstwo.WeatherEditor.SetWeather", "Set Weather", HacksUIHelper.ButtonColor);
        UIFactory.SetLayoutElement(setCustomWeatherButton.GameObject, 256, 32, 0, 0);
        setCustomWeatherButton.Component.image = setCustomWeatherButton.GameObject.GetComponent<Image>();
        setCustomWeatherButton.Component.image.sprite = HacksUIHelper.RoundedRect;
        setCustomWeatherButton.OnClick = () =>
        {
            if (customWeather == null)
            {
                customWeather = MakeWeatherData();
                customWeather.data.title = "Custom";
                AddNewWeather(customWeather);
                RefreshWeatherDatas();
            }

            else
            {
                weatherTitleInput.Text = "Custom";
                customWeather = SetWeather(weatherDatas.ToList().IndexOf(customWeather));
                RefreshWeatherDatas();
            }

            WeatherSystem.Instance.SetWeather(customWeather);
        };

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", group), 32);

        var addCustomWeatherButton = UIFactory.CreateButton(group, "lstwo.WeatherEditor.AddToList", "Add Weather to List", HacksUIHelper.ButtonColor);
        UIFactory.SetLayoutElement(addCustomWeatherButton.GameObject, 256, 32, 0, 0);
        addCustomWeatherButton.Component.image = addCustomWeatherButton.GameObject.GetComponent<Image>();
        addCustomWeatherButton.Component.image.sprite = HacksUIHelper.RoundedRect;
        addCustomWeatherButton.OnClick = () =>
        {
            AddNewWeather(MakeWeatherData());
            RefreshWeatherDatas();
            RefreshDropdownValues();
        };

        UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", group), 32);

        var overrideSelectedWeatherButton = UIFactory.CreateButton(group, "lstwo.WeatherEditor.Override", "Override Selected Weather", HacksUIHelper.ButtonColor);
        UIFactory.SetLayoutElement(overrideSelectedWeatherButton.GameObject, 256, 32, 0, 0);
        overrideSelectedWeatherButton.Component.image = overrideSelectedWeatherButton.GameObject.GetComponent<Image>();
        overrideSelectedWeatherButton.Component.image.sprite = HacksUIHelper.RoundedRect;
        overrideSelectedWeatherButton.OnClick = () =>
        {
            SetWeather(setWeatherLDB.Dropdown.value);
            RefreshWeatherDatas();
            RefreshDropdownValues();
        };

        ui.AddSpacer(6);

        ui.CreateButton("Inspect \"Weather System\" Component", () =>
        {
            if (WeatherSystem.InstanceExists)
            {
                InspectorManager.Inspect(WeatherSystem.Instance);
                UIManager.ShowMenu = true;
            }
        }, "lstwo.WeatherEditor.inspect", null, 256 * 3 + 32 * 2, 32);

        ui.AddSpacer(6);
    }

    private WeatherData SetWeather(int index)
    {
        var datas = WeatherSystem.Instance.GetAllWeatherData();
        datas[index] = MakeWeatherData();
        typeof(WeatherSystem).GetField("weatherDatas", Plugin.Flags).SetValue(WeatherSystem.Instance, datas);
        return datas[index];
    }

    private void AddNewWeather(WeatherData data)
    {
        var datas = WeatherSystem.Instance.GetAllWeatherData();

        var newDatas = new WeatherData[datas.Length + 1];

        for (int i = 0; i < datas.Length; i++)
        {
            if (i < datas.Length)
            {
                newDatas[i] = datas[i];
            }
        }

        newDatas[datas.Length] = data;

        typeof(WeatherSystem).GetField("weatherDatas", Plugin.Flags).SetValue(WeatherSystem.Instance, newDatas);
    }

    private WeatherData MakeWeatherData()
    {
        var data = new WeatherData
        {
            pickWeight = TryParseFloat(pickWeightInput.Text, 0),
            data = ScriptableObject.CreateInstance<WeatherDataScriptableObject>()
        };

        data.data.title = weatherTitleInput.Text;
        data.data.fogEndDistance = TryParseFloat(fogDistanceInput.Text, 200);
        data.data.rainState = (WeatherIntensity)rainStateDropdown.value;
        float transitionTime = TryParseFloat(transitionTimeInput.Text, 1);
        data.data.transitionTime = transitionTime < 1 ? 1 : transitionTime;
        data.data.bIsStorm = isStormToggle.isOn;
        data.data.thunderData = new ThunderData
        {
            minLightingStrikeFrequency = TryParseFloat(minLightingStrikeFrequency.Text, 0),
            maxLightingStrikeFrequency = TryParseFloat(maxLightingStrikeFrequency.Text, 0),
            minLightingSkyFrequency = TryParseFloat(minLightingSkyFrequency.Text, 0),
            maxLightingSkyFrequency = TryParseFloat(maxLightingSkyFrequency.Text, 0),
            chanceToGetHit = TryParseFloat(chanceToGetHit.Text, 0),
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

    private float TryParseFloat(string text, float defaultValue)
    {
        try
        {
            return float.Parse(text);
        }

        catch
        {
            return defaultValue;
        }
    }

    private void ConstructThunderDataUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        minLightingStrikeFrequency = ui.CreateLIDuo("Min. Lighting Strike Frequency", inputPlaceholder: "").Input;
        minLightingStrikeFrequency.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        maxLightingStrikeFrequency = ui.CreateLIDuo("Max. Lighting Strike Frequency", inputPlaceholder: "").Input;
        maxLightingStrikeFrequency.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        minLightingSkyFrequency = ui.CreateLIDuo("Min. Lighting Sky Frequency", inputPlaceholder: "").Input;
        minLightingSkyFrequency.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        maxLightingSkyFrequency = ui.CreateLIDuo("Max. Lighting Sky Frequency", inputPlaceholder: "").Input;
        maxLightingSkyFrequency.Component.characterValidation = InputField.CharacterValidation.Decimal;

        ui.AddSpacer(6);

        chanceToGetHit = ui.CreateLIDuo("Chance to Get Hit", inputPlaceholder: "e.g. 0.5 => 50%").Input;
        chanceToGetHit.Component.characterValidation = InputField.CharacterValidation.Decimal;
    }

    public override void RefreshUI()
    {
        RefreshWeatherDatas();
        RefreshDropdown();

        var index = setWeatherLDB.Dropdown.value;
        var data = index < weatherDatas.Length && index != -1 ? weatherDatas[index] : null;

        RefreshInputs(data);
    }

    private void RefreshWeatherDatas()
    {
        if (!WeatherSystem.Instance)
        {
            weatherDatas = new WeatherData[0];
            return;
        }

        weatherDatas = WeatherSystem.Instance.GetAllWeatherData();
    }

    private void RefreshDropdownValues()
    {
        setWeatherLDB.Dropdown.ClearOptions();
        foreach (var weatherData in weatherDatas)
        {
            setWeatherLDB.Dropdown.options.Add(new(weatherData.data.title));
        }
        setWeatherLDB.Dropdown.RefreshShownValue();
    }

    private void RefreshDropdown()
    {
        RefreshDropdownValues();

        if (!WeatherSystem.InstanceExists) return;

        setWeatherLDB.Dropdown.value = weatherDatas.ToList().IndexOf(WeatherSystem.Instance.GetCurrentWeatherData());
        setWeatherLDB.Dropdown.RefreshShownValue();
    }

    private void RefreshInputs(WeatherData data)
    {
        if (data == null) return;

        weatherTitleInput.Text = data.data.title;
        fogDistanceInput.Text = "" + data.data.fogEndDistance;
        transitionTimeInput.Text = "" + data.data.transitionTime;

        minLightingStrikeFrequency.Text = "" + data.data.thunderData.minLightingStrikeFrequency;
        maxLightingStrikeFrequency.Text = "" + data.data.thunderData.maxLightingStrikeFrequency;
        minLightingSkyFrequency.Text = "" + data.data.thunderData.minLightingSkyFrequency;
        maxLightingSkyFrequency.Text = "" + data.data.thunderData.maxLightingSkyFrequency;
        chanceToGetHit.Text = "" + data.data.thunderData.chanceToGetHit;

        rainStateDropdown.value = (int)data.data.rainState;
        isStormToggle.isOn = data.data.bIsStorm;
    }

    public override void Update()
    {
    }
}