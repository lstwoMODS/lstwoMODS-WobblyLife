using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public static class NameColorUtil
{
    public static uint Pack(Color c)
    {
        var r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        var g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        var b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        return (r << 16) | (g << 8) | b;
    }

    public static Color Unpack(uint packed)
    {
        var r = ((packed >> 16) & 0xFF) / 255f;
        var g = ((packed >> 8) & 0xFF) / 255f;
        var b = (packed & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    public static Color AutoColor(ulong steamId, string name = null)
    {
        var hue = steamId != 0UL ? HueFromId(steamId) : HueFromName(name);
        return Color.HSVToRGB(hue, 0.6f, 1f);
    }

    public static Color Resolve(uint packed, ulong steamId, string name)
        => packed != 0 ? Unpack(packed) : AutoColor(steamId, name);

    private static float HueFromId(ulong x)
    {
        // fmix64 (MurmurHash3 finalizer) for a good spread of nearby ids across the hue wheel.
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;
        x *= 0xc4ceb9fe1a85ec53UL;
        x ^= x >> 33;
        return (x % 360UL) / 360f;
    }

    private static float HueFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0f;
        ulong h = 1469598103934665603UL; // FNV-1a 64-bit
        foreach (var ch in name)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return HueFromId(h);
    }
}
