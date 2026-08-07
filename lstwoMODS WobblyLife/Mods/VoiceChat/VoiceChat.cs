using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Steamworks;
using HawkNetworking;
using WLProxChat.Transport;

namespace WLProxChat
{
    /// <summary>
    /// Per-player voice: one of these rides along with each player object and plays that player's
    /// voice. Frames travel over the shared <see cref="VoiceTransport"/>, which drains the socket
    /// centrally and hands each instance only the frames from its own owner.
    /// </summary>
    public class VoiceChat : HawkNetworkBehaviour
    {
        public PlayerController player;

        private FmodVoiceStream voiceStream;

        private MemoryStream decompressedStream = new MemoryStream(1024 * 32);

        private bool running = true;
        private int sampleRate = 16000;

        private Transform playerBodyTransform;

        private bool isMuted = false;

        private int totalSamplesQueued;
        private string lastError = "";

        private byte RPC_INFORM_PLAYER;
        private byte RPC_DESTROY_OBJECT;

        private HawkConnection myConnection;
        private HawkConnection ownerConnection;

        /// <summary>Connection id whose frames this instance plays. -1 until the owner is known.</summary>
        private int ownerConnectionId = -1;

        public override void Start()
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.Start();

            // Pin the decode rate so the FMOD stream's frequency matches what DecompressVoice produces.
            SteamUser.SampleRate = SteamUser.OptimalSampleRate;
            sampleRate = (int)SteamUser.SampleRate;

            voiceStream = new FmodVoiceStream(gameObject.name, sampleRate);

            VoiceTransport.PacketReceived += OnVoicePacket;
            VoiceTransport.Refresh();

            running = true;
        }

        private void ClientInformPlayer(HawkNetReader reader, HawkRPCInfo info)
        {
            var networkId = reader.ReadUInt32();
            StartCoroutine(ClientInformPlayerRoutine(networkId));
        }

        private IEnumerator ClientInformPlayerRoutine(uint networkId)
        {
            yield return new WaitUntil(() =>
                GameInstance.InstanceExists && GameInstance.Instance.GetPlayerControllerByNetworkID(networkId));

            player = GameInstance.Instance.GetPlayerControllerByNetworkID(networkId);
            gameObject.name = "VoiceChatManager_" + player.GetPlayerName();
        }

        private IEnumerator WaitForOwner(HawkNetworkObject networkObject)
        {
            while (networkObject.GetOwner() == null)
                yield return null;

            ownerConnection = networkObject.GetOwner();
            ownerConnectionId = ownerConnection.Id;
        }

        private void ClientDestroyObject(HawkNetReader reader, HawkRPCInfo info)
        {
            Destroy(gameObject);
        }

        public override void RegisterRPCs(HawkNetworkObject networkObject)
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.RegisterRPCs(networkObject);

            RPC_INFORM_PLAYER = networkObject.RegisterRPC(ClientInformPlayer);
            RPC_DESTROY_OBJECT = networkObject.RegisterRPC(ClientDestroyObject);
        }

        public override void NetworkPost(HawkNetworkObject networkObject)
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            base.NetworkPost(networkObject);

            myConnection = networkObject.GetMe();
            ownerConnection = networkObject.GetOwner();

            if(ownerConnection != null)
                ownerConnectionId = ownerConnection.Id;

            if (networkObject.IsServer())
            {
                //print("setup owner shit");

                player = GameInstance.Instance.GetPlayerControllers()
                    .First(x => x.networkObject.GetOwner() == networkObject.GetOwner());
                //print(player);
                gameObject.name = "VoiceChatManager_" + player.GetPlayerName();

                networkObject.SendRPC(RPC_INFORM_PLAYER, RPCRecievers.Others, player.networkObject.GetNetworkID());

                player.onDestroy.AddCallback(behaviour =>
                {
                    networkObject.SendRPC(RPC_DESTROY_OBJECT, RPCRecievers.All);
                });

                HawkNetworkManager.DefaultInstance.onPlayerAccepted += connection =>
                {
                    //print("informing player");

                    networkObject.AssignOwnership(player.networkObject.GetOwner());
                    networkObject.SendRPC(RPC_INFORM_PLAYER, connection, player.networkObject.GetNetworkID());
                };
            }
            else
            {
                StartCoroutine(WaitForOwner(networkObject));
            }

            StartCoroutine(VoiceCaptureLoop());
        }

        private void Update()
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            voiceStream?.UpdateSpatial(transform.position, VoiceChatSettings.ForStream());
        }

        private void FixedUpdate()
        {
            if (gameObject.hideFlags == HideFlags.HideAndDontSave) return;

            if (playerBodyTransform == null && player?.GetPlayerCharacter()?.GetPlayerBody() != null)
            {
                playerBodyTransform = player.GetPlayerCharacter().GetPlayerBody().transform;
            }

            if (playerBodyTransform != null)
            {
                transform.position = playerBodyTransform.position;
            }
        }

        private IEnumerator VoiceCaptureLoop()
        {
            var wait = new WaitForSeconds(0.025f);

            //debug("voice capture loop method");

            while (running)
            {
                yield return wait;

                if (networkObject == null || !networkObject.IsOwner())
                {
                    continue;
                }

                SetSteamVoiceRecord();

                if (!SteamUser.HasVoiceData || !VoiceChatSettings.Enabled)
                {
                    continue;
                }

                OwnerSendVoiceData();
            }
        }

        /// <summary>
        /// A frame arrived from the shared transport. Only the ones sent by our own owner belong to
        /// this instance; every other player's copy of this behaviour claims the rest.
        /// </summary>
        private void OnVoicePacket(VoicePacket packet)
        {
            if (ownerConnectionId < 0 || packet.ConnectionId != ownerConnectionId)
                return;

            HandleVoicePacket(packet.Data, packet.Offset, packet.Length);
        }

        private void HandleVoicePacket(byte[] compressed, int offset, int length)
        {
            if (!VoiceChatSettings.Enabled || voiceStream == null || length <= 0)
                return;

            if (player?.networkObject?.IsOwner() == true && !VoiceChatSettings.HearYourself)
                return;

            try
            {
                hasReceivedData = true;

                // DecompressVoice consumes the whole array it is handed, so a framed payload (or a
                // shared receive buffer) has to be lifted into an exactly sized one first.
                if (offset != 0 || length != compressed.Length)
                {
                    var exact = new byte[length];
                    Buffer.BlockCopy(compressed, offset, exact, 0, length);
                    compressed = exact;
                }

                decompressedStream.Position = 0;
                decompressedStream.SetLength(0);

                int written = SteamUser.DecompressVoice(
                    compressed,
                    decompressedStream
                );

                lastDecompressedSize = written;
                lastCompressedSize = compressed.Length;

                if (written <= 0)
                    return;

                // GetBuffer avoids the per packet copy ToArray would make.
                voiceStream.Enqueue(decompressedStream.GetBuffer(), written);
                totalSamplesQueued += written / 2;
            }
            catch (Exception e)
            {
                lastError = e.Message;
                UnityEngine.Debug.LogWarning($"Voice receive error: {e}");
            }
        }

        private void OwnerSendVoiceData()
        {
            //debug("OwnerSendVoiceData");

            if (networkObject == null || !networkObject.IsOwner())
            {
                return;
            }

            //debug("check");

            try
            {
                var compressed = SteamUser.ReadVoiceDataBytes();
                //debug(compressed);

                if (compressed == null || compressed.Length == 0)
                {
                    return;
                }

                //debug(compressed.Length);

                //networkObject.SendRPCUnreliable(RPC_SEND_VOICE_DATA, RPCRecievers.All, compressed);

                // No transport delivers a frame back to its sender, so hearing yourself has to be a
                // local loopback rather than a round trip.
                if (VoiceChatSettings.HearYourself)
                    HandleVoicePacket(compressed, 0, compressed.Length);

                VoiceTransport.Broadcast(compressed, compressed.Length);

                //debug("sent rpc");
            }
            catch (Exception e)
            {
                lastError = e.Message;
                UnityEngine.Debug.LogError($"Error reading / sending voice data: {e.Message} {e.StackTrace}");
            }
        }

        private void SetSteamVoiceRecord()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                isMuted = !isMuted;
            }

            if (VoiceChatSettings.Enabled && !isMuted)
            {
                if (VoiceChatSettings.Mode == VoiceChatMode.Off)
                {
                    SteamUser.VoiceRecord = false;
                }
                else if (VoiceChatSettings.Mode == VoiceChatMode.PushToTalk)
                {
                    SteamUser.VoiceRecord = Input.GetKey(KeyCode.T);
                }
                else if (VoiceChatSettings.Mode == VoiceChatMode.AlwaysOn)
                {
                    SteamUser.VoiceRecord = true;
                }
            }
            else
            {
                SteamUser.VoiceRecord = false;
            }
        }

        /*private void RpcReceiveVoiceData(HawkNetReader reader, HawkRPCInfo info)
        {
            debug("RpcReceiveVoiceData");

            if (player.networkObject.IsOwner() && !VoiceChatSettings.HearYourself || !VoiceChatSettings.Enabled)
            {
                return;
            }

            try
            {
                hasReceivedData = true;

                var compressed = reader.ReadBytesAndSize().ToArray();
                debug(compressed);

                compressedStream.SetLength(0);
                compressedStream.Write(compressed, 0, compressed.Length);
                compressedStream.Position = 0;

                lastCompressedSize = compressed.Length;

                decompressedStream.SetLength(0);
                var written = SteamUser.DecompressVoice(compressed, decompressedStream);
                debug(written);

                lastDecompressedSize = written;

                if (written <= 0)
                {
                    return;
                }

                decompressedStream.Position = 0;
                var buffer = decompressedStream.ToArray();
                debug(buffer.Length);

                for (var i = 0; i < buffer.Length; i += 2)
                {
                    var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                    var f = sample / 32768f;
                    audioQueue.Enqueue(f);
                    totalSamplesQueued++;
                }

                bufferCountLastFrame = audioQueue.Count;
                debug("finished receive voice data");
            }
            catch (Exception e)
            {
                lastError = e.Message;
                UnityEngine.Debug.LogWarning($"Error receiving / decompressing voice data: {e.Message} {e.StackTrace}");
            }
        }*/

        public override void OnDestroy()
        {
            base.OnDestroy();

            running = false;
            SteamUser.VoiceRecord = false;

            VoiceTransport.PacketReceived -= OnVoicePacket;

            voiceStream?.Dispose();
            voiceStream = null;
        }

        private int lastCompressedSize = 0;
        private int lastDecompressedSize = 0;
        private bool hasReceivedData = false;

        private void OnGUI()
        {
            if (!VoiceChatSettings.ShowDebug) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 650), "WL Voice Chat Debug", GUI.skin.window);

            if (networkObject == null || !networkObject.IsOwner())
            {
                GUILayout.Label("Object not networked");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"[SteamUser.HasVoiceData]: {SteamUser.HasVoiceData}");
            GUILayout.Label($"[SteamUser.VoiceRecord]: {SteamUser.VoiceRecord}");
            GUILayout.Label($"Muted: {isMuted}");
            GUILayout.Label($"VoiceChat Enabled: {VoiceChatSettings.Enabled}");
            GUILayout.Label($"Mode: {VoiceChatSettings.Mode}");
            GUILayout.Space(10);

            GUILayout.Label($"[FMOD Stream] Valid: {voiceStream?.IsValid}");
            GUILayout.Label($"Volume: {VoiceChatSettings.Volume:0.00}, SpatialBlend: {VoiceChatSettings.SpatialBlend:0.00}");
            GUILayout.Label($"Stream Pos: {transform.position}");
            GUILayout.Space(10);

            GUILayout.Label($"Buffered: {voiceStream?.BufferedBytes ?? 0} bytes ({(voiceStream?.BufferedSeconds ?? 0f):0.000}s)");
            GUILayout.Label($"Samples Queued: {totalSamplesQueued}");
            GUILayout.Label($"Last Compressed Size: {lastCompressedSize} bytes");
            GUILayout.Label($"Last Decompressed Size: {lastDecompressedSize} bytes");
            GUILayout.Label($"Received Audio: {hasReceivedData}");

            GUILayout.Space(10);

            if (!string.IsNullOrEmpty(lastError))
            {
                GUILayout.Label($"<color=red>Last Error: {lastError}</color>");
            }

            GUILayout.EndArea();
        }

        private void debug(string message)
        {
            //print("[VC] [" + player.GetPlayerName() + "]" + message);
        }

        private void debug(object message)
        {
            debug(message?.ToString());
        }
    }
}
