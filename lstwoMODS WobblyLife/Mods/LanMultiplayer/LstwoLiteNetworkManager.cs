using HawkNetworking;
using Steamworks;

namespace lstwoMODS_WobblyLife.Mods.LanMultiplayer;

/// <summary>
/// LAN transport used while LAN mode is active. Identical to the game's LiteNetLib
/// transport except it reports a real player name  the stock <see cref="LiteNetworkManager"/>
/// hardcodes "Developer". The name comes from the LAN mod's configured name, falling back to
/// the Steam username when Steam is available, then a generic default. The rest of the Hawk
/// name handshake (message id 14) propagates it to other peers automatically.
/// </summary>
public class LstwoLiteNetworkManager : LiteNetworkManager
{
    public override string GetMyPlayerName()
    {
        var name = LanMultiplayerMod.PlayerName;
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (SteamClient.IsValid && !string.IsNullOrWhiteSpace(SteamClient.Name))
            return SteamClient.Name;

        return "Player";
    }
}
