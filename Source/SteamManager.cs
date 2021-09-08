using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
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

            try
            {
                SteamClient.Init(appID, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Couldn't log onto steam! " + ex);
                return;
            }
            SteamNetworkingUtils.InitRelayNetworkAccess();

            InitSteamEvents();
            InternalMessages.Initialize();
            if (SteamClient.IsValid)
            {
                Debug.Log($"Successfully logged into steam as {SteamName} ({SteamID})");
            }

            if (tickRate > 0)
            {
                Time.fixedDeltaTime = 1f / tickRate;
                Debug.Log($"Set physics tickrate to {tickRate} ticks per second ({Time.fixedDeltaTime}s per physics update).");
            }

            TryRegisterSpawnablePrefab(playerPrefab.gameObject);

            foreach (GameObject obj in spawnablePrefabs)
            {
                TryRegisterSpawnablePrefab(obj);
            }
        }

        // Inspector Stuff
        [Header("The steam app id of your app.")]
        [SerializeField] private uint appID = 480;
        public static uint AppID => instance.appID;

        [Header("Disconnects clients if their Application.version is different from the server's.")]
        [SerializeField] private bool disconnectClientsFromDifferentVersion = true;

        [Header("The maximum number of players who can join at once.")]
        [SerializeField] private uint maxPlayers = 4;

        [Header("Sets the fixed update rate. Set to 0 to keep as it is.")]
        [Range(0, 128)]
        [SerializeField] private int tickRate;
        public static int TickRate => instance.tickRate;

        [Header("All prefabs that can be spawned on the client should be in this list.")]
        [SerializeField] private GameObject[] spawnablePrefabs;

        [Header("The object spawned in when a client joins.")]
        [SerializeField] private Client playerPrefab;
        internal static Client PlayerPrefab => instance.playerPrefab;

        //private

        // Static Members
        public static Lobby CurrentLobby { get; private set; }
        private static SteamId steamID = 0;
        public static SteamId SteamID
        {
            get
            {
                if (steamID == 0)
                    steamID = SteamClient.SteamId;

                return steamID;
            }
        }

        public static string SteamName => new Friend(SteamID).Name;

        public static SteamId ServerID { get; private set; }
        public static bool ConnectedToServer => ServerID.IsValid;
        public static bool IsServer => SteamID == ServerID;
        public static int ClientCount => clients.Count;

        private static bool serverShuttingDown = false;

        //private static SteamSocketServer currentServer;
        //private static SteamSocketConnection currentConnection;

        // Events
        //public static event Action OnServerStart;
        //public static event Action OnServerStop;
        internal static event Action OnServerStart;

        public static event LobbyCreatedCallback OnLobbyCreated; // Server
        public static event LobbyCallback OnLobbyJoined; // Both
        public static event LobbyMemberCallback OnLobbyMemberJoined; // Both
        public static event LobbyCallback OnLobbyLeft; // Both
        public static event LobbyMemberCallback OnLobbyMemberLeave; // Both

        public static event Action<SteamId> OnClientDisconnect; // Both

        public static event Action<SteamId> OnClientSceneLoaded; // Server
        public static event Action OnAllClientsSceneLoaded; // Server

        public static event ClientMessageReceivedCallback OnMessageFromClient;
        public static event ServerMessageReceivedCallback OnMessageFromServer;

        internal static event ClientMessageReceivedCallback OnInternalMessageFromClient;
        internal static event ServerMessageReceivedCallback OnInternalMessageFromServer;

        // Delegates (Really just used so the parameters have names)
        public delegate void ClientMessageReceivedCallback(ushort messageID, SteamId clientSteamID, Message message);
        public delegate void ServerMessageReceivedCallback(ushort messageID, Message message);

        public delegate void ClientMessageCallback(SteamId clientSteamID, Message message);
        public delegate void MessageCallback(Message message);

        public delegate void LobbyMemberCallback(Lobby lobby, Friend member);
        public delegate void LobbyCallback(Lobby lobby);
        public delegate void LobbyCreatedCallback(Lobby lobby, bool success);

        // Dictionaries
        public static readonly Dictionary<SteamId, Client> clients = new Dictionary<SteamId, Client>();

        private static readonly Dictionary<ushort, ClientMessageCallback> messagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, MessageCallback> messagesFromServerCallbacks = new Dictionary<ushort, MessageCallback>();

        private static readonly Dictionary<ushort, ClientMessageCallback> internalMessagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, MessageCallback> internalMessagesFromServerCallbacks = new Dictionary<ushort, MessageCallback>();

        internal static readonly Dictionary<Guid, GameObject> registeredPrefabs = new Dictionary<Guid, GameObject>();


        // Constants
        private const string LOBBY_SERVER_VERSION = "server_version";
        internal const ushort PUBLIC_MESSAGE_BUFFER_SIZE = 2048;
        private const bool DONT_COUNT_NETSTATS_IF_SENDING_TO_SELF = false;

        // Allocation Reduction
        internal static byte[] tempMessageByteBuffer = new byte[PUBLIC_MESSAGE_BUFFER_SIZE];
        
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
                if (friend.Id != SteamID)
                {
                    Client.Create(friend.Id);

                    try
                    {
                        foreach (NetworkID networkID in NetworkID.networkIDs.Values)
                        {
                            Debug.LogError($"Spawned {networkID.name} on {friend.Name}'s client");
                            SpawnObject(networkID, friend.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Caught error spawning network objects for {friend.Name}: {ex}");
                    }
                }
            }

            OnLobbyMemberJoined?.Invoke(lobby, friend);
        }

        private void SteamMatchmaking_OnLobbyEntered(Lobby lobby)
        {
            if (IsServer)
            {
                if (clients.ContainsKey(SteamID)) return;
                Client.Create(SteamID);
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

        private async void SteamFriends_OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
        {
            CurrentLobby.Leave();

            if (await lobby.Join() != RoomEnter.Success)
            {
                Debug.LogError("Tried to join lobby, but was not successful!");
                LeaveServer();
                return;
            }
            CurrentLobby = lobby;
            ServerID = CurrentLobby.Owner.Id;
            AcceptP2P(ServerID);
        }

        private void SteamMatchmaking_OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            if (friend.Id == SteamID)
            {
                OnLeaveLobby(lobby);
            }

            OnLobbyMemberLeave?.Invoke(lobby, friend); // Call before client is removed from dict

            if (IsServer)
            {
                if (friend.Id != SteamID)
                {
                    if (!clients.ContainsKey(friend.Id))
                        Debug.LogWarning("Client " + friend.Id + " left the lobby, but they were never added to the Clients dictionary!");

                    SteamNetworking.CloseP2PSessionWithUser(friend.Id);
                    clients[friend.Id].Destroy();
                }
                else
                {
                    LeaveServer();
                    return;
                }
            }

            if (friend.Id == ServerID && !IsServer)
                LeaveServer();
        }

        private static void OnLeaveLobby(Lobby lobby)
        {
            DestroyAllRuntimeNetworkIDs();
            OnLobbyLeft?.Invoke(lobby);
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
                    try
                    {
                        HandleDataFromServer(Message.Create(packet.Value.Data), packet.Value.SteamId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Exception handling data from server: " + ex);
                    }
                }
            }

            if (IsServer)
            {
                while (SteamNetworking.IsP2PPacketAvailable((int)P2PMessageChannels.SERVER))
                {
                    P2Packet? packet = SteamNetworking.ReadP2PPacket((int)P2PMessageChannels.SERVER);
                    if (packet.HasValue)
                    {
                        try
                        {
                            HandleDataFromClient(Message.Create(packet.Value.Data), packet.Value.SteamId);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("Exception handling data from " + new Friend(packet.Value.SteamId) + ": " + ex);
                        }
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
            LeaveServer();
            try
            {
                SteamClient.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.LogError("Error shutting down steam client!" + ex);
            }
            
        }
        #endregion

        #region Lobby
        public static async Task<bool> CreateLobby(uint maxPlayers = 4, SteamLobbyPrivacyMode mode = SteamLobbyPrivacyMode.FRIENDS_ONLY, bool joinable = true)
        {
            Lobby? lobby =  await SteamMatchmaking.CreateLobbyAsync((int)maxPlayers);
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
                    Debug.Log("Error joining own lobby!");
                }

                return true;
            }
            else
            {
                OnLobbyCreated?.Invoke(CurrentLobby, false);
                throw new NullReferenceException("Lobby created but returned null value!");
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
            if (CurrentLobby.Id != 0)
            {
                CurrentLobby.Leave();
                OnLeaveLobby(CurrentLobby);
                CurrentLobby = default;
            }
        }
        #endregion

        #region Server
        public static async void HostServer(SteamLobbyPrivacyMode mode = SteamLobbyPrivacyMode.FRIENDS_ONLY, bool joinable = true)
        {
            //LeaveServer();
            LeaveCurrentLobby();
            ResetValues();

            serverShuttingDown = false;

            if (!await CreateLobby(instance.maxPlayers, mode, joinable))
            {
                Debug.Log("Error starting server: Lobby not created successfully.");
                return;
            }

            SteamNetworking.AllowP2PPacketRelay(true);
            OnServerStart?.Invoke();
        }

        private static void StopServer()
        {
            if (!IsServer) return;

            //OnServerStop?.Invoke();
            serverShuttingDown = true;
            KickAllClients();
        }

        private static void KickAllClients()
        {
            if (IsServer)
            {
                List<SteamId> clientIDs = new List<SteamId>((int)instance.maxPlayers);

                foreach (Client client in clients.Values)
                {
                    clientIDs.Add(client.SteamID);
                }

                for (int i = 0; i < clientIDs.Count; i++)
                {
                    DisconnectClient(clientIDs[i]);
                }
            }

            clients.Clear();
        }

        /// <summary>
        /// Leaves the server and the steam lobby. Stops the server if you are the host.
        /// </summary>
        public static void LeaveServer()
        {
            if (serverShuttingDown) return;

            try
            {
                if (IsServer)
                    StopServer();

                InternalClientMessages.SendDisconnect();
                SteamNetworking.CloseP2PSessionWithUser(ServerID);
                LeaveCurrentLobby();
                ResetValues();
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while trying to leave server! " + ex);
            }
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
            if (!ConnectedToServer)
            {
                Debug.LogWarning("Tried to send message to server, but ID is invalid! (Are you connected to a server?)");
                return;
            }

            if (!IsServer || !DONT_COUNT_NETSTATS_IF_SENDING_TO_SELF)
            {
                NetStats.OnPacketSent(message.WrittenLength);
            }

            if (!IsServer)
            {
                SteamNetworking.SendP2PPacket(ServerID, message.Bytes, message.WrittenLength, (int)P2PMessageChannels.SERVER, message.SendType);
            }
            else
            {
                Array.Copy(message.Bytes, 0, tempMessageByteBuffer, 0, message.WrittenLength);
                HandleDataFromClient(Message.Create(tempMessageByteBuffer, (ushort)message.WrittenLength), SteamID);
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

            if (id != SteamID || !DONT_COUNT_NETSTATS_IF_SENDING_TO_SELF)
            {
                NetStats.OnPacketSent(message.WrittenLength);
            }

            if (id != SteamID)
            {
                SteamNetworking.SendP2PPacket(id, message.Bytes, message.WrittenLength, (int)P2PMessageChannels.CLIENT, message.SendType);
            }
            else
            {
                Array.Copy(message.Bytes, 0, tempMessageByteBuffer, 0, message.WrittenLength);
                HandleDataFromServer(Message.Create(tempMessageByteBuffer, (ushort)message.WrittenLength), SteamID);
            }
        }

        public static void SendMessageToAllClients(Message message)
        {
            foreach (Client client in clients.Values)
            {
                SendMessageToClient(client.SteamID, message);
            }
        }

        public static void SendMessageToAllClients(SteamId except, Message message)
        {
            foreach (Client client in clients.Values)
            {
                if (client.SteamID != except)
                    SendMessageToClient(client.SteamID, message);
            }
        }
        #endregion

        #region Handle
        private static void HandleDataFromServer(Message message, SteamId fromId)
        {
            if (fromId != ServerID)
            {
                Debug.LogWarning($"Received packet from {new Friend(fromId).Name} ({fromId}), but current server ID is set to {new Friend(ServerID).Name} ({ServerID})!");
                return;
            }

            if (fromId != SteamID || !DONT_COUNT_NETSTATS_IF_SENDING_TO_SELF)
                NetStats.OnPacketReceived(message.WrittenLength);

            ushort id = message.GetUShort();

            if (Message.IsInternalMessage(id))
            {
                OnInternalMessageFromServer?.Invoke(id, message);
                if (internalMessagesFromServerCallbacks.ContainsKey(id))
                {
                    internalMessagesFromServerCallbacks[id]?.Invoke(message);
                }
            }
            else
            {
                OnMessageFromServer?.Invoke(id, message);
                if (messagesFromServerCallbacks.ContainsKey(id))
                {
                    messagesFromServerCallbacks[id]?.Invoke(message);
                }
            }
        }

        private static void HandleDataFromClient(Message message, SteamId fromClient)
        {
            if (fromClient != SteamID || !DONT_COUNT_NETSTATS_IF_SENDING_TO_SELF)
                NetStats.OnPacketReceived(message.WrittenLength);

            if (serverShuttingDown) return;

            if (!clients.ContainsKey(fromClient))
            {
                Debug.LogWarning($"Received message from {new Friend(fromClient).Name}, but they are not in the clients dictionary!");
                return;
            }

            ushort id = message.GetUShort();

            if (!clients[fromClient].sceneLoaded)
            {
                Debug.Log($"Received message from {clients[fromClient].SteamName}, but they have not loaded the next scene yet.");
                return;
            }

            if (Message.IsInternalMessage(id))
            {
                OnInternalMessageFromClient?.Invoke(id, fromClient, message);
                if (internalMessagesFromClientCallbacks.ContainsKey(id))
                {
                    internalMessagesFromClientCallbacks[id]?.Invoke(fromClient, message);
                }
            }
            else
            {
                OnMessageFromClient?.Invoke(id, fromClient, message);
                if (messagesFromClientCallbacks.ContainsKey(id))
                {
                    messagesFromClientCallbacks[id]?.Invoke(fromClient, message);
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
            else messagesFromClientCallbacks[messageID] += callback;
        }

        /// <summary>
        /// Invokes the <paramref name="callback"/> on the client when a message from the server of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnMessageFromServer to get notified when any message is received, not only a message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when a message with ID <paramref name="messageID"/> is received.</param>
        public static void RegisterMessageHandler_FromServer(ushort messageID, MessageCallback callback)
        {
            if (Message.IsInternalMessage(messageID))
            {
                Debug.LogWarning($"Tried to register a ServerMessageCallback with ID of {messageID}, but that ID is used internally!");
                return;
            }

            if (!messagesFromServerCallbacks.ContainsKey(messageID)) messagesFromServerCallbacks.Add(messageID, callback);
            else messagesFromServerCallbacks[messageID] += callback;
        }
        #endregion

        #region Internal messages
        /// <summary>
        /// Invokes the <paramref name="callback"/> on the server when an internal message from a client of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnInternalMessageFromClient to get notified when any internal message is received, not only an internal message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when an internal message with ID <paramref name="messageID"/> is received.</param>
        internal static void RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs messageID, ClientMessageCallback callback)
        {
            if (!Message.IsInternalMessage((ushort)messageID))
            {
                Debug.LogWarning($"Tried to register an internal ClientMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (!internalMessagesFromClientCallbacks.ContainsKey((ushort)messageID)) internalMessagesFromClientCallbacks.Add((ushort)messageID, callback);
            else internalMessagesFromClientCallbacks[(ushort)messageID] += callback;
        }

        /// <summary>
        /// Invokes the <paramref name="callback"/> on the client when an internal message from the server of ID <paramref name="messageID"/> is received.
        /// Subscribe to OnInternalMessageFromServer to get notified when any internal message is received, not only an internal message of ID <paramref name="messageID"/>.
        /// </summary>
        /// <param name="messageID">The message ID that will be listened for.</param>
        /// <param name="callback">The callback to invoke when an internal message with ID <paramref name="messageID"/> is received.</param>
        internal static void RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs messageID, MessageCallback callback)
        {
            if (!Message.IsInternalMessage((ushort)messageID))
            {
                Debug.LogWarning($"Tried to register an internal ServerMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (!internalMessagesFromServerCallbacks.ContainsKey((ushort)messageID)) internalMessagesFromServerCallbacks.Add((ushort)messageID, callback);
            else internalMessagesFromServerCallbacks[(ushort)messageID] += callback;
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

        public static void DeregisterMessageHandler_FromServer(ushort messageID, MessageCallback callback)
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
        internal static void DeregisterInternalMessageHandler_FromClient(InternalClientMessageIDs messageID, ClientMessageCallback callback)
        {
            if (!Message.IsInternalMessage((ushort)messageID))
            {
                Debug.LogWarning($"Tried to deregister an internal ClientMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (internalMessagesFromClientCallbacks.ContainsKey((ushort)messageID)) internalMessagesFromClientCallbacks[(ushort)messageID] -= callback;
        }

        internal static void DeregisterInternalMessageHandler_FromServer(InternalServerMessageIDs messageID, MessageCallback callback)
        {
            if (!Message.IsInternalMessage((ushort)messageID))
            {
                Debug.LogWarning($"Tried to deregister an internal ServerMessageCallback with ID of {messageID}, but that ID is not used internally!");
                return;
            }

            if (internalMessagesFromServerCallbacks.ContainsKey((ushort)messageID)) internalMessagesFromServerCallbacks[(ushort)messageID] -= callback;
        }
        #endregion

        #endregion

        public static void DisconnectClient(SteamId clientID)
        {
            if (!IsServer)
            {
                Debug.LogWarning("Tried to disconnect " + new Friend(clientID) + ", but this client is not the server!");
                return;
            }

            if (!clients.ContainsKey(clientID))
            {
                if (serverShuttingDown && SteamID == clientID) return;

                Debug.LogWarning("Tried to disconnect client with ID " + clientID + ", but clients dictionary does not contain that ID! (May be duplicate call)");
                if (SteamID == clientID) Debug.Log("^^^ This ID was your ID, may happen when closing the server.");
            }

            InternalServerMessages.SendClientDisconnected(clientID);

            OnClientDisconnect?.Invoke(clientID);

            if (clients.TryGetValue(clientID, out Client client))
                client.Destroy();

            SteamNetworking.CloseP2PSessionWithUser(clientID);
        }

        public static void DisconnectClient(Client client)
        {
            if (client == null)
            {
                Debug.LogWarning("Tried to disconnect client, but it was null!");
                return;
            }

            DisconnectClient(client.SteamID);
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

            //messagesFromClientCallbacks.Clear();
            //messagesFromServerCallbacks.Clear();
            //
            //internalMessagesFromClientCallbacks.Clear();
            //internalMessagesFromServerCallbacks.Clear();

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

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);

            if (!scene.IsValid())
            {
                Debug.LogError($"Tried to load scene {sceneName}, but that scene does not exist!");
            }

            foreach (Client client in clients.Values)
            {
                client.sceneLoaded = false;
            }

            NetworkID.ResetNetIDs();

            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);

            InternalServerMessages.SendChangeScene(scene.buildIndex);
        }

        public static bool AllClientsLoadedInScene()
        {
            foreach (Client client in clients.Values)
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

        internal static void SpawnObject(NetworkID networkID)
        {
            if (!IsServer) return;

            InternalServerMessages.SendNetworkIDSpawn(networkID);
        }

        internal static void SpawnObject(NetworkID networkID, SteamId onlyTo)
        {
            if (!IsServer) return;

            InternalServerMessages.SendNetworkIDSpawn(networkID, onlyTo);
        }

        internal static void DestroyObject(NetworkID networkID)
        {
            // Called in NetworkIDs OnDestroy(), no need to destroy it here, just send the destroy to clients
            if (!IsServer) return;

            InternalServerMessages.SendNetworkIDDestroy(networkID);
        }

        public static void TryRegisterSpawnablePrefab(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Tried to register a spawnable prefab, but the passed GameObject was null!");
                return;
            }

            if (!obj.TryGetComponent(out NetworkID networkID))
            {
                Debug.LogWarning($"Tried to register a spawnable prefab ({obj.name}), but that object has no NetworkID component!");
                return;
            }

            if (networkID.assetID == Guid.Empty)
            {
                Debug.LogWarning($"Tried to register a spawnable prefab ({obj.name}), but that object's NetworkID has no assetID! Is it a prefab?");
                return;
            }

            if (registeredPrefabs.ContainsKey(networkID.assetID))
            {
                Debug.Log($"A passed in prefab ({obj.name}) has the same assetID as a registered prefab ({registeredPrefabs[networkID.assetID].name}). Overwriting...");
            }

            registeredPrefabs[networkID.assetID] = obj;
        }

        private static void DestroyAllRuntimeNetworkIDs()
        {
            // Used when disconnecting or server shutdown when the NetworkID destroy messages may not be received/sent

            if (IsServer) return;

            uint[] netIDsToDestroy = new uint[NetworkID.networkIDs.Count];
            int numNetworkIDsToDestroy = 0;

            foreach (NetworkID networkID in NetworkID.networkIDs.Values)
            {
                if (networkID != null && networkID.sceneID == 0)
                {
                    netIDsToDestroy[numNetworkIDsToDestroy++] = networkID.netID;
                }
            }

            for (uint i = 0; i < numNetworkIDsToDestroy; i++)
            {
                NetworkID networkID = NetworkID.networkIDs[netIDsToDestroy[i]];
                if (networkID != null)
                {
                    Debug.Log("Destroying NetworkID " + networkID.name);
                    Destroy(networkID.gameObject);
                }
            }

            NetworkID.ResetNetIDs();
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
