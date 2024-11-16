using IngameDebugConsole;
using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks.Paid
{
    public class ClothesManager : BaseHack
    {
        public override string Name => "Clothing Manager";

        public override string Description => "Unlock or Lock all Clothes!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public void UnlockAllClothes()
        {
            if (Player == null) return;
            if (!Player.Controller.networkObject.IsOwner()) return;

            foreach (ClothingAssetReference clothing in ClothingManager.Instance.GetAllClothingReferences())
            {
                Player.ControllerUnlocker.UnlockClothing(Plugin.Instance, clothing);
            }
        }

        public void LockAllClothes()
        {
            if (Player == null) return;
            if (!Player.Controller.networkObject.IsOwner()) return;

            foreach (ClothingAssetReference clothing in ClothingManager.Instance.GetAllClothingReferences())
            {
                Player.ControllerUnlocker.LockClothing(clothing);
            }
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Clothing Unlocker", "Clothing Unlocker", UnlockAllClothes, "Unlock All Clothes", LockAllClothes, "Lock All Clothes");

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
