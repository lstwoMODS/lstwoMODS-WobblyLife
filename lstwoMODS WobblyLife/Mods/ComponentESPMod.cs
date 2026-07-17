using System;
using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Mods.ESP;
using UnityEngine;
using UnityExplorer;
using Object = UnityEngine.Object;

namespace lstwoMODS_WobblyLife.Mods;

public class ComponentESPMod : BaseMod
{
    public override string Name => "Component ESP";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    private static readonly Ref<bool> _masterEnabled = new();

    private static readonly Ref<string[]> _trackedTypeNames = new(Array.Empty<string>());
    private static readonly Ref<int>      _selectedIndex    = new();
    private static readonly Ref<bool>     _hasNoSelection   = new(true);

    private static readonly Ref<bool> _perEnabled = new();
    private static readonly Ref<bool> _perLines   = new();
    private static readonly Ref<bool> _perBoxes   = new();
    private static readonly Ref<bool> _perText    = new();

    private static readonly Dictionary<Type, GameObjectTracker> _componentTrackers = new();
    private static readonly List<Type> _trackerOrder = new();
    private static float _scanTimer;
    private const float ScanInterval = 0.5f;

    protected override void OnStaticInit()
    {
        _perEnabled.Changed += v => { if (GetSelectedTracker() is { } t) t.draw      = v; };
        _perLines.Changed   += v => { if (GetSelectedTracker() is { } t) t.drawLines = v; };
        _perBoxes.Changed   += v => { if (GetSelectedTracker() is { } t) t.drawBoxes = v; };
        _perText.Changed    += v => { if (GetSelectedTracker() is { } t) t.drawText  = v; };
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            new Checkbox("Enable Component ESP").WithValue(_masterEnabled),

            new SeparatorText($"{id}-sep-track", "Tracked Components"),
            new Button("Add Inspected Component", AddInspectedComponent).WithContentWidth(),
            new Button("Remove Inspected Component", RemoveInspectedComponent).WithContentWidth(),
            new Combo("Select Component", Array.Empty<string>(), 0, i => SyncPerComponentRefs(i))
                .WithItems(_trackedTypeNames)
                .WithSelectedIndex(_selectedIndex),

            new SeparatorText($"{id}-sep-opts", "Component Options"),
            new UIText("No Component Selected", $"{id}-no-sel").WithVisible(_hasNoSelection),
            new Group($"{id}-opts",
                new Checkbox("Draw").WithValue(_perEnabled),
                new Checkbox("Draw Lines").WithValue(_perLines),
                new Checkbox("Draw Boxes").WithValue(_perBoxes),
                new Checkbox("Draw Text").WithValue(_perText)
            ).WithDisabled(_hasNoSelection)
        );
    }

    public override void Update()
    {
        if (!_masterEnabled.Value || _componentTrackers.Count == 0) return;

        _scanTimer += Time.deltaTime;
        if (_scanTimer < ScanInterval) return;
        _scanTimer = 0f;

        // FindObjectsOfType and all Unity API calls below must stay on the main thread.
        // The original Task.Run() around these calls was the source of crashes.
        var allComponents = Object.FindObjectsOfType<Component>();

        foreach (var kvp in _componentTrackers)
        {
            var tracker = kvp.Value;
            tracker.trackedObjects = allComponents
                .Where(c => c != null && c.GetType() == kvp.Key)
                .Select(c => c.gameObject)
                .Where(go => go != null)
                .Distinct()
                .ToList();

            tracker.RefreshCache();
        }
    }

    private void AddInspectedComponent()
    {
        if (InspectorManager.ActiveInspector == null) return;

        var type = InspectorManager.ActiveInspector.Target switch
        {
            Type t when t.IsSubclassOf(typeof(Component)) => t,
            Component c => c.GetType(),
            _ => null
        };

        if (type == null || _componentTrackers.ContainsKey(type)) return;

        var tracker = new GameObjectTracker();
        _componentTrackers[type] = tracker;
        _trackerOrder.Add(type);
        ESPManager.trackers.Add(tracker);
        ESPManager.Refresh();
        SyncTypeNames();

        // Auto-select the new entry so its options are immediately visible
        var newIndex = _trackerOrder.Count - 1;
        _selectedIndex.Value = newIndex;
        SyncPerComponentRefs(newIndex);
    }

    private void RemoveInspectedComponent()
    {
        if (InspectorManager.ActiveInspector == null) return;

        var type = InspectorManager.ActiveInspector.Target switch
        {
            Type t when t.IsSubclassOf(typeof(Component)) => t,
            Component c => c.GetType(),
            _ => null
        };

        if (type == null || !_componentTrackers.TryGetValue(type, out var tracker)) return;

        ESPManager.trackers.Remove(tracker);
        _componentTrackers.Remove(type);
        _trackerOrder.Remove(type);
        SyncTypeNames();
        
        var newIndex = Math.Min(_selectedIndex.Value, _trackerOrder.Count - 1);
        _selectedIndex.Value = Math.Max(0, newIndex);
        SyncPerComponentRefs(_selectedIndex.Value);
    }

    private static void SyncPerComponentRefs(int index)
    {
        GameObjectTracker tracker = null;
        if (index >= 0 && index < _trackerOrder.Count)
            _componentTrackers.TryGetValue(_trackerOrder[index], out tracker);

        _hasNoSelection.Value = tracker == null;
        if (tracker == null) return;

        _perEnabled.Value = tracker.draw;
        _perLines.Value   = tracker.drawLines;
        _perBoxes.Value   = tracker.drawBoxes;
        _perText.Value    = tracker.drawText;
    }

    private static GameObjectTracker GetSelectedTracker()
    {
        var i = _selectedIndex.Value;
        if (i < 0 || i >= _trackerOrder.Count) return null;
        return _componentTrackers.TryGetValue(_trackerOrder[i], out var t) ? t : null;
    }

    private static void SyncTypeNames()
    {
        _trackedTypeNames.Value = _trackerOrder.Select(t => t.Name).ToArray();
    }
}
