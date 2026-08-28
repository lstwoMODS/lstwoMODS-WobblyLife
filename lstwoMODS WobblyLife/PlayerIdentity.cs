using System.Collections.Generic;
using System.Reflection;
using HawkNetworking;
using Steamworks;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// Resolves players to a <see cref="PlayerKey"/> and back, on either game build.
/// <para>
/// This is the one place that knows how a connection carries its account id. On the Steam build that
/// is <c>SteamConnection.steamId</c>; on the crossplay build it is the private
/// <c>EOSConnection.remoteUserId</c> handle, read reflectively so that no EOS type name is ever
/// baked into this assembly (they do not exist in the Steam build's <c>HawkNetworking.dll</c>, and a
/// typeref to one would fail to resolve the moment a method touching it was compiled).
/// </para>
/// </summary>
public static class PlayerIdentity
{
    private const string EosConnectionTypeName = "EOSConnection";
    private const string EosRemoteIdField = "remoteUserId";

    // Cached per connection type rather than looked up per call: this sits on paths that run per
    // frame and per received packet.
    private static System.Type _cachedEosType;
    private static FieldInfo _cachedEosField;

    /// <summary>
    /// The account behind a connection, or <see cref="PlayerKey.None"/> when it has none: an offline
    /// game, a LAN peer (<c>LiteConnection</c> identifies by address, which is not an account), or a
    /// connection whose handle is not yet valid.
    /// </summary>
    public static PlayerKey Of(HawkConnection connection)
    {
        switch (connection)
        {
            case null:
                return PlayerKey.None;
            case SteamConnection steam:
                return PlayerKey.Steam(steam.steamId.Value);
            default:
                return EpicKeyOf(connection);
        }
    }

    /// <summary>
    /// The account that owns a controller. Split-screen controllers share their host account. Local
    /// controllers fall back to the local account, because ownership is only populated on the host
    /// and a client would otherwise fail to identify itself.
    /// </summary>
    public static PlayerKey Of(PlayerController controller)
    {
        if (!controller) return PlayerKey.None;

        var key = Of(controller.networkObject?.GetOwner());
        if (key.IsValid) return key;

        return controller.IsLocal() ? Local() : PlayerKey.None;
    }

    /// <summary>Our own account.</summary>
    public static PlayerKey Local()
    {
        // On the Steam build the Steam client is the authority and is available before any lobby
        // exists. On the crossplay build our own connection is a real EOSConnection wrapping our
        // product user id (EOSNetworkManager builds it with Me = true), so it reads like any peer.
        if (GameBuild.IsSteam)
            return SteamClient.IsValid ? PlayerKey.Steam(SteamClient.SteamId.Value) : PlayerKey.None;

        return Of(HawkNetworkManager.DefaultInstance?.GetMe());
    }

    /// <summary>
    /// The live connection for an account, or null when they are not connected. Host only in
    /// practice: <c>GetPlayers()</c> holds the full roster only on the server.
    /// </summary>
    public static HawkConnection ConnectionFor(PlayerKey key)
    {
        if (!key.IsValid) return null;

        var players = HawkNetworkManager.DefaultInstance?.GetPlayers();
        if (players == null) return null;

        for (var i = 0; i < players.Count; i++)
            if (Of(players[i]) == key)
                return players[i];

        return null;
    }

    /// <summary>
    /// Every player controller in the current game owned by an account: more than one in split
    /// screen, empty when they are not in the game.
    /// </summary>
    public static List<PlayerController> ControllersFor(PlayerKey key)
    {
        var result = new List<PlayerController>();
        if (!key.IsValid || !GameInstance.InstanceExists) return result;

        var controllers = GameInstance.Instance.GetPlayerControllers();
        if (controllers == null) return result;

        var local = Local();

        for (var i = 0; i < controllers.Count; i++)
        {
            var controller = controllers[i];
            if (!controller) continue;

            var owner = Of(controller.networkObject?.GetOwner());

            if (owner.IsValid)
            {
                if (owner == key) result.Add(controller);
            }
            else if (controller.IsLocal() && local.IsValid && local == key)
            {
                result.Add(controller);
            }
        }

        return result;
    }

    /// <summary>
    /// A name to show for an account. Steam accounts resolve to the persona name; everything else
    /// falls back to whatever the game replicates for them, which is the only name the crossplay
    /// build ever exposes for a peer.
    /// </summary>
    public static string DisplayNameOf(PlayerKey key)
    {
        if (!key.IsValid) return null;

        if (key.Platform == PlayerPlatform.Steam)
        {
            var persona = PlayerProfileHelper.GetSteamName(key.SteamId);
            if (!string.IsNullOrEmpty(persona)) return persona;
        }

        var controllers = ControllersFor(key);
        if (controllers.Count > 0)
        {
            var name = controllers[0].GetPlayerName();
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return ConnectionFor(key)?.Name;
    }

    /// <summary>
    /// Read the EOS product user id off an <c>EOSConnection</c>. Returns
    /// <see cref="PlayerKey.None"/> for any other connection type, which is what every connection is
    /// on the Steam build.
    /// </summary>
    private static PlayerKey EpicKeyOf(HawkConnection connection)
    {
        var type = connection.GetType();
        if (type.Name != EosConnectionTypeName) return PlayerKey.None;

        if (!ReferenceEquals(type, _cachedEosType))
        {
            _cachedEosType = type;
            _cachedEosField = type.GetField(EosRemoteIdField, Plugin.Flags);

            if (_cachedEosField == null)
                Plugin.LogSource.LogWarning($"[Identity] {EosConnectionTypeName} has no '{EosRemoteIdField}' field; crossplay identities will be unavailable.");
        }

        // ProductUserId.ToString() is the canonical form the game itself compares by, and yields
        // null for a handle that never became valid.
        var id = _cachedEosField?.GetValue(connection)?.ToString();

        return string.IsNullOrEmpty(id) ? PlayerKey.None : PlayerKey.Epic(id);
    }
}
