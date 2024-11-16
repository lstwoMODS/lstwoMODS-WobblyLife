using BepInEx;
using UnityEngine;
using NotAzzamods.Hacks.Free;
using NotAzzamods.Hacks.Paid;
using UniverseLib.UI;
using UniverseLib;
using UniverseLib.Config;
using NotAzzamods.UI;
using System.Collections.Generic;
using NotAzzamods.UI.TabMenus;
using NotAzzamods.Hacks;
using NotAzzamods.Hacks.Custom;
using System.Reflection;
using System.Collections;
using NotAzzamods.Hacks.Custom.JobManager;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System;
using BepInEx.Logging;
using NotAzzamods.UI.Keybinds;
using NotAzzamods.Keybinds;
using NotAzzamods.CustomItems;
using BepInEx.Configuration;
using System.Linq;

namespace NotAzzamods
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        
        public static ManualLogSource LogSource { get => Instance.Logger; }
        public static ConfigFile ConfigFile { get => Instance.Config; }

        public static Plugin Instance { get; private set; }

        public static UIBase UiBase { get; private set; }
        public static MainPanel MainPanel { get; private set; }
        public static KeybindPanel KeybindPanel { get; private set; }

        public static List<BaseTab> TabMenus { get; private set; } = new();
        public static List<BaseHack> Hacks { get; private set; } = new();

        public static HacksTab PlayerHacksTab { get; private set; } = new("Player Mods");
        public static HacksTab ServerHacksTab { get; private set; } = new("Server Mods");
        public static HacksTab VehicleHacksTab { get; private set; } = new("Vehicle Mods");
        public static HacksTab SaveHacksTab { get; private set; } = new("Save File Mods");
        public static HacksTab ExtraHacksTab { get; private set; } = new("Extra Mods");
        public static PropSpawnerTab PropSpawnerTab { get; private set; } = new();
        public static CustomItemsTab CustomItemsTab { get; private set; } = new();
        public static SettingsTab SettingsTab { get; private set; }

        public static KeybindManager KeybindManager { get; private set; }

        public static List<CustomItemPack> CustomItemPacks { get; private set; } = new();

        private void Awake()
        {
            Instance = this;

            InitMods();

            KeybindManager = gameObject.AddComponent<KeybindManager>();

            InitUI();

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
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
                UiBase.Enabled = !UiBase.Enabled;

                if(UiBase.Enabled)
                {
                    MainPanel.Refresh();
                }
            }

            foreach(var hack in Hacks)
            {
                hack.Update();
            }
        }
    }
}