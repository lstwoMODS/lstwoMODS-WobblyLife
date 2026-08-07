using System.Collections.Generic;
using UnityEngine;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// Reads and writes the <c>enabled</c> flags of the components on a spawned prop.
///
/// A baseline is taken right after the prop spawns, and only the components that differ from it
/// are written to the group file. That keeps the files small and — more importantly — captures
/// both directions: a component the user switched *on* is as much a deviation from the prefab as
/// one they switched off, and a "record everything that is false" scheme would lose the former.
/// </summary>
public static class PropComponentState
{
    /// <summary>
    /// Above this many transforms the walk is skipped entirely. Some prefabs (vehicles, buildings)
    /// have hierarchies deep enough that scanning them every spawn would cost a visible hitch, and
    /// nobody is hand-toggling components on those anyway.
    /// </summary>
    public const int MaxTransforms = 512;

    /// <summary>Every toggleable component's state, keyed by <see cref="ComponentToggle.MakeKey"/>.
    /// Null when the object is too big to be worth scanning.</summary>
    public static Dictionary<string, bool> CaptureBaseline(GameObject root)
    {
        if (root == null) return null;
        if (root.GetComponentsInChildren<Transform>(true).Length > MaxTransforms) return null;

        var result = new Dictionary<string, bool>();
        Walk(root.transform, "", (path, type, ordinal, enabled) =>
            result[ComponentToggle.MakeKey(path, type, ordinal)] = enabled);
        return result;
    }

    /// <summary>The components whose state differs from <paramref name="baseline"/>.
    /// Null when nothing deviates, so the field stays out of the JSON entirely.</summary>
    public static List<ComponentToggle> CaptureDeviations(GameObject root, Dictionary<string, bool> baseline)
    {
        if (root == null || baseline == null) return null;

        List<ComponentToggle> deviations = null;

        Walk(root.transform, "", (path, type, ordinal, enabled) =>
        {
            // Absent from the baseline means the component was added after the prop spawned.
            // That isn't ours to restore, so leave it alone.
            if (!baseline.TryGetValue(ComponentToggle.MakeKey(path, type, ordinal), out var was)) return;
            if (was == enabled) return;

            deviations ??= new List<ComponentToggle>();
            deviations.Add(new ComponentToggle
            {
                Path    = path,
                Type    = type,
                Ordinal = ordinal,
                Enabled = enabled,
            });
        });

        return deviations;
    }

    /// <summary>Re-applies saved toggles. Anything that no longer exists is skipped silently —
    /// a prefab that changed between game versions must not abort a restore.</summary>
    public static void Apply(GameObject root, List<ComponentToggle> toggles)
    {
        if (root == null || toggles == null) return;

        foreach (var toggle in toggles)
        {
            var target = string.IsNullOrEmpty(toggle.Path)
                ? root.transform
                : root.transform.Find(toggle.Path);
            if (target == null) continue;

            var ordinal = 0;
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component.GetType().FullName != toggle.Type) continue;
                if (ordinal++ != toggle.Ordinal) continue;

                SetEnabled(component, toggle.Enabled);
                break;
            }
        }
    }

    /// <summary>Path from <paramref name="root"/> down to <paramref name="target"/>, in the form
    /// <c>Transform.Find</c> accepts. Empty when they are the same transform.</summary>
    public static string RelativePath(Transform root, Transform target)
    {
        if (root == null || target == null || target == root) return "";

        var segments = new List<string>();
        for (var t = target; t != null && t != root; t = t.parent)
            segments.Add(t.name);

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static void Walk(Transform current, string path,
                             System.Action<string, string, int, bool> visit)
    {
        var counts = new Dictionary<string, int>();

        foreach (var component in current.GetComponents<Component>())
        {
            if (component == null) continue;   // missing script

            var type = component.GetType().FullName;
            if (type == null) continue;

            counts.TryGetValue(type, out var ordinal);
            counts[type] = ordinal + 1;

            if (!TryGetEnabled(component, out var enabled)) continue;

            visit(path, type, ordinal, enabled);
        }

        for (var i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            var childPath = path.Length == 0 ? child.name : path + "/" + child.name;
            Walk(child, childPath, visit);
        }
    }

    // Unity has no common base exposing `enabled` — Behaviour, Renderer and Collider each
    // declare their own. Everything else (Transform, MeshFilter, Rigidbody, ...) has no
    // enabled flag at all and is skipped.
    private static bool TryGetEnabled(Component component, out bool enabled)
    {
        switch (component)
        {
            case Behaviour behaviour: enabled = behaviour.enabled; return true;
            case Renderer renderer:   enabled = renderer.enabled;  return true;
            case Collider collider:   enabled = collider.enabled;  return true;
            default:                  enabled = false;             return false;
        }
    }

    private static void SetEnabled(Component component, bool enabled)
    {
        switch (component)
        {
            case Behaviour behaviour: behaviour.enabled = enabled; break;
            case Renderer renderer:   renderer.enabled  = enabled; break;
            case Collider collider:   collider.enabled  = enabled; break;
        }
    }
}
