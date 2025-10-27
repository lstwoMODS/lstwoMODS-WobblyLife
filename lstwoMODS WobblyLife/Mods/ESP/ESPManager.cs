using System.Collections.Generic;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks.ESP;

public class ESPManager : MonoBehaviour
{
    public static ESPManager Instance { get; private set; }
    
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
        if (!Camera.main?.GetComponent<ESPDrawer>())
        {
            Camera.main?.gameObject.AddComponent<ESPDrawer>();
        }
        
        foreach (var tracker in trackers)
        {
            tracker.RefreshCache();
        }
    }
    
    private void Awake()
    {
        Instance = this;
    }
}