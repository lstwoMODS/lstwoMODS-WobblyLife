using System;
using System.Linq;
using lstwoMODS_Core.Macros;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>
/// Registers <see cref="WeatherData"/> as a macro object type so weather parameters on macro
/// steps (the Weather Editor's "Set Weather" action) get a live "By Title" dropdown of the
/// weather presets currently loaded in the game. String values (constants / expressions / step
/// outputs) also resolve to a preset by its title.
/// </summary>
public static class WeatherMacroType
{
    public static void Register()
    {
        MacroTypes.Register(new MacroTypeDescriptor
        {
            Type = typeof(WeatherData),
            DisplayName = "Weather",
            DefaultModeId = "byTitle",
            ResolveFromString = FindByTitle,
            Modes =
            {
                new MacroTypeMode
                {
                    Id = "byTitle", Label = "By Title",
                    Param = new MacroParam { Name = "title", Type = typeof(string) },
                    Resolve = args => FindByTitle((string)args[0]),
                    Choices = Titles,
                },
            },
        });
    }

    /// <summary>Titles of every weather preset currently loaded (throws when no game is running
    /// so the editor shows an empty dropdown, matching the other macro types).</summary>
    private static string[] Titles()
    {
        var datas = AllWeather();
        return datas.Where(d => d?.data != null).Select(d => d.data.title).ToArray();
    }

    private static object FindByTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Weather title is empty.");

        var datas = AllWeather();
        // Exact title first, then a case-insensitive contains so "rain" finds "Heavy Rain".
        var match = datas.FirstOrDefault(d => d?.data != null && string.Equals(d.data.title, title, StringComparison.OrdinalIgnoreCase))
                 ?? datas.FirstOrDefault(d => d?.data != null && (d.data.title ?? "").IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
        if (match == null)
            throw new ArgumentException(
                $"No weather titled '{title}'. Available: {string.Join(", ", datas.Where(d => d?.data != null).Select(d => d.data.title))}");
        return match;
    }

    private static WeatherData[] AllWeather()
    {
        if (!WeatherSystem.InstanceExists)
            throw new InvalidOperationException("Weather system not available (no game running?).");
        return WeatherSystem.Instance.GetAllWeatherData();
    }
}
