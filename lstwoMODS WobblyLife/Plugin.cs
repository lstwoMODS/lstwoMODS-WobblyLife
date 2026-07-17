using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using lstwoMODS_WobblyLife.UI.TabMenus;
using System.Reflection;
using System.Collections;
using System;
using BepInEx.Logging;
using lstwoMODS_WobblyLife.CustomItems;
using BepInEx.Configuration;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
//using lstwoMODS_WobblyLife.Hacks.JobManager;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS.ImGui.Shared;
using lstwoMODS.WobblyLife.SharedObjects;
using lstwoMODS_WobblyLife.Mods;
using lstwoMODS_WobblyLife.Mods.Chat;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine.AddressableAssets;
using Steamworks;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("net.lstwo.lstwomods_core")]
public class Plugin : BaseUnityPlugin
{
    private static FieldInfo uImGuiCameraField;

    // QUICK ACCESS
    public const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        
    public static ManualLogSource LogSource => Instance.Logger;
    public static ConfigFile ConfigFile => Instance.Config;

    // INSTANCES
    public static Plugin Instance { get; private set; }

    // TABS
    public static PlayerBasedModsWindow PlayerModsWindow { get; private set; }
    public static PlayerBasedModsWindow VehicleModsWindow { get; private set; }
    public static ModsWindow ServerModsWindow { get; private set; }
    public static ModsWindow ClientModsWindow { get; private set; }
    public static ModsWindow SaveModsWindow { get; private set; }
    public static ModsWindow ExtraModsWindow { get; private set; }
    public static PropSpawnerWindow PropSpawnerWindow { get; private set; }

    public static List<CustomItemPack> CustomItemPacks { get; private set; } = new();

    internal static readonly Ref<bool> ScanWindowVisible  = new();
    internal static readonly Ref<float> ScanProgressValue  = new();
    internal static readonly Ref<string> ScanProgressText   = new("");


    private void Awake()
    {
        Instance = this;

        // Must run before GameInstance.Awake creates the (DontDestroyOnLoad, never-rebuilt)
        // network singletons: flips the transport selectors to LAN when the persistent flag is set.
        Mods.LanMultiplayer.LanMultiplayerMod.ApplyBootTransport();

        UIManager.OnInitialized += OnIpcInitialized;
        LstwoModsOverlay.OnConstructUI += OnConstructUI;
        SettingsWindow.OnBuildUI += OnBuildSettingsUI;
        GameInstance.onAssignedPlayerCharacter += OnAssignedPlayerCharacter;
        SceneManager.sceneLoaded += OnSceneLoaded;
        lstwoMODS_Core.Plugin.OnUIToggle += OnUIToggle;
        LstwoModsOverlay.OnConstructUI += ChatMod.BuildChatUI;

        PlayerModsWindow = new("Player Mods", Lucide.User);
        VehicleModsWindow = new("Vehicle Mods", Lucide.CarFront);
        ServerModsWindow = new("Server Mods", Lucide.Earth);
        ClientModsWindow = new("Client Mods", Lucide.Monitor);
        SaveModsWindow = new("Save File Mods", Lucide.Save);
        ExtraModsWindow = new("Extra Mods", Lucide.Grip);
        PropSpawnerWindow = new ();
        
        QualityOfLifeMod.ApplyPatches(new Harmony("net.lstwo.lstwoMODS.WobblyLife.QoL"));
        QualityOfLifeMod.EarlyLoadData();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnAssignedPlayerCharacter(PlayerCharacter character)
    {
        if (!HawkNetworkManager.DefaultInstance.IsOffline())
        {
            StartCoroutine(NameEasterEggThingy(character));
        }
    }

    private void OnBuildSettingsUI(List<BaseUIElement> elements)
    {
        var confirmDialog = new ConfirmDialog("asset-db-rescan-confirm",
            title:         "Rescan Asset Database",
            message:       "This will delete the cached asset database and re-scan all game assets. This may take several minutes.",
            confirmLabel:  "Rescan",
            onConfirm:     () => _StartCoroutine(RunAssetDatabaseScan(true)));

        elements.Add(new SeparatorText("asset-db-sep", "Wobbly Life"));
        elements.Add(confirmDialog);
        elements.Add(new Button("Rescan Asset Database", confirmDialog.Show).WithContentWidth());
    }

    private void OnConstructUI()
    {
        lstwoMODS_Core.Plugin.Window.AddElement(
            new GuiWindow("asset-db-scan", "##asset-db-scan",
                new UIText("scan-title", "Scanning Assets"),
                new ProgressBar("scan-bar", sizeY: 22f)
                    .WithValue(ScanProgressValue)
                    .WithOverlay(ScanProgressText)
                    .WithRequireInput(false)
            )
            .WithOpen(ScanWindowVisible)
            .WithRequireInput(false)
            .WithNoClose()
            .WithFlags(
                ImGuiWindowFlags.NoDecoration          |
                ImGuiWindowFlags.NoMove                |
                ImGuiWindowFlags.NoInputs              |
                ImGuiWindowFlags.NoSavedSettings       |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoDocking
            )
            .WithSize(540f, 62f, ImGuiCond.Always)
            .WithPosition(-1f, 20f, ImGuiCond.Always, 0.5f, 0f)
        );
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name is "LoadingScene" || loadMode != LoadSceneMode.Single)
        {
            return;
        }

        if (scene.name is "MainMenu")
        {
            if (SteamClient.IsValid && SteamClient.AppId != 1211020)
            {
                AntiPiracy.Initialize();
            }

            lstwoMODS_Core.Plugin.Window.LstwoModsPanels.Enabled = false;
            _StartCoroutine(RunAssetDatabaseScan());
        }
    }

