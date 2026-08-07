using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HawkNetworking;
using Newtonsoft.Json;
using Steamworks;

namespace lstwoMODS_WobblyLife;

public class SteamProfile : IEquatable<SteamProfile>
{
    public ulong SteamId { get; set; }
    public string SteamName { get; set; }

    public SteamProfile() { }

    public SteamProfile(ulong steamId, string steamName)
    {
        SteamId = steamId;
        SteamName = steamName;
    }

    [JsonIgnore]
    public Friend? Friend => SteamClient.IsValid && SteamId != 0 ? new Friend(SteamId) : null;

    /// <summary>Live Steam persona name, falling back to the saved <see cref="SteamName"/>.</summary>
    [JsonIgnore]
    public string CurrentSteamName => SteamProfileHelper.GetSteamName(SteamId) ?? SteamName;

    /// <summary>Whether this is the local Steam account.</summary>
    [JsonIgnore]
    public bool IsMe => Friend?.IsMe ?? false;

    /// <summary>Whether this account is on the local user's Steam friends list.</summary>
    [JsonIgnore]
    public bool IsFriend => Friend?.IsFriend ?? false;

    /// <summary>Whether the local user has blocked this account.</summary>
    [JsonIgnore]
    public bool IsBlocked => Friend?.IsBlocked ?? false;

    /// <summary>Whether Steam reports this account as online.</summary>
    [JsonIgnore]
    public bool IsOnline => Friend?.IsOnline ?? false;

    /// <summary>Whether this account is currently playing Wobbly Life (per Steam).</summary>
    [JsonIgnore]
    public bool IsPlayingThisGame => Friend?.IsPlayingThisGame ?? false;

    /// <summary>Steam community level, or 0 if unknown.</summary>
    [JsonIgnore]
    public int SteamLevel => Friend?.SteamLevel ?? 0;

    /// <summary>Steam online state (Online / Away / Busy / Offline / ...).</summary>
    [JsonIgnore]
    public FriendState State => Friend?.State ?? FriendState.Offline;

    /// <summary>Fetch this account's avatar (medium size), or null if unavailable.</summary>
    public Task<Steamworks.Data.Image?> GetAvatarAsync() =>
        Friend?.GetMediumAvatarAsync() ?? Task.FromResult<Steamworks.Data.Image?>(null);

    
    /// <summary>The live network connection for this account, or null if they're not in the lobby.</summary>
    [JsonIgnore]
    public SteamConnection Connection => SteamProfileHelper.GetConnection(SteamId);

    /// <summary>Whether this account is currently connected to the lobby.</summary>
    [JsonIgnore]
    public bool IsInLobby => Connection != null;

    /// <summary>Whether this account is the lobby host.</summary>
    [JsonIgnore]
    public bool IsHost => Connection?.IsHost ?? false;

    /// <summary>Whether the connection has been authenticated by the host.</summary>
    [JsonIgnore]
    public bool IsAuthenticated => Connection?.Authenticated ?? false;

    /// <summary>
    /// The player's current in-game (changeable) name, or null if not in the lobby.
    /// Prefer <see cref="SteamName"/> / <see cref="CurrentSteamName"/> for anything identity-related.
    /// </summary>
    [JsonIgnore]
    public string InGameName => Connection?.Name;

    /// <summary>
    /// All player controllers this account owns in the current game (more than one in split-screen,
    /// empty if the account isn't in the game).
    /// </summary>
    [JsonIgnore]
    public List<PlayerController> Controllers => SteamProfileHelper.GetPlayerControllers(SteamId);

    /// <summary>Re-resolve the saved <see cref="SteamName"/> from Steam if the account is known.</summary>
    public void RefreshName() => SteamName = SteamProfileHelper.GetSteamName(SteamId) ?? SteamName;

    public bool Equals(SteamProfile other) => other != null && SteamId == other.SteamId;
    public override bool Equals(object obj) => Equals(obj as SteamProfile);
    public override int GetHashCode() => SteamId.GetHashCode();
    public override string ToString() => $"{SteamName} ({SteamId})";
}
