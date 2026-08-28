using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class BanPlayer : PlayerBasedMod
{
    public override string Name => "Ban Player";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    public static List<PlayerProfile> SessionBannedPlayerIDs = [];
    public static List<PlayerProfile> PermanentlyBannedPlayers = [];

    private HawkNetworkManager _subscribedInstance;

    private static Ref<string[]> sessionBannedPlayerDropdownItems = new();
    private static Ref<string[]> permanentlyBannedPlayerDropdownItems = new();
    private static Ref<int> sessionBannedPlayerDropdownIndex = new();
    private static Ref<int> permanentlyBannedPlayerDropdownIndex = new();

    protected override void OnStaticInit()
    {
        PermanentlyBannedPlayers = LoadData<List<PlayerProfile>>("PermanentlyBannedPlayers") ?? [];
    }

    public override void Update()
    {
        // The live manager, not the Steam subclass: bans key off PlayerKey, which resolves on the
        // Steam build, the crossplay build, and (as PlayerKey.None, so nothing matches) on LAN.
        var current = HawkNetworkManager.InstanceExists ? HawkNetworkManager.DefaultInstance : null;
        if (current == _subscribedInstance) return;

        if (_subscribedInstance != null)
            _subscribedInstance.onPlayerConnected -= OnPlayerConnected;

        _subscribedInstance = current;

        if (_subscribedInstance != null)
            _subscribedInstance.onPlayerConnected += OnPlayerConnected;
    }

    private void OnPlayerConnected(HawkConnection connection)
    {
        var manager = _subscribedInstance;
        if (manager?.IsServer() != true) return;

        var key = PlayerIdentity.Of(connection);
        if (!key.IsValid) return;

        if (PermanentlyBannedPlayers.Any(x => x.Key == key) ||
            SessionBannedPlayerIDs.Any(x => x.Key == key))
        {
            manager.DisconnectPlayer(connection);
        }
    }

    [ModAction(ShowInUI = false)]
    public void Ban()
    {
        var profile = PlayerProfileHelper.FromPlayer(Player.Controller);
        if (profile == null) return;
        
        PermanentlyBannedPlayers.Add(profile);
        SaveData("PermanentlyBannedPlayers", PermanentlyBannedPlayers);
        Refresh();
        
        QuietlyKickPlayer(Player);
    }

    [ModAction(ShowInUI = false)]
    public void BanForSession()
    {
        var profile = PlayerProfileHelper.FromPlayer(Player.Controller);
        if (profile == null) return;
        
        SessionBannedPlayerIDs.Add(profile);
        Refresh();

        QuietlyKickPlayer(Player);
    }
    
    [ModAction(ShowInUI = false)]
    public void Unban(string key)
    {
        var target = PlayerKey.Parse(key);
        if (!target.IsValid) return;

        PermanentlyBannedPlayers.RemoveAll(x => x.Key == target);
        SaveData("PermanentlyBannedPlayers", PermanentlyBannedPlayers);
        Refresh();
    }

    [ModAction(ShowInUI = false)]
    public void UnbanForSession(string key)
    {
        var target = PlayerKey.Parse(key);
        if (!target.IsValid) return;

        SessionBannedPlayerIDs.RemoveAll(x => x.Key == target);
        Refresh();
    }

    private void Refresh()
    {
        permanentlyBannedPlayerDropdownItems.Value = PermanentlyBannedPlayers.Select(x => x.Name).ToArray();
        sessionBannedPlayerDropdownItems.Value = SessionBannedPlayerIDs.Select(x => x.Name).ToArray();
    }

    public override void RefreshUI()
    {
        base.RefreshUI();
        Refresh();
    }

    [ModAction(ShowInUI = false)]
    public static void QuietlyKickPlayer(PlayerRef player)
    {
        HawkNetworkManager.DefaultInstance.DisconnectPlayer(player.Controller.networkObject.GetOwner());
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new HStack("Ban",
                ActionMenu(new Button("Permanently Ban Player", Ban), nameof(Ban)),
                ActionMenu(new Button("Ban Player for Session", BanForSession), nameof(BanForSession))
            ).WithContentWidth(),

            new SeparatorText("Manage Bans", "Manage Bans"),

            new Combo("Permanently Banned Players", []).WithItems(permanentlyBannedPlayerDropdownItems).WithSelectedIndex(permanentlyBannedPlayerDropdownIndex),
            ActionMenu(new Button("Unban Player", () => Unban(KeyAt(PermanentlyBannedPlayers, permanentlyBannedPlayerDropdownIndex.Value))).WithContentWidth(), nameof(Unban)),

            new Spacing("spacer"),

            new Combo("Session Banned Players", []).WithItems(sessionBannedPlayerDropdownItems).WithSelectedIndex(sessionBannedPlayerDropdownIndex),
            ActionMenu(new Button("Unban Session Banned Player", () => UnbanForSession(KeyAt(SessionBannedPlayerIDs, sessionBannedPlayerDropdownIndex.Value))).WithContentWidth(), nameof(UnbanForSession))
        );
    }

    /// <summary>The stored key of the nth entry, or empty when the dropdown index has gone stale
    /// (the list can shrink between a rebuild and a click).</summary>
    private static string KeyAt(List<PlayerProfile> profiles, int index)
        => index >= 0 && index < profiles.Count ? profiles[index].Key.ToString() : "";
}