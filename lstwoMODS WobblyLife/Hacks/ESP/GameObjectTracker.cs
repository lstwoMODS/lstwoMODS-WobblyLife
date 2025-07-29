using System.Collections.Generic;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks.ESP;

public class GameObjectTracker
{
    public bool draw = false;
    public bool drawText = false;
    public bool drawBoxes = false;
    public bool drawLines = false;
    
    public List<GameObject> trackedObjects = new();
    
    protected Dictionary<GameObject, Bounds> objectBoundsCache = new();
    protected Camera mainCamera;
    protected Rigidbody playerHipRb;
    
    protected Material lineMaterial = new(Shader.Find("Unlit/Color"));

    public virtual void RefreshCache()
    {
        playerHipRb = GameInstance.Instance.GetFirstLocalPlayerController().GetPlayerCharacter().GetHipRigidbody();
        mainCamera = Camera.main;
        objectBoundsCache.Clear();
        
        foreach (var obj in trackedObjects)
        {
            RefreshCacheForObject(obj);
        }
    }

    public virtual void RefreshCacheForObject(GameObject obj)
    {
        if (!obj)
        {
            return;
        }
            
        var meshFilter = obj.GetComponent<MeshFilter>();
        var bounds = meshFilter && meshFilter.sharedMesh && meshFilter.sharedMesh.isReadable && meshFilter.sharedMesh.bounds.extents.magnitude != 0 ? meshFilter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        objectBoundsCache[obj] = bounds;
    }

    public virtual void AddTrackedObject(GameObject obj)
    {
        if (trackedObjects.Contains(obj))
        {
            return;
        }
        
        trackedObjects.Add(obj);
        RefreshCacheForObject(obj);
    }

    public virtual void RemoveTrackedObject(GameObject obj)
    {
        if (!trackedObjects.Contains(obj) && !objectBoundsCache.ContainsKey(obj))
        {
            return;
        }
        
        trackedObjects.Remove(obj);
        objectBoundsCache.Remove(obj);
    }

    public virtual void DrawGUI()
    {
        if (!draw || !ESPManager.draw || Event.current.type != EventType.Repaint || !mainCamera)
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
            var screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (!(screenPos.z > 0))
            {
                continue;
            }
            
            screenPos.y = Screen.height - screenPos.y;

            if (drawText)
            {
                DrawText(screenPos, $"{obj.name}\n{worldPos.x}, {worldPos.y}, {worldPos.z}");
            }
        }
    }

    public virtual void DrawGL()
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

            if (drawBoxes && objectBoundsCache.TryGetValue(obj, out var bounds))
            {
                DrawBox(obj, bounds, Color.white);
            }

            if (drawLines)
            {
                DrawLine(playerHipRb.transform.position, worldPos, Color.HSVToRGB(Mathf.Clamp01(Vector3.Distance(worldPos, playerHipRb.transform.position) / 300f), 1f, 1f));
            }
        }
    }
    
    protected virtual void DrawText(Vector3 screenPos, string text)
    {
        GUI.Label(new(screenPos.x, screenPos.y, 200, 20), text, ESPManager.style);
    }

    protected virtual void DrawBox(GameObject obj, Bounds bounds, Color color)
    {
        var corners = new Vector3[8];
        var ext = bounds.extents;
        var t = obj.transform;
        corners[0] = t.TransformPoint(bounds.center + new Vector3(-ext.x, -ext.y, -ext.z));
        corners[1] = t.TransformPoint(bounds.center + new Vector3(ext.x, -ext.y, -ext.z));
        corners[2] = t.TransformPoint(bounds.center + new Vector3(ext.x, ext.y, -ext.z));
        corners[3] = t.TransformPoint(bounds.center + new Vector3(-ext.x, ext.y, -ext.z));
        corners[4] = t.TransformPoint(bounds.center + new Vector3(-ext.x, -ext.y, ext.z));
        corners[5] = t.TransformPoint(bounds.center + new Vector3(ext.x, -ext.y, ext.z));
        corners[6] = t.TransformPoint(bounds.center + new Vector3(ext.x, ext.y, ext.z));
        corners[7] = t.TransformPoint(bounds.center + new Vector3(-ext.x, ext.y, ext.z));

        /*for (var i = 0; i < 8; i++)
        {
            corners[i] = mainCamera.WorldToScreenPoint(corners[i]);
            corners[i].y = Screen.height - corners[i].y;
        }*/

        DrawLine(corners[0], corners[1], color); DrawLine(corners[1], corners[2], color);
        DrawLine(corners[2], corners[3], color); DrawLine(corners[3], corners[0], color);
        DrawLine(corners[4], corners[5], color); DrawLine(corners[5], corners[6], color);
        DrawLine(corners[6], corners[7], color); DrawLine(corners[7], corners[4], color);
        DrawLine(corners[0], corners[4], color); DrawLine(corners[1], corners[5], color);
        DrawLine(corners[2], corners[6], color); DrawLine(corners[3], corners[7], color);
    }

    protected virtual void DrawLine(Vector3 worldStart, Vector3 worldEnd, Color color)
    {
        var screenStart = mainCamera.WorldToScreenPoint(worldStart);
        var screenEnd = mainCamera.WorldToScreenPoint(worldEnd);

        if (screenStart.z < 0 || screenEnd.z < 0)
        {
            return;
        }
        
        var normalizedStart = new Vector2(screenStart.x / Screen.width, screenStart.y / Screen.height);
        var normalizedEnd = new Vector2(screenEnd.x / Screen.width, screenEnd.y / Screen.height);

        GL.PushMatrix();
        GL.LoadOrtho();
        
        lineMaterial.SetColor("_Color", color);
        lineMaterial.SetPass(0);
        
        GL.Begin(GL.LINES);

        GL.Vertex(normalizedStart);
        GL.Vertex(normalizedEnd);

        GL.End();
        GL.PopMatrix();
    }
}