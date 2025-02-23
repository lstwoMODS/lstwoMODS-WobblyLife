using BepInEx;
using UnityEngine;
using lstwoMODS_WobblyLife.UI;
using System.Collections.Generic;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System.Reflection;
using System.Collections;
using System;
using BepInEx.Logging;
using lstwoMODS_WobblyLife.CustomItems;
using BepInEx.Configuration;
using System.Linq;
using HawkNetworking;
using lstwoMODS_WobblyLife.Hacks.JobManager;
using ShadowLib;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        // QUICK ACCESS
        public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        
        public static ManualLogSource LogSource { get => Instance.Logger; }
        public static ConfigFile ConfigFile { get => Instance.Config; }

        // INSTANCES
        public static Plugin Instance { get; private set; }

        // TABS
        public static PlayerBasedHacksTab PlayerHacksTab { get; private set; } = new("Player Mods");
        public static PlayerBasedHacksTab VehicleHacksTab { get; private set; } = new("Vehicle Mods");
        public static HacksTab ServerHacksTab { get; private set; } = new("Server Mods");
        public static HacksTab SaveHacksTab { get; private set; } = new("Save File Mods");
        public static HacksTab ExtraHacksTab { get; private set; } = new("Extra Mods");
        public static PropSpawnerTab PropSpawnerTab { get; private set; } = new();
        public static CustomItemsTab CustomItemsTab { get; private set; } = new();

        public static List<CustomItemPack> CustomItemPacks { get; private set; } = new();

        private void Awake()
        {
            Instance = this;

            GameInstance.onAssignedPlayerCharacter += (character) =>
            {
                if(!HawkNetworkManager.DefaultInstance.IsOffline())
                {
                    StartCoroutine(NameEasterEggThingy(character));
                }
            };

            GameInstance.onSceneLoaded += (scene) =>
            {
                if(scene == LoadScene.MainMenu && lstwoMODS_Core.Plugin.UiBase != null)
                {
                    lstwoMODS_Core.Plugin.UiBase.Enabled = false;
                }
            };

            lstwoMODS_Core.Plugin.OnUIToggle += (toggle) =>
            {
                var inputManager = PlayerUtils.GetMyPlayer().GetPlayerControllerInputManager();

                if (toggle)
                {
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
            };

            lstwoMODS_Core.Plugin.UIConditions.Add(() => GameInstance.InstanceExists && GameInstance.Instance.GetGamemode());

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void Start()
        {
            InitMods();
        }

        private static IEnumerator NameEasterEggThingy(PlayerCharacter character)
        {
            yield return null;
            var component = character.gameObject.GetComponentInChildren<CharacterNameTag>().gameObject.AddComponent<NameEasterEgg>();
            component.playerParent = character.GetPlayerController();
        }

        public static void InitMods()
        {
            _StartCoroutine(CustomItemsTab.InitCustomItems());
            WobblyServerUtilCompat.Init();

            InitChildClasses<BaseJobManager>();
        }

        public static void InitChildClasses<T>()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = new List<Type>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    types.AddRange(assembly.GetTypes());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error getting types from assembly '{assembly.FullName}': {ex.Message} {ex.StackTrace}");
                }
            }

            foreach (var type in types)
            {
                try
                {
                    if (type.IsSubclassOf(typeof(T)) && !type.IsAbstract)
                    {
                        Activator.CreateInstance(type);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error evaluating / initializing type '{type.FullName}': {ex.Message} {ex.StackTrace}");
                }
            }
        }

        public static Coroutine _StartCoroutine(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        public static void _StopCoroutine(Coroutine routine)
        {
            Instance.StopCoroutine(routine);
        }
    }
}