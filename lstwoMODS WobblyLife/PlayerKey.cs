using System;
using System.Globalization;
using Newtonsoft.Json;

namespace lstwoMODS_WobblyLife;

/// <summary>Which account system a <see cref="PlayerKey"/> came from.</summary>
public enum PlayerPlatform : byte
{
    /// <summary>No identity: an offline game, a LAN peer, or a player we could not place.</summary>
    None = 0,

    /// <summary>A Steam account id. Only the Steam build hands these out.</summary>
    Steam = 1,

    /// <summary>An Epic Online Services <c>ProductUserId</c>. Only the crossplay build hands these out.</summary>
    Epic = 2,
}

/// <summary>
/// A player's account identity, in the one form that works on both game builds.
/// <para>
/// The Steam build gives every peer a <c>SteamConnection</c> carrying a Steam id. The crossplay
/// build gives every peer an <c>EOSConnection</c> carrying an EOS <c>ProductUserId</c> instead, and
/// never constructs a <c>SteamConnection</c> at all, so a bare <c>ulong</c> Steam id cannot identify
/// a player there. Anything that has to remember a player <i>between sessions</i> (the ban list, a
/// command whitelist) keys off this instead.
/// </para>
/// <para>
/// Identity that only has to hold for the current session is better served by the owning
/// <c>PlayerController</c>'s network id, which is the same number on every machine and every
/// transport. See <c>WLProxChat.Transport.VoiceIdentity</c>.
/// </para>
/// <para>
/// Serializes to a single string, <c>"steam:76561198…"</c> or <c>"epic:0002abc…"</c>, so saved data
/// stays readable and a key written by one build is recognisable on the other.
/// </para>
/// </summary>
[JsonConverter(typeof(PlayerKeyJsonConverter))]
public readonly struct PlayerKey : IEquatable<PlayerKey>
{
    private const string SteamPrefix = "steam";
    private const string EpicPrefix = "epic";

    /// <summary>The empty key. Never matches anything, including itself in a whitelist check.</summary>
    public static readonly PlayerKey None = default;

    public PlayerPlatform Platform { get; }

    /// <summary>The raw account id: a decimal Steam id, or an EOS product user id. Never null.</summary>
    public string Id { get; }

    private PlayerKey(PlayerPlatform platform, string id)
    {
        Platform = platform;
        Id = id ?? "";
    }

    public bool IsValid => Platform != PlayerPlatform.None && !string.IsNullOrEmpty(Id);

    /// <summary>The Steam id behind this key, or 0 when it is not a Steam key.</summary>
    public ulong SteamId =>
        Platform == PlayerPlatform.Steam && ulong.TryParse(Id, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0UL;

    public static PlayerKey Steam(ulong steamId) =>
        steamId == 0UL ? None : new PlayerKey(PlayerPlatform.Steam, steamId.ToString(CultureInfo.InvariantCulture));

    public static PlayerKey Epic(string productUserId) =>
        string.IsNullOrEmpty(productUserId) ? None : new PlayerKey(PlayerPlatform.Epic, productUserId);

    /// <summary>
    /// Read a key back from its stored form. A bare decimal number is read as a Steam id, so lists
    /// written before the crossplay split still load.
    /// </summary>
    public static PlayerKey Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return None;

        text = text.Trim();

        var split = text.IndexOf(':');
        if (split < 0)
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var legacy) ? Steam(legacy) : None;

        var prefix = text.Substring(0, split);
        var id = text.Substring(split + 1);

        if (string.Equals(prefix, SteamPrefix, StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId) ? Steam(steamId) : None;

        if (string.Equals(prefix, EpicPrefix, StringComparison.OrdinalIgnoreCase))
            return Epic(id);

        return None;
    }

    /// <summary>The stored / wire form. Empty string for <see cref="None"/>.</summary>
    public override string ToString() =>
        Platform switch
        {
            PlayerPlatform.Steam => SteamPrefix + ":" + Id,
            PlayerPlatform.Epic => EpicPrefix + ":" + Id,
            _ => "",
        };

    public bool Equals(PlayerKey other) =>
        Platform == other.Platform && string.Equals(Id, other.Id, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is PlayerKey other && Equals(other);

    public override int GetHashCode() => unchecked(((int)Platform * 397) ^ (Id?.GetHashCode() ?? 0));

    public static bool operator ==(PlayerKey a, PlayerKey b) => a.Equals(b);
    public static bool operator !=(PlayerKey a, PlayerKey b) => !a.Equals(b);
}

/// <summary>Keeps <see cref="PlayerKey"/> a plain string in saved JSON rather than an object.</summary>
public sealed class PlayerKeyJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(PlayerKey);

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var key = (PlayerKey)value;
        if (key.IsValid) writer.WriteValue(key.ToString());
        else writer.WriteNull();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // Integer tolerated so a hand-edited (or very old) file holding a bare Steam id still loads.
        return reader.TokenType switch
        {
            JsonToken.String => PlayerKey.Parse((string)reader.Value),
            JsonToken.Integer => PlayerKey.Steam(Convert.ToUInt64(reader.Value, CultureInfo.InvariantCulture)),
            _ => PlayerKey.None,
        };
    }
}
