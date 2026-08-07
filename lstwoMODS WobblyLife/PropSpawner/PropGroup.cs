using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// A position in a saved layout. Deliberately not <see cref="Vector3"/>: Newtonsoft serializes
/// its derived properties (normalized, magnitude, sqrMagnitude) too, and these files are meant
/// to be readable and hand-editable by the people sharing them.
/// </summary>
public readonly struct PropVec3
{
    [JsonProperty("x")] public readonly float X;
    [JsonProperty("y")] public readonly float Y;
    [JsonProperty("z")] public readonly float Z;

    [JsonConstructor]
    public PropVec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static implicit operator PropVec3(Vector3 v) => new(v.x, v.y, v.z);
    public static implicit operator Vector3(PropVec3 v) => new(v.X, v.Y, v.Z);

    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}

/// <summary>A rotation in a saved layout. See <see cref="PropVec3"/> for why it isn't a Quaternion.</summary>
public readonly struct PropQuat
{
    [JsonProperty("x")] public readonly float X;
    [JsonProperty("y")] public readonly float Y;
    [JsonProperty("z")] public readonly float Z;
    [JsonProperty("w")] public readonly float W;

    [JsonConstructor]
    public PropQuat(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>False for a default-constructed (all-zero) value, which is not a rotation.</summary>
    [JsonIgnore] public bool IsValid => X * X + Y * Y + Z * Z + W * W > 0.0001f;

    public static implicit operator PropQuat(Quaternion q) => new(q.x, q.y, q.z, q.w);
    public static implicit operator Quaternion(PropQuat q) => new(q.X, q.Y, q.Z, q.W);
}

/// <summary>
/// A named set of props plus, once saved, the layout they were in. Persisted to its own file
/// (<c>prop_groups/{Id}.json</c>) so a whole build can be shared as a single file: copy the file
/// into another install's prop_groups folder and it shows up as a new group on load.
/// </summary>
public class PropGroup
{
    /// <summary>Bumped when the on-disk shape changes. Files newer than this are refused, not half-read.</summary>
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;

    /// <summary>
    /// Slug, and the file name stem. The file name is the source of truth: it is written back
    /// over this field on load, so a shared file dropped into the folder imports cleanly.
    /// </summary>
    public string Id;

    public string Name = "New Group";
    public string Description;
    public string Author;

    public long CreatedUtc;
    public long ModifiedUtc;

    /// <summary>Application.version at capture time. Only for triaging addresses that stop resolving.</summary>
    public string GameVersion;

    public string SceneName;

    /// <summary>Player position at capture time. The anchor "spawn rebased to me" measures from.</summary>
    public PropVec3 Origin;

    /// <summary>Player yaw at capture time, in degrees.</summary>
    public float OriginYaw;

    public List<PropPlacement> Props = new();

    [JsonIgnore] public int PropCount => Props?.Count ?? 0;

    public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
