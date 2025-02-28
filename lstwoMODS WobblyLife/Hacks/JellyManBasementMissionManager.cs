using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class JellyManBasementMissionManager : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Unlock Basement", "lstwo.JellyManBasementMissionManager.UnlockBasement", () => JellyManBasementMission?.UnlockBasement(), "Unlock", 
            "lstwo.JellyManBasementMissionManager.UnlockBasementButton");
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Place All Wheels", "lstwo.JellyManBasementMissionManager.PlaceWheels", () => JellyManBasementMission?.PlacedAllWheels(), "Place",  
            "lstwo.JellyManBasementMissionManager.PlaceWheelsButton");
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Place Engine", "lstwo.JellyManBasementMissionManager.PlaceEngine", () => JellyManBasementMission?.PlacedEngine(), "Place",  
            "lstwo.JellyManBasementMissionManager.PlaceEngineButton");
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Deliver All Jelly", "lstwo.JellyManBasementMissionManager.DeliverJelly", () =>
        {
            for (var i = 0; i < 5; i++)
            {
                JellyManBasementMission?.IncrementJellyDeliveredCount();
            }
        }, "Deliver", "lstwo.JellyManBasementMissionManager.DeliverJellyButton");
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Place Steering Wheel", "lstwo.JellyManBasementMissionManager.PlaceSteeringWheel", () => JellyManBasementMission?.PlacedSteeringWheel(), "Place",  
            "lstwo.JellyManBasementMissionManager.PlaceSteeringWheelButton");
        
        ui.AddSpacer(6);

        ui.CreateLBDuo("Finish Jelly Car", "lstwo.JellyManBasementMissionManager.FinishJellyCar", () => JellyManBasementMission?.CompleteBuiltJellyCar(), "Finish",  
            "lstwo.JellyManBasementMissionManager.FinishJellyCarButton");
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Jelly Man Basement Mission Manager";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.SaveHacksTab;

    public static WorldMissionWobblyIslandJellyManBasement JellyManBasementMission => WorldMissionManager.Instance?.GetFirstMissionByType<WorldMissionWobblyIslandJellyManBasement>();
}