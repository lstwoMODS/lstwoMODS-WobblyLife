using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

internal class PresentUnlocker : BaseMod
{
    public override string Name => "Present Manager";
    public override string Description => "Manages your Presents.";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    [ModAction(Order = 10)]
    public static void UnlockAllPresents()
    {
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

    [ModAction(Order = 20)]
    public static void LockAllPresents()
    {
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