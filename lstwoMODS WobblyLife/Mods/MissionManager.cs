using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods;

internal class MissionManager : BaseMod
{
    public override string Name => "Mission Manager";
    public override string Description => "Completes all Active Missions";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    private List<WorldMission> missions;
    private Ref<string[]> missionDropdownItems = new();
    private Ref<int> selectedMissionIndex = new();

    // NOTE: Uncompleting makes them just get re-completed after reloading
    
    [ModAction]
    public void CompleteActiveMissions()
    {
        if (SaveGuard.On) return;

        var missions = WorldMissionManager.Instance.GetActiveMissions();

        foreach (var mission in missions)
        {
            mission.CompleteMission();
        }
    }

    [ModAction]
    public void CompleteAllMissions()
    {
        if (SaveGuard.On) return;

        var missions = WorldMissionManager.Instance.GetAllMissions();

        foreach (var mission in missions)
        {
            mission.CompleteMission();
        }
    }

    public void UncompleteAllMissions()
    {
        if (SaveGuard.On) return;

        var missions = WorldMissionManager.Instance.GetAllMissions();

        foreach (var mission in missions)
        {
            SaveGameManager.Instance.GetSaveMissionData().Uncomplete(mission.GetGuid());
        }
    }

    [ModAction(ShowInUI = false)]
    public void CompleteMission(WorldMission mission)
    {
        if (SaveGuard.On) return;

        mission.CompleteMission();
    }

    public void UncompleteMission(WorldMission mission)
    {
        if (SaveGuard.On) return;

        SaveGameManager.Instance.GetSaveMissionData().Uncomplete(mission.GetGuid());
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            SaveGuard.Notice("mission-guard-notice"),

            SaveGuard.Guard(base.BuildPanel(id)),

            new Spacing("spacing"),

            new Combo("Select Mission to Complete", []).WithItems(missionDropdownItems).WithSelectedIndex(selectedMissionIndex),
            SaveGuard.Guard(ActionMenu(new Button("Complete Selected Mission", () => CompleteMission(missions[selectedMissionIndex.Value])).WithContentWidth(), nameof(CompleteMission)))
        );
    }

    public override void RefreshUI()
    {
        if (!WorldMissionManager.InstanceExists) return;
        missions = WorldMissionManager.Instance.GetAllMissions().ToList();
        missionDropdownItems.Value = missions.Select(x => x.GetWorldMissionInfo().GetMissionTitle() + " (" + x.name + ")").ToArray();
    }
}