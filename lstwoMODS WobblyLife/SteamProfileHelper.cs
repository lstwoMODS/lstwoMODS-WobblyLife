using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using Steamworks;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Resolves players (controllers / network connections) to their Steam account.
/// Names come from Steam (persona name), not the changeable in-game player name;
/// the in-game connection name is only used as a fallback when Steam doesn't know the account.
/// </summary>
public static class SteamProfileHelper
{
    /// <summary>The local Steam account, or null if Steam isn't running.</summary>
    public static SteamProfile GetLocalProfile() =>
        SteamClient.IsValid ? new SteamProfile(SteamClient.SteamId.Value, SteamClient.Name) : null;

    /// <summary>
    /// Build a profile for a raw Steam id (e.g. one loaded from a saved list). Resolves the name
    /// from Steam if known. Returns null only if <paramref name="steamId"/> is 0.
    /// </summary>
    public static SteamProfile FromSteamId(ulong steamId) =>
        steamId == 0 ? null : new SteamProfile(steamId, GetSteamName(steamId));

    /// <summary>
    /// The Steam account that owns <paramref name="controller"/>. Local (split-screen) controllers
    /// all resolve to the local Steam account. Returns null if the owner isn't a Steam connection
    /// (e.g. offline without Steam).
    /// </summary>
    public static SteamProfile FromPlayer(PlayerController controller)
    {
        if (!controller) return null;

        if (controller.networkObject != null && controller.networkObject.GetOwner() is SteamConnection steamConnection)
            return FromConnection(steamConnection);

        return controller.IsLocal() ? GetLocalProfile() : null;
    }

    /// <summary>The Steam account behind <paramref name="connection"/>, or null if it isn't a Steam connection.</summary>
    public static SteamProfile FromConnection(HawkConnection connection)
    {
        if (connection is not SteamConnection steamConnection) return null;

        var steamId = steamConnection.steamId.Value;
        if (steamId == 0) return null;

        return new SteamProfile(steamId, GetSteamName(steamId) ?? steamConnection.Name);
    }

    /// <summary>
    /// The Steam persona name for <paramref name="steamId"/>, or null if Steam doesn't know the
    /// account (a fetch is requested so it usually resolves a moment later).
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

    public static List<SteamProfile> GetLobbyProfiles()
    {
        var profiles = new Dictionary<ulong, SteamProfile>();

        var local = GetLocalProfile();
        if (local != null) profiles[local.SteamId] = local;

        var manager = HawkNetworkManager.DefaultInstance;

        if (manager != null)
        {
            foreach (var connection in manager.GetPlayers())
            {
                var profile = FromConnection(connection);
                if (profile != null) profiles[profile.SteamId] = profile;
            }
        }

        return profiles.Values.ToList();
    }

    /// <summary>The live connection for <paramref name="steamId"/>, or null if they're not in the lobby.</summary>
    public static SteamConnection GetConnection(ulong steamId) =>
        HawkNetworkManager.DefaultInstance?.GetPlayers()
            .OfType<SteamConnection>()
            .FirstOrDefault(c => c.steamId.Value == steamId);

    /// <summary>
    /// All player controllers in the current game owned by <paramref name="steamId"/>
    /// (multiple in split-screen, empty if the account isn't in the game).
    /// </summary>
    public static List<PlayerController> GetPlayerControllers(ulong steamId)
    {
        var result = new List<PlayerController>();
        if (!GameInstance.InstanceExists) return result;

        var controllers = GameInstance.Instance.GetPlayerControllers();
        if (controllers == null) return result;

        var localId = SteamClient.IsValid ? SteamClient.SteamId.Value : 0;

        foreach (var controller in controllers)
        {
            if (!controller) continue;

            if (controller.networkObject != null && controller.networkObject.GetOwner() is SteamConnection steamConnection)
            {
                if (steamConnection.steamId.Value == steamId) result.Add(controller);
            }
            else if (controller.IsLocal() && localId != 0 && localId == steamId)
            {
                result.Add(controller);
            }
        }

        return result;
    }
}
