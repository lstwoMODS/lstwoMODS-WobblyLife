using lstwoMODS_WobblyLife.UI.TabMenus;
using NWH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using ModWobblyLife;
using UnityEngine.Localization;

namespace lstwoMODS_WobblyLife.Hacks;

public class VehicleUnlocker : BaseHack
{
    public override string Name => "Vehicle Unlocker";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.SaveHacksTab;

    private List<RewardVehicleData> vehicleRewards = new();
    private HacksUIHelper.LDBTrio unlockVehicleLDB;
    private HacksUIHelper.LDBTrio lockVehicleLDB;

    public void UnlockAllVehicles()
    {
        var playerController = GameInstance.Instance.GetFirstLocalPlayerController();

        if (!playerController)
        {
            return;
        }

        foreach (var vehicleReward in vehicleRewards)
        {
            vehicleReward.Reward([playerController]);
        }
    }

    public void UnlockVehicle(RewardData vehicle)
    {
        var playerController = GameInstance.Instance.GetFirstLocalPlayerController();
            
        if (!playerController)
        {
            return;
        }
            
        vehicle.Reward([playerController]);
    }

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        ui.CreateLabel("NOTE: This does NOT include purchasable vehicles");
            
        ui.AddSpacer(6);

        unlockVehicleLDB = ui.CreateLDBTrio("Unlock Vehicle", "unlockVehicle", onClick: () =>
            {
                if (vehicleRewards == null || unlockVehicleLDB.Dropdown.value >= vehicleRewards.Count)
                {
                    return;
                }

                UnlockVehicle(vehicleRewards[unlockVehicleLDB.Dropdown.value]);
            },
            buttonText: "Unlock");
            
        ui.AddSpacer(6);

        ui.CreateLBDuo("Vehicle Unlocker", "lstwo.VehicleUnlocker.Vehicle Unlocker", UnlockAllVehicles, "Unlock All Vehicles", "lstwo.VehicleUnlocker.UnlockAll");

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        var rewardManager = RewardManagerInstance.Instance;
        var rewardDatabase = typeof(RewardManager).GetField("managerDatabase", lstwoMODS_Core.Plugin.Flags)?.GetValue(rewardManager);
        var rewardDataDict = (Dictionary<Guid, RewardData>)typeof(RewardManagerDatabase).GetField("registeredRewardsHashMap", lstwoMODS_Core.Plugin.Flags)?.GetValue(rewardDatabase);
        var vehicleRewards = rewardDataDict?.Where(x => x.Value is RewardVehicleData).Select(x => x.Value as RewardVehicleData).ToList();

        if (vehicleRewards == null)
        {
            return;
        }

        this.vehicleRewards = vehicleRewards;
        
        unlockVehicleLDB.Dropdown.ClearOptions();
        var nameField = typeof(RewardVehicleData).GetField("textLocalized", lstwoMODS_Core.Plugin.Flags);

        foreach (var vehicleOption in this.vehicleRewards.Select(vehicle => (LocalizedString)nameField.GetValue(vehicle)))
        {
            unlockVehicleLDB.Dropdown.options.Add(new(vehicleOption.GetLocalizedString()));
        }

        unlockVehicleLDB.Dropdown.RefreshShownValue();
    }

    public override void Update()
    {
    }
}