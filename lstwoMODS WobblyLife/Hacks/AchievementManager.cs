using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Mono.Security.X509.X520;
using UniverseLib.UI;
using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityExplorer;
using UnityExplorer.UI;

namespace lstwoMODS_WobblyLife.Hacks;

public class AchievementManager : BaseHack
{
    public override string Name => "Achievement Manager";

    public override string Description => "Unlock and Lock Achievements!";

    public override HacksTab HacksTab => Plugin.SaveHacksTab;

    private Dropdown achievementDropdown;

    private WobblyAchievement selectedAchievement;
    private WobblyAchievement[] achievements;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        var selector = UIFactory.CreateHorizontalGroup(root, "AchievementSelector", true, true, true, true);
        UIFactory.SetLayoutElement(selector);

        var selectorLabel = UIFactory.CreateLabel(selector, "SelectorLabel", " Select Achievement");
        UIFactory.SetLayoutElement(selectorLabel.gameObject, 256, 32);

        var spacer1 = UIFactory.CreateUIObject("spacer1", selector);
        UIFactory.SetLayoutElement(spacer1, 32);

        var dropdown = UIFactory.CreateDropdown(selector, "AchievementDropdon", out achievementDropdown, "- Select Achievement -", 16, (i) =>
        {
            if (i >= achievements.Length) return;
            selectedAchievement = achievements[i];
        });
        achievementDropdown.image.sprite = HacksUIHelper.RoundedRect;
        UIFactory.SetLayoutElement(dropdown, 256 * 2 + 32, 32, 0, 0);

        ui.AddSpacer(6);

        ui.CreateLBBTrio("Unlock / Lock Achievement", "AchievementControls", () =>
        {
            global::AchievementManager.Instance.UnlockAchievement(selectedAchievement, GameInstance.Instance.GetFirstLocalPlayerController());
        }, "Unlock", "lstwo.AchievementManager.Unlock", () =>
        {
            global::AchievementManager.Instance.LockAchievement(selectedAchievement, GameInstance.Instance.GetFirstLocalPlayerController());
        }, "Lock", "lstwo.AchievementManager.Lock");

        ui.AddSpacer(6);

        ui.CreateLBBTrio("Unlock / Lock ALL Achievements", "AllAchievementControls", () =>
        {
            foreach (var achievement in achievements)
            {
                global::AchievementManager.Instance.UnlockAchievement(achievement, GameInstance.Instance.GetFirstLocalPlayerController());
            }
        }, "Unlock All", "lstwo.AchievementManager.UnlockAll", () =>
        {
            foreach (var achievement in achievements)
            {
                global::AchievementManager.Instance.LockAchievement(achievement, GameInstance.Instance.GetFirstLocalPlayerController());
            }
        }, "Lock All", "lstwo.AchievementManager.LockAll");

        ui.AddSpacer(6);

        ui.CreateButton("Inspect \"Achievement Manager\" Component", () =>
        {
            if(global::AchievementManager.InstanceExists)
            {
                InspectorManager.Inspect(global::AchievementManager.Instance);
                UIManager.ShowMenu = true;
            }
        }, "lstwo.AchievementManager.Inspect", null, 256 * 3 + 32 * 2, 32);

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        achievements = (WobblyAchievement[])Enum.GetValues(typeof(WobblyAchievement));

        achievementDropdown.ClearOptions();

        foreach (var achievement in achievements)
        {
            achievementDropdown.options.Add(new(Enum.GetName(typeof(WobblyAchievement), achievement)));
        }

        achievementDropdown.RefreshShownValue();
    }

    public override void Update()
    {
    }
}