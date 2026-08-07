using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using UnityExplorer.UI;

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
        get => TrafficManager.Instance?.enabled ?? false;
        set => TrafficManager.Instance?.enabled = value;
    }

    [ModAction]
    public static void SpawnVehicle()
    {
        TrafficManager.Instance?.SpawnAI();
    }

    [ModAction]
    public static void InspectTrafficManager()
    {
        if (!TrafficManager.InstanceExists) return;
        
        InspectorManager.Inspect(TrafficManager.Instance);
        UIManager.ShowMenu = true;
    }
}