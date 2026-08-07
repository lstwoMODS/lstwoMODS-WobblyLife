using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ESP;

public class ESPDrawer : MonoBehaviour
{
    private void OnGUI()
    {
        foreach (var tracker in ESPManager.trackers)
        {
            tracker.DrawGUI();
        }
    }
    
    private void OnPostRender()
    {
        foreach (var tracker in ESPManager.trackers)
        {
            tracker.DrawGL();
        }
    }
}