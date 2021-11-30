using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace VirtualVoid.Net
{
    public class Client
    {
        internal Connection connection;

        private SteamId steamID;

        public SteamId SteamID
        {
            get
            {
                if (Authenticated) return steamID;
                else return default;
            }
            private set
            {
                steamID = value;
            }
        }
        public string SteamName
        {
            get
            {
                if (steamID.IsValid) return new Friend(steamID).Name;
                else return Authenticated ? "Invalid ID" : "Unauthenticated";
            }
        }
        public bool sceneLoaded { get; internal set; } = true;

        private bool destroyed = false;

        public GameObject CurrentPawn { get; set; }
        public VoiceOutput VoiceOutput { get; set; }
        //public VoiceOutput voiceOutput { get; private set; }

        // Auth stuff
        public bool Authenticated { get; internal set; }
        public float TimeCreated { get; private set; }
        internal bool connected;


        //internal static Client Create(Connection connection, SteamId steamIDFromConn)
        //{
        //    if (!SteamManager.IsServer)
        //    {
        //        Debug.LogWarning("Calling the server-side Client.Create method on the client!");
        //        return null;
        //    }
        //
        //
        //    Client client = new Client();
        //
        //    client.connection = connection;
        //    client.SteamID = steamIDFromConn;
        //    client.Authenticated = false;
        //    client.TimeCreated = Time.realtimeSinceStartup;
        //
        //    SteamManager.clientsPendingAuth[connection.Id] = client;
        //    return client;
        //}

        internal void OnCreate(Connection connection, SteamId steamIDFromConn)
        {
            if (!SteamManager.IsServer)
            {
                Debug.LogWarning("Calling the server-side Client.OnCreate method on the client!");
            }

            this.connection = connection;
            this.SteamID = steamIDFromConn;
            this.Authenticated = false;
            this.TimeCreated = Time.realtimeSinceStartup;

            SteamManager.clientsPendingAuth[connection.Id] = this;
        }

        internal void Destroy()
        {
            if (!destroyed)
            {
                destroyed = true;

                try
                {
                    OnDisconnect();
                }
                catch (System.Exception ex)
                {
                    Debug.Log("Error calling the OnDisconnect method. " + ex);
                }
                

                if (CurrentPawn != null)
                    UnityEngine.Object.Destroy(CurrentPawn);

                connection.Close();

                if (SteamManager.IsServer && Authenticated)
                    SteamUser.EndAuthSession(SteamID);

                if (SteamManager.clients.ContainsKey(SteamID)) SteamManager.clients.Remove(SteamID);
                if (SteamManager.connIDToSteamID.ContainsKey(connection.Id)) SteamManager.connIDToSteamID.Remove(connection.Id);
            }
        }


        internal void OnAuthorized(SteamId id)
        {
            if (!SteamManager.IsServer) return;

            steamID = id;
            Authenticated = true;

            if (SteamManager.clientsPendingAuth.ContainsKey(connection.Id))
                SteamManager.clientsPendingAuth.Remove(connection.Id);

            if (SteamManager.unverifiedSteamIDToConnID.ContainsKey(steamID))
                SteamManager.unverifiedSteamIDToConnID.Remove(steamID);

            SteamManager.clients[id] = this;
            SteamManager.connIDToSteamID[connection.Id] = id;

            OnConnect();
        }

        internal void OnSceneFinishedLoading()
        {
            //try
            //{
            //    foreach (NetworkID networkID in NetworkID.networkIDs.Values)
            //    {
            //        Debug.Log($"Spawned {networkID.name} on {SteamName}'s client");
            //        SteamManager.SpawnObject(networkID, SteamID);
            //    }
            //}
            //catch (System.Exception ex)
            //{
            //    Debug.Log($"Caught error spawning network objects for {SteamName}: {ex}");
            //}

            // Already spawn Network IDs when all clients' scenes are loaded

            OnSceneLoaded();
        }



        /// <summary>
        /// Called once this clients identity is authenticated. No default implementation.
        /// </summary>
        protected virtual void OnConnect() { }

        /// <summary>
        /// Called when this client disconnects. No default implementation.
        /// </summary>
        protected virtual void OnDisconnect() { }

        /// <summary>
        /// Called when this client has loaded a new scene. No default implementation.
        /// </summary>
        protected virtual void OnSceneLoaded() { }

        /// <summary>
        /// Use this method to fetch the spawn data added in SteamManager.AddSpawnData().
        /// </summary>
        /// <param name="message"></param>
        protected internal virtual void GetSpawnData(Message message) { }
    }
}
