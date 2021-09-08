using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace VirtualVoid.Networking.Steam
{
    [DisallowMultipleComponent]
    public class Client : NetworkBehaviour
    {
        [Header("Can leave null if not using built in voice chat.")]
        public VoiceOutput voiceOutput;

        /// <summary>
        /// Is this client the local client. Will not be assigned in Awake() or OnEnable().
        /// </summary>
        public bool IsLocalClient => SteamID == SteamManager.SteamID;
        /// <summary>
        /// The SteamID of this client. Will not be assigned in Awake() or OnEnable().
        /// </summary>
        public SteamId SteamID { get; private set; }
        public static Client LocalClient { get; private set; }
        public string SteamName { get; private set; }
        public bool sceneLoaded { get; internal set; } = true;
        public uint netID => networkID.netID;

        private bool destroyed = false;

        private new void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        protected internal sealed override void AddSpawnData(Message message)
        {
            message.Add(SteamID);

            try
            {
                _AddSpawnData(message);
            }
            catch
            {
                throw;
            }
        }

        protected virtual void _AddSpawnData(Message message)
        {

        }

        protected internal sealed override void GetSpawnData(Message message)
        {
            SteamID = message.GetSteamId();
            SteamName = new Friend(SteamID).Name;
            SteamManager.clients[SteamID] = this;
            if (IsLocalClient) LocalClient = this;

            try
            {
                _GetSpawnData(message);
            }
            catch
            {
                throw;
            }
        }

        protected virtual void _GetSpawnData(Message message)
        {

        }

        //private void Start()
        //{
        //    if (IsServer) return;
        //
        //    SendCommand(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.CLIENT_ID));
        //}



        internal static Client Create(SteamId steamID)
        {
            Client client;

            if (SteamManager.PlayerPrefab == null)
            {
                Debug.LogWarning("Please assign a proper PlayerPrefab to your SteamManager instance!");
                client = new GameObject("Temp Player").AddComponent<Client>();
            }
            else client = Instantiate(SteamManager.PlayerPrefab).GetComponent<Client>();

            client.SteamID = steamID;
            client.SteamName = new Friend(steamID).Name;
            if (client.IsLocalClient) LocalClient = client;

            //Client client = new Client { SteamID = steamID, Name = new Friend(steamID).Name };
            //client.SteamID = steamID;
            //client.SteamName = new Friend(steamID).Name;

            if (SteamManager.clients.ContainsKey(steamID)) SteamManager.clients[steamID]?.Destroy();

            SteamManager.clients[steamID] = client;
            return client;
        }

        public override void SendCommand(Message message)
        {
            InternalClientMessages.SendClientCommand(this, message);
        }

        protected internal sealed override void OnCommandReceived(SteamId from, Message message, ushort messageID)
        {
            if (messageID == (ushort)InternalClientMessageIDs.CLIENT_ID)
            {
                SendRPC(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.CLIENT_ID).Add(SteamID), from);
                return;
            }

            if (from != SteamID)
            {
                Debug.LogWarning("Player " + name + " received command from " + new Friend(from).Name + ", but players can only receive commands from themselves!");
                return;
            }

            OnCommandReceived(message, messageID);
        }

        protected internal override void OnRPCReceived(Message message, ushort messageID)
        {
            //if (messageID == (ushort)InternalServerMessageIDs.CLIENT_ID)
            //{
            //    SteamID = message.GetSteamId();
            //    SteamName = new Friend(SteamID).Name;
            //    SteamManager.clients[SteamID] = this;
            //    if (IsLocalPlayer) LocalClient = this;
            //    return;
            //}
        }

        protected internal virtual void OnCommandReceived(Message message, ushort messageID) { }

        internal void Destroy()
        {
            if (!destroyed)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            try
            {
                if (destroyed) return;
            
                if (SteamManager.clients.ContainsKey(SteamID)) SteamManager.clients.Remove(SteamID);
                destroyed = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error OnDestroy client! " + ex);
            }
        }
    }
}
