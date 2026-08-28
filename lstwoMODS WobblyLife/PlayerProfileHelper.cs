using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using Steamworks;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Builds <see cref="PlayerProfile"/>s for players (controllers / network connections).
/// <para>
/// Identity resolution itself lives in <see cref="PlayerIdentity"/>, which is what makes this work
/// on both game builds. This layer only decides which name to store alongside a key: a Steam
/// persona name where one exists, and the replicated in-game name otherwise.
/// </para>
/// </summary>
public static class PlayerProfileHelper
{
    /// <summary>The local account, or null when we have no identity yet.</summary>
    public static PlayerProfile GetLocalProfile()
    {
        var key = PlayerIdentity.Local();
        if (!key.IsValid) return null;

        var name = key.Platform == PlayerPlatform.Steam && SteamClient.IsValid
            ? SteamClient.Name
            : PlayerIdentity.DisplayNameOf(key);

        return new PlayerProfile(key, name);
    }

    /// <summary>
    /// Build a profile for a stored key (e.g. one loaded from a saved list), resolving the name if
    /// the account is currently reachable. Returns null for an invalid key.
    /// </summary>
    public static PlayerProfile FromKey(PlayerKey key) =>
        key.IsValid ? new PlayerProfile(key, PlayerIdentity.DisplayNameOf(key)) : null;

    /// <summary>
    /// The account that owns <paramref name="controller"/>. Local (split-screen) controllers all
    /// resolve to the local account. Returns null when the owner has no account identity, which is
    /// the case on a LAN transport and in an offline game.
    /// </summary>
    public static PlayerProfile FromPlayer(PlayerController controller)
    {
        if (!controller) return null;

        var key = PlayerIdentity.Of(controller);
        if (!key.IsValid) return null;

        var name = PlayerIdentity.DisplayNameOf(key) ?? controller.GetPlayerName();
        return new PlayerProfile(key, name);
    }

    /// <summary>The account behind <paramref name="connection"/>, or null when it carries none.</summary>
    public static PlayerProfile FromConnection(HawkConnection connection)
    {
        var key = PlayerIdentity.Of(connection);
        if (!key.IsValid) return null;

        return new PlayerProfile(key, PlayerIdentity.DisplayNameOf(key) ?? connection.Name);
    }

    /// <summary>
    /// The Steam persona name for <paramref name="steamId"/>, or null when Steam does not know the
    /// account (a fetch is requested, so it usually resolves a moment later). Always null on the
    /// crossplay build, where peers are EOS accounts Steam has never heard of.
    /// </summary>
    public static string GetSteamName(ulong steamId)
    {
        if (!SteamClient.IsValid || steamId == 0) return null;

        if (SteamClient.SteamId.Value == steamId) return SteamClient.Name;

        var name = new Friend(steamId).Name;

        if (string.IsNullOrEmpty(name) || name == "[unknown]")
        {
            SteamFriends.RequestUserInformation(steamId);
            return null;
        }

        return name;
    }

    /// <summary>Every account currently in the lobby, the local one included. Host only for peers:
    /// <c>GetPlayers()</c> is the full roster on the server and just the host elsewhere.</summary>
    public static List<PlayerProfile> GetLobbyProfiles()
    {
        var profiles = new Dictionary<PlayerKey, PlayerProfile>();

        var local = GetLocalProfile();
        if (local != null) profiles[local.Key] = local;

        var manager = HawkNetworkManager.DefaultInstance;

        if (manager != null)
        {
            foreach (var connection in manager.GetPlayers())
            {
                var profile = FromConnection(connection);
                if (profile != null) profiles[profile.Key] = profile;
            }
        }

        return profiles.Values.ToList();
    }
}
