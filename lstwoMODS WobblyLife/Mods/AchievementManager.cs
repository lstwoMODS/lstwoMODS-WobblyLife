using System;
using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using Button = lstwoMODS_Core.UI.Elements.Button;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class AchievementManager : BaseMod
{
    public override string Name => "Achievement Manager";
    public override string Description => "Unlock and Lock Achievements!";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    private Ref<int> selectedAchievementIndex = new();
    private WobblyAchievement selectedAchievement => Enum.GetValues(typeof(WobblyAchievement)).Cast<WobblyAchievement>().ToArray()[selectedAchievementIndex.Value];

    [ModAction(ShowInUI = false)]
    public static void UnlockAchievement(WobblyAchievement achievement)
    {
        global::AchievementManager.Instance.UnlockAchievement(achievement, GameInstance.Instance.GetFirstLocalPlayerController());
    }

    [ModAction(ShowInUI = false)]
    public static void LockAchievement(WobblyAchievement achievement)
    {
        global::AchievementManager.Instance.LockAchievement(achievement, GameInstance.Instance.GetFirstLocalPlayerController());
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockAllAchievements()
    {
        foreach (var achievement in Enum.GetValues(typeof(WobblyAchievement)).Cast<WobblyAchievement>())
        {
            UnlockAchievement(achievement);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void LockAllAchievements()
    {
        foreach (var achievement in Enum.GetValues(typeof(WobblyAchievement)).Cast<WobblyAchievement>())
        {
            UnlockAchievement(achievement);
        }
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new Combo("Select Achievement", Enum.GetNames(typeof(WobblyAchievement)).Select(x => ModRegistry.NicifyName(x)).ToArray()).WithSelectedIndex(selectedAchievementIndex),
            
            new HStack("Lock / Unlock",
                ActionMenu(new Button("Unlock Achievement", () => UnlockAchievement(selectedAchievement)), nameof(UnlockAchievement)),
                ActionMenu(new Button("Lock Achievement", () => LockAchievement(selectedAchievement)), nameof(LockAchievement))
            ).WithContentWidth(),

            new SeparatorText("Unlock / Lock All Achievements", "Unlock / Lock All Achievements"),

            new HStack("Lock / Unlock All",
                ActionMenu(new Button("Unlock All Achievements", UnlockAllAchievements), nameof(UnlockAllAchievements)),
                ActionMenu(new Button("Lock All Achievements", LockAllAchievements), nameof(LockAllAchievements))
            ).WithContentWidth(),
            
            new Button("Inspect \"Achievement Manager\" Component", () =>
            {
                if(global::AchievementManager.InstanceExists)
                {
                    InspectorManager.Inspect(global::AchievementManager.Instance);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }
}