using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace lstwoMODS_WobblyLife;

internal class WobblyServerUtilCompat
{
    public static Assembly Assembly { get; private set; }

    public static void Init()
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "WobblyServerUtil.dll");
            Assembly = Assembly.LoadFile(path);
        }
        catch { }
    }

    public static void SetSettingsManagerValue(string name, object value)
    {
        var type = Assembly.GetType("WobblyServerUtil.Plugin");
        var settingsManager = type.GetField("_settingsManager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        settingsManager.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public).SetValue(settingsManager, value);
    }

    public static void AddToBoostList(PlayerVehicleRoadMovement vehicle, bool boost)
    {
        var type = Assembly.GetType("WobblyServerUtil.PlayerVehicleRoadMovementPatch");
        var boostEnabledList = (Dictionary<PlayerVehicleRoadMovement, bool>)type.GetField("boostEnabled", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        if(!boostEnabledList.TryAdd(vehicle, boost))
            boostEnabledList[vehicle] = boost;
        type.GetField("boostEnabled", BindingFlags.Public | BindingFlags.Static).SetValue(null, boostEnabledList);
    }
}