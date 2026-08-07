using System.Collections.Generic;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ESP;

public class ESPManager : MonoBehaviour
{
    public static ESPManager Instance
    {
        get
        {
            if (field != null) return field;
            
            field = new GameObject("ESP Behavior").AddComponent<ESPManager>();
            return field;
        }
    }
    
    public static bool draw = true;
    
    public static GameObjectTracker vehicleTracker = new();
    public static PlayerTracker playerTracker = new();

    public static List<GameObjectTracker> trackers = new();
    
    public static GUIStyle style = new()
    {
        normal =
        {
            textColor = Color.white
        },
        fontSize = 14
    };

    static ESPManager()
    {
        trackers.Add(vehicleTracker);
        trackers.Add(playerTracker);
    }

    public static void Refresh()
    {
        _ = Instance;
        
        if (!Camera.main?.GetComponent<ESPDrawer>())
        {
            Camera.main?.gameObject.AddComponent<ESPDrawer>();
        }
        
        foreach (var tracker in trackers)
        {
            tracker.RefreshCache();
        }
    }
}