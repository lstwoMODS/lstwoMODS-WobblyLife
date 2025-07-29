using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class SetLobbyName : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        lobbyNameLIB = ui.CreateLIBTrio("Lobby Name", "net.lstwo.lobbyname");
        lobbyNameLIB.Button.OnClick += () => LobbyName = lobbyNameLIB.Input.Text;
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
        
    }

    public override void RefreshUI()
    {
        lobbyNameLIB.Input.Text = LobbyName;
    }

    public override string Name => "Set Lobby Name";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ServerHacksTab;

    private HacksUIHelper.LIBTrio lobbyNameLIB;

    public static string LobbyName
    {
        get => SteamP2PNetworkManager.SteamInstance.GetLobby().GetData("lobbyname");
        set => SteamP2PNetworkManager.SteamInstance.SetServerName(value);
    }
}