using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Hacks.ESP;
using UnityEngine;
using UnityExplorer;
using Object = UnityEngine.Object;

namespace lstwoMODS_WobblyLife.Hacks;

// CRASHES THE GAME

public class ComponentESPMod : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.ComponentESPMod.Enabled", "Enable Component ESP", b => Draw = b);
        
        ui.AddSpacer(12);

        ui.CreateLBBTrio("Select Components to Track (Unity Explorer)", "lstwo.ComponentESPMod.SelectLBB", 
            AddInspectedComponent, "Add", "lstwo.ComponentESPMod.AddButton", 
            RemoveInspectedComponent, "Remove", "lstwo.ComponentESPMod.RemoveButton");
        
        ui.AddSpacer(12);

        ui.CreateToggle("lstwo.ComponentESPMod.DrawLines", "Draw Lines", b => DrawLines = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.ComponentESPMod.DrawBoxes", "Draw Boxes", b => DrawBoxes = b);
        
        ui.AddSpacer(6);
        
        ui.CreateToggle("lstwo.ComponentESPMod.DrawText", "Draw Text", b => DrawText = b);
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
        if (!draw)
        {
            return;
        }
        
        timingCounter += Time.deltaTime;

        if (!(timingCounter >= 0.5f))
        {
            return;
        }
        
        Task.Run(ScanForComponents);

        foreach (var tracker in ComponentTrackers.Values)
        {
            Task.Run(() => tracker.RefreshCache());
        }
        
        timingCounter = 0f;
    }

    public override void RefreshUI()
    {
    }

    private async Task ScanForComponents()
    {
        var allComponents = Object.FindObjectsOfType<Component>();
        var dict = new Dictionary<Type, GameObject[]>();

        var tasks = ComponentTrackers.Keys.Select(async componentType =>
        {
            var result = await Task.Run(() => ScanForComponent(componentType, ComponentTrackers[componentType], allComponents));
            return result;
        }).ToList();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            dict[result.Key] = result.Value;
        }

        foreach (var type in dict.Keys)
        {
            var objects = dict[type];
            var tracker = ComponentTrackers[type];
            tracker.trackedObjects = objects.ToList();
        }
    }

    private KeyValuePair<Type, GameObject[]> ScanForComponent(Type componentType, GameObjectTracker tracker, Component[] allComponents)
    {
        var foundComponents = allComponents
            .Where(component => component.GetType() == componentType)
            .Select(component => component.gameObject)
            .ToArray();

        return new KeyValuePair<Type, GameObject[]>(componentType, foundComponents);
    }

    private void AddInspectedComponent()
    {
        var inspectedObj = InspectorManager.ActiveInspector.Target;

        var type = inspectedObj switch
        {
            Type inspectedType when inspectedType.IsSubclassOf(typeof(Component)) => inspectedType,
            Component inspectedComponent => inspectedComponent.GetType(),
            _ => null
        };

        if (type == null || ComponentTrackers.ContainsKey(type))
        {
            return;
        }
        
        var tracker = new GameObjectTracker
        {
            draw = draw,
            drawLines = drawLines,
            drawBoxes = drawBoxes,
            drawText = drawText
        };

        ComponentTrackers.Add(type, tracker);
    }
    
    private void RemoveInspectedComponent()
    {
        var inspectedObj = InspectorManager.ActiveInspector.Target;

        switch (inspectedObj)
        {
            case Type inspectedType when inspectedType.IsSubclassOf(typeof(Component)):
            {
                if (!ComponentTrackers.ContainsKey(inspectedType))
                {
                    return;
                }
            
                ComponentTrackers.Remove(inspectedType);
                break;
            }
            case Component inspectedComponent:
            {
                var type = inspectedComponent.GetType();
            
                if (!ComponentTrackers.ContainsKey(type))
                {
                    return;
                }
            
                ComponentTrackers.Remove(type);
                break;
            }
        }
    }

    public static bool Draw
    {
        set
        {
            draw = value;
            
            foreach (var tracker in ComponentTrackers.Values)
            {
                tracker.draw = value;
            }
        }
    }
    
    public static bool DrawLines
    {
        set
        {
            drawLines = value;
            
            foreach (var tracker in ComponentTrackers.Values)
            {
                tracker.drawLines = value;
            }
        }
    }
    
    public static bool DrawBoxes
    {
        set
        {
            drawBoxes = value;
            
            foreach (var tracker in ComponentTrackers.Values)
            {
                tracker.drawBoxes = value;
            }
        }
    }
    
    public static bool DrawText
    {
        set
        {
            drawText = value;
            
            foreach (var tracker in ComponentTrackers.Values)
            {
                tracker.drawText = value;
            }
        }
    }

    private static bool draw;
    private static bool drawLines;
    private static bool drawBoxes;
    private static bool drawText;

    public override string Name => "Component ESP";
    public override string Description => "";
    public override HacksTab HacksTab => null;
    
    private static Dictionary<Type, GameObjectTracker> ComponentTrackers = new();
    private static float timingCounter;
}