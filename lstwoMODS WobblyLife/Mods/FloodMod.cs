using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using NWH.DWP2.WaterData;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks;

public class FloodMod : BaseHack
{
    public override string Name => "Flood Mod";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ExtraHacksTab;

    private static GameObject floodWaterPlane;
    private static GameObject waterBoundsObj;

    private static bool isFlooding = false;
    private static Coroutine floodCoroutine;

    private static float floodRiseSpeed = 0.35f;
    private static float floodStartDepth = 20f;

    private static HacksUIHelper.LIBTrio speedLIB;
    private static HacksUIHelper.LIBTrio startDepthLIB;

    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        speedLIB = ui.CreateLIBTrio("Flood Speed (m/s)", "lstwo.FloodMod.SpeedLIB", "0.35");
        speedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        speedLIB.Button.OnClick = () =>
        {
            floodRiseSpeed = float.Parse(speedLIB.Input.Text);

            FloodManager.Instance?.ServerChangeFloodSpeed();
        };

        ui.AddSpacer(6);

        startDepthLIB = ui.CreateLIBTrio("Flood Start Depth (m below sea level)", "lstwo.FloodMod.StartDepthLIB", "25.0");
        startDepthLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        startDepthLIB.Button.OnClick = () => floodStartDepth = float.Parse(startDepthLIB.Input.Text);

        ui.AddSpacer(6);

        ui.CreateLBBTrio("Flood Controls", "lstwo.FloodMod.Controls", () => { FloodManager.Instance.ServerStartFlood(); }, "Start Flood", "lstwo.FloodMod.StartFlood", 
            () => { FloodManager.Instance.ServerEndFlood(); }, "End Flood", "lstwo.FloodMod.EndFlood");

        ui.AddSpacer(6);

        SceneManager.sceneLoaded += (scene, loadMode) =>
        {
            if (loadMode == LoadSceneMode.Additive || scene.name != "WobblyIsland")
            {
                return;
            }

            var behavior = FloodManager.Instance.CreateFloodPlane();
            isFlooding = false;

            if (behavior)
            {
                HawkNetworkManager.DefaultInstance.Assign(behavior);
            }
        };
    }

    public override void RefreshUI()
    {
        speedLIB.Input.Text = floodRiseSpeed.ToString();
        startDepthLIB.Input.Text = floodStartDepth.ToString();
    }

    public override void Update()
    {
    }

    public class FloodManager : HawkNetworkBehaviour
    {
        public static FloodManager Instance;

        private byte RPC_START_FLOOD;
        private byte RPC_END_FLOOD;
        private byte RPC_CHANGE_FLOOD_SPEED;

        private long serverStartTimestamp = 0;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        protected override void RegisterRPCs(HawkNetworkObject networkObject)
        {
            base.RegisterRPCs(networkObject);

            RPC_START_FLOOD = networkObject.RegisterRPC(ClientStartFlood);
            RPC_END_FLOOD = networkObject.RegisterRPC(ClientEndFlood);
            RPC_CHANGE_FLOOD_SPEED = networkObject.RegisterRPC(ClientChangeFloodSpeed);
        }

        public IEnumerator ServerFloodCoroutine()
        {
            if (networkObject == null || !networkObject.IsServer())
            {
                yield break;
            }

            while (isFlooding)
            {
                floodWaterPlane.transform.position += Vector3.up * floodRiseSpeed * Time.deltaTime;

                if (WeatherSystem.Instance.GetAllWeatherData().ToList().IndexOf(WeatherSystem.Instance.GetCurrentWeatherData()) != 4)
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

            floodWaterPlane.transform.position = (Vector3.up * -floodStartDepth) + (Vector3.up * (floodRiseSpeed * ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - serverStartTimestamp) / 1000f)));

            while (isFlooding)
            {
                floodWaterPlane.transform.position += Vector3.up * (floodRiseSpeed * Time.deltaTime);

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

            serverStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            floodWaterPlane.transform.position = Vector3.up * -floodStartDepth;
            floodCoroutine = StartCoroutine(ServerFloodCoroutine());

            networkObject.SendRPC(RPC_START_FLOOD, RPCRecievers.All, serverStartTimestamp, floodRiseSpeed, floodStartDepth);
        }

        private void ClientStartFlood(HawkNetReader reader, HawkRPCInfo info)
        {
            floodWaterPlane.SetActive(true);
            waterBoundsObj.SetActive(false);

            if(networkObject.IsServer())
            {
                return;
            }

            serverStartTimestamp = reader.ReadInt64();
            floodRiseSpeed = reader.ReadSingle();
            floodStartDepth = reader.ReadSingle();

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

            networkObject.SendRPC(RPC_CHANGE_FLOOD_SPEED, RPCRecievers.All, timestamp, floodRiseSpeed);
        }

        private void ClientChangeFloodSpeed(HawkNetReader reader, HawkRPCInfo info)
        {
            if (!isFlooding || networkObject.IsServer())
            {
                return;
            }

            var timestamp = reader.ReadInt64();
            var newSpeed = reader.ReadSingle();

            floodWaterPlane.transform.position += Vector3.up * (newSpeed - floodRiseSpeed) * ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp) / 1000f);

            floodRiseSpeed = newSpeed;
        }

        public HawkNetworkBehaviour CreateFloodPlane()
        {
            // Flood Plane Object
            floodWaterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floodWaterPlane.name = "Flood Plane";
            floodWaterPlane.transform.localScale = new(5000, 1, 5000);
            floodWaterPlane.layer = LayerMask.NameToLayer("Water");
            Destroy(floodWaterPlane.GetComponent<MeshCollider>());

            // Getting Existing Water Component
            waterBoundsObj = GameObject.Find("WaterBounds");

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

            var wobblySurfaceMaterial = floodWaterPlane.AddComponent<WobblySurfaceMaterial>();
            typeof(WobblySurfaceMaterial).GetField("surface", Plugin.Flags).SetValue(wobblySurfaceMaterial, WobblySurface.Water);

            // Configuring Water Trigger and Water Components
            typeof(WaterTrigger).GetField("water", Plugin.Flags).SetValue(waterTrigger, waterComponent);

            var waterReflection = new QuickReflection<Water>(waterComponent, Plugin.Flags);
            waterReflection.SetField("waterDepth", 500f);
            waterReflection.SetField("waterDataScriptableObject", typeof(Water).GetField("waterDataScriptableObject", Plugin.Flags).GetValue(_water.GetComponent<Water>()));
            waterReflection.SetField("bIsDeep", true);

            floodWaterPlane.SetActive(false);

            return waterComponent;
        }
    }
}