using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace lstwoMODS_WobblyLife.PropSpawner;

[JsonConverter(typeof(StringEnumConverter))]
public enum PropSourceKind
{
    Addressable = 0,
    CustomItem  = 1,
}

/// <summary>
/// What to spawn, in a form that survives being copied to another install.
/// </summary>
public class PropIdentity
{
    public PropSourceKind Kind;

    /// <summary>Addressables key the prop was spawned from (AssetEntry.LoadKey).</summary>
    public string Address;

    /// <summary>HawkNetworkBehaviour.assetID, captured at save time. First thing the resolver tries.</summary>
    public string NetworkAssetId;

    /// <summary>Unity asset GUID, captured at save time. Late fallback.</summary>
    public string Guid;

    /// <summary>Asset name, captured at save time. Last-resort lookup when no key resolves.</summary>
    public string AssetName;

    /// <summary>
    /// Custom items are identified by name, never by the (PackIndex, ItemIndex) pair used on the
    /// wire: those come from Directory.GetDirectories / AssetBundle load order and are not stable
    /// across installs.
    /// </summary>
    public string PackName;

    public string ItemName;

    /// <summary>How it was spawned. Restore honours this rather than re-deriving it.</summary>
    public bool Networked;

    [JsonIgnore] public bool IsCustomItem => Kind == PropSourceKind.CustomItem;

    /// <summary>Short label for logs and error reporting.</summary>
    public string Describe()
        => IsCustomItem ? $"{PackName}/{ItemName}" : (Address ?? Guid ?? AssetName ?? "<unknown>");

    public PropIdentity Clone() => (PropIdentity)MemberwiseClone();
}

/// <summary>
/// One component whose <c>enabled</c> flag differs from what the prefab spawned with. Only
/// deviations are stored, so the file stays small and a component the user *enabled* is captured
/// just as faithfully as one they disabled.
/// </summary>
public class ComponentToggle
{
    /// <summary>Transform path relative to the prop root. Empty string means the root itself.</summary>
    public string Path;

    /// <summary>Component type full name.</summary>
    public string Type;

    /// <summary>Which one, when a transform has several components of the same type.</summary>
    public int Ordinal;

    public bool Enabled;

    [JsonIgnore] public string Key => MakeKey(Path, Type, Ordinal);

    public static string MakeKey(string path, string type, int ordinal) => $"{path}|{type}|{ordinal}";
}

/// <summary>One prop in a saved layout: what it is, where it is, and how it was tweaked.</summary>
public class PropPlacement
{
    /// <summary>
    /// Stable identity across sessions. SpawnId cannot serve this purpose: it is a process-local
    /// counter that restarts with the game.
    /// </summary>
    public string Id = System.Guid.NewGuid().ToString("N");

    public PropIdentity Source = new();

    /// <summary>GameObject.name at capture, so a rename made in UnityExplorer survives.</summary>
    public string Name;

    public bool Active = true;

    // World TRS is always written, for every prop. It is authoritative for roots, and the
    // fallback for children whose parent failed to spawn or whose parenting was refused.
    public PropVec3 Position;
    public PropQuat Rot;

    /// <summary>Euler form of <see cref="Rot"/>, written purely so the file can be hand-edited.
    /// Only consulted when <see cref="Rot"/> is invalid.</summary>
    public PropVec3 RotEuler;

    public PropVec3 Scale = new(1f, 1f, 1f);

    /// <summary>Index into the owning group's Props list, or -1 for a world root.</summary>
    public int ParentIndex = -1;

    /// <summary>
    /// Path under the parent prop's root transform to the transform this prop is actually
    /// attached to. Empty means the parent's root. Lets a prop attached to a sub-transform of
    /// another prop reattach to the same one.
    /// </summary>
    public string ParentPath;

    public PropVec3 LocalPosition;
    public PropQuat LocalRot;
    public PropVec3 LocalScale = new(1f, 1f, 1f);

    /// <summary>Null or empty when nothing was toggled.</summary>
    public List<ComponentToggle> Toggles;

    [JsonIgnore] public bool HasParent => ParentIndex >= 0;

    /// <summary>Prefers the quaternion; falls back to the euler field for hand-edited files.</summary>
    public UnityEngine.Quaternion Rotation()
        => Rot.IsValid ? Rot : UnityEngine.Quaternion.Euler(RotEuler);

    public UnityEngine.Quaternion LocalRotation()
        => LocalRot.IsValid ? LocalRot : UnityEngine.Quaternion.identity;
}
