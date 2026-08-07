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

    [ModSetting("Lobby Name", ShowInUI = false)]
    public static string LobbyName
    {
        get => SteamP2PNetworkManager.SteamInstance == null || SteamP2PNetworkManager.SteamInstance.GetLobby().Id == 0 ? "" : SteamP2PNetworkManager.SteamInstance.GetLobby().GetData("lobbyname") ?? "";
        set => SteamP2PNetworkManager.SteamInstance?.SetServerName(value);
    }

    private static Ref<bool> _disableLobbyNameInput = new();
    private static Ref<string> _lobbyNameInput = new("");

    public override Container BuildPanel(string id)
    {
        return new(id,
            new UIText("NoLobbyInfo", "Not in active steam lobby").WithVisible(_disableLobbyNameInput),
            
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