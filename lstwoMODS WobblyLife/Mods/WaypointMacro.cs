using System.Reflection;
using lstwoMODS_Core.Macros;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>
/// Registers the "Get Map Marker Position" macro step. It reads the local player's minimap
/// waypoint (the marker you drop on the map with <c>UIWaypoint</c>) and returns its world
/// position as a <see cref="Vector3"/>.
///
/// The marker only carries accurate X/Z: the game builds it from the top-down orthographic map
/// camera, so its raw Y is the camera's near-plane altitude, not ground height (the game itself
/// flattens the icon's Y to 0 when it reuses the position). We therefore raycast straight down at
/// the marker's XZ against the gameplay camera's collision mask to resolve a real ground Y.
///
/// Output is a <c>vec3</c> step output (pipe it via <c>prev</c> or store it with Set Variable);
/// returns <c>null</c> when no marker is placed so callers can branch on it.
/// </summary>
public static class WaypointMacro
{
    private const string Id = "world.waypointPosition";

    private static readonly FieldInfo WaypointIconLocalField =
        typeof(PlayerControllerWaypoint).GetField("waypointIconLocal",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Register()
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id          = Id,
            Label       = "Get Map Marker Position",
            Category    = "World",
            PickerLabel = "Get Map Marker Position",
            ReturnType  = typeof(Vector3),
            Execute     = _ => GetMarkerPosition(),
        });
    }

    /// <summary>The local waypoint's world position with a raycasted ground Y, or <c>null</c> when
    /// no marker is placed (or the local player isn't available).</summary>
    private static object GetMarkerPosition()
    {
        var controller = GameInstance.Instance?.GetFirstLocalPlayerController();
        if (controller == null) return null;

        var waypoint = controller.GetPlayerControllerWaypoint();
        if (waypoint == null || !waypoint.IsWaypointCreated()) return null;

        if (WaypointIconLocalField?.GetValue(waypoint) is not Component icon || icon == null)
            return null;

        var raw = icon.transform.position;
        return new Vector3(raw.x, GroundYAt(raw), raw.z);
    }

    private static int? _groundMask;
    private static int GroundMask => _groundMask ??= LayerMask.GetMask(
        "Terrain", "World", "WorldLarge", "MeshDisplacement", "Building", "Road", "Default");

    /// <summary>Raycasts down at the given XZ to find ground height. Falls back to 0 (sea level,
    /// matching the game's own flattening) when nothing solid is hit below.</summary>
    private static float GroundYAt(Vector3 xz)
    {
        var origin = new Vector3(xz.x, 100000f, xz.z);

        if (Physics.Raycast(origin, Vector3.down, out var hit, Mathf.Infinity, GroundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return 0f;
    }
}