    private static readonly object UiPauseHandle = new();

    private void OnUIToggle(bool enabled)
    {
        if (enabled)
            GamePause.AddPauseHandle(UiPauseHandle);
        else
            GamePause.ReleasePauseHandle(UiPauseHandle);
    }

    private static int _nextSpawnId;
    private static readonly Dictionary<int, GameObject> _spawnedProps = new();

    private static void OnIpcInitialized()
    {
        UIManager.IpcChannel.MessageReceived += async msg =>
        {
            if (msg.Type != "_plugin") return;
            try
            {
                var inner = Newtonsoft.Json.JsonConvert.DeserializeObject<IpcMessage>(msg.Payload);
                if (inner?.Type == SpawnPropMessage.MessageType)
                {
                    var spawn = SpawnPropMessage.Deserialize(inner);
                    MainThread.Enqueue(() => ExecuteSpawnProp(spawn));
                }
                else if (inner?.Type == SpawnedPropActionMessage.MessageType)
                {
                    var action = SpawnedPropActionMessage.Deserialize(inner);
                    MainThread.Enqueue(() => ExecuteSpawnedPropAction(action));
                }
                else if (inner?.Type == SpawnCustomItemMessage.MessageType)
                {
                    var spawn = SpawnCustomItemMessage.Deserialize(inner);
                    MainThread.Enqueue(() => ExecuteSpawnCustomItem(spawn));
                }
            }
            catch { }
        };
    }

    private static void ExecuteSpawnProp(SpawnPropMessage spawn)
    {
        var player = GameInstance.Instance?.GetFirstLocalPlayerController();
        if (player == null) return;
        var character = player.GetPlayerCharacter();
        var pos        = character.GetPlayerPosition() + character.GetPlayerForward();
        var candidates = AddressFallbacks(spawn.Address).ToList();
        var spawnId    = ++_nextSpawnId;

        if (spawn.Networked)
            TrySpawnNetworkedAt(candidates, 0, pos, spawnId, spawn);
        else
            _StartCoroutine(TryInstantiateAsync(candidates, pos, spawnId, spawn));
    }

    private static void ExecuteSpawnCustomItem(SpawnCustomItemMessage spawn)
    {
        if (spawn.PackIndex < 0 || spawn.PackIndex >= CustomItemPacks.Count) return;
        var pack = CustomItemPacks[spawn.PackIndex];
        if (spawn.ItemIndex < 0 || spawn.ItemIndex >= pack.items.Count) return;
        var item = pack.items[spawn.ItemIndex];

        var player = GameInstance.Instance?.GetFirstLocalPlayerController();
        if (player == null) return;
        var character = player.GetPlayerCharacter();
        var pos = item.spawnAtPos
            ? item.customSpawnPos
            : character.GetPlayerPosition() + character.GetPlayerForward();

        NetworkPrefab.SpawnNetworkPrefab(item.gameObject, pos);
    }

    private static void TrySpawnNetworkedAt(List<string> candidates, int index, Vector3 pos, int spawnId, SpawnPropMessage spawn)
    {
        if (index >= candidates.Count)
        {
            LogSource.LogWarning("[PropSpawner] Networked spawn failed: no valid key found.");
            return;
        }
        NetworkPrefab.SpawnNetworkPrefab(candidates[index], b =>
        {
            if (b == null) { TrySpawnNetworkedAt(candidates, index + 1, pos, spawnId, spawn); return; }
            OnSpawnSuccess(spawnId, spawn, b.gameObject);
        }, pos, bSendTransform: true);
    }

