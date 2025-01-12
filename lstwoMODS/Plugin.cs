using BepInEx;
using UnityEngine;
using UniverseLib.UI;
using UniverseLib;
using UniverseLib.Config;
using NotAzzamods.UI;
using System.Collections.Generic;
using NotAzzamods.UI.TabMenus;
using NotAzzamods.Hacks;
using System.Reflection;
using System.Collections;
using System;
using BepInEx.Logging;
using NotAzzamods.UI.Keybinds;
using NotAzzamods.Keybinds;
using NotAzzamods.CustomItems;
using BepInEx.Configuration;
using System.Linq;
using HawkNetworking;
using NotAzzamods.Hacks.JobManager;
using ShadowLib;

namespace NotAzzamods
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        // ASSETS
        public static AssetBundle AssetBundle { get; private set; }

        // QUICK ACCESS
        public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        
        public static ManualLogSource LogSource { get => Instance.Logger; }
        public static ConfigFile ConfigFile { get => Instance.Config; }

        // INSTANCES
        public static Plugin Instance { get; private set; }

        public static UIBase UiBase { get; private set; }
        public static MainPanel MainPanel { get; private set; }
        public static KeybindPanel KeybindPanel { get; private set; }

        public static List<BaseTab> TabMenus { get; private set; } = new();
        public static List<BaseHack> Hacks { get; private set; } = new();

        // TABS
        public static HacksTab PlayerHacksTab { get; private set; } = new("Player Mods");
        public static HacksTab ServerHacksTab { get; private set; } = new("Server Mods");
        public static HacksTab VehicleHacksTab { get; private set; } = new("Vehicle Mods");
        public static HacksTab SaveHacksTab { get; private set; } = new("Save File Mods");
        public static HacksTab ExtraHacksTab { get; private set; } = new("Extra Mods");
        //public static PropSpawnerTab PropSpawnerTab { get; private set; } = new();
        public static CustomItemsTab CustomItemsTab { get; private set; } = new();
        public static SettingsTab SettingsTab { get; private set; }

        // OTHER FEATURES
        public static KeybindManager KeybindManager { get; private set; }

        public static List<CustomItemPack> CustomItemPacks { get; private set; } = new();

        private void Awake()
        {
            Instance = this;
            AssetBundle = AssetUtils.LoadAssetBundleFromPluginsFolder("lstwo.lstwomods.assets");
            Debug.Log(AssetBundle);
            KeybindManager = gameObject.AddComponent<KeybindManager>();

            InitMods();
            InitUI();

            GameInstance.onAssignedPlayerCharacter += (character) =>
            {
                if(!HawkNetworkManager.DefaultInstance.IsOffline())
                {
                    StartCoroutine(NameEasterEggThingy(character));
                }
            };

            GameInstance.onSceneLoaded += (scene) =>
            {
                if(scene == LoadScene.MainMenu && UiBase != null)
                {
                    UiBase.Enabled = false;
                }
            };

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private static IEnumerator NameEasterEggThingy(PlayerCharacter character)
        {
            yield return null;
            var component = character.gameObject.GetComponentInChildren<CharacterNameTag>().gameObject.AddComponent<NameEasterEgg>();
            component.playerParent = character.GetPlayerController();
        }

        public static void InitMods()
        {
            _ = PropSpawnerTab.DownloadPrefabJSON();
            _StartCoroutine(CustomItemsTab.InitCustomItems());
            WobblyServerUtilCompat.Init();

            InitChildClasses<BaseHack>();
            InitChildClasses<BaseJobManager>();
        }

        public static void InitUI()
        {
            float startupDelay = 1f;
            UniverseLibConfig config = new()
            {
                Disable_EventSystem_Override = false,
                Force_Unlock_Mouse = true
            };

            Universe.Init(startupDelay, () =>
            {
                UiBase = UniversalUI.RegisterUI("lstwo.NotAzza", null);

                MainPanel = new(UiBase);
                KeybindPanel = new(UiBase);

                KeybindPanel.Enabled = false;
                UiBase.Enabled = false;
            }, null, config);
        }

        public static void InitChildClasses<T>()
        {
            var childTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(T)) && !t.IsAbstract);

            foreach (var type in childTypes)
            {
                Activator.CreateInstance(type);
            }
        }

        public static Coroutine _StartCoroutine(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.F2))
            {
                ToggleUI();
            }

            foreach(var hack in Hacks)
            {
                hack.Update();
            }
        }

        private void ToggleUI()
        {
            if (!GameInstance.InstanceExists || !GameInstance.Instance.GetGamemode())
            {
                UiBase.Enabled = false;
                return;
            }

            var enabled = !UiBase.Enabled;
            UiBase.Enabled = enabled;

            var inputManager = PlayerUtils.GetMyPlayer().GetPlayerControllerInputManager();

            if (enabled)
            {
                MainPanel.Refresh();

                inputManager.DisableGameplayCameraInput(this);
                inputManager.DisableGameplayInput(this);
                inputManager.DisableInteratorInput(this);
                inputManager.DisablePlayerTransformInput(this);
                inputManager.DisableUIInput(this);

                return;
            }

            inputManager.EnableGameplayCameraInput(this);
            inputManager.EnableGameplayInput(this);
            inputManager.EnableInteratorInput(this);
            inputManager.EnablePlayerTransformInput(this);
            inputManager.EnableUIInput(this);
        }
    }
}