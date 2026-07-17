using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.Mods.LanMultiplayer;

/// <summary>
/// Optional LAN / direct-IP multiplayer for local testing or when Steam is unavailable.
/// Steam stays the default; this only swaps the network transport to the game's built-in
/// LiteNetLib path (<see cref="LstwoLiteNetworkManager"/>).
///
/// Two ways to enable it, sharing one core (<see cref="SetSelectors"/>):
///   Persistent  a DataStorage-backed flag applied at boot (before the network singletons
///               are created), so the game starts directly in LAN mode. Toggling it needs a
///               restart.
///   Runtime     a main-menu-only, offline-only rebuild that destroys and recreates the
///               network singletons live (no restart). If the rebuild throws, the selectors
///               are left pointing at the target so a manual restart lands in the right mode.
///
/// The transport is locked at boot by the game (both singletons are lazy + DontDestroyOnLoad
/// and never rebuilt), which is why the persistent path is the robust baseline and the runtime
/// path is a convenience layered on top.
/// </summary>
public class LanMultiplayerMod : BaseMod
{
    public override string Name => "LAN Multiplayer";
    public override string Description =>
        "Host / join over LAN or direct IP instead of Steam. For local testing or when Steam is unavailable.";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    // Storage identity must equal BaseMod.Id (GetType().FullName) so the boot-time reads below
    // (which run before any instance exists) hit the same data.json bag the UI writes to.
    internal static readonly string StorageId = typeof(LanMultiplayerMod).FullName;

    private const string KeyPersistentLan = "PersistentLanMode";
    private const string KeyPlayerName    = "PlayerName";
    private const string KeyHostPort      = "HostPort";
    private const string KeyJoinIp        = "JoinIp";
    private const string KeyJoinPort      = "JoinPort";

    private const int DefaultPort = 8080;

    /// <summary>Name reported to peers by <see cref="LstwoLiteNetworkManager"/>. Kept in sync
    /// with the persisted name and also loadable at boot before any UI exists.</summary>
    public static string PlayerName { get; private set; } = "";

    /// <summary>True when the live transport is currently the LAN transport.</summary>
    public static bool LiveLanMode { get; private set; }

    /// <summary>Set when a runtime switch failed midway and the process should be restarted.</summary>
    public static bool RestartRecommended { get; private set; }

    // ── Reflection into the game's singleton internals (for the runtime rebuild) ──
    private static readonly FieldInfo FHawkInstance   = AccessTools.Field(typeof(HawkNetworkManager),  "m_Instance");
    private static readonly FieldInfo FWobblyInstance = AccessTools.Field(typeof(WobblyNetworkManager), "m_Instance");
    private static readonly FieldInfo FManagerInit    = AccessTools.Field(typeof(HawkNetworkBehaviour), "ManagerInit");
    private static readonly FieldInfo FManagerSingle  = AccessTools.Field(typeof(HawkNetworkBehaviour), "ManagerSingleton");
    private static readonly FieldInfo FGameInstanceNM = AccessTools.Field(typeof(GameInstance),         "networkManager");
    // Prefab registries the transport owns  a rebuilt transport starts empty, so custom-networked
    // mods (flood, chat, clothing) would stop replicating. These are carried across the switch.
    private static readonly FieldInfo FRegDic    = AccessTools.Field(typeof(HawkNetworkManager), "registeredNetworkBehavioursDic");
    private static readonly FieldInfo FRegDicRaw = AccessTools.Field(typeof(HawkNetworkManager), "registeredNetworkBehavioursDicRaw");

    // ── UI bindings ──
    private readonly Ref<bool>   _persistentLan = new();
    private readonly Ref<string> _playerName    = new("");
    private readonly Ref<int>    _hostPort      = new(DefaultPort);
    private readonly Ref<string> _joinIp        = new("127.0.0.1");
    private readonly Ref<int>    _joinPort      = new(DefaultPort);

