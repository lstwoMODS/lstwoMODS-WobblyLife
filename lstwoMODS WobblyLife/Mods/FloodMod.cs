using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using NWH.DWP2.WaterData;
using System;
using System.Collections;
using System.Linq;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = lstwoMODS_Core.UI.Elements.Button;

namespace lstwoMODS_WobblyLife.Mods;

public class FloodMod : BaseMod
{
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;
    
    private static readonly string floodPlaneAssetId = "d87a8866-04e2-49b9-bad1-6aa508429855";
    private static readonly string floodManagerAssetId = "16392b83-3911-422e-a232-78d7112f43d2";

    public override string Name => "Flood Mod";
    private static HawkNetworkBehaviour floodPlanePrefab;
    private static GameObject floodWaterPlane;
    private static GameObject waterBoundsObj;
    private static GameObject floodManagerPrefab;

    private static bool isFlooding;
    private static Coroutine floodCoroutine;

    // The flood is driven by a networked FloodManager the host spawns; without a modded host it never appears.
    private static ModNetworkIndicator _netStatus;

    [ModSetting(Label = "Flood Speed (m/s)", Order = 10)] public static Ref<float> FloodRiseSpeed = new(0.35f);
    [ModSetting(Label = "Flood Start Depth (m below sea level)", Speed = 0.25f, Order = 20)] public static Ref<float> FloodStartDepth = new(20f);
    [ModSetting(Order = 30)] public static Ref<bool> KillPlayerOnTouch = new();
    [ModSetting(Order = 40)] public static Ref<bool> ForceWeatherToThundering = new(true);

    protected override void OnStaticInit()
    {
        floodManagerPrefab = NetworkPrefabHelper.CreateNetworkPrefab("FloodManager", typeof(FloodManager), floodManagerAssetId);

        SceneManager.sceneLoaded += (scene, loadMode) =>
        {
            if (loadMode == LoadSceneMode.Additive) return;
            
            waterBoundsObj = GameObject.Find("WaterBounds");

            if (scene.name == "MainMenu" && floodPlanePrefab == null)
            {
                floodPlanePrefab = FloodManager.CreateFloodPlanePrefab();
                if (floodPlanePrefab != null)
                    HawkNetworkManager.DefaultInstance.RegisterPrefab(floodPlanePrefab);
            }

            if (scene.name != "WobblyIsland") return;

            NetworkPrefab.SpawnNetworkPrefab(floodManagerPrefab, new Vector3(0f, 0f, 0f));
            isFlooding = false;
        };

        KillPlayerOnTouch.Changed += value =>
        {
            GameObject[] objs = [floodWaterPlane, floodPlanePrefab.gameObject];

            foreach (var obj in objs)
            {
                var waterTrigger = obj.transform.Find("WaterTrigger");
                
                if (value && !waterTrigger.GetComponent<DeathArea>())
                    waterTrigger.gameObject.AddComponent<DeathArea>();
                else if (!value && waterTrigger.GetComponent<DeathArea>())
                    UnityEngine.Object.Destroy(waterTrigger.gameObject.GetComponent<DeathArea>());
            }
        };
    }

    [ModAction(ShowInUI = false)]
    public static void StartFlood()
    {
        FloodManager.Instance.ServerStartFlood();
    }
    
    [ModAction(ShowInUI = false)]
    public static void EndFlood()
    {
        FloodManager.Instance.ServerEndFlood();
    }

    public override void Update() => _netStatus?.Tick();

    public override Container BuildPanel(string id)
    {
        _netStatus = new ModNetworkIndicator("flood-net-status", () => FloodManager.Instance != null);

        return new Container(id,

            _netStatus,

            base.BuildPanel(id),
            new Button("Apply Settings", () => FloodManager.Instance.ServerChangeFloodSpeed()),
            
            new Spacing("spacing"),
            
            new HStack("actions",
                ActionMenu(new Button("Start Flood", StartFlood), nameof(StartFlood)),
                ActionMenu(new Button("End Flood", EndFlood), nameof(EndFlood))
            ).WithContentWidth()
        );
    }

