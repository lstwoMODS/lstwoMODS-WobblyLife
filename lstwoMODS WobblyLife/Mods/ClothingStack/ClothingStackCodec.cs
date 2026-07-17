using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

/// <summary>One extra clothing piece layered on top of the normal outfit.</summary>
public class StackLayer
{
    public string Guid { get; set; }

    public float R { get; set; } = 1f;
    public float G { get; set; } = 1f;
    public float B { get; set; } = 1f;
    public float A { get; set; } = -1f;
}

/// <summary>
/// Compact string (de)serialization for a layer list, used both for the network payload
/// and as the change-signature the applier compares against.
/// Format: <c>guid[|RRGGBBAA]</c> pieces joined by <c>;</c>. The colour segment is omitted
/// when the layer has no override.
/// </summary>
public static class ClothingStackCodec
{
    public const int MaxLayers = 12;

    public static string Encode(List<StackLayer> layers)
    {
        if (layers == null || layers.Count == 0) return "";

        var sb = new StringBuilder();
        var count = 0;
        foreach (var layer in layers)
        {
            if (layer == null || string.IsNullOrEmpty(layer.Guid)) continue;
            if (count >= MaxLayers) break;

            if (sb.Length > 0) sb.Append(';');
            sb.Append(layer.Guid);
            if (layer.A >= 0f)
            {
                sb.Append('|');
                sb.Append(Hex(layer.R)).Append(Hex(layer.G)).Append(Hex(layer.B)).Append(Hex(layer.A));
            }
            count++;
        }
        return sb.ToString();
    }

    public static List<StackLayer> Decode(string encoded)
    {
        var list = new List<StackLayer>();
        if (string.IsNullOrEmpty(encoded)) return list;

        foreach (var part in encoded.Split(';'))
        {
            if (list.Count >= MaxLayers) break;
            if (string.IsNullOrEmpty(part)) continue;

            var bar = part.IndexOf('|');
            var guidStr = bar < 0 ? part : part.Substring(0, bar);
            if (!Guid.TryParse(guidStr, out var guid) || guid == Guid.Empty) continue;

            var layer = new StackLayer { Guid = guid.ToString() };
            if (bar >= 0)
            {
                var hex = part.Substring(bar + 1);
                if (hex.Length >= 8)
                {
                    layer.R = Channel(hex, 0);
                    layer.G = Channel(hex, 2);
                    layer.B = Channel(hex, 4);
                    layer.A = Channel(hex, 6);
                }
            }
            list.Add(layer);
        }
        return list;
    }

    private static string Hex(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255).ToString("x2");

    private static float Channel(string hex, int index)
    {
        try { return Convert.ToInt32(hex.Substring(index, 2), 16) / 255f; }
        catch { return 1f; }
    }
}
