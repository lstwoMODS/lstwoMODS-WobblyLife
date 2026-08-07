using System;
using HawkNetworking;
using UnityEngine;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Owns the active <see cref="IVoiceTransport"/> and pumps it.
    /// <para>
    /// The pump lives here rather than in the voice chat behaviours because received frames must be
    /// drained exactly once: two components polling the same socket would steal each other's packets.
    /// Consumers subscribe to <see cref="PacketReceived"/> instead, which always fires on the game
    /// thread, and filter by connection id.
    /// </para>
    /// </summary>
    public static class VoiceTransport
    {
        /// <summary>Fires per received frame, on the game thread. Buffer is only valid for the call.</summary>
        public static event Action<VoicePacket> PacketReceived;

        private static IVoiceTransport current;
        private static VoiceTransportMode activeMode = VoiceTransportMode.None;
        private static Driver driver;

        public static IVoiceTransport Current => current;

        public static bool IsReady => current != null && current.IsReady;

        public static string Status => current == null
            ? "no transport"
            : $"{current.Name}: {current.Status}";

        /// <summary>Which transport is running.</summary>
        public static VoiceTransportMode ActiveMode => activeMode;

        /// <summary>
        /// True once a direct transport is live, at which point the voice port is fixed for the
        /// lifetime of the lobby: it was captured when the socket was bound and there is nothing
        /// left to change. Closing the lobby tears the transport down and releases it.
        /// </summary>
        public static bool PortLocked => current is DirectVoiceTransport;

        /// <summary>Port in use, or 0 when the running transport does not have one.</summary>
        public static int ActivePort => current is DirectVoiceTransport direct ? direct.BoundPort : 0;

        /// <summary>Send a compressed frame to every other player.</summary>
        public static void Broadcast(byte[] data, int length)
        {
            if (length > 0)
                current?.Broadcast(data, length);
        }

        /// <summary>
        /// Bring the transport in line with the current network manager and settings. Cheap to call
        /// repeatedly: it only does work when the answer changes.
        /// </summary>
        public static void Refresh()
        {
            // Unconditionally, and before the early-out: the driver is what re-resolves once the
            // game comes online. Creating it only alongside a transport meant that resolving to
            // None first (the normal case, at the main menu) left nothing to ever try again.
            EnsureDriver();

            var wanted = Resolve();

            if (wanted == activeMode && (wanted == VoiceTransportMode.None || current != null))
                return;

            Shutdown();

            switch (wanted)
            {
                case VoiceTransportMode.Steam:
                    current = new SteamVoiceTransport();
                    break;

                case VoiceTransportMode.Direct:
                    current = CreateDirect();
                    break;
            }

            activeMode = current == null ? VoiceTransportMode.None : wanted;

            if (current != null)
                Plugin.LogSource.LogInfo($"[VoiceChat] transport is now {current.Name}");
        }

        /// <summary>Tear the transport down, e.g. on disconnect.</summary>
        public static void Shutdown()
        {
            current?.Dispose();
            current = null;
            activeMode = VoiceTransportMode.None;
        }

        /// <summary>
        /// Which transport the game's own network manager calls for. Not a user choice: voice has to
        /// travel the same way the game does, so whichever transport the LAN Multiplayer mod put the
        /// game on decides this too.
        /// </summary>
        private static VoiceTransportMode Resolve()
        {
            // InstanceExists first: DefaultInstance builds the network singleton on demand, which
            // would pin the transport type before the LAN mod has chosen it.
            if (!HawkNetworkManager.InstanceExists)
                return VoiceTransportMode.None;

            var manager = HawkNetworkManager.DefaultInstance;

            if (manager == null || manager.IsOffline())
                return VoiceTransportMode.None;

            // The manager exists from the main menu onwards, long before it holds a socket or a
            // lobby. Standing a transport up that early gives it nothing to poll but the underlying
            // Steam interface is not up either, so it throws once a frame instead of idling.
            if (!manager.IsConnected())
                return VoiceTransportMode.None;

            if (manager is SteamP2PNetworkManager)
                return VoiceTransportMode.Steam;

            if (manager is LiteNetworkManager)
                return VoiceTransportMode.Direct;

            return VoiceTransportMode.None;
        }

        private static IVoiceTransport CreateDirect()
        {
            if (!HawkNetworkManager.InstanceExists) return null;

            var manager = HawkNetworkManager.DefaultInstance;
            if (manager == null) return null;

            var direct = new DirectVoiceTransport
            {
                LocalConnectionId = manager.GetMe()?.Id ?? -1
            };

            var started = manager.IsServer()
                ? direct.StartHost(VoiceChatSettings.DirectVoicePort)
                : direct.StartClient();

            if (!started)
            {
                direct.Dispose();
                return null;
            }

            // The host has to publish where it is listening; clients wait to be told. Both sides of
            // that conversation live in VoiceNetworkManager, which may not have spawned yet.
            VoiceNetworkManager.Instance?.OnTransportReady(direct);

            return direct;
        }

        private static void EnsureDriver()
        {
            if (driver != null) return;

            var go = new GameObject("lstwoMODS_VoiceTransport") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);

            driver = go.AddComponent<Driver>();
        }

        /// <summary>The single owner of the receive pump.</summary>
        private class Driver : MonoBehaviour
        {
            private float nextRefresh;

            private void Update()
            {
                // The network manager can be swapped out from under us (join, host, disconnect), and
                // there is no event for it, so re-resolve on a slow timer.
                if (Time.realtimeSinceStartup >= nextRefresh)
                {
                    nextRefresh = Time.realtimeSinceStartup + 1f;
                    Refresh();
                }

                var transport = current;
                if (transport == null) return;

                transport.Tick();

                while (transport.TryReceive(out var packet))
                {
                    try
                    {
                        PacketReceived?.Invoke(packet);
                    }
                    catch (Exception e)
                    {
                        Plugin.LogSource.LogError($"[VoiceChat] voice packet handler threw: {e}");
                    }
                }
            }

            private void OnDestroy()
            {
                if (driver == this) driver = null;
            }
        }
    }

    /// <summary>The transport actually running.</summary>
    public enum VoiceTransportMode
    {
        None,
        Steam,
        Direct
    }
}