    public class FloodManager : HawkNetworkBehaviour
    {
        public static FloodManager Instance;

        private byte RPC_START_FLOOD;
        private byte RPC_END_FLOOD;
        private byte RPC_CHANGE_FLOOD_SPEED;

        private float speed;
        private float startDepth;

        private long serverStartTimestamp = 0;

        public override void RegisterRPCs(HawkNetworkObject networkObject)
        {
            base.RegisterRPCs(networkObject);

            RPC_START_FLOOD = networkObject.RegisterRPC(ClientStartFlood);
            RPC_END_FLOOD = networkObject.RegisterRPC(ClientEndFlood);
            RPC_CHANGE_FLOOD_SPEED = networkObject.RegisterRPC(ClientChangeFloodSpeed);
        }

        /// <summary>
        /// Claim the singleton here rather than in Start: the registered prefab is a live GameObject
        /// whose own Unity Start runs too, and Instantiate copies its hideFlags onto the clone, so a
        /// flag check can't tell the two apart. NetworkPost only ever runs from
        /// HawkNetworkBehaviour.Initialize, which the template never goes through.
        /// </summary>
        public override void NetworkPost(HawkNetworkObject networkObject)
        {
            base.NetworkPost(networkObject);

            Instance = this;

            // The flood plane is a purely local visual: every peer drives its own plane height from the
            // RPC start-timestamp + speed (see ClientFloodCoroutine), so it is instantiated locally per
            // peer rather than network-spawned (spawning is server-only and left clients without one).
            if (floodPlanePrefab == null) return;

            floodWaterPlane = UnityEngine.Object.Instantiate(floodPlanePrefab.gameObject, Vector3.zero, Quaternion.identity);
            floodWaterPlane.hideFlags = HideFlags.None;
            floodWaterPlane.SetActive(false);
        }

        public IEnumerator ServerFloodCoroutine()
        {
            if (networkObject == null || !networkObject.IsServer())
            {
                yield break;
            }

            while (isFlooding)
            {
                floodWaterPlane.transform.position += Vector3.up * (speed * Time.deltaTime);

                if (WeatherSystem.Instance.GetAllWeatherData().ToList().IndexOf(WeatherSystem.Instance.GetCurrentWeatherData()) != 4 && ForceWeatherToThundering.Value)
                {
                    WeatherSystem.Instance.ServerSetWeatherByIndex(4);
                }

                yield return null;
            }
        }

        public IEnumerator ClientFloodCoroutine()
        {
            if (networkObject == null || networkObject.IsServer())
            {
                yield break;
            }

            floodWaterPlane.transform.position = (Vector3.up * -startDepth) + (Vector3.up * (speed * ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - serverStartTimestamp) / 1000f)));

