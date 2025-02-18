using lstwoMODS_WobblyLife.UI.TabMenus;
using NWH;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class VehicleUnlocker : BaseHack
    {
        public override string Name => "Vehicle Unlocker";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        private List<PlayerVehicle> allVehicles;
        private HacksUIHelper.LDBTrio unlockVehicleLDB;
        private HacksUIHelper.LDBTrio lockVehicleLDB;

        public void UnlockAllVehicles()
        {
            var playerController = PlayerUtils.GetMyPlayer();

            if (!playerController)
            {
                return;
            }

            var Player = new PlayerRef();
            Player.SetPlayerController(playerController);

            foreach (var vehicle in allVehicles)
            {
                var reference = VehicleManager.Instance.GetVehicleReference(vehicle.GetAssetId());
                Player.ControllerUnlocker.UnlockVehicle(reference);
            }
        }

        public void LockAllVehicles()
        {
            var playerController = PlayerUtils.GetMyPlayer();

            if (!playerController)
            {
                return;
            }

            var Player = new PlayerRef();
            Player.SetPlayerController(playerController);

            foreach (var vehicle in allVehicles)
            {
                var reference = VehicleManager.Instance.GetVehicleReference(vehicle.GetAssetId());
                Player.ControllerUnlocker.LockVehicle(reference);
            }
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            /*ui.AddSpacer(6);

            unlockVehicleLDB = ui.CreateLDBTrio("Unlock Vehicle", "unlockClothe", onClick: () =>
            {
                var playerController = PlayerUtils.GetMyPlayer();

                if (!playerController)
                {
                    return;
                }

                var Player = new PlayerRef();
                Player.SetPlayerController(playerController);

                var index = unlockVehicleLDB.Dropdown.value;
                var vehicle = allVehicles[index];
                var reference = VehicleManager.Instance.GetVehicleReference(vehicle.GetAssetId());

                Player.ControllerUnlocker.UnlockVehicle(reference);
            },
            buttonText: "Unlock");

            ui.AddSpacer(6);

            lockVehicleLDB = ui.CreateLDBTrio("Lock Vehicle", "lockClothe", onClick: () =>
            {
                var playerController = PlayerUtils.GetMyPlayer();

                if (!playerController)
                {
                    return;
                }

                var Player = new PlayerRef();
                Player.SetPlayerController(playerController);

                var index = lockVehicleLDB.Dropdown.value;
                var vehicle = allVehicles[index];
                var reference = VehicleManager.Instance.GetVehicleReference(vehicle.GetAssetId());

                Player.ControllerUnlocker.LockVehicle(reference);
            },
            buttonText: "Unlock");*/

            ui.AddSpacer(6);

            ui.CreateLBBTrio("Vehicle Unlocker", "lstwo.VehicleUnlocker.Vehicle Unlocker", UnlockAllVehicles, "Unlock All Vehicles", "lstwo.VehicleUnlocker.UnlockAll", 
                LockAllVehicles, "Lock All Vehicles", "lstwo.VehicleUnlocker.LockAll");

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            var vehicleManager = VehicleManager.Instance;

            if (!vehicleManager)
            {
                return;
            }

            allVehicles = vehicleManager.GetVehicles();

            /*unlockVehicleLDB.Dropdown.ClearOptions();
            lockVehicleLDB.Dropdown.ClearOptions();

            foreach (var vehicle in allVehicles)
            {
                var vehicleOption = vehicle.name;

                unlockVehicleLDB.Dropdown.options.Add(new(vehicleOption));
                lockVehicleLDB.Dropdown.options.Add(new(vehicleOption));
            }

            unlockVehicleLDB.Dropdown.RefreshShownValue();
            lockVehicleLDB.Dropdown.RefreshShownValue();*/
        }

        public override void Update()
        {
        }
    }
}