    private static IEnumerator TryInstantiateAsync(List<string> candidates, Vector3 pos,
                                                    int spawnId, SpawnPropMessage spawn)
    {
        foreach (var key in candidates)
        {
            var handle = Addressables.InstantiateAsync(key, pos, Quaternion.identity);
            yield return handle;
            if (handle.Result != null)
            {
                OnSpawnSuccess(spawnId, spawn, handle.Result);
                yield break;
            }
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        LogSource.LogWarning("[PropSpawner] Local spawn failed: no valid Addressables key found.");
    }

    private static void OnSpawnSuccess(int spawnId, SpawnPropMessage spawn, GameObject go)
    {
        _spawnedProps[spawnId] = go;

        UIManager.IpcChannel.SendMessage(new PropSpawnedMessage
        {
            SpawnId   = spawnId,
            Address   = spawn.Address,
            Name      = go.name,
            Networked = spawn.Networked
        }.Serialize());
    }

    private static void ExecuteSpawnedPropAction(SpawnedPropActionMessage action)
    {
        // Prune stale entries (destroyed by other means)
        var stale = _spawnedProps.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
        foreach (var k in stale) _spawnedProps.Remove(k);

        if (!_spawnedProps.TryGetValue(action.SpawnId, out var go) || go == null)
        {
            _spawnedProps.Remove(action.SpawnId);
            return;
        }

        switch (action.Action)
        {
            case "Delete":
                var hawk = go.GetComponent<HawkNetworkBehaviour>();
                if (hawk?.networkObject != null)
                    hawk.networkObject.Destroy();
                else
                    Destroy(go);
                _spawnedProps.Remove(action.SpawnId);
                break;
            case "Inspect":
                TryInspectWithUnityExplorer(go);
                break;
        }
    }

    private static void TryInspectWithUnityExplorer(GameObject go)
    {
        try
        {
            UnityExplorer.InspectorManager.Inspect(go);
            UnityExplorer.UI.UIManager.ShowMenu = true;
        }
        catch { }
    }

    // Yields: HawkNetworkBehaviour.assetID → as-is → stripped prefix → stripped prefix+ext → asset GUID
    private static IEnumerable<string> AddressFallbacks(string address)
    {
        var entry = AssetDatabase.Get(address);

        if (!string.IsNullOrEmpty(entry?.NetworkAssetId))
            yield return entry.NetworkAssetId;

        yield return address;

        const string prefix = "Assets/Content/";
        if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var stripped = address.Substring(prefix.Length);
            yield return stripped;
            if (stripped.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                yield return stripped.Substring(0, stripped.Length - 7);
        }
        else if (address.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            yield return address.Substring(0, address.Length - 7);
        }

        if (!string.IsNullOrEmpty(entry?.Guid))
            yield return entry.Guid;
    }

    private void Start()
    {
        PlayerMacroType.Register();
        WeatherMacroType.Register();
        WaypointMacro.Register();
        WLMacroTriggers.Register();
        Mods.Chat.ChatMacroTrigger.Register();
        InitMods();
    }

    private static IEnumerator RunAssetDatabaseScan(bool forceRefresh = false)
    {
        if (!forceRefresh && AssetDatabase.IsInitialized && AssetDatabase.Entries.All(e => e.ComponentsLoaded) && AssetDatabase.SceneEntries.All(e => e.ComponentsLoaded))
            yield break;

        yield return AssetDatabase.InitializeAsync(forceRefresh);

        if (!AssetDatabase.Entries.Any(e => !e.ComponentsLoaded) && !AssetDatabase.SceneEntries.Any(e => !e.ComponentsLoaded))
            yield break;

        ScanWindowVisible.Value = true;
        ScanProgressValue.Value = 0f;
        var total = AssetDatabase.Entries.Count + AssetDatabase.SceneEntries.Count;
        ScanProgressText.Value = $"Scanning 0 / {total}";

        yield return AssetDatabase.ScanAllComponentsAsync((done, t) =>
        {
            ScanProgressValue.Value = t > 0 ? (float)done / t : 1f;
            ScanProgressText.Value  = $"Scanning {done} / {t}";
        });

        ScanProgressText.Value  = $"Done: {AssetDatabase.Entries.Count} assets, {AssetDatabase.SceneEntries.Count} scenes";
        ScanProgressValue.Value = 1f;
        yield return new WaitForSecondsRealtime(3f);
        ScanWindowVisible.Value = false;
    }

    private static IEnumerator NameEasterEggThingy(PlayerCharacter character)
    {
        yield return null;
        var component = character.gameObject.GetComponentInChildren<CharacterNameTag>().gameObject.AddComponent<NameEasterEgg>();
        component.playerParent = character.GetPlayerController();
    }

    public static void InitMods()
    {
        _StartCoroutine(InitModsRoutine());
        //InitChildClasses<BaseJobManager>();
    }

    private static IEnumerator InitModsRoutine()
    {
        yield return InitCustomItems();
        SendCustomItemsReady();
    }

    private static IEnumerator InitCustomItems()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lstwoMODS", "CustomItems");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            yield break;
        }

        foreach (var itemDirectory in Directory.GetDirectories(path))
        {
            try
            {
                CustomItemPacks.Add(new(itemDirectory));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading Custom Item Pack at \"{itemDirectory}\": {ex}");
            }
        }
    }

    private static void SendCustomItemsReady()
    {
        if (UIManager.IpcChannel == null) return;

        var packs = CustomItemPacks.Select(p => new CustomItemPackData
        {
            PackName   = p.packName,
            PackAuthor = p.packAuthor,
            Items      = p.items.Select(i => new CustomItemData
            {
                Name        = i.itemName,
                Description = i.itemDescription
            }).ToList()
        }).ToList();

        UIManager.IpcChannel.SendMessage(new CustomItemsReadyMessage { Packs = packs }.Serialize());
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