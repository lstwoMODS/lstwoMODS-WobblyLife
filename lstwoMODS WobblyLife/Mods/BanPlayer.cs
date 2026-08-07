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

    public static List<SteamProfile> SessionBannedPlayerIDs = [];
    public static List<SteamProfile> PermanentlyBannedPlayers = [];

    private SteamP2PNetworkManager _subscribedInstance;

    private static Ref<string[]> sessionBannedPlayerDropdownItems = new();
    private static Ref<string[]> permanentlyBannedPlayerDropdownItems = new();
    private static Ref<int> sessionBannedPlayerDropdownIndex = new();
    private static Ref<int> permanentlyBannedPlayerDropdownIndex = new();

    protected override void OnStaticInit()
    {
        PermanentlyBannedPlayers = LoadData<List<SteamProfile>>("PermanentlyBannedPlayers") ?? [];
    }

    public override void Update()
    {
        var current = SteamP2PNetworkManager.SteamInstance;
        if (current == _subscribedInstance) return;

        if (_subscribedInstance != null)
            _subscribedInstance.onPlayerConnected -= OnPlayerConnected;

        _subscribedInstance = current;

        if (_subscribedInstance != null)
            _subscribedInstance.onPlayerConnected += OnPlayerConnected;
    }

    private void OnPlayerConnected(HawkConnection connection)
    {
        if (connection is not SteamConnection steamConnection || SteamP2PNetworkManager.SteamInstance?.IsServer() != true) return;

        if (PermanentlyBannedPlayers.Any(x => x.SteamId == steamConnection.steamId) ||
            SessionBannedPlayerIDs.Any(x => x.SteamId == steamConnection.steamId))
        {
            SteamP2PNetworkManager.SteamInstance.DisconnectPlayer(steamConnection);
        }
    }

    [ModAction(ShowInUI = false)]
    public void Ban()
    {
        var profile = SteamProfileHelper.FromPlayer(Player.Controller);
        if (profile == null) return;
        
        PermanentlyBannedPlayers.Add(profile);
        SaveData("PermanentlyBannedPlayers", PermanentlyBannedPlayers);
        Refresh();
        
        QuietlyKickPlayer(Player);
    }

    [ModAction(ShowInUI = false)]
    public void BanForSession()
    {
        var profile = SteamProfileHelper.FromPlayer(Player.Controller);
        if (profile == null) return;
        
        SessionBannedPlayerIDs.Add(profile);
        Refresh();

        QuietlyKickPlayer(Player);
    }
    
    [ModAction(ShowInUI = false)]
    public void Unban(ulong id)
    {
        PermanentlyBannedPlayers.RemoveAll(x => x.SteamId == id);
        SaveData("PermanentlyBannedPlayers", PermanentlyBannedPlayers);
        Refresh();
    }

    [ModAction(ShowInUI = false)]
    public void UnbanForSession(ulong id)
    {
        SessionBannedPlayerIDs.RemoveAll(x => x.SteamId == id);
        Refresh();
    }

    private void Refresh()
    {
        permanentlyBannedPlayerDropdownItems.Value = PermanentlyBannedPlayers.Select(x => x.SteamName).ToArray();
        sessionBannedPlayerDropdownItems.Value = SessionBannedPlayerIDs.Select(x => x.SteamName).ToArray();
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
            ActionMenu(new Button("Unban Player", () => Unban(PermanentlyBannedPlayers.Select(x => x.SteamId).ToArray()[permanentlyBannedPlayerDropdownIndex.Value])).WithContentWidth(), nameof(Unban)),

            new Spacing("spacer"),

            new Combo("Session Banned Players", []).WithItems(sessionBannedPlayerDropdownItems).WithSelectedIndex(sessionBannedPlayerDropdownIndex),
            ActionMenu(new Button("Unban Session Banned Player", () => UnbanForSession(SessionBannedPlayerIDs.Select(x => x.SteamId).ToArray()[sessionBannedPlayerDropdownIndex.Value])).WithContentWidth(), nameof(UnbanForSession))
        );
    }
}