using IngameDebugConsole;
using lstwoMODS_WobblyLife.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class ClothesManager : BaseHack
    {
        public override string Name => "Clothing Manager";

        public override string Description => "Unlock or Lock all Clothes!";

        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        private List<ClothingAssetReference> allClothing;
        private HacksUIHelper.LDBTrio unlockClotheLDB;
        private HacksUIHelper.LDBTrio lockClotheLDB;

        public void UnlockAllClothes()
        {
            var playerController = PlayerUtils.GetMyPlayer();

            if (!playerController)
            {
                return;
            }

            var Player = new PlayerRef();
            Player.SetPlayerController(playerController);

            foreach (var clothing in allClothing)
            {
                Player.ControllerUnlocker.UnlockClothing(Plugin.Instance, clothing);
            }
        }

        public void LockAllClothes()
        {
            var playerController = PlayerUtils.GetMyPlayer();

            if (!playerController)
            {
                return;
            }

            var Player = new PlayerRef();
            Player.SetPlayerController(playerController);

            foreach (var clothing in allClothing)
            {
                Player.ControllerUnlocker.LockClothing(clothing);
            }
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            /*ui.AddSpacer(6);

            unlockClotheLDB = ui.CreateLDBTrio("Unlock Clothing Piece", "unlockClothe", onClick: () =>
            {
                var playerController = PlayerUtils.GetMyPlayer();

                if (!playerController)
                {
                    return;
                }

                var Player = new PlayerRef();
                Player.SetPlayerController(playerController);

                var index = unlockClotheLDB.Dropdown.value;
                var clothingReference = allClothing[index];

                Player.ControllerUnlocker.UnlockClothing(this, clothingReference);
            }, 
            buttonText: "Unlock");

            ui.AddSpacer(6);

            lockClotheLDB = ui.CreateLDBTrio("Lock Clothing Piece", "lockClothe", onClick: () =>
            {
                var playerController = PlayerUtils.GetMyPlayer();

                if (!playerController)
                {
                    return;
                }

                var Player = new PlayerRef();
                Player.SetPlayerController(playerController);

                var index = lockClotheLDB.Dropdown.value;
                var clothingReference = allClothing[index];

                Player.ControllerUnlocker.LockClothing(clothingReference);
            },
            buttonText: "Unlock");*/

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Clothing Unlocker", "Clothing Unlocker", UnlockAllClothes, "Unlock All Clothes", LockAllClothes, "Lock All Clothes");

            ui.AddSpacer(6);
        }

        public override void Update()
        {
        }

        public override void RefreshUI()
        {
            var clothingManager = ClothingManager.Instance;

            if (!clothingManager)
            {
                return;
            }

            allClothing = clothingManager.GetAllClothingReferences().ToList();

            /*unlockClotheLDB.Dropdown.ClearOptions();
            lockClotheLDB.Dropdown.ClearOptions();

            foreach (var clothing in allClothing)
            {
                var clothingOption = clothing.clothingIcon.;

                unlockClotheLDB.Dropdown.options.Add(new(clothingOption));
                lockClotheLDB.Dropdown.options.Add(new(clothingOption));
            }

            unlockClotheLDB.Dropdown.RefreshShownValue();
            lockClotheLDB.Dropdown.RefreshShownValue();*/
        }
    }
}
