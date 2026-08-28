using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class SetLobbyName : BaseMod
{
    public override string Name => "Set Lobby Name";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    /// <summary>
    /// The advertised server name.
    /// <para>
    /// Writing goes through <c>SetServerName</c> on either build; where it lands differs. The Steam
    /// build writes it straight onto the Steam lobby's <c>lobbyname</c> data, so the lobby is also
    /// where it can be read back. The crossplay build keeps server data in the network manager and
    /// pushes it to the EOS session instead, leaving the Steam lobby (which still exists there, but
    /// only as an invite surface) empty, so reading it back off the lobby would always come up
    /// blank. There we mirror the value locally instead.
    /// </para>
    /// </summary>
    [ModSetting("Lobby Name", ShowInUI = false)]
    public static string LobbyName
    {
        get
        {
            if (GameBuild.IsCrossplay) return _lastAppliedName;

            var steam = SteamP2PNetworkManager.SteamInstance;
            if (steam == null || steam.GetLobby().Id == 0) return "";

            return steam.GetLobby().GetData("lobbyname") ?? "";
        }
        set
        {
            _lastAppliedName = value ?? "";
            (HawkNetworkManager.InstanceExists ? HawkNetworkManager.DefaultInstance : null)?.SetServerName(_lastAppliedName);
        }
    }

    // Crossplay only: what we last asked for, since nothing exposes it back to us.
    private static string _lastAppliedName = "";

    private static Ref<bool> _disableLobbyNameInput = new();
    private static Ref<string> _lobbyNameInput = new("");

    public override Container BuildPanel(string id)
    {
        return new(id,
            new UIText("NoLobbyInfo", "Not in an active lobby").WithVisible(_disableLobbyNameInput),

            new HStack("lobby name",
                new InputText("##Lobby Name").WithDisabled(_disableLobbyNameInput).WithValue(_lobbyNameInput),
                SettingMenu(new Button("Apply Lobby Name", () => LobbyName = _lobbyNameInput.Value).WithDisabled(_disableLobbyNameInput), nameof(LobbyName))
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        _lobbyNameInput.Value = LobbyName;
    }
}
