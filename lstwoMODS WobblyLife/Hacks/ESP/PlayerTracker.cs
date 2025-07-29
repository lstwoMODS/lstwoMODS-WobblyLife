using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace lstwoMODS_WobblyLife.Hacks.ESP;

public class PlayerTracker : GameObjectTracker
{
    new public List<PlayerBody> trackedObjects = new();
    
    protected Dictionary<PlayerBody, PlayerController> controllerCache = new();

    public void AddTrackedObject(PlayerBody body)
    {
        if (trackedObjects.Contains(body))
        {
            return;
        }
        
        trackedObjects.Add(body);
        Plugin._StartCoroutine(WaitUntilPlayerController(body));
    }

    private IEnumerator WaitUntilPlayerController(PlayerBody body)
    {
        yield return new WaitUntil(() => body.GetPlayerCharacter().GetPlayerController() != null);
        
        controllerCache.Add(body, body.GetPlayerCharacter().GetPlayerController());
    }

    public void RemoveTrackedObject(PlayerBody body)
    {
        if (trackedObjects.Contains(body))
        {
            trackedObjects.Remove(body);
        }

        if (controllerCache.ContainsKey(body))
        {
            controllerCache.Remove(body);
        }
    }
    
    public override void RefreshCache()
    {
        playerHipRb = GameInstance.Instance.GetFirstLocalPlayerController().GetPlayerCharacter().GetHipRigidbody();
        mainCamera = Camera.main;

        for (var i = 0; i < trackedObjects.Count; i++)
        {
            var body = trackedObjects[i];

            if (body)
            {
                continue;
            }

            trackedObjects.RemoveAt(i);
            controllerCache.Remove(body);
        }
    }

    public override void DrawGUI()
    {
        if (!draw || !ESPManager.draw || Event.current.type != EventType.Repaint || !mainCamera)
        {
            return;
        }

        foreach (var body in trackedObjects)
        {
            var controller = controllerCache[body];
                
            if (!body || !controller)
            {
                continue;
            }
            
            var worldPos = body.transform.position;
            var screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (!(screenPos.z > 0))
            {
                continue;
            }
            
            screenPos.y = Screen.height - screenPos.y;

            if (drawText)
            {
                DrawText(screenPos, $"{controller.GetPlayerName()}\n{worldPos.x}, {worldPos.y}, {worldPos.z}");
            }
        }
    }
    
    public override void DrawGL()
    {
        if (!draw || !ESPManager.draw || !mainCamera || !GameInstance.InstanceExists || !GameInstance.Instance.GetGamemode())
        {
            return;
        }
        
        foreach (var obj in trackedObjects)
        {
            if (!obj)
            {
                continue;
            }
            
            var worldPos = obj.transform.position;

            if (drawBoxes)
            {
                DrawBox(obj.gameObject, new Bounds(Vector3.up * 0.5f, Vector3.one + Vector3.up), Color.yellow);
            }

            if (drawLines)
            {
                DrawLine(playerHipRb.transform.position, worldPos, Color.HSVToRGB(Mathf.Clamp01(Vector3.Distance(worldPos, playerHipRb.transform.position) / 300f), 1f, 1f));
            }
        }
    }
}