    private readonly Ref<string> _liveModeText  = new("");
    private readonly Ref<string> _statusText    = new("");
    private readonly Ref<bool>   _notLanMode    = new(true);  // disables host/join off LAN
    private readonly Ref<bool>   _switchDisabled = new();     // disables runtime switch when unsafe

    // ────────────────────────────── boot / core ──────────────────────────────

    /// <summary>
    /// Applied from <c>Plugin.Awake</c>, before <c>GameInstance.Awake</c> creates the network
    /// singletons. Reads the persisted flag + name straight from DataStorage (no instance yet)
    /// and flips the transport selectors when persistent LAN is enabled.
    /// </summary>
    public static void ApplyBootTransport()
    {
        try
        {
            if (DataStorage.BagEntryExists(StorageId, KeyPlayerName))
                PlayerName = (DataStorage.LoadFromBag<string>(StorageId, KeyPlayerName) ?? "").Trim();

            var persistentLan = DataStorage.BagEntryExists(StorageId, KeyPersistentLan)
                                && DataStorage.LoadFromBag<bool>(StorageId, KeyPersistentLan);

            if (persistentLan)
                SetSelectors(true);   // else leave the game's Steam defaults untouched

            LiveLanMode = persistentLan;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[LAN] Boot transport apply failed: {ex}");
        }
    }

    /// <summary>The one shared core: point the game's two transport-type selectors at LAN or Steam.</summary>
    private static void SetSelectors(bool lan)
    {
        if (lan)
        {
            HawkNetworkManager.HawkNetworkManagerType   = typeof(LstwoLiteNetworkManager);
            WobblyNetworkManager.HawkNetworkManagerType = typeof(WobblyNetworkManager);        // base, not the Steam subclass
        }
        else
        {
            HawkNetworkManager.HawkNetworkManagerType   = typeof(SteamP2PNetworkManager);
            WobblyNetworkManager.HawkNetworkManagerType = typeof(WobblyNetworkManagerSteam);
        }
    }

    protected override void OnStaticInit()
    {
        // Runs once, at core mod init (before any host/join). Guards the one game-side hard cast
        // that would otherwise crash on every connect under a non-Steam transport.
        new Harmony("net.lstwo.lstwoMODS.WobblyLife.LanMultiplayer").PatchAll(typeof(LanMultiplayerMod));
    }

    protected override void Awake()
    {
        _playerName.Changed += v => PlayerName = (v ?? "").Trim();

        BindData(_playerName,    KeyPlayerName,    "");
        BindData(_hostPort,      KeyHostPort,      DefaultPort);
        BindData(_joinIp,        KeyJoinIp,        "127.0.0.1");
        BindData(_joinPort,      KeyJoinPort,      DefaultPort);
        BindData(_persistentLan, KeyPersistentLan, false);

        PlayerName = (_playerName.Value ?? "").Trim();
    }

    // ────────────────────────────── runtime switch ──────────────────────────────

    // Copy dictionary contents out before the owning component is destroyed (managed Dictionary
    // survives the native destroy, but snapshotting keeps this obviously safe).
    private static List<DictionaryEntry> Snapshot(IDictionary src)
    {
        var list = new List<DictionaryEntry>();
        if (src != null)
            foreach (DictionaryEntry e in src)
                list.Add(e);
        return list;
    }

    private static void Restore(IDictionary dst, List<DictionaryEntry> entries)
    {
        if (dst == null)
            return;
        foreach (var e in entries)
            if (!dst.Contains(e.Key))
                dst.Add(e.Key, e.Value);
    }

    private static bool IsSafeToSwitch()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
            return false;
        if (HawkNetworkManager.InstanceExists && HawkNetworkManager.DefaultInstance.IsConnected())
            return false;
        return true;
    }

    /// <summary>
    /// Live transport swap at the main menu, no restart. Destroys the wrapper + transport
    /// singletons, clears their static caches, resets the networked-behaviour manager cache,
    /// recreates both under the new type, then re-points the one cached transport field that
    /// does not self-heal (GameInstance.networkManager) and rebinds rich presence.
    /// </summary>
    public static bool SwitchTransport(bool lan)
    {
        if (LiveLanMode == lan)
            return true;
        if (!IsSafeToSwitch())
            return false;

        try
        {
            SetSelectors(lan);

            var oldTransport = HawkNetworkManager.InstanceExists  ? HawkNetworkManager.DefaultInstance : null;
            var oldWrapper   = WobblyNetworkManager.InstanceExists ? WobblyNetworkManager.Instance     : null;

            // Snapshot the prefab registries before the old transport dies so mods' networked
            // objects still replicate on the new transport (the game re-establishes its own).
            var regDic    = Snapshot(oldTransport != null ? FRegDic.GetValue(oldTransport)    as IDictionary : null);
            var regDicRaw = Snapshot(oldTransport != null ? FRegDicRaw.GetValue(oldTransport) as IDictionary : null);

            // Clear singleton caches so the getters lazily rebuild with the new types.
            FHawkInstance.SetValue(null, null);
            FWobblyInstance.SetValue(null, null);

            // Force networked behaviours to re-resolve the transport on the next gameplay scene.
            FManagerInit.SetValue(null, false);
            FManagerSingle.SetValue(null, null);

            // Destroy the old singleton GameObjects synchronously (safe at the menu, offline).
            // DestroyImmediate (not Destroy) so FindObjectOfType can't grab a lingering instance.
            if (oldWrapper != null)   UnityEngine.Object.DestroyImmediate(oldWrapper.gameObject);
            if (oldTransport != null) UnityEngine.Object.DestroyImmediate(oldTransport.gameObject);

            // Recreate: transport first, then the wrapper. The wrapper's Awake binds to the new
            // transport and re-subscribes its ~15 events, so that side self-heals.
            var newTransport = HawkNetworkManager.DefaultInstance;
            WobblyNetworkManager.TouchInstance();

            // Re-register carried-over prefabs onto the new transport.
            Restore(FRegDic.GetValue(newTransport)    as IDictionary, regDic);
            Restore(FRegDicRaw.GetValue(newTransport) as IDictionary, regDicRaw);

            // GameInstance caches the transport once in its Awake and never re-fetches it.
            if (UnitySingleton<GameInstance>.InstanceExists)
                FGameInstanceNM.SetValue(UnitySingleton<GameInstance>.Instance, newTransport);

            // Rich presence subscribes to the transport in OnEnable; toggling it rebinds to the
            // new transport (harmless no-op removals on the way out).
            if (UnitySingleton<RichPresenceManager>.InstanceExists)
            {
                var rp = UnitySingleton<RichPresenceManager>.Instance;
                rp.enabled = false;
                rp.enabled = true;
            }

            LiveLanMode = lan;
            RestartRecommended = false;
            Plugin.LogSource.LogInfo($"[LAN] Switched live transport to {(lan ? "LAN" : "Steam")}.");
            return true;
        }
        catch (Exception ex)
        {
            // Selectors already point at the target, so a manual restart lands in the right mode.
            RestartRecommended = true;
            Plugin.LogSource.LogError($"[LAN] Runtime transport switch failed  restart recommended: {ex}");
            return false;
        }
    }

    // ────────────────────────────── host / join actions ──────────────────────────────

    private static ushort ClampPort(int p) => (p <= 0 || p > 65535) ? (ushort)DefaultPort : (ushort)p;

    private void JoinLan()
    {
        if (!LiveLanMode)
            return;

        // Mirror the game's join flow (OnSearchGamesClicked): pick a save slot first so the client
        // has a character, then connect directly by IP/port instead of the (LAN-empty) server browser.
        UnitySingleton<UISaveSystemCanvas>.Instance.PlaySlotSelectionSystem_HostOwnIfTutorial(() =>
        {
            var wm = WobblyNetworkManager.Instance;
            var ip = string.IsNullOrWhiteSpace(_joinIp.Value) ? "127.0.0.1" : _joinIp.Value.Trim();
            var port = ClampPort(_joinPort.Value);
            wm.SetIpAddress(ip);
            wm.SetPort(port);
            Plugin.LogSource.LogInfo($"[LAN] Joining {ip}:{port}.");
            wm.JoinServer();
        });
    }

    private void ToggleRuntime() => SwitchTransport(!LiveLanMode);

    // ────────────────────────────── hardening patch ──────────────────────────────

    // RichPresenceManager.OnConnectToServer hard-casts the live transport to
    // SteamP2PNetworkManager, which throws under a LAN transport. Skip it when Steam isn't the
    // transport (rich presence simply isn't set on LAN); run the original untouched under Steam.
    [HarmonyPatch(typeof(RichPresenceManager), "OnConnectToServer")]
    [HarmonyPrefix]
    private static bool RichPresence_OnConnectToServer_Prefix()
        => HawkNetworkManager.DefaultInstance is SteamP2PNetworkManager;

    // ────────────────────────────── UI ──────────────────────────────

    private static void SetIf(Ref<string> r, string v) { if (r.Value != v) r.Value = v; }
    private static void SetIf(Ref<bool> r, bool v)      { if (r.Value != v) r.Value = v; }

    public override void Update()
    {
        SetIf(_liveModeText, LiveLanMode ? "Live mode: LAN (direct IP)" : "Live mode: Steam");
        SetIf(_notLanMode, !LiveLanMode);

        var safe = IsSafeToSwitch();
        SetIf(_switchDisabled, !safe);

        string status;
        if (RestartRecommended) status = "A transport switch failed  please restart the game.";
        else if (!safe)         status = "Runtime switch is available only at the main menu while offline.";
        else                    status = "";
        SetIf(_statusText, status);

        // Feed the chosen port into the wrapper so the game's own Host button hosts on it.
        // SetPort just stores a field read at host time, so this is safe to keep applying.
        if (LiveLanMode && WobblyNetworkManager.InstanceExists)
            WobblyNetworkManager.Instance.SetPort(ClampPort(_hostPort.Value));
    }

    public override void RefreshUI() { }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            new UIText("lan-live-mode", "").WithText(_liveModeText),
            new TextDisabled("lan-status", "").WithText(_statusText),

            new SeparatorText("lan-identity-sep", "Identity"),
            new HStack("lan-name-row",
                new UIText("lan-name-label", "Name"),
                new InputText("##lan-name", hint: "Player name (defaults to Steam name)").WithValue(_playerName)
            ).WithContentWidth(),

            new SeparatorText("lan-host-sep", "Host (LAN)"),
            new HStack("lan-host-row",
                new UIText("lan-host-port-label", "Port"),
                new InputInt("##lan-host-port").WithValue(_hostPort).WithDisabled(_notLanMode)
            ).WithContentWidth(),
            new TextDisabled("lan-host-note", "Set the port, then host from the main menu (the game's Host button)."),

            new SeparatorText("lan-join-sep", "Join (LAN)"),
            new HStack("lan-join-row",
                new InputText("IP##lan-join-ip", hint: "127.0.0.1").WithValue(_joinIp).WithDisabled(_notLanMode),
                new InputInt("Port##lan-join-port").WithValue(_joinPort).WithDisabled(_notLanMode)
            ).WithContentWidth(),
            new Button("Join LAN Game", JoinLan).WithContentWidth().WithDisabled(_notLanMode),

            new SeparatorText("lan-mode-sep", "Mode"),
            new Button("Switch LAN <-> Steam (main menu only)", ToggleRuntime)
                .WithContentWidth().WithDisabled(_switchDisabled),
            new Checkbox("Start in LAN mode on next launch").WithValue(_persistentLan),
            new TextDisabled("lan-persist-note", "Persistent mode applies on the next game start.")
        );
    }
}
