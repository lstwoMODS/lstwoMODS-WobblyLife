using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class RoadVehicleModifier : PlayerBasedMod
{
    public override string Name => "Road Vehicle Modifier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.VehicleModsWindow;

    private Ref<bool> _settingsDisabled = new();

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new TextWrapped("NoVehicleInfo", "Player is not currently in a road vehicle.").WithVisible(_settingsDisabled),
            
            new Group("Settings",
                base.BuildPanel(id),
                
                new Button("Inspect \"Road Vehicle\" Game Object", () =>
                {
                    if (Vehicle)
                    {
                        InspectorManager.Inspect(Vehicle.gameObject);
                        UIManager.ShowMenu = true;
                    }
                }).WithContentWidth()
            ).WithDisabled(_settingsDisabled)
        );
    }

    public override void RefreshUI()
    {
        base.RefreshUI();
        _settingsDisabled.Value = Vehicle == null;
    }

    public PlayerVehicleRoad Vehicle => Player?.ControllerInteractor.GetEnteredAction()?.GetGameObject()?.GetComponent<PlayerVehicleRoad>();
    public PlayerVehicleRoadMovement Movement => Vehicle?.GetVehicleMovementBase() as PlayerVehicleRoadMovement;

    [ModSetting(Order = 10)]
    public bool IsVehicleIndestructible
    {
        get => Vehicle?.IsIndestructable() ?? false;
        set => Vehicle?.SetIndestructable(this, value);
    }

    [ModSetting(Order = 20)]
    public bool LockVehicleMovement
    {
        get => Vehicle?.IsLockedMovement() ?? false;
        set => Vehicle?.SetLockMovement(this, value);
    }

    [ModSetting(Order = 30)]
    public bool IsPersonalVehicle
    {
        get => Vehicle?.IsPersonalVehicle() ?? false;
        set => Vehicle?.SetIsPersonalVehicle(value);
    }

    [ModSetting(Order = 40)]
    public float DamageSpeed
    {
        get => Movement?.damageSpeedMul ?? 0;
        set => Movement?.SetDamageSpeedMul(value);
    }

    [ModSetting(Order = 50)]
    public float TopSpeed
    {
        get => Movement?.topSpeedMph ?? 0;
        set
        {
            Movement?.topSpeedMph = value;
            Movement?.SetTopSpeedMPHDefault();
        }
    }

    [ModSetting(Order = 60)]
    public float DownForce
    {
        get => Movement?.downForce ?? 0;
        set => Movement?.downForce = value;
    }

    [ModSetting(Order = 70)]
    public float WheelStiffness
    {
        get => Movement?.wheelStiffness ?? 0;
        set => Movement?.wheelStiffness = value;
    }

    [ModSetting(Order = 80)]
    public bool HasBoost
    {
        get => Movement?.bAllowBoost ?? false;
        set
        {
            Movement?.bAllowBoost = value;
            
            if (Movement != null && !ServerSettings.PlayerVehicleRoadMovementPatch.BoostEnabled.TryAdd(Movement, value))
            {
                ServerSettings.PlayerVehicleRoadMovementPatch.BoostEnabled[Movement] = value;
            }
        }
    }

    [ModSetting(Order = 90)]
    public float BoostPower
    {
        get => Movement?.boostPow ?? 0;
        set => Movement?.boostPow = value;
    }

    [ModSetting(Order = 100)]
    public float BoostSeconds
    {
        get => Movement?.boostSeconds ?? 0;
        set => Movement?.boostSeconds = value;
    }
}