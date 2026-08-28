using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using HawkNetworking;
using Newtonsoft.Json;
using Steamworks;

namespace lstwoMODS_WobblyLife;

/// <summary>
/// A remembered player: their account (<see cref="Key"/>) plus the name they went by when we saved
/// them. This is what the ban list and command whitelists persist.
/// <para>
/// The Steam-flavoured members below (friend state, avatar, level) only mean anything for a Steam
/// account. On the crossplay build a peer is an EOS product user id and the Steam client knows
/// nothing about them, so those degrade to null / false rather than lying.
/// </para>
/// </summary>
public class PlayerProfile : IEquatable<PlayerProfile>
{
    /// <summary>The account this profile is about.</summary>
    [JsonProperty("Key")]
    public PlayerKey Key { get; set; }

    /// <summary>The display name captured when this profile was saved.</summary>
    [JsonProperty("SteamName")]
    public string Name { get; set; }

    /// <summary>
    /// Accepts the bare Steam id that lists written before the crossplay split stored. Read only:
    /// it is folded into <see cref="Key"/> on load and never written back.
    /// </summary>
    [JsonProperty("SteamId")]
    public ulong LegacySteamId { get; set; }

    public bool ShouldSerializeLegacySteamId() => false;

    public PlayerProfile() { }

    public PlayerProfile(PlayerKey key, string name)
    {
        Key = key;
        Name = name;
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (!Key.IsValid && LegacySteamId != 0UL)
            Key = PlayerKey.Steam(LegacySteamId);

        LegacySteamId = 0UL;
    }

    /// <summary>The Steam id behind this profile, or 0 when it is not a Steam account.</summary>
    [JsonIgnore]
    public ulong SteamId => Key.SteamId;

    /// <summary>True when the Steam-specific members below can say anything useful.</summary>
    [JsonIgnore]
    public bool IsSteamAccount => Key.Platform == PlayerPlatform.Steam;

    [JsonIgnore]
    public Friend? Friend => SteamClient.IsValid && SteamId != 0 ? new Friend(SteamId) : null;

    /// <summary>Live name, falling back to the saved <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string CurrentName => PlayerIdentity.DisplayNameOf(Key) ?? Name;

    /// <summary>Whether this is the local account.</summary>
    [JsonIgnore]
    public bool IsMe => Key.IsValid && Key == PlayerIdentity.Local();

    /// <summary>Whether this account is on the local user's Steam friends list. Steam accounts only.</summary>
    [JsonIgnore]
    public bool IsFriend => Friend?.IsFriend ?? false;

    /// <summary>Whether the local user has blocked this account. Steam accounts only.</summary>
    [JsonIgnore]
    public bool IsBlocked => Friend?.IsBlocked ?? false;

    /// <summary>Whether Steam reports this account as online. Steam accounts only.</summary>
    [JsonIgnore]
    public bool IsOnline => Friend?.IsOnline ?? false;

    /// <summary>Whether this account is currently playing Wobbly Life (per Steam). Steam accounts only.</summary>
    [JsonIgnore]
    public bool IsPlayingThisGame => Friend?.IsPlayingThisGame ?? false;

    /// <summary>Steam community level, or 0 when unknown or not a Steam account.</summary>
    [JsonIgnore]
    public int SteamLevel => Friend?.SteamLevel ?? 0;

    /// <summary>Steam online state, Offline for anything that is not a Steam account.</summary>
    [JsonIgnore]
    public FriendState State => Friend?.State ?? FriendState.Offline;

    /// <summary>Fetch this account's avatar (medium size), or null when unavailable.</summary>
    public Task<Steamworks.Data.Image?> GetAvatarAsync() =>
        Friend?.GetMediumAvatarAsync() ?? Task.FromResult<Steamworks.Data.Image?>(null);

    /// <summary>The live network connection for this account, or null when they are not in the lobby.</summary>
    [JsonIgnore]
    public HawkConnection Connection => PlayerIdentity.ConnectionFor(Key);

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
    /// The player's current in-game (changeable) name, or null when not in the lobby.
    /// Prefer <see cref="Name"/> / <see cref="CurrentName"/> for anything identity-related.
    /// </summary>
    [JsonIgnore]
    public string InGameName => Connection?.Name;

    /// <summary>
    /// All player controllers this account owns in the current game (more than one in split screen,
    /// empty when the account is not in the game).
    /// </summary>
    [JsonIgnore]
    public List<PlayerController> Controllers => PlayerIdentity.ControllersFor(Key);

    /// <summary>Re-resolve the saved <see cref="Name"/> if the account is currently reachable.</summary>
    public void RefreshName() => Name = PlayerIdentity.DisplayNameOf(Key) ?? Name;

    public bool Equals(PlayerProfile other) => other != null && Key == other.Key;
    public override bool Equals(object obj) => Equals(obj as PlayerProfile);
    public override int GetHashCode() => Key.GetHashCode();
    public override string ToString() => $"{Name} ({Key})";
}
