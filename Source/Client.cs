using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace VirtualVoid.Networking.Steam
{
    public class Client : NetworkBehavior
    {
        /// <summary>
        /// Is this client the local client. Will not be assigned in Awake() or OnEnable().
        /// </summary>
        public bool IsLocalPlayer => SteamID == SteamManager.SteamID;
        /// <summary>
        /// The SteamID of this client. Will not be assigned in Awake() or OnEnable().
        /// </summary>
        public SteamId SteamID { get; private set; }
        public string SteamName { get; private set; }
        public bool sceneLoaded { get; internal set; } = true;

        private bool destroyed = false;

        internal static Client Create(SteamId steamID)
        {
            Client client;

            if (SteamManager.PlayerPrefab == null)
            {
                Debug.LogError("Please assign a proper PlayerPrefab to your SteamManager instance!");
                client = new GameObject("Temp Player").AddComponent<Client>();
            }
            else client = Instantiate(SteamManager.PlayerPrefab).GetComponent<Client>();

            client.SteamID = steamID;
            client.SteamName = new Friend(steamID).Name;

            //Client client = new Client { SteamID = steamID, Name = new Friend(steamID).Name };
            //client.SteamID = steamID;
            //client.SteamName = new Friend(steamID).Name;

            if (SteamManager.clients.ContainsKey(steamID)) SteamManager.clients[steamID]?.Destroy();

            SteamManager.clients[steamID] = client;
            return client;
        }

        protected internal sealed override void OnCommandReceived(SteamId from, Message message, ushort messageID)
        {
            if (from != SteamID)
            {
                Debug.LogWarning("Player " + name + " received command from " + new Friend(from).Name + ", but players can only receive commands from themselves!");
                return;
            }

            OnCommandReceived(message, messageID);
        }

        protected virtual void OnCommandReceived(Message message, ushort messageID) { }

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
