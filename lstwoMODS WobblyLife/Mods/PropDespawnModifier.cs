using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class PropDespawnModifier : BaseMod
{
    public override string Name => "Prop Despawn Modifier";
    public override string Description => "Controls when the host despawns props: streaming distance, the deep-water sink timer and the out-of-chunk cleanup.";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    private const float PruneInterval = 5f;

    private static int _chunkDistanceOverride;

    private static readonly Ref<bool> NotHost = new();
    private float _nextPrune;

    [ModSetting(Order = 10, Min = 1, Max = 16, Widget = WidgetType.Slider,
        Description = "Chunks kept loaded around each player. Props outside the loaded area are destroyed, so this is the despawn radius. Vanilla clamps this to 3-5 (150-250m), and higher values keep more of the world, and everything in it, alive at once.")]
    public static int ChunkDistance
    {
        get
        {
            var worldManager = WorldManager.InstanceExists ? WorldManager.Instance : null;
            if (worldManager) return (int)worldManager.viewDistance;
            return _chunkDistanceOverride > 0 ? _chunkDistanceOverride : 3;
        }
        set
        {
            _chunkDistanceOverride = value;
            ApplyChunkDistance();
        }
    }

    [ModSetting(Order = 20, Label = "Disable Water Despawn", SeparatorText = "",
        Description = "Props dropped in deep water normally sink and are destroyed after 6 seconds. Turning this on also rescues whatever is already sinking; turning it off re-arms the timer on everything still floating.")]
    public static Ref<bool> NoWaterDespawn = new();

    [ModSetting(Order = 30, Label = "Disable Out-of-Chunk Despawn",
        Description = "Keeps spawned props alive when their chunk unloads by taking them out of the chunk system. They freeze in place while the terrain around them is unloaded. Scene props are left alone, since the game already replaces those with a spawned copy once a player grabs them. Note that nothing is ever cleaned up while this is on, so object count only grows.")]
    public static Ref<bool> NoChunkDespawn = new();

    protected override void OnStaticInit()
    {
        new Harmony("net.lstwo.lstwoMODS.WobblyLife.PropDespawnModifier").PatchAll(typeof(Patches));

        NoWaterDespawn.Changed += value =>
        {
            if (value) EnableWaterProtection();
            else DisableWaterProtection();
        };

        NoChunkDespawn.Changed += value =>
        {
            if (value) EnableChunkProtection();
            else DisableChunkProtection();
        };
    }

    public override void Update()
    {
        var notHost = !IsHost;
        if (NotHost.Value != notHost) NotHost.Value = notHost;

        if (Time.unscaledTime < _nextPrune) return;
        _nextPrune = Time.unscaledTime + PruneInterval;
        PruneDead();
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new TextWrapped("PropDespawnHostOnly",
                    $"{Lucide.TriangleAlert} Host only. The game destroys props server-side, so these settings do nothing while you are a client.")
                .WithVisible(NotHost),

            base.BuildPanel(id)
        );
    }

    [ModAction(Order = 40, Label = "Reset Chunk Distance",
        Description = "Drops the override and hands the streaming distance back to the game's settings option.")]
    public static void ResetChunkDistance()
    {
        _chunkDistanceOverride = 0;

        var worldManager = WorldManager.InstanceExists ? WorldManager.Instance : null;
        if (!worldManager) return;

        var options = SaveGameManager.Instance ? SaveGameManager.Instance.GetOptionsData() : null;
        worldManager.SetViewDistance(options?.chunkDistance ?? 4f);
    }

    private static bool IsHost
    {
        get
        {
            var manager = HawkNetworkManager.InstanceExists ? HawkNetworkManager.DefaultInstance : null;
            return manager != null && manager.IsServer();
        }
    }

    private static void ApplyChunkDistance()
    {
        if (_chunkDistanceOverride <= 0) return;

        var worldManager = WorldManager.InstanceExists ? WorldManager.Instance : null;
        if (!worldManager) return;

        worldManager.viewDistance = _chunkDistanceOverride;
        worldManager.viewDistanceHalfMulChunkSize = (int)(_chunkDistanceOverride * 0.5f * WorldManager.ChunkSize);
        worldManager.destroyDistanceHalfMulChunkSize = (int)(_chunkDistanceOverride * 1.1f * 0.5f * WorldManager.ChunkSize);
    }

    #region Water despawn

    private static readonly HashSet<DynamicObject> InDeepWater = new();

    private static readonly HashSet<DynamicObject> WaterOverridden = new();

    private static readonly FieldInfo FDestroyCoroutine = AccessTools.Field(typeof(HawkNetworkObject), "destoryCoroutine");

    private static void EnableWaterProtection()
    {
        PruneDead();

        foreach (var dynamicObject in InDeepWater)
        {
            ProtectFromWater(dynamicObject);
            RescueFromWater(dynamicObject);
        }
    }

    private static void DisableWaterProtection()
    {
        PruneDead();

        foreach (var dynamicObject in WaterOverridden)
        {
            if (dynamicObject) dynamicObject.SetOverrideDestroyInWater(false);
        }

        WaterOverridden.Clear();

        foreach (var dynamicObject in InDeepWater) ReArmWaterDespawn(dynamicObject);
    }

    private static void ProtectFromWater(DynamicObject dynamicObject)
    {
        if (!dynamicObject || dynamicObject.bOverrideDestoryInWater) return;

        dynamicObject.SetOverrideDestroyInWater(true);
        WaterOverridden.Add(dynamicObject);
    }

    private static void RescueFromWater(DynamicObject dynamicObject)
    {
        if (!dynamicObject) return;

        var networkObject = dynamicObject.networkObject;
        if (networkObject == null || !networkObject.IsServer()) return;

        if (dynamicObject.inWaterDestroyCoroutine == null) return;

        dynamicObject.StopCoroutine(dynamicObject.inWaterDestroyCoroutine);
        dynamicObject.inWaterDestroyCoroutine = null;

        if (!CancelPendingDestroy(networkObject)) return;
        if (!dynamicObject.gameObject.activeSelf) VanishComponent.SetVisible(dynamicObject.gameObject, true);
    }

    private static void ReArmWaterDespawn(DynamicObject dynamicObject)
    {
        if (!dynamicObject) return;

        var networkObject = dynamicObject.networkObject;
        if (networkObject == null || !networkObject.IsServer()) return;

        if (dynamicObject.bOverrideDestoryInWater) return;
        if (dynamicObject.inWaterDestroyCoroutine != null) return;
        if (!dynamicObject.worldDynamicObject || dynamicObject.waterMask) return;
        if (dynamicObject.waterObjects != null && dynamicObject.waterObjects.Length != 0) return;
        if (!dynamicObject.gameObject.activeInHierarchy) return;

        dynamicObject.inWaterDestroyCoroutine = dynamicObject.StartCoroutine(dynamicObject.DestroyDynamicObjectInWaterEnumerator());
    }

    private static bool CancelPendingDestroy(HawkNetworkObject networkObject)
    {
        if (FDestroyCoroutine == null || !NetworkCoroutine.InstanceExists) return false;
        if (FDestroyCoroutine.GetValue(networkObject) is not Coroutine coroutine) return false;

        NetworkCoroutine.Instance.StopCoroutine(coroutine);
        FDestroyCoroutine.SetValue(networkObject, null);
        return true;
    }

    #endregion

    #region Out-of-chunk despawn

    private struct ChunkState
    {
        public bool MoveScene;
        public bool AllowSceneChange;
    }

    private static readonly Dictionary<WorldDynamicObject, ChunkState> ChunkOverrides = new();
    private static readonly List<WorldDynamicObject> DeadChunkKeys = new();

    private static void EnableChunkProtection()
    {
        foreach (var worldDynamicObject in AllWorldDynamicObjects()) ProtectFromChunkUnload(worldDynamicObject);
    }

    private static void DisableChunkProtection()
    {
        foreach (var pair in ChunkOverrides)
        {
            if (!pair.Key) continue;

            pair.Key.SetMoveSceneOnChunkChanged(pair.Value.MoveScene);

            pair.Key.SetAllowedSceneChanged(pair.Value.AllowSceneChange);
        }

        ChunkOverrides.Clear();
    }

    private static void ProtectFromChunkUnload(WorldDynamicObject worldDynamicObject, bool? vanillaState = null)
    {
        if (!worldDynamicObject) return;

        var networkObject = worldDynamicObject.networkObject;
        if (networkObject == null || !networkObject.IsServer()) return;

        if (networkObject.IsSceneNetworkObject()) return;

        var behaviour = networkObject.GetNetworkBehaviour();
        if (behaviour is PlayerCharacter or StaticObject) return;

        if (vanillaState.HasValue)
        {
            ChunkOverrides[worldDynamicObject] = new ChunkState
            {
                MoveScene = vanillaState.Value,
                AllowSceneChange = vanillaState.Value
            };
        }
        else if (!ChunkOverrides.ContainsKey(worldDynamicObject))
        {
            ChunkOverrides[worldDynamicObject] = new ChunkState
            {
                MoveScene = worldDynamicObject.IsMoveSceneOnChunkChanged(),
                AllowSceneChange = worldDynamicObject.IsAllowedSceneChange()
            };
        }

        worldDynamicObject.SetMoveSceneOnChunkChanged(false);

        worldDynamicObject.SetAllowedSceneChanged(false);
    }

    private static IEnumerable<WorldDynamicObject> AllWorldDynamicObjects()
    {
        if (WorldDynamicObjectManager.InstanceExists) return WorldDynamicObjectManager.Instance.worldDynamicObjects;

        return UnityEngine.Object.FindObjectsOfType<WorldDynamicObject>();
    }

    #endregion

    private static void PruneDead()
    {
        InDeepWater.RemoveWhere(dynamicObject => !dynamicObject);
        WaterOverridden.RemoveWhere(dynamicObject => !dynamicObject);

        if (ChunkOverrides.Count == 0) return;

        DeadChunkKeys.Clear();

        foreach (var worldDynamicObject in ChunkOverrides.Keys)
        {
            if (!worldDynamicObject) DeadChunkKeys.Add(worldDynamicObject);
        }

        foreach (var worldDynamicObject in DeadChunkKeys) ChunkOverrides.Remove(worldDynamicObject);

        DeadChunkKeys.Clear();
    }

    public static class Patches
    {
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.SetViewDistance)), HarmonyPostfix]
        public static void SetViewDistance_Postfix()
        {
            ApplyChunkDistance();
        }

        [HarmonyPatch(typeof(DynamicObject), nameof(DynamicObject.OnEnteredWater)), HarmonyPrefix]
        public static void OnEnteredWater_Prefix(DynamicObject __instance)
        {
            if (NoWaterDespawn.Value) ProtectFromWater(__instance);
        }

        [HarmonyPatch(typeof(DynamicObject), nameof(DynamicObject.OnEnteredWater)), HarmonyPostfix]
        public static void OnEnteredWater_Postfix(DynamicObject __instance, Water water)
        {
            if (water && water.IsDeep()) InDeepWater.Add(__instance);
        }

        [HarmonyPatch(typeof(DynamicObject), nameof(DynamicObject.OnExitedWater)), HarmonyPostfix]
        public static void OnExitedWater_Postfix(DynamicObject __instance)
        {
            InDeepWater.Remove(__instance);
        }

        [HarmonyPatch(typeof(WorldDynamicObject), nameof(WorldDynamicObject.NetworkPost)), HarmonyPostfix]
        public static void NetworkPost_Postfix(WorldDynamicObject __instance)
        {
            if (NoChunkDespawn.Value) ProtectFromChunkUnload(__instance);
        }

        [HarmonyPatch(typeof(NetworkPrefab), "HandleSpawnedObject"), HarmonyPostfix]
        public static void HandleSpawnedObject_Postfix(HawkNetworkBehaviour networkBehaviour, bool bUseChunkSystem)
        {
            if (!NoChunkDespawn.Value || !networkBehaviour) return;

            foreach (var worldDynamicObject in networkBehaviour.GetComponentsInChildren<WorldDynamicObject>(true))
            {
                ProtectFromChunkUnload(worldDynamicObject, bUseChunkSystem);
            }
        }
    }
}
