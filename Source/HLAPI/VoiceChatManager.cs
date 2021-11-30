using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.IO;

namespace VirtualVoid.Net
{
    public class VoiceChatManager : MonoBehaviour
    {
        //[SerializeField]
        //private AudioSource source;

        [SerializeField]
        private GameObject microphoneRecordingIcon;

        [SerializeField]
        private bool debugCanHearSelf = false;

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
            if (microphoneRecordingIcon != null)
                microphoneRecordingIcon.SetActive(Input.GetKey(microphoneActivationKeycode));

            SteamUser.VoiceRecord = Input.GetKey(microphoneActivationKeycode) &&
                (SteamManager.IsServer || SteamManager.ConnectedToServer);

            if (SteamUser.HasVoiceData)
            {
                int compressedWritten = SteamUser.ReadVoiceData(stream);
                stream.Position = 0;

                //Debug.Log($"Sent {compressedWritten} bytes of voice data to server");
                SteamManager.SendMessageToServer(Message.CreateInternal(SendType.Unreliable | SendType.NoDelay | SendType.NoNagle, (ushort)InternalClientMessageIDs.SEND_VOICE).Add(compressedWritten).Add(stream.GetBuffer()));
            }

        }

        private void OnClientVoice(SteamId client, Message message)
        {
            if (!SteamManager.IsServer) return;

            int bytesWritten = message.GetInt();
            byte[] compressed = message.GetByteArray(bytesWritten);
            Message sendMessage = Message.CreateInternal(SendType.Unreliable | SendType.NoDelay | SendType.NoNagle, (ushort)InternalServerMessageIDs.BOUNCE_VOICE);

            if (debugCanHearSelf)
                SteamManager.SendMessageToAllClients(sendMessage.Add(SteamManager.clients[client].SteamID).Add(bytesWritten).Add(compressed));
            else 
                SteamManager.SendMessageToAllClients(client, sendMessage.Add(SteamManager.clients[client].SteamID).Add(bytesWritten).Add(compressed));
        }

        private void OnServerVoice(Message message)
        {
            SteamId clientID = message.GetSteamId();

            //NetworkID networkID = message.GetNetworkID();
            //if (networkID == null)
            //{
            //    Debug.LogWarning("Received voice data for a player, but could not get their NetworkID!");
            //    return;
            //}
            //if (!networkID.TryGetComponent(out Client client))
            //{
            //    Debug.LogWarning("Could not get Client attached to NetworkID of ID " + networkID.netID);
            //    return;
            //}

            if (!SteamManager.clients.TryGetValue(clientID, out Client client))
            {
                Debug.Log("Could not get client for " + new Friend(clientID).Name);
                return;
            }

            if (client.VoiceOutput == null)
            {
                Debug.LogWarning($"Tried to handle voice data for {client.SteamName}, but their VoiceOutput was null!");
                return;
            }

            int bytesWritten = message.GetInt();
            byte[] compressed = message.GetByteArray(bytesWritten);

            client.VoiceOutput.OnDataReceived(bytesWritten, compressed);
        }
    }
}
