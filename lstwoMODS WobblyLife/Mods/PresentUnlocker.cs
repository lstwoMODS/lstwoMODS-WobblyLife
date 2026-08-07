using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

internal class PresentUnlocker : BaseMod
{
    public override string Name => "Present Manager";
    public override string Description => "Manages your Presents.";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            SaveGuard.Notice("present-guard-notice"),

            SaveGuard.Guard(ActionMenu(new Button("Unlock All Presents", UnlockAllPresents).WithContentWidth(), nameof(UnlockAllPresents))),
            SaveGuard.Guard(ActionMenu(new Button("Lock All Presents", LockAllPresents).WithContentWidth(), nameof(LockAllPresents))),

            // Minimap-only toggle, nothing persistent, so it stays usable while the guard is on.
            base.BuildPanel(id)
        );
    }

    [ModAction(Order = 10, ShowInUI = false)]
    public static void UnlockAllPresents()
    {
        if (SaveGuard.On) return;

        var player = new PlayerRef();
        player.SetPlayerController(GameInstance.Instance.GetFirstLocalPlayerController());

        if (player.Controller == null || !player.Controller.networkObject.IsOwner() || !PresentManager.InstanceExists) return;

        foreach (var guid in PresentManager.Instance.presentsGUIDs)
        {
            player.Controller.GetPlayerPersistentData().MiscData.UnlockPresent(Guid.Parse(guid));
        }

        player.ControllerUnlocker.ShowCounter(PromptCounterType.Present);
        player.ControllerUnlocker.OnPresentUnlockedChanged();
    }

    [ModAction(Order = 20, ShowInUI = false)]
    public static void LockAllPresents()
    {
        if (SaveGuard.On) return;

        var player = new PlayerRef();
        player.SetPlayerController(GameInstance.Instance.GetFirstLocalPlayerController());

        if (player.Controller == null || !player.Controller.networkObject.IsOwner() || !PresentManager.InstanceExists) return;

        player.Controller.GetPlayerControllerUnlocker().LockAllPresents();
        player.Controller.GetPlayerControllerUnlocker().ShowCounter(PromptCounterType.Present);
        player.ControllerUnlocker.OnPresentUnlockedChanged();
    }

    [ModSetting(Order = 30)]
    public static bool ShowPresentsOnMap
    {
        get => PresentManager.Instance?.IsShowingAllPresentsOnMinimap() ?? false;
        set => PresentManager.Instance?.SetShowAllPresentsOnMinimap(value);
    }
}