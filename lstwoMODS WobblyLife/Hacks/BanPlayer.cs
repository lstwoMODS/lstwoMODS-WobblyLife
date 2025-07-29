using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using Steamworks;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class BanPlayer : PlayerBasedHack
{
    public override void ConstructUI(GameObject root)
    {
        SteamP2PNetworkManager.SteamInstance.onPlayerConnected += connection =>
        {
            var steamConnection = (SteamConnection)connection;

            if (PermanentlyBannedPlayers.Value.Select(x => x.Key).Contains(steamConnection.steamId) ||
                SessionBannedPlayerIDs.Select(x => x.Key).Contains(steamConnection.steamId))
            {
                SteamP2PNetworkManager.SteamInstance.DisconnectPlayer(connection);
            }
        };
        
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);

        ui.CreateLBBTrio("Ban Player", "net.lstwo.banplayer", () =>
        {
            var steamConnection = (SteamConnection)Player.Controller.networkObject.GetOwner();
            var steamId = steamConnection.steamId;
            var name = steamConnection.Name;
            SessionBannedPlayerIDs.Add(new(steamId, name));
        }, 
            "Ban for Session", "net.lstwo.banplayer.session", () =>
        {
            var steamConnection = (SteamConnection)Player.Controller.networkObject.GetOwner();
            var steamId = steamConnection.steamId;
            var name = steamConnection.Name;
            PermanentlyBannedPlayers.Value.AddItem(new(steamId, name));
        }, 
            "Ban Permanently", "net.lstwo.banplayer.permanent");
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Ban Player";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.PlayerHacksTab;
    
    public static List<KeyValuePair<ulong, string>> SessionBannedPlayerIDs = [];
    public static ConfigEntry<KeyValuePair<ulong, string>[]> PermanentlyBannedPlayers;
}