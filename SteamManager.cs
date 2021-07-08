using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using VirtualVoid.Networking.Steam.LLAPI;
using System.Threading.Tasks;

namespace VirtualVoid.Networking.Steam
{
    public class SteamManager : MonoBehaviour
    {
        // Singleton
        public static SteamManager instance;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(this);
                return;
            }

            SteamClient.Init(appId, false);
            SteamNetworkingUtils.InitRelayNetworkAccess();

            InitSteamEvents();
            InternalMessages.Initialize();

            if (tickRate > 0)
            {
                Time.fixedDeltaTime = 1f / tickRate;
                Debug.Log($"Set physics tickrate to {tickRate} ticks per second ({Time.fixedDeltaTime}s per physics update).");
            }
        }

        // Inspector Stuff
        [Header("The steam app id of your app.")]
        public uint appId = 480;

        [Header("Disconnects clients if their Application.version is different from the servers.")]
        public bool disconnectClientsFromDifferentVersion = true;

        [Header("The maximum number of players who can join at once.")]
        public int maxPlayers = 4;

        [Header("Sets the fixed update rate. Set to 0 to keep as it is.")]
        [Range(0, 128)]
        public int tickRate;

        // Static Members
        public static Lobby CurrentLobby { get; private set; }
        public static SteamId SteamID => SteamClient.SteamId;

        public static SteamId ServerID { get; private set; }
        public static bool IsServer => SteamID == ServerID;

        //private static SteamSocketServer currentServer;
        //private static SteamSocketConnection currentConnection;

        // Events
        public static event Action<Lobby, bool> OnLobbyCreated; // Server
        public static event Action<Lobby> OnLobbyJoined; // Both
        public static event Action<Lobby, Friend> OnLobbyMemberJoined; // Both
        public static event Action<Lobby> OnLobbyLeft; // Client
        public static event Action<Lobby, Friend> OnLobbyMemberLeave; // Both

        public static event Action<SteamId> OnClientDisconnect; // Both

        public static event Action<SteamId> OnClientSceneLoaded; // Server
        public static event Action OnAllClientsSceneLoaded; // Server

        public static event ClientMessageCallback OnMessageFromClient;
        public static event ServerMessageCallback OnMessageFromServer;

        internal static event ClientMessageCallback OnInternalMessageFromClient;
        internal static event ServerMessageCallback OnInternalMessageFromServer;

        // Delegates
        public delegate void ClientMessageCallback(ushort messageID, SteamId clientSteamID, Message message);
        public delegate void ServerMessageCallback(ushort messageID, Message message);

        // Dictionaries
        public static readonly Dictionary<SteamId, ConnectedClient> clients = new Dictionary<SteamId, ConnectedClient>();

        private static readonly Dictionary<ushort, ClientMessageCallback> messagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, ServerMessageCallback> messagesFromServerCallbacks = new Dictionary<ushort, ServerMessageCallback>();

        private static readonly Dictionary<ushort, ClientMessageCallback> internalMessagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, ServerMessageCallback> internalMessagesFromServerCallbacks = new Dictionary<ushort, ServerMessageCallback>();


        // Constants
        private const string LOBBY_SERVER_VERSION = "server_version";
        
        private void InitSteamEvents()
        {
            SteamFriends.OnGameLobbyJoinRequested += SteamFriends_OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;
        }


        #region Steam Callbacks

        private void SteamMatchmaking_OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            if (IsServer)
            {
                if (!clients.ContainsKey(friend.Id))
                {
                    clients.Add(friend.Id, new ConnectedClient(friend.Id));
                }
                else
                {
                    clients[friend.Id] = new ConnectedClient(friend.Id);
                }
            }

            OnLobbyMemberJoined?.Invoke(lobby, friend);
        }

        private void SteamMatchmaking_OnLobbyEntered(Lobby lobby)
        {
            if (IsServer)
            {
                clients.Add(SteamID, new ConnectedClient(SteamID));
                OnLobbyJoined?.Invoke(lobby);
                return;
            }

            string version = Application.version;
            string data = lobby.GetData(LOBBY_SERVER_VERSION);
            if (version != data && disconnectClientsFromDifferentVersion)
            {
                Debug.Log($"Current version is {version}, but server version is {data}! Please update game...");
                LeaveCurrentLobby();
                return;
            }

            OnLobbyJoined?.Invoke(lobby);
            AcceptP2P(ServerID);
        }

        private void SteamFriends_OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
        {
            CurrentLobby.Leave();

            lobby.Join();
            CurrentLobby = lobby;
            ServerID = CurrentLobby.Owner.Id;
            AcceptP2P(ServerID);
        }

        private void SteamMatchmaking_OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            if (friend.Id == SteamID)
            {
                OnLobbyLeft?.Invoke(lobby);
            }

            if (IsServer)
            {
                if (friend.Id != SteamID)
                {
                    if (clients.ContainsKey(friend.Id))
                    {
                        clients.Remove(friend.Id);
                    }
                    else
                    {
                        Debug.LogWarning("Client " + friend.Id + " left the lobby, but they were never added to the Clients dictionary!");
                    }
                }
                else
                {
                    LeaveServer();
                }
            }

            OnLobbyMemberLeave?.Invoke(lobby, friend);
        }

        #endregion

        #region Unity Functions
        private void Update()
        {
            SteamClient.RunCallbacks();

            while (SteamNetworking.IsP2PPacketAvailable((int)P2PMessageChannels.CLIENT))
            {
                P2Packet? packet = SteamNetworking.ReadP2PPacket((int)P2PMessageChannels.CLIENT);
                if (packet.HasValue)
                {
                    HandleDataFromServer(Message.Create(P2PSend.Unreliable, packet.Value.Data), packet.Value.SteamId);
                }
            }

            if (IsServer)
            {
                while (SteamNetworking.IsP2PPacketAvailable((int)P2PMessageChannels.SERVER))
                {
                    P2Packet? packet = SteamNetworking.ReadP2PPacket((int)P2PMessageChannels.SERVER);
                    if (packet.HasValue)
                    {
                        HandleDataFromClient(Message.Create(P2PSend.Unreliable, packet.Value.Data), packet.Value.SteamId);
                    }
                }
            }

            //try
            //{
            //    if (currentServer != null)
            //    {
            //        currentServer.Receive();
            //    }
            //    if (currentConnection != null)
            //    {
            //        currentConnection.Receive();
            //    }
            //}
            //catch
            //{
            //    UnityEngine.Debug.Log("Error receiving data on socket/connection");
            //}
        }

        private void OnDisable()
        {
            LeaveCurrentLobby();
            LeaveServer();
            SteamClient.Shutdown();
        }
        #endregion

        #region Lobby
        public static async Task<bool> CreateLobby(int maxPlayers = 4, SteamLobbyPrivacyMode mode = SteamLobbyPrivacyMode.FRIENDS_ONLY, bool joinable = true)
        {
            Lobby? lobby =  await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (lobby.HasValue)
            {
                CurrentLobby = lobby.Value;
                //ServerID = CurrentLobby.Owner.Id;
                ServerID = SteamID;
                SetLobbyPrivacyMode(CurrentLobby, mode);
                CurrentLobby.SetJoinable(joinable);
                CurrentLobby.SetData(LOBBY_SERVER_VERSION, Application.version);

                OnLobbyCreated?.Invoke(CurrentLobby, true);

                if (await CurrentLobby.Join() != RoomEnter.Success)
                {
                    Debug.Log("Error joining lobby!");
                }

                return true;
            }
            else
            {
                Debug.LogError("Lobby created but returned null value!");

                OnLobbyCreated?.Invoke(CurrentLobby, false);
                throw new NullReferenceException();
            }
        }

        public static void SetLobbyPrivacyMode(Lobby lobby, SteamLobbyPrivacyMode mode)
        {
            switch (mode)
            {
                case SteamLobbyPrivacyMode.PUBLIC:
                    lobby.SetPublic();
                    break;
                case SteamLobbyPrivacyMode.PRIVATE:
                    lobby.SetPrivate();
                    break;
                case SteamLobbyPrivacyMode.INVISIBLE:
                    lobby.SetInvisible();
                    break;
                case SteamLobbyPrivacyMode.FRIENDS_ONLY:
                    lobby.SetFriendsOnly();
                    break;
                default:
                    break;
            }
        }

        private static void LeaveCurrentLobby()
        {
            CurrentLobby.Leave();
            OnLobbyLeft?.Invoke(CurrentLobby);
        }
        #endregion

        #region Server
        public static async void HostServer(SteamLobbyPrivacyMode mode = SteamLobbyPrivacyMode.FRIENDS_ONLY, bool joinable = true)
        {
            if (!await CreateLobby(instance.maxPlayers, mode, joinable))
            {
                Debug.Log("Error starting server: Lobby not created successfully.");
                return;
            }

            SteamNetworking.AllowP2PPacketRelay(true);
        }

        private static void StopServer()
        {
            if (!IsServer) return;

            KickAllClients();
        }

        private static void KickAllClients()
        {
            if (IsServer)
            {
                List<SteamId> clientIDs = new List<SteamId>();

                foreach (ConnectedClient client in clients.Values)
                {
                    clientIDs.Add(client.steamId);
                }

                for (int i = 0; i < clientIDs.Count; i++)
                {
                    DisconnectClient(clientIDs[i]);
                }
            }

            clients.Clear();
        }

        public static void LeaveServer()
        {
            if (IsServer)
                StopServer();

            InternalClientMessages.SendDisconnect();
            SteamNetworking.CloseP2PSessionWithUser(ServerID);
            LeaveCurrentLobby();
            ResetValues();
        }

        #endregion

        #region Socket Server Methods (Obsolete)

        //public static void ProcessServerMessage(IntPtr data, int size)
        //{
        //    try
        //    {
        //        byte[] message = new byte[size];
        //        System.Runtime.InteropServices.Marshal.Copy(data, message, 0, size);
        //
        //        // Do something with received message
        //
        //        string messageString = System.Text.Encoding.UTF8.GetString(message);
        //        Debug.Log("Received message from server: " + messageString);
        //    }
        //    catch
        //    {
        //        Debug.Log("Unable to process message from socket server");
        //    }
        //}
        //
        //public static void ProcessConnectionMessage(IntPtr data, int size, uint connectionID)
        //{
        //    try
        //    {
        //        byte[] message = new byte[size];
        //        System.Runtime.InteropServices.Marshal.Copy(data, message, 0, size);
        //
        //        // Do something with received message
        //
        //        string messageString = System.Text.Encoding.UTF8.GetString(message);
        //        Debug.Log("Received message from client: " + messageString + ". Sending to all other clients...");
        //    }
        //    catch
        //    {
        //        Debug.Log("Unable to process message from connection " + connectionID);
        //    }
        //
        //    // Send it back to all other connections
        //
        //    try
        //    {
        //        for (int i = 0; i < currentServer.Connected.Count; i++)
        //        {
        //            if (currentServer.Connected[i].Id != connectionID)
        //            {
        //                Result success = currentServer.Connected[i].SendMessage(data, size);
        //                if (success != Result.OK)
        //                {
        //                    Result retry = currentServer.Connected[i].SendMessage(data, size);
        //                }
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        Debug.Log("Unable to relay socket server message.");
        //    }
        //}
        //
        //
        //public static bool SendMessageToServer(Message message)
        //{
        //    if (currentConnection == null)
        //    {
        //        Debug.LogError("Tried to send message to server, but no connection exists! Did you call JoinServer()?");
        //    }
        //
        //    try
        //    {
        //        // Convert byte[] message into IntPtr data type for efficient message send / garbage management
        //        int sizeOfMessage = message.WrittenLength;
        //        IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
        //        System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);
        //
        //        Result success = currentConnection.Connection.SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //        if (success == Result.OK)
        //        {
        //            System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
        //            return true;
        //        }
        //        else
        //        {
        //            // RETRY
        //            Result retry = currentConnection.Connection.SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //            System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
        //            if (retry == Result.OK)
        //            {
        //                return true;
        //            }
        //            return false;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.Log(e.Message);
        //        Debug.Log("Unable to send message to socket server");
        //        return false;
        //    }
        //}
        //
        //public static bool SendMessageToAllClients(Message message)
        //{
        //    if (currentServer == null)
        //    {
        //        Debug.LogError("Tried to send message to all clients, but no server exists! Did you call HostServer()?");
        //    }
        //
        //    try
        //    {
        //        // Convert byte[] message into IntPtr data type for efficient message send / garbage management
        //        int sizeOfMessage = message.WrittenLength;
        //        IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
        //        System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);
        //
        //        for (int i = 0; i < currentServer.Connected.Count; i++)
        //        {
        //            Result success = currentServer.Connected[i].SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //            if (success != Result.OK)
        //            {
        //                Result retry = currentServer.Connected[i].SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //                if (retry != Result.OK)
        //                {
        //                    Debug.LogWarning("Could not send message to connection " + currentServer.Connected[i].Id + " after 2 attempts.");
        //                }
        //            }
        //        }
        //
        //        System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
        //        return true;
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.Log(e.Message);
        //        Debug.Log("Unable to send message to all clients.");
        //        return false;
        //    }
        //}
        //
        //public static bool SendMessageToAllClients(Message message, uint exceptClient)
        //{
        //    if (currentServer == null)
        //    {
        //        Debug.LogError("Tried to send message to all clients, but no server exists! Did you call HostServer()?");
        //    }
        //
        //    try
        //    {
        //        // Convert byte[] message into IntPtr data type for efficient message send / garbage management
        //        int sizeOfMessage = message.WrittenLength;
        //        IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
        //        System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);
        //
        //        for (int i = 0; i < currentServer.Connected.Count; i++)
        //        {
        //            if (currentServer.Connected[i].Id == exceptClient) continue;
        //
        //            Result success = currentServer.Connected[i].SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //            if (success != Result.OK)
        //            {
        //                Result retry = currentServer.Connected[i].SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
        //                if (retry != Result.OK)
        //                {
        //                    Debug.LogWarning("Could not send message to connection " + currentServer.Connected[i].Id + " after 2 attempts.");
        //                }
        //            }
        //        }
        //
        //        System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
        //        return true;
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.Log(e.Message);
        //        Debug.Log("Unable to send message to all clients.");
        //        return false;
        //    }
        //}

        #endregion

        #region Send
        public static void SendMessageToServer(Message message)
        {
            if (!IsServer)
            {
                NetStats.OnPacketSent(message.WrittenLength);
                SteamNetworking.SendP2PPacket(ServerID, message.Bytes, message.WrittenLength, (int)P2PMessageChannels.SERVER, message.SendType);
            }
            else
            {
                byte[] truncatedBytes = new byte[message.WrittenLength];
                Array.Copy(message.Bytes, 0, truncatedBytes, 0, message.WrittenLength);
                HandleDataFromClient(Message.Create(P2PSend.Unreliable, truncatedBytes), SteamID);
            }
        }

        public static void SendMessageToClient(SteamId id, Message message)
        {
            if (!IsServer)
            {
                Debug.LogWarning("Tried to send message to client " + id + ", but this client is not the server!");
                return;
            }

            if (!clients.ContainsKey(id) && id != SteamID)
            {
                Debug.LogWarning("Tried to send message to client " + id + " but Clients dictionary does not contain that ID!");
                return;
            }

            if (id != SteamID)
            {
                NetStats.OnPacketSent(message.WrittenLength);
                SteamNetworking.SendP2PPacket(id, message.Bytes, message.WrittenLength, (int)P2PMessageChannels.CLIENT, message.SendType);
            }
            else
            {
                byte[] truncatedBytes = new byte[message.WrittenLength];
                Array.Copy(message.Bytes, 0, truncatedBytes, 0, message.WrittenLength);
                HandleDataFromServer(Message.Create(P2PSend.Unreliable, truncatedBytes), SteamID);
            }
        }

        public static void SendMessageToAllClients(Message message)
        {
            foreach (ConnectedClient client in clients.Values)
            {
                SendMessageToClient(client.steamId, message);
            }
        }

        public static void SendMessageToAllClients(SteamId except, Message message)
        {
            foreach (ConnectedClient client in clients.Values)
            {
                if (client.steamId != except)
                    SendMessageToClient(client.steamId, message);
            }
        }
        #endregion

        #region Handle
        private static void HandleDataFromServer(Message message, SteamId fromId)
        {
            if (fromId != ServerID)
            {
                Debug.LogWarning($"Received packet from {fromId}, but current server ID is set to {ServerID}!");
                return;
            }

            if (fromId != SteamID)
                NetStats.OnPacketReceived(message.WrittenLength);

            ushort id = message.GetUShort();

            if (Message.IsInternalMessage(id))
            {
                OnInternalMessageFromServer?.Invoke(id, message);
                if (internalMessagesFromServerCallbacks.ContainsKey(id))
                {
                    internalMessagesFromServerCallbacks[id]?.Invoke(id, message);
                }
            }
            else
            {
                OnMessageFromServer?.Invoke(id, message);
                if (messagesFromServerCallbacks.ContainsKey(id))
                {
                    messagesFromServerCallbacks[id]?.Invoke(id, message);
                }
            }
        }

        private static void HandleDataFromClient(Message message, SteamId fromClient)
        {
            // check if steam ID is in clients dict

            if (fromClient != SteamID)
                NetStats.OnPacketReceived(message.WrittenLength);

            ushort id = message.GetUShort();

            if (Message.IsInternalMessage(id))
            {
                OnInternalMessageFromClient?.Invoke(id, fromClient, message);
                if (internalMessagesFromClientCallbacks.ContainsKey(id))
                {
                    internalMessagesFromClientCallbacks[id]?.Invoke(id, fromClient, message);
                }
            }
            else
            {
                OnMessageFromClient?.Invoke(id, fromClient, message);
                if (messagesFromClientCallbacks.ContainsKey(id))
                {
                    messagesFromClientCallbacks[id]?.Invoke(id, fromClient, message);
                }
            }
        }
        #endregion

        #region Message Callback Registration

        #region Normal Messages
        /// <summary>
        /// Invokes the <paramref name="callback"/> on the server when a message from a client of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnMessageFromClient to get notified when any message is received, not only a message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when a message with ID <paramref name="messageID"/> is received.</param>
        public static void RegisterMessageHandler_FromClient(ushort messageID, ClientMessageCallback callback)
        {
            if (Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to register a ClientMessageCallback with ID of {messageID}, but that ID is used internally!");
                return;
            }

            if (!messagesFromClientCallbacks.ContainsKey(messageID)) messagesFromClientCallbacks.Add(messageID, callback);

            messagesFromClientCallbacks[messageID] += callback;
        }

        /// <summary>
        /// Invokes the <paramref name="callback"/> on the client when a message from the server of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnMessageFromServer to get notified when any message is received, not only a message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when a message with ID <paramref name="messageID"/> is received.</param>
        public static void RegisterMessageHandler_FromServer(ushort messageID, ServerMessageCallback callback)
        {
            if (Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to register a ServerMessageCallback with ID of {messageID}, but that ID is used internally!");
                return;
            }

            if (!messagesFromServerCallbacks.ContainsKey(messageID)) messagesFromServerCallbacks.Add(messageID, callback);

            messagesFromServerCallbacks[messageID] += callback;
        }
        #endregion

        #region Internal messages
        /// <summary>
        /// Invokes the <paramref name="callback"/> on the server when an internal message from a client of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnInternalMessageFromClient to get notified when any internal message is received, not only an internal message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when an internal message with ID <paramref name="messageID"/> is received.</param>
        public static void RegisterInternalMessageHandler_FromClient(ushort messageID, ClientMessageCallback callback)
        {
            if (!Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to register an internal ClientMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (!internalMessagesFromClientCallbacks.ContainsKey(messageID))
                internalMessagesFromClientCallbacks.Add(messageID, callback);

            internalMessagesFromClientCallbacks[messageID] += callback;
        }

        /// <summary>
        /// Invokes the <paramref name="callback"/> on the client when an internal message from the server of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnInternalMessageFromServer to get notified when any internal message is received, not only an internal message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when an internal message with ID <paramref name="messageID"/> is received.</param>
        public static void RegisterInternalMessageHandler_FromServer(ushort messageID, ServerMessageCallback callback)
        {
            if (!Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to register an internal ServerMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (!internalMessagesFromServerCallbacks.ContainsKey(messageID)) internalMessagesFromServerCallbacks.Add(messageID, callback);

            internalMessagesFromServerCallbacks[messageID] += callback;
        }
        #endregion

        #endregion

        #region Message Callback Deregistration

        #region Normal Messages
        public static void DeregisterMessageHandler_FromClient(ushort messageID, ClientMessageCallback callback)
        {
            if (Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to deregister a ClientMessageCallback with ID of {messageID}, but that ID is used internally!");
                return;
            }

            if (messagesFromClientCallbacks.ContainsKey(messageID)) messagesFromClientCallbacks[messageID] -= callback;
        }

        public static void DeregisterMessageHandler_FromServer(ushort messageID, ServerMessageCallback callback)
        {
            if (Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to deregister a ServerMessageCallback with ID of {messageID}, but that ID is used internally!");
                return;
            }

            if (messagesFromServerCallbacks.ContainsKey(messageID)) messagesFromServerCallbacks[messageID] -= callback;
        }
        #endregion

        #region Internal Messages
        public static void DeregisterInternalMessageHandler_FromClient(ushort messageID, ClientMessageCallback callback)
        {
            if (!Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to deregister an internal ClientMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (internalMessagesFromClientCallbacks.ContainsKey(messageID)) internalMessagesFromClientCallbacks[messageID] -= callback;
        }

        public static void DeregisterInternalMessageHandler_FromServer(ushort messageID, ServerMessageCallback callback)
        {
            if (!Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to deregister an internal ServerMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (internalMessagesFromServerCallbacks.ContainsKey(messageID)) internalMessagesFromServerCallbacks[messageID] -= callback;
        }
        #endregion

        #endregion

        public static void DisconnectClient(SteamId clientID)
        {
            if (!IsServer)
            {
                Debug.LogWarning("Tried to disconnect client, but this client is not the server!");
                return;
            }

            if (!clients.ContainsKey(clientID))
            {
                Debug.LogWarning("Tried to disconnect client with ID " + clientID + ", but clients dictionary does not contain that ID!");
                if (SteamID == clientID) Debug.Log("^^^ This ID was your ID, may happen when closing the server.");
            }
            else
            {
                clients.Remove(clientID);
            }

            SteamNetworking.CloseP2PSessionWithUser(clientID);

            OnClientDisconnect?.Invoke(clientID);

            InternalServerMessages.SendClientDisconnected(clientID);
        }

        public static void DisconnectClient(ConnectedClient client)
        {
            if (!IsServer)
            {
                Debug.LogWarning("Tried to disconnect client, but this client is not the server!");
                return;
            }

            DisconnectClient(client.steamId);
        }

        private void AcceptP2P(SteamId otherID)
        {
            try
            {
                SteamNetworking.AcceptP2PSessionWithUser(otherID);
            }
            catch
            {
                Debug.Log("Unable to accept P2P Session with user " + otherID);
            }
        }

        public static void OpenSteamOverlayLobbyInvite()
        {
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Id);
        }

        private static void ResetValues()
        {
            clients.Clear();

            messagesFromClientCallbacks.Clear();
            messagesFromServerCallbacks.Clear();

            internalMessagesFromClientCallbacks.Clear();
            internalMessagesFromServerCallbacks.Clear();

            CurrentLobby = default;
            ServerID = default;

            NetworkID.ResetNetIDs();
        }


        public static void LoadScene(string sceneName)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Tried to load scene {sceneName}, but this client is not the server!");
                return;
            }

            foreach (ConnectedClient client in clients.Values)
            {
                client.sceneLoaded = false;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);

            InternalServerMessages.SendChangeScene(sceneName);
        }

        public static bool AllClientsLoadedInScene()
        {
            foreach (ConnectedClient client in clients.Values)
            {
                if (!client.sceneLoaded) return false;
            }

            return true;
        }

        internal static void ClientSceneLoaded(SteamId id)
        {
            clients[id].sceneLoaded = true;
            OnClientSceneLoaded?.Invoke(id);

            if (AllClientsLoadedInScene())
            {
                OnAllClientsSceneLoaded?.Invoke();
            }
        }

        public static void ChangeLobbyPrivacy(SteamLobbyPrivacyMode mode)
        {
            if (CurrentLobby.Owner.Id == SteamID)
            {
                SetLobbyPrivacyMode(CurrentLobby, mode);
            }
        }

        public static void ChangeLobbyJoinability(bool joinable)
        {
            if (CurrentLobby.Owner.Id == SteamID)
            {
                CurrentLobby.SetJoinable(joinable);
            }
        }


        #region Objects

        public static void DestroyObject(NetworkID id)
        {
            id.Destroy();
        }

        #endregion
    }

    public enum SteamLobbyPrivacyMode
    {
        PUBLIC,
        PRIVATE,
        INVISIBLE,
        FRIENDS_ONLY
    }

    public enum P2PMessageChannels : int
    {
        CLIENT = 0,
        SERVER = 1
    }
}