            while (isFlooding)
            {
                floodWaterPlane.transform.position += Vector3.up * (speed * Time.deltaTime);

                yield return null;
            }
        }

        public void ServerEndFlood()
        {
            if (!isFlooding || networkObject == null || !networkObject.IsServer())
            {
                return;
            }

            isFlooding = false;
            networkObject.SendRPC(RPC_END_FLOOD, RPCRecievers.All);
        }

        private void ClientEndFlood(HawkNetReader reader, HawkRPCInfo info)
        {
            if (floodWaterPlane == null || waterBoundsObj == null) return;
            floodWaterPlane.SetActive(false);
            waterBoundsObj.SetActive(true);
        }

        public void ServerStartFlood()
        {
            if (isFlooding || networkObject == null || !networkObject.IsServer())
            {
                return;
            }

            isFlooding = true;
            speed = FloodRiseSpeed.Value;
            startDepth = FloodStartDepth.Value;

            serverStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            floodWaterPlane.transform.position = Vector3.up * -startDepth;
            floodCoroutine = StartCoroutine(ServerFloodCoroutine());

            networkObject.SendRPC(RPC_START_FLOOD, RPCRecievers.All, serverStartTimestamp, speed, startDepth);
        }

        private void ClientStartFlood(HawkNetReader reader, HawkRPCInfo info)
        {
            if (floodWaterPlane == null || waterBoundsObj == null) return;
            floodWaterPlane.SetActive(true);
            waterBoundsObj.SetActive(false);

            if(networkObject.IsServer())
            {
                return;
            }

            serverStartTimestamp = reader.ReadInt64();
            speed = reader.ReadSingle();
            startDepth = reader.ReadSingle();

            isFlooding = true;

            StartCoroutine(ClientFloodCoroutine());
        }

        public void ServerChangeFloodSpeed()
        {
            if (!isFlooding || networkObject == null || !networkObject.IsServer())
            {
                return;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            speed = FloodRiseSpeed.Value;

            networkObject.SendRPC(RPC_CHANGE_FLOOD_SPEED, RPCRecievers.All, timestamp, speed);
        }

        private void ClientChangeFloodSpeed(HawkNetReader reader, HawkRPCInfo info)
        {
            if (!isFlooding || networkObject.IsServer() || floodWaterPlane == null)
            {
                return;
            }

            var timestamp = reader.ReadInt64();
            var newSpeed = reader.ReadSingle();

            floodWaterPlane.transform.position += Vector3.up * (newSpeed - speed) * ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp) / 1000f);

            speed = newSpeed;
        }

        public static HawkNetworkBehaviour CreateFloodPlanePrefab()
        {
            // Flood Plane Object
            var floodWaterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floodWaterPlane.hideFlags = HideFlags.HideAndDontSave;
            floodWaterPlane.name = "Flood Plane";
            floodWaterPlane.transform.localScale = new(5000, 1, 5000);
            floodWaterPlane.layer = LayerMask.NameToLayer("Water");
            Destroy(floodWaterPlane.GetComponent<MeshCollider>());
            
            if (waterBoundsObj == null)
            {
                return null;
            }

            var _water = waterBoundsObj.transform.Find("Water");

            if (_water == null)
            {
                return null;
            }

            // Setting Water Material
            var material = _water.GetComponent<MeshRenderer>().material;

            var renderer = floodWaterPlane.GetComponent<MeshRenderer>();
            renderer.material = material;

            // Water Trigger Object
            var waterTriggerObj = new GameObject("WaterTrigger");
            waterTriggerObj.transform.SetParent(floodWaterPlane.transform);
            waterTriggerObj.AddComponent<BoxCollider>();
            waterTriggerObj.GetComponent<BoxCollider>().isTrigger = true;
            waterTriggerObj.transform.localScale = new Vector3(1, 500, 1);
            waterTriggerObj.transform.localPosition = Vector3.up * -250;
            waterTriggerObj.layer = LayerMask.NameToLayer("Water");

            // Water Trigger Components
            waterTriggerObj.AddComponent<FlatWaterDataProvider>();

            var waterTrigger = waterTriggerObj.AddComponent<WaterTrigger>();

            // Flood Plane Components
            var waterComponent = floodWaterPlane.AddComponent<Water>();
            waterComponent.assetID = "d87a8866-04e2-49b9-bad1-6aa508429855";

            var wobblySurfaceMaterial = floodWaterPlane.AddComponent<WobblySurfaceMaterial>();
            wobblySurfaceMaterial.surface = WobblySurface.Water;

            // Configuring Water Trigger and Water Components
            waterTrigger.water = waterComponent;

            var waterReflection = new QuickReflection<Water>(waterComponent, Plugin.Flags);
            waterReflection.SetField("waterDepth", 500f);
            waterReflection.SetField("waterDataScriptableObject", _water.GetComponent<Water>().waterDataScriptableObject);
            waterReflection.SetField("bIsDeep", true);

            floodWaterPlane.SetActive(false);

            return waterComponent;
        }
    }
}