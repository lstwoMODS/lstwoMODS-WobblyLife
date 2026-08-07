using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class JellyManBasementMissionManager : BaseMod
{
    public override string Name => "Jelly Man Basement Mission Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    public static WorldMissionWobblyIslandJellyManBasement JellyManBasementMission => WorldMissionManager.Instance?.GetFirstMissionByType<WorldMissionWobblyIslandJellyManBasement>();

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            SaveGuard.Notice("jellyman-guard-notice"),
            SaveGuard.Guard(base.BuildPanel(id))
        );
    }

    [ModAction(Order = 10)]
    public static void UnlockBasement()
    {
        if (SaveGuard.On) return;

        JellyManBasementMission?.UnlockBasement();
    }

    [ModAction(Order = 20)]
    public static void PlaceWheels()
    {
        if (SaveGuard.On) return;

        JellyManBasementMission?.PlacedAllWheels();
    }

    [ModAction(Order = 30)]
    public static void PlaceEngine()
    {
        if (SaveGuard.On) return;

        JellyManBasementMission?.PlacedEngine();
    }

    [ModAction(Order = 40)]
    public static void DeliverAllJelly()
    {
        if (SaveGuard.On) return;

        for (var i = 0; i < 5; i++)
        {
            JellyManBasementMission?.IncrementJellyDeliveredCount();
        }
    }

    [ModAction(Order = 50)]
    public static void PlaceSteeringWheel()
    {
        if (SaveGuard.On) return;

        JellyManBasementMission?.PlacedSteeringWheel();
    }

    [ModAction(Order = 60)]
    public static void CompleteJellyCar()
    {
        if (SaveGuard.On) return;

        JellyManBasementMission?.CompleteBuiltJellyCar();
    }
}
