using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class JellyManBasementMissionManager : BaseMod
{
    public override string Name => "Jelly Man Basement Mission Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    public static WorldMissionWobblyIslandJellyManBasement JellyManBasementMission => WorldMissionManager.Instance?.GetFirstMissionByType<WorldMissionWobblyIslandJellyManBasement>();

    [ModAction(Order = 10)]
    public static void UnlockBasement()
    {
        JellyManBasementMission?.UnlockBasement();
    }

    [ModAction(Order = 20)]
    public static void PlaceWheels()
    {
        JellyManBasementMission?.PlacedAllWheels();
    }

    [ModAction(Order = 30)]
    public static void PlaceEngine()
    {
        JellyManBasementMission?.PlacedEngine();
    }
    
    [ModAction(Order = 40)]
    public static void DeliverAllJelly()
    {
        for (var i = 0; i < 5; i++)
        {
            JellyManBasementMission?.IncrementJellyDeliveredCount();
        }
    }

    [ModAction(Order = 50)]
    public static void PlaceSteeringWheel()
    {
        JellyManBasementMission?.PlacedSteeringWheel();
    }

    [ModAction(Order = 60)]
    public static void CompleteJellyCar()
    {
        JellyManBasementMission?.CompleteBuiltJellyCar();
    }
}
