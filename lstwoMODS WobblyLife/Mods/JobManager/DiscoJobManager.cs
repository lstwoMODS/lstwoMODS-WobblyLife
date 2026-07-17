using System;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class DiscoJobManager : BaseJobManager
{
    public override Type missionType => typeof(DiscoJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<DiscoJobMission>("missTile", "Miss Tile", m => m.TileMiss());
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            WithMacroMenu(new Button("Miss Tile", MissTile), "missTile", "Miss Tile")
        );
    }

    public override void RefreshUI()
    {
    }

    public void MissTile()
    {
        if (CheckMission())
            ((DiscoJobMission)Mission).TileMiss();
    }
}
