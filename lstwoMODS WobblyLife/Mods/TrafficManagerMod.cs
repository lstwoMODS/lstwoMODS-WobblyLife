using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class TrafficManagerMod : BaseMod
{
    public override string Name => "Traffic Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModSetting]
    public static uint MaximumVehicles
    {
        get => TrafficManager.Instance?.maxVehicles ?? 0;
        set => TrafficManager.Instance?.maxVehicles = value;
    }

    [ModSetting]
    public static bool EnableTraffic
    {
        get => TrafficManager.Instance?.bTrafficEnabled ?? false;
        set => TrafficManager.Instance?.bTrafficEnabled = value;
    }

    [ModAction]
    public static void SpawnVehicle()
    {
        TrafficManager.Instance?.SpawnAI();
    }
}