using System;
using System.Collections;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using Unity.Microsoft.GDK;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class LoadSceneMod : BaseMod
{
    public override string Name => "Load Scene";
    public override string Description => "Loads a different scene, may be broken on clients, loads it for all clients when used as host.";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    private static readonly object Handle = new();
    private static bool isSwitching;

    [ModAction]
    public static void Load(LoadSceneModArg sceneToLoad)
    {
        if (isSwitching) return; 
        
        if (!GameInstance.InstanceExists || !GameInstance.Instance.GetGamemode())
        {
            Plugin.LogSource.LogWarning("Not in game, won't load scene");
            return;
        }

        Plugin._StartCoroutine(LoadRoutine(
            sceneToLoad switch
            {
                LoadSceneModArg.WobblyIsland => LoadScene.WobblyIsland,
                LoadSceneModArg.Space => LoadScene.Space,
                LoadSceneModArg.ArcadeLobby => LoadScene.Arcade_Lobby,
                _ => throw new Exception("Invalid arg")
            }
        ));
    }

    private static IEnumerator LoadRoutine(LoadScene scene)
    {
        isSwitching = true;

        var hawk = HawkNetworkManager.InstanceExists ? HawkNetworkManager.DefaultInstance : null;
        var isHost = !hawk || !hawk.IsConnected() || hawk.IsServer();
        var world = WorldManager.InstanceExists ? WorldManager.Instance : null;
        var gated = isHost && hawk;

        try
        {
            if (world) world.gameObject.SetActive(false);

            if (gated)
            {
                hawk.SetJoinable(Handle, false);
                GameInstance.Instance?.IteratePlayerControllers(x =>
                {
                    if (x) x.SetAllowedToRespawn(Handle, false);
                });
            }

            yield return WaitFor(HawkSceneManager.HasFinishedAllSceneAsyncLoading, 15f);

            if (gated)
            {
                hawk.AddDisableFlush(Handle);
                hawk.ServerStartActiveSceneLoading(Handle);
            }

            var loaded = false;
            GameInstance.Instance.Load(scene, _ => loaded = true);
            yield return WaitFor(() => loaded, 120f);

            if (gated)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                hawk.RemoveDisableFlush(Handle);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return WaitFor(() => hawk.HasFlushed() && hawk.HasFinishedRegisteringNetworkObjects(), 30f);
                hawk.ServerEndActiveSceneLoading(Handle);
            }
        }
        finally
        {
            if (gated && hawk)
            {
                hawk.RemoveDisableFlush(Handle);
                hawk.SetJoinable(Handle, true);

                GameInstance.Instance?.IteratePlayerControllers(x =>
                {
                    if (x) x.SetAllowedToRespawn(Handle, true);
                });
            }

            if (world) world.gameObject.SetActive(true);

            isSwitching = false;
        }
    }

    private static IEnumerator WaitFor(Func<bool> predicate, float timeoutSeconds)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    public enum LoadSceneModArg
    {
        WobblyIsland,
        Space,
        ArcadeLobby
    }
}
