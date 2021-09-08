using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System.IO;

namespace VirtualVoid.Networking.Steam
{
    public class VoiceChatManager : MonoBehaviour
    {
        //[SerializeField]
        //private AudioSource source;

        [SerializeField]
        private GameObject microphoneRecordingIcon;

        [SerializeField]
        private bool debugMode = false;

        public KeyCode microphoneActivationKeycode = KeyCode.V;

        //private MemoryStream output;
        private MemoryStream stream;
        //private MemoryStream input;

        //private int optimalRate;
        //private int clipBufferSize;
        //private float[] clipBuffer;
        //
        //private int playbackBuffer;
        //private int dataPosition;
        //private int dataReceived;

        private void Start()
        {
            stream = new MemoryStream();
        }

        private void OnEnable()
        {
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.SEND_VOICE, OnClientVoice);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.BOUNCE_VOICE, OnServerVoice);
        }

        private void OnDisable()
        {
            SteamManager.DeregisterInternalMessageHandler_FromClient(InternalClientMessageIDs.SEND_VOICE, OnClientVoice);
            SteamManager.DeregisterInternalMessageHandler_FromServer(InternalServerMessageIDs.BOUNCE_VOICE, OnServerVoice);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) && debugMode) SteamManager.HostServer();

            microphoneRecordingIcon?.SetActive(Input.GetKey(microphoneActivationKeycode));

            SteamUser.VoiceRecord = Input.GetKey(microphoneActivationKeycode);

            if (SteamUser.HasVoiceData)
            {
                int compressedWritten = SteamUser.ReadVoiceData(stream);
                stream.Position = 0;

                //Debug.Log($"Sent {compressedWritten} bytes of voice data to server");
                SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Unreliable, (ushort)InternalClientMessageIDs.SEND_VOICE).Add(compressedWritten).Add(stream.GetBuffer()));
            }

        }

        private void OnClientVoice(SteamId client, Message message)
        {
            if (!SteamManager.IsServer) return;

            int bytesWritten = message.GetInt();
            byte[] compressed = message.GetByteArray(bytesWritten);
            Message sendMessage = Message.CreateInternal(P2PSend.Unreliable, (ushort)InternalServerMessageIDs.BOUNCE_VOICE);

            if (debugMode)
                SteamManager.SendMessageToAllClients(sendMessage.Add(SteamManager.clients[client].networkID).Add(bytesWritten).Add(compressed));
            else 
                SteamManager.SendMessageToAllClients(client, sendMessage.Add(SteamManager.clients[client].networkID).Add(bytesWritten).Add(compressed));
        }

        private void OnServerVoice(Message message)
        {
            //SteamId clientID = message.GetSteamId();
            NetworkID networkID = message.GetNetworkID();
            if (networkID == null)
            {
                Debug.LogWarning("Received voice data for a player, but could not get their NetworkID!");
                return;
            }
            if (!networkID.TryGetComponent(out Client client))
            {
                Debug.LogWarning("Could not get Client attached to NetworkID of ID " + networkID.netID);
                return;
            }

            int bytesWritten = message.GetInt();
            byte[] compressed = message.GetByteArray(bytesWritten);

            if (client.voiceOutput == null)
            {
                Debug.LogWarning($"Tried to handle voice data for {client.SteamName}, but their VoiceOutput was null!");
                return;
            }

            client.voiceOutput.OnDataReceived(bytesWritten, compressed);
        }
    }
}
