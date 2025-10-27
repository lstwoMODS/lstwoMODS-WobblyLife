using IngameDebugConsole;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.Hacks.ModActions;

namespace lstwoMODS_WobblyLife.Hacks;

internal class PresentUnlocker : BaseMod
{
    public override string Name => "Present Manager";
    public override string Description => "Manages your Presents.";
    public override ModsTab ModsTab => Plugin.SaveModsTab;

    private bool showPresentsOnMap;

    [ModAction(Name = "Unlock All Presents")]
    public static void UnlockAll()
    {
        var Player = new PlayerRef();
        Player.SetPlayerController(GameInstance.Instance.GetFirstLocalPlayerController());

        if (Player == null) return;
        if (!Player.Controller.networkObject.IsOwner()) return;

        foreach (string gUID in (List<string>)typeof(PresentManager).GetField("presentsGUIDs", Plugin.Flags)
                     .GetValue(PresentManager.Instance))
        {
            Player.Controller.GetPlayerPersistentData().MiscData.UnlockPresent(Guid.Parse(gUID));
        }

        Player.ControllerUnlocker.ShowCounter(PromptCounterType.Present);
        typeof(PlayerControllerUnlocker).GetMethod("OnPresentUnlockedChanged", Plugin.Flags)
            .Invoke(Player.ControllerUnlocker, null);
    }

    [ModAction(Name = "Lock All Presents")]
    public void LockAll()
    {
        var Player = new PlayerRef();
        Player.SetPlayerController(GameInstance.Instance.GetFirstLocalPlayerController());

        if (Player == null) return;
        if (!Player.Controller.networkObject.IsOwner()) return;

        Player.Controller.GetPlayerControllerUnlocker().LockAllPresents();

        Player.Controller.GetPlayerControllerUnlocker().ShowCounter(PromptCounterType.Present);
        typeof(PlayerControllerUnlocker).GetMethod("OnPresentUnlockedChanged", Plugin.Flags)
            .Invoke(Player.Controller.GetPlayerControllerUnlocker(), null);
    }

    [ModAction(Name = "Show Presents on Map")]
    public static void ShowPresentsOnMap(bool b)
    {
        PresentManager.Instance.SetShowAllPresentsOnMinimap(b);
    }

    public override void RenderUI()
    {
        ImGui.Text("Present Unlocker");
        ImGui.SameLine();
        
        if (ImGui.Button("Unlock All"))
        {
            UnlockAll();
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Lock All"))
        {
            LockAll();
        }

        if (ImGui.Checkbox("Show Presents on Map", ref showPresentsOnMap))
        {
            ShowPresentsOnMap(showPresentsOnMap);
        }
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
        showPresentsOnMap = PresentManager.Instance?.IsShowingAllPresentsOnMinimap() ?? false;
    }
}