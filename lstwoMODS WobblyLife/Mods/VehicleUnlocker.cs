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
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using ModWobblyLife;
using UMod.Settings;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using VisualDesignCafe.Threading;

namespace lstwoMODS_WobblyLife.Mods;

public class VehicleUnlocker : BaseMod
{
    public override string Name => "Vehicle Unlocker";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    public static AllVehiclesAssetReferences AllGameVehicleAssetReferences => VehicleManager.Instance.allVehiclesAssetReferences[0];
    public static AllVehiclesAssetReferences AllSpaceVehicleAssetReferences => VehicleManager.Instance.allVehiclesAssetReferences[1];
    
    private Ref<string[]> gameVehicleDropdownItems = new([]);
    private Ref<int> gameVehicleDropdownIndex = new();
    
    private Ref<string[]> spaceVehicleDropdownItems = new([]); 
    private Ref<int> spaceVehicleDropdownIndex = new();

    private int vehicleLoadCounter;

    [ModAction(ShowInUI = false)]
    public static void UnlockVehicle(VehicleAssetReference vehicleAssetReference)
    {
        if (SaveGuard.On) return;

        var player = (PlayerRef) GameInstance.Instance?.GetFirstLocalPlayerController();
        player?.ControllerUnlocker?.UnlockVehicle(vehicleAssetReference);
    }

    [ModAction(ShowInUI = false)]
    public static void LockVehicle(VehicleAssetReference vehicleAssetReference)
    {
        if (SaveGuard.On) return;

        var player = (PlayerRef) GameInstance.Instance?.GetFirstLocalPlayerController();
        player?.ControllerUnlocker?.LockVehicle(vehicleAssetReference);
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockAllGameVehicles()
    {
        foreach (var vehicle in AllGameVehicleAssetReferences.vehicleAssetReferences)
        {
            UnlockVehicle(vehicle);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void LockAllGameVehicles()
    {
        foreach (var vehicle in AllGameVehicleAssetReferences.vehicleAssetReferences)
        {
            LockVehicle(vehicle);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockAllSpaceVehicles()
    {
        foreach (var vehicle in AllSpaceVehicleAssetReferences.vehicleAssetReferences)
        {
            UnlockVehicle(vehicle);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void LockAllSpaceVehicles()
    {
        foreach (var vehicle in AllSpaceVehicleAssetReferences.vehicleAssetReferences)
        {
            LockVehicle(vehicle);
        }
    }
    
    public override Container BuildPanel(string id)
    {
        return new Container(id,

            SaveGuard.Notice("vehicle-guard-notice"),

            SaveGuard.Guard(new Group("WobblyIsland",

                new SeparatorText("Wobbly Island Vehicles", "Wobbly Island Vehicles"),
        
                new SearchableCombo("Select Vehicle", []).WithItems(gameVehicleDropdownItems).WithSelectedIndex(gameVehicleDropdownIndex),
            
                new HStack("Actions",
                    ActionMenu(new Button("Unlock Vehicle", () => UnlockVehicle(AllGameVehicleAssetReferences.vehicleAssetReferences[gameVehicleDropdownIndex.Value])), nameof(UnlockVehicle)),
                    ActionMenu(new Button("Lock Vehicle", () => LockVehicle(AllGameVehicleAssetReferences.vehicleAssetReferences[gameVehicleDropdownIndex.Value])), nameof(LockVehicle))
                ).WithContentWidth(),

                new HStack("All Vehicle Actions",
                    ActionMenu(new Button("Unlock All Vehicles", UnlockAllGameVehicles).WithTooltip("May cause lag spike"), nameof(UnlockAllGameVehicles)),
                    ActionMenu(new Button("Lock All Vehicles", LockAllGameVehicles), nameof(LockAllGameVehicles))
                ).WithContentWidth()
                
            ).WithId("WobblyIsland")),


            SaveGuard.Guard(new Group("Space",

                new SeparatorText("Space Vehicles", "Space Vehicles"),
        
                new SearchableCombo("Select Vehicle", []).WithItems(spaceVehicleDropdownItems).WithSelectedIndex(spaceVehicleDropdownIndex),
            
                new HStack("Actions",
                    ActionMenu(new Button("Unlock Vehicle", () => UnlockVehicle(AllSpaceVehicleAssetReferences.vehicleAssetReferences[spaceVehicleDropdownIndex.Value])), nameof(UnlockVehicle)),
                    ActionMenu(new Button("Lock Vehicle", () => LockVehicle(AllSpaceVehicleAssetReferences.vehicleAssetReferences[spaceVehicleDropdownIndex.Value])), nameof(LockVehicle))
                ).WithContentWidth(),

                new HStack("All Vehicle Actions",
                    ActionMenu(new Button("Unlock All Vehicles", UnlockAllSpaceVehicles).WithTooltip("May cause lag spike"), nameof(UnlockAllSpaceVehicles)),
                    ActionMenu(new Button("Lock All Vehicles", LockAllSpaceVehicles), nameof(LockAllSpaceVehicles))
                ).WithContentWidth()
                
            ).WithId("Space"))
        );
    }

    protected override void OnStaticInit()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (vehicleLoadCounter > 0 || mode != LoadSceneMode.Single || scene.name != "MainMenu") return;

            void AddGameVehicle(GameObject vehicle)
            {
                var temp = gameVehicleDropdownItems.Value.ToList();
                temp.Add(vehicle.name);
                gameVehicleDropdownItems.Value = temp.ToArray();
                vehicleLoadCounter++;
            }

            foreach (var vehicleAssetReference in AllGameVehicleAssetReferences.vehicleAssetReferences)
            {
                if (vehicleAssetReference.prefab.Asset)
                {
                    AddGameVehicle(vehicleAssetReference.prefab.Asset as GameObject);
                    continue;
                }

                vehicleAssetReference.prefab.LoadAssetAsync<GameObject>().Completed += handle =>
                {
                    AddGameVehicle(handle.Result);
                };
            }


            void AddSpaceVehicle(GameObject vehicle)
            {
                var temp = spaceVehicleDropdownItems.Value.ToList();
                temp.Add(vehicle.name);
                spaceVehicleDropdownItems.Value = temp.ToArray();
                vehicleLoadCounter++;
            }

            foreach (var vehicleAssetReference in AllSpaceVehicleAssetReferences.vehicleAssetReferences)
            {
                if (vehicleAssetReference.prefab.Asset)
                {
                    AddSpaceVehicle(vehicleAssetReference.prefab.Asset as GameObject);
                    continue;
                }

                vehicleAssetReference.prefab.LoadAssetAsync<GameObject>().Completed += handle =>
                {
                    AddSpaceVehicle(handle.Result);
                };
            }
        };
    }
}