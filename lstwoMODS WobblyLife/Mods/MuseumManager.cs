using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using Object = UnityEngine.Object;

namespace lstwoMODS_WobblyLife.Mods;

public class MuseumManager : BaseMod
{
    public override string Name => "Museum Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    private static Ref<string[]> museumCollectionDropdownItems = new();
    private static Ref<int> museumCollectionDropdownSelection = new();
    
    public static List<MissionMuseumCollectionData> MuseumCollections => MuseumMission?.collectionDatas?.ToList();
    public static WorldMissionMuseum MuseumMission => Object.FindObjectOfType<WorldMissionMuseum>();

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("Museum Collection", "Museum Collection"),
            new Combo("Collection", []).WithItems(museumCollectionDropdownItems).WithSelectedIndex(museumCollectionDropdownSelection),
            new Button("Unlock All Artifacts", () => MuseumCollections[museumCollectionDropdownSelection.Value].Unlock(MuseumMission.museumData)).WithContentWidth(),
            
            new SeparatorText("All Collections", "All Collections"),
            new Button("Finish All Collections", () => MuseumCollections.ForEach(x => x.Unlock(MuseumMission.museumData))).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        if (MuseumMission == null)
        {
            museumCollectionDropdownItems.Value = [];
            return;
        }
        
        museumCollectionDropdownItems.Value = MuseumCollections.Select(x => x.name).ToArray();
    }
}