using BepInEx;
using UnityEngine;
using System.Collections.Generic;
//using lstwoMODS_WobblyLife.UI.TabMenus;
using System.Reflection;
using System.Collections;
using System;
using BepInEx.Logging;
using lstwoMODS_WobblyLife.CustomItems;
using BepInEx.Configuration;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HawkNetworking;
using ImGuiNET;
using lstwoMODS_Core.UI;
//using lstwoMODS_WobblyLife.Hacks.JobManager;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.UI.TabMenus;
using Steamworks;
using UImGui;
using UImGui.Assets;
using UImGui.Texture;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static FieldInfo uImGuiCameraField;

    // QUICK ACCESS
    public const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        
    public static ManualLogSource LogSource { get => Instance.Logger; }
    public static ConfigFile ConfigFile { get => Instance.Config; }
    public static AssetBundle AssetBundle { get; private set; }

    // INSTANCES
    public static Plugin Instance { get; private set; }

    // TABS
    public static PlayerBasedModsTab PlayerHacksTab { get; private set; }
    public static PlayerBasedModsTab VehicleHacksTab { get; private set; }
    public static ModsTab ServerModsTab { get; private set; }
    public static ModsTab ClientModsTab { get; private set; }
    public static ModsTab SaveModsTab { get; private set; }
    public static ModsTab ExtraModsTab { get; private set; }
    //public static PropSpawnerTab PropSpawnerTab { get; private set; }
    //public static CustomItemsTab CustomItemsTab { get; private set; }

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
            if (scene != LoadScene.MainMenu)
            {
                return;
            }
            
            Window.Enabled = false;
        };

        GameInstance.onAssignedPlayerController += (controller) =>
        {
            if (!controller.IsLocal() || 
                (lstwoMODS_Core.Plugin.ImGuiRenderer != null && 
                 (Camera)uImGuiCameraField.GetValue(lstwoMODS_Core.Plugin.ImGuiRenderer) != null))
            {
                return;
            }
            
            controller.onGameplayCameraCreated += (playerController, camera) =>
            {
                if (!lstwoMODS_Core.Plugin.ImGuiRenderer)
                {
                    try {InitUI();} catch(Exception e) {Debug.LogException(e);}
                }

                lstwoMODS_Core.Plugin.ImGuiRenderer.enabled = true;
                lstwoMODS_Core.Plugin.ImGuiRenderer.SetCamera(camera.GetCamera());
                lstwoMODS_Core.Plugin.ImGuiRenderer.enabled = true;
                
                lstwoMODS_Core.Plugin.OnUIInitialize?.Invoke();

                camera.onDestroy += cam =>
                {
                    lstwoMODS_Core.Plugin.ImGuiRenderer.enabled = false;
                };
                
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            };
        };

        SceneManager.sceneLoaded += (scene, loadMode) =>
        {
            if (scene.name is "LoadingScene" && loadMode != LoadSceneMode.Single)
            {
                return;
            }

            if (scene.name is "MainMenu")
            {
            }
                
            try
            {
                if (SteamClient.IsValid && !string.IsNullOrEmpty(SteamClient.SteamId.ToString()) &&
                    SteamClient.AppId == 1211020)
                {
                    return;
                }

                var obj = AssetBundle.LoadAsset<GameObject>("PiracyScreenCanvas");
                var newObj = Instantiate(obj);
                DontDestroyOnLoad(newObj);

                StartCoroutine(PiracyScreenRoutine());
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                var obj = AssetBundle.LoadAsset<GameObject>("PiracyScreenCanvas");
                var newObj = Instantiate(obj);
                DontDestroyOnLoad(newObj);
            }
        };

        lstwoMODS_Core.Plugin.OnUIToggle += (toggle) =>
        {
            print(toggle);
            
            var localPlayers = GameInstance.Instance.GetLocalPlayerControllers();

            foreach (var inputManager in localPlayers.Select(localPlayer => localPlayer.GetPlayerControllerInputManager()))
            {
                if (toggle)
                {
                    inputManager.DisableGameplayCameraInput(this);
                    inputManager.DisableGameplayInput(this);
                    inputManager.DisableInteratorInput(this);
                    inputManager.DisablePlayerTransformInput(this);
                    inputManager.DisableUIInput(this);
                    
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                    return;
                }
                
                inputManager.EnableGameplayCameraInput(this);
                inputManager.EnableGameplayInput(this);
                inputManager.EnableInteratorInput(this);
                inputManager.EnablePlayerTransformInput(this);
                inputManager.EnableUIInput(this);
                
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        };

        lstwoMODS_Core.Plugin.UIConditions.Add(() => GameInstance.InstanceExists && GameInstance.Instance.GetGamemode());

        //AssetBundle = AssetUtils.LoadFromEmbeddedResources("lstwoMODS_WobblyLife.Resources.lstwomods.wobblylife.bundle");

        PlayerHacksTab = new("Player Mods");
        VehicleHacksTab = new("Vehicle Mods");
        ServerModsTab = new("Server Mods");
        ClientModsTab = new("Client Mods");
        SaveModsTab = new("Save File Mods");
        ExtraModsTab = new("Extra Mods");
        //PropSpawnerTab = new(AssetBundle.LoadAsset<Sprite>("PropSpawnerIcon"));
        //CustomItemsTab = new(AssetBundle.LoadAsset<Sprite>("CustomItemsIcon"));

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
        //_StartCoroutine(CustomItemsTab.InitCustomItems());
        WobblyServerUtilCompat.Init();

        //InitChildClasses<BaseJobManager>();
    }
    
    public static void InitUI()
    {
        var uImGuiBundle = lstwoMODS_Core.Plugin.UImGuiBundle;
        var rendererPrefab = uImGuiBundle.LoadAsset<GameObject>("lstwoMODS uImGui Renderer");
        var rendererObj = Instantiate(rendererPrefab);
        DontDestroyOnLoad(rendererObj);
        
        var renderer = rendererObj.GetComponent<UImGui.UImGui>();
        lstwoMODS_Core.Plugin.ImGuiRenderer = renderer;

        var uImGuiType = renderer.GetType();
        uImGuiCameraField = uImGuiType.GetField("_camera", Flags);
        //var fontAtlasConfigAsset = (FontAtlasConfigAsset)uImGuiType.GetField("_fontAtlasConfiguration", Flags).GetValue(renderer);
        var shadersAsset = (ShaderResourcesAsset)uImGuiType.GetField("_shaders", Flags).GetValue(renderer);
        
        /*var fontDefinition = new FontDefinition
        {
            Path = @"mods\net.lstwo.lstwoMODS\InterVariable.ttf",
            Config =
            {
                FontDataOwnedByAtlas = true,
                FontNo = 0,
                SizeInPixels = 18,
                OversampleH = 2,
                OversampleV = 1,
                PixelSnapH = true,
                GlyphExtraSpacing = new Vector2(0,0),
                GlyphOffset = new Vector2(0,0),
                GlyphRanges = ScriptGlyphRanges.Default,
                GlyphMinAdvanceX = 0,
                GlyphMaxAdvanceX = float.MaxValue,
                MergeMode = false,
                FontBuilderFlags = 0,
                RasterizerMultiply = 1,
                EllipsisChar = '\uffff',
                CustomGlyphRanges = []
            }
        };*/
            
        //fontAtlasConfigAsset.Fonts = [fontDefinition];

        var shaderProperties = new ShaderProperties
        {
            BaseVertex = "_BaseVertex",
            Vertices = "_Vertices",
            Texture = "_Texture"
        };

        var meshShader = uImGuiBundle.LoadAsset<Shader>("assets/uimgui-4.1.0/resources/shaders/dearimgui-mesh.shader");
        var proceduralShader = uImGuiBundle.LoadAsset<Shader>("assets/uimgui-4.1.0/resources/shaders/dearimgui-procedural.shader");

        var shaderData = new ShaderData
        {
            Mesh = meshShader,
            Procedural = proceduralShader,
        };
        
        shadersAsset.Shader = shaderData;
        shadersAsset.PropertyNames = shaderProperties;
        
        /*UImGuiUtility.SetCurrentContext((Context)uImGuiType.GetField("_context", Flags).GetValue(renderer));
        var io = ImGui.GetIO();
        Window.Font = io.Fonts.AddFontFromFileTTF($@"{Application.streamingAssetsPath}\mods\net.lstwo.lstwoMODS\InterVariable.ttf", 18, null, io.Fonts.GetGlyphRangesDefault());
        io.Fonts.Build();*/

        ((UnityEvent<ImGuiIOPtr>)uImGuiType.GetField("_fontCustomInitializer", Flags).GetValue(renderer)).AddListener(io =>
        {
            /*var fontPath = System.IO.Path.Combine(Application.streamingAssetsPath, "mods/net.lstwo.lstwoMODS/InterVariable.ttf");
            io.Fonts.AddFontFromFileTTF(fontPath, 18f, null, io.Fonts.GetGlyphRangesDefault());
            
            var allocateGlyphRangeArrayMethodInfo = typeof(TextureManager).GetMethod("AllocateGlyphRangeArray", Flags);
            var context = (Context)typeof(UImGui.UImGui).GetField("_context", Flags).GetValue(renderer);
            var textureManager = (TextureManager)typeof(Context).GetField("TextureManager", Flags).GetValue(context);
            
            unsafe
            {
                ImFontConfig fontConfig = default;
                ImFontConfigPtr fontConfigPtr = new ImFontConfigPtr(&fontConfig);

                fontDefinition.Config.ApplyTo(fontConfigPtr);
                fontConfigPtr.GlyphRanges = (IntPtr)allocateGlyphRangeArrayMethodInfo.Invoke(textureManager, [fontDefinition.Config]);

                io.Fonts.AddFontFromFileTTF(fontPath, fontDefinition.Config.SizeInPixels, fontConfigPtr);
            }*/
            
            Window.Font = io.Fonts.AddFontFromFileTTF($@"{Application.streamingAssetsPath}\mods\net.lstwo.lstwoMODS\InterVariable.ttf", 18, null, io.Fonts.GetGlyphRangesDefault());
        });
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

    public static IEnumerator PiracyScreenRoutine()
    {
        yield return new WaitForSecondsRealtime(5f);
        Application.Quit();
    }
}