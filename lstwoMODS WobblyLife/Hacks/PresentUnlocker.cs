using IngameDebugConsole;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using ShadowLib;

namespace lstwoMODS_WobblyLife.Hacks
{
    internal class PresentUnlocker : BaseHack
    {
        public override string Name => "Present Manager";

        public override string Description => "Manages your Presents.";

        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        public void UnlockAll()
        {
            var Player = new PlayerRef();
            Player.SetPlayerController(PlayerUtils.GetMyPlayer());

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

        public void LockAll()
        {
            var Player = new PlayerRef();
            Player.SetPlayerController(PlayerUtils.GetMyPlayer());

            if (Player == null) return;
            if (!Player.Controller.networkObject.IsOwner()) return;

            Player.Controller.GetPlayerControllerUnlocker().LockAllPresents();

            Player.Controller.GetPlayerControllerUnlocker().ShowCounter(PromptCounterType.Present);
            typeof(PlayerControllerUnlocker).GetMethod("OnPresentUnlockedChanged", Plugin.Flags)
                .Invoke(Player.Controller.GetPlayerControllerUnlocker(), null);
        }

        public static void ShowPresentsOnMap(bool b)
        {
            PresentManager.Instance.SetShowAllPresentsOnMinimap(b);
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Present Unlocker", "PresentUnlocker", UnlockAll, "Unlock All", LockAll, "Lock All");

            ui.AddSpacer(6);

            ui.CreateToggle("ShowPresentsOnMap", "Show Presents on Map", ShowPresentsOnMap);

            ui.AddSpacer(6);
        }

        public override void Update()
        {
        }

        public override void RefreshUI()
        {
        }
    }
}
