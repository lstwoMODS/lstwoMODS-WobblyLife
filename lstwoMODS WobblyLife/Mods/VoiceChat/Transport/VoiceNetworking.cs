using System;
using HawkNetworking;
using lstwoMODS_WobblyLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Spawns the <see cref="VoiceNetworkManager"/> on the host, mirroring how the chat mod bootstraps
    /// its own networked singleton. Only the host spawns it; it replicates to everyone else.
    /// </summary>
    public static class VoiceNetworking
    {
        private const string VoiceManagerAssetId = "3c7f61d8-9a4e-4d61-8f2b-6d0a5e93b114";

        private static GameObject _voiceManagerPrefab;
        private static bool _initialized;

        /// <summary>
        /// Only hooks the scene event. The prefab itself is registered on the first gameplay scene,
        /// not here: this runs from <c>OnStaticInit</c>, which is early enough that touching
        /// <see cref="HawkNetworkManager.DefaultInstance"/> would force the lazy network singleton
        /// into existence before the LAN mod has chosen a transport type for it.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Anything in here throwing must not take the scene load with it.
            try
            {
                if (mode == LoadSceneMode.Additive) return;
                if (scene.name is "MainMenu" or "LoadingScene") return;

                if (!HawkNetworkManager.InstanceExists) return;

                // Registration happens on every peer, not just the host. Only the host spawns the
                // object, but a client that has not registered the asset id cannot instantiate the
                // spawn message it receives: Hawk looks the id up in its own prefab registry and
                // logs "Prefab assetid: ... is not registered" instead. Doing this behind the
                // IsServer gate left every client without a voice channel, which the Steam build
                // hid because Steam P2P voice does not go through this object at all.
                if (!EnsurePrefab()) return;

                if (!HawkNetworkManager.DefaultInstance.IsServer()) return;

                NetworkPrefab.SpawnNetworkPrefab(_voiceManagerPrefab, Vector3.zero);
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError($"[VoiceChat] could not spawn the voice network manager: {e}");
            }
        }

        private static bool EnsurePrefab()
        {
            if (_voiceManagerPrefab != null) return true;

            _voiceManagerPrefab = NetworkPrefabHelper.CreateNetworkPrefab(
                "VoiceNetworkManager",
                typeof(VoiceNetworkManager),
                VoiceManagerAssetId);

            return _voiceManagerPrefab != null;
        }
    }
}
