using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class SetTime : BaseMod
{
    public override string Name => "Set Time of Day";

    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    private const float DegreesPerHour = 15f; // 360 degrees / 24 hours
    private const float SunriseHour = 6f;     // 0 degrees == 06:00

    private Ref<float> timeSpeed = new();
    private Ref<string> timeText = new("");
    private Ref<float> timeDegrees = new();
    private Ref<string> timeHhmm = new("");

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            new DragFloat("Set Time Speed", 0, .05f, onValueChanged: SetTimeOfDaySpeed).WithValue(timeSpeed),
            new TextWrapped("TimeText", "").WithText(timeText),

            new DragFloat("Set Time (Degrees)", 0, .5f, 0, 360, "%.1f", onValueChanged: SetTimeDegrees).WithValue(timeDegrees),

            new InputText("##hhmm", hint: "e.g. 14:30").WithValue(timeHhmm),
            new SameLine("sl-hhmm-2"),
            ActionMenu(new Button("Set Time (HH:MM)##hhmm", ApplyHhmm), nameof(ApplyHhmm)),

            new UIText("SetTimeTo", "Set Time to "),
            new SameLine("sl-1"),
            ActionMenu(new Button("Morning", SetTimeMorning), nameof(SetTimeMorning)),
            new SameLine("sl-2"),
            ActionMenu(new Button("Midday", SetTimeMidday), nameof(SetTimeMidday)),
            new SameLine("sl-3"),
            ActionMenu(new Button("Evening", SetTimeEvening), nameof(SetTimeEvening)),
            new SameLine("sl-4"),
            ActionMenu(new Button("Midnight", SetTimeMidnight), nameof(SetTimeMidnight)),
            
            new Button("Inspect \"Day Night Cycle\" Component", () =>
            {
                if(DayNightCycle.InstanceExists)
                {
                    InspectorManager.Inspect(DayNightCycle.Instance);
                    UIManager.ShowMenu = true;
                }
            }).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var dnc = DayNightCycle.Instance;
        timeSpeed.Value = dnc?.GetSpeed() ?? 0;

        var degrees = dnc?.GetTimeOfDay() ?? 0f;
        timeDegrees.Value = degrees;
        timeHhmm.Value = DegreesToHhmm(degrees);
    }

    public override void Update()
    {
        var dnc = DayNightCycle.Instance;

        if (dnc == null)
        {
            return;
        }

        var time = dnc.GetTimeString();
        var day = dnc.GetDayNum();
        var degrees = dnc.GetTimeOfDay();

        timeText.Value = $"Time: {time} ({degrees:0.#}deg), Day: {day}";
    }

    [ModAction(ShowInUI = false)]
    public void SetTimeDegrees(float degrees)
    {
        DayNightCycle.Instance?.SetTimeOfDay(NormalizeDegrees(degrees));
    }

    [ModAction(ShowInUI = false, Label = "Get Time (Degrees)",
        Description = "Current time of day in degrees (0-360). 0 = 06:00, 90 = 12:00, 180 = 18:00, 270 = 00:00.")]
    public float GetTimeDegrees()
    {
        return DayNightCycle.Instance?.GetTimeOfDay() ?? 0f;
    }

    [ModAction(ShowInUI = false, Label = "Get Time (String)",
        Description = "Current in-game time as the game's formatted clock string (e.g. \"14:30\").")]
    public string GetTimeText()
    {
        return DayNightCycle.Instance?.GetTimeString() ?? "";
    }

    [ModAction(ShowInUI = false)]
    public void ApplyHhmm()
    {
        if (!TryParseHhmm(timeHhmm.Value, out var degrees))
        {
            return;
        }

        timeDegrees.Value = degrees;
        DayNightCycle.Instance?.SetTimeOfDay(degrees);
    }

    private static float NormalizeDegrees(float degrees)
    {
        return (degrees % 360f + 360f) % 360f;
    }

    // Inverse of the game's degrees -> clock mapping: degrees = ((hours24 - 6 + 24) mod 24) * 15.
    private static float HoursToDegrees(float hours24)
    {
        return ((hours24 - SunriseHour) % 24f + 24f) % 24f * DegreesPerHour;
    }

    private static float DegreesToHours(float degrees)
    {
        return (NormalizeDegrees(degrees) / DegreesPerHour + SunriseHour) % 24f;
    }

    private static string DegreesToHhmm(float degrees)
    {
        var hours = DegreesToHours(degrees);
        var h = (int)hours;
        var m = (int)((hours - h) * 60f + 0.5f);
        if (m >= 60)
        {
            m -= 60;
            h = (h + 1) % 24;
        }
        return $"{h:00}:{m:00}";
    }

    private static bool TryParseHhmm(string text, out float degrees)
    {
        degrees = 0f;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0].Trim(), out var h) || !int.TryParse(parts[1].Trim(), out var m))
        {
            return false;
        }

        if (h < 0 || h > 23 || m < 0 || m > 59)
        {
            return false;
        }

        degrees = HoursToDegrees(h + m / 60f);
        return true;
    }

    [ModAction(ShowInUI = false)]
    public static void SetTimeOfDaySpeed(float speed)
    {
        DayNightCycle.Instance.SetSpeed(speed);
    }

    [ModAction(ShowInUI = false)]
    public static void SetTimeMorning()
    {
        DayNightCycle.Instance.SetMorning();
    }

    [ModAction(ShowInUI = false)]
    public static void SetTimeMidday()
    {
        DayNightCycle.Instance.SetMidday();
    }

    [ModAction(ShowInUI = false)]
    public static void SetTimeEvening()
    {
        DayNightCycle.Instance.SetEvening();
    }

    [ModAction(ShowInUI = false)]
    public static void SetTimeMidnight()
    {
        DayNightCycle.Instance.SetMidnight();
    }
}