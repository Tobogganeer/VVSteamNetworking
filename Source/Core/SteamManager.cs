using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using ClientMessageReceivedCallback = System.Action<ushort, Steamworks.SteamId, VirtualVoid.Net.Message>;
using ServerMessageReceivedCallback = System.Action<ushort, VirtualVoid.Net.Message>;
using ClientMessageCallback = System.Action<Steamworks.SteamId, VirtualVoid.Net.Message>;
using MessageCallback = System.Action<VirtualVoid.Net.Message>;

namespace VirtualVoid.Net
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
            if (SteamClient.IsValid)
            {
                Debug.Log($"Successfully logged into steam as {SteamName} ({SteamID})");
            }

            if (tickRate > 0)
            {
                Time.fixedDeltaTime = 1f / tickRate;
                Debug.Log($"Set physics tickrate to {tickRate} ticks per second ({Time.fixedDeltaTime}s per physics update).");
            }

            //TryRegisterSpawnablePrefab(playerPrefab.gameObject);

            foreach (GameObject obj in spawnablePrefabs)
            {
                TryRegisterSpawnablePrefab(obj);
            }
        }

        // Inspector Stuff
        [Header("The steam app id of your app.")]
        [SerializeField] private uint appID = 480;
        public static uint AppID => instance.appID;

        [Header("The maximum number of players who can join at once.")]
        [SerializeField] private uint maxPlayers = 4;
        public static uint MaxPlayers => instance.maxPlayers;

        [Header("Sets the fixed update rate. Set to 0 to keep as it is.")]
        [Range(0, 128)]
        [SerializeField] private int tickRate;
        public static int TickRate => instance.tickRate;

        [Header("All prefabs that can be spawned on the client should be in this list.")]
        [SerializeField] private GameObject[] spawnablePrefabs;

        //[Header("The object spawned in when a client joins.")]
        //[SerializeField] private NetworkBehaviour playerPrefab;
        //internal static Client PlayerPrefab => instance.playerPrefab;

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

        private static SteamSocketManager socketServer;
        private static SteamConnectionManager connectionToServer;
        private static AuthTicket currentAuthTicket;

        public static SteamId LobbyOwnerID => CurrentLobby.Owner.Id; //{ get; private set; }
        public static bool ConnectedToServer => connectionToServer != null;// && connectionToServer.Connected;
        public static bool IsServer => socketServer != null;

        #region Events
        public static event Action<Lobby> OnLobbyCreated; // Server
        public static event Action<Lobby> OnLobbyJoined; // Both
        public static event Action<Lobby, Friend> OnLobbyMemberJoined; // Both
        public static event Action<Lobby> OnLobbyLeft; // Both
        public static event Action<Lobby, Friend> OnLobbyMemberLeave; // Both

        public static event Action<SteamId> OnClientSceneLoaded; // Server
        public static event Action OnAllClientsSceneLoaded; // Server

        public static event ClientMessageReceivedCallback OnMessageFromClient;
        public static event ServerMessageReceivedCallback OnMessageFromServer;

        internal static event ClientMessageReceivedCallback OnInternalMessageFromClient;
        internal static event ServerMessageReceivedCallback OnInternalMessageFromServer;

        public static event Action<Client> OnClientConnected;
        public static event Action<Client> OnClientDisconnected;
        #endregion

        // Delegates (Really just used so the parameters have names)
        //public delegate void ClientMessageReceivedCallback(ushort messageID, SteamId clientSteamID, Message message);
        //public delegate void ServerMessageReceivedCallback(ushort messageID, Message message);
        //
        //public delegate void ClientMessageCallback(SteamId clientSteamID, Message message);
        //public delegate void MessageCallback(Message message);
        //
        //public delegate void LobbyMemberCallback(Lobby lobby, Friend member);
        //public delegate void LobbyCallback(Lobby lobby);
        //public delegate void LobbyCreatedCallback(Lobby lobby, bool success);

        // Dictionaries

        public static readonly Dictionary<uint, SteamId> connIDToSteamID = new Dictionary<uint, SteamId>();
        public static readonly Dictionary<SteamId, Client> clients = new Dictionary<SteamId, Client>();
        internal static readonly Dictionary<uint, Client> clientsPendingAuth = new Dictionary<uint, Client>();
        internal static readonly Dictionary<SteamId, uint> unverifiedSteamIDToConnID = new Dictionary<SteamId, uint>();

        #region Callback Dicts
        private static readonly Dictionary<ushort, ClientMessageCallback> messagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, MessageCallback> messagesFromServerCallbacks = new Dictionary<ushort, MessageCallback>();

        private static readonly Dictionary<ushort, ClientMessageCallback> internalMessagesFromClientCallbacks = new Dictionary<ushort, ClientMessageCallback>();
        private static readonly Dictionary<ushort, MessageCallback> internalMessagesFromServerCallbacks = new Dictionary<ushort, MessageCallback>();
        #endregion

        internal static readonly Dictionary<Guid, GameObject> registeredPrefabs = new Dictionary<Guid, GameObject>();


        // Constants
        private const string LOBBY_SERVER_VERSION = "server_version";
        internal const ushort PUBLIC_MESSAGE_BUFFER_SIZE = 4096;
        private const int STEAM_VIRTUAL_PORT = 225;
        private const float UNAUTHENTICATED_CLIENT_TIMEOUT = 5f;

        private bool suppressNetworkIDDestroyMessages;

        // Allocation Reduction
        //internal static byte[] tempMessageByteBuffer = new byte[PUBLIC_MESSAGE_BUFFER_SIZE];
        
        private void InitSteamEvents()
        {
            SteamFriends.OnGameLobbyJoinRequested += SteamFriends_OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;
            SteamUser.OnValidateAuthTicketResponse += SteamUser_OnValidateAuthTicketResponse;
        }



        #region Steam Callbacks

        private void SteamMatchmaking_OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            OnLobbyMemberJoined?.Invoke(lobby, friend);
        }

        private void SteamMatchmaking_OnLobbyEntered(Lobby lobby)
        {
            if (!IsServer)
            {
                string version = Application.version;
                string data = lobby.GetData(LOBBY_SERVER_VERSION);
                if (version != data)
                {
                    Debug.Log($"Current version is {version}, but server version is {data}! Please update game...");
                    Leave();
                    return;
                }
            }

            OnLobbyJoined?.Invoke(lobby);
        }

        private async void SteamFriends_OnGameLobbyJoinRequested(Lobby lobby, SteamId id)
        {
            if (ConnectedToServer)
                Leave();

            if (await lobby.Join() != RoomEnter.Success)
            {
                Debug.LogError("Tried to join lobby, but was not successful!");
                Leave();
                return;
            }
            CurrentLobby = lobby;

            ConnectToServer(LobbyOwnerID);
        }

        private void SteamMatchmaking_OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            OnLobbyMemberLeave?.Invoke(lobby, friend);

            if (friend.Id == LobbyOwnerID && !IsServer)
                Leave();
        }

        private void SteamUser_OnValidateAuthTicketResponse(SteamId userSteamID, SteamId gameOwnerID, AuthResponse response)
        {
            //Debug.Log($"Received auth ticket response for {new Friend(userSteamID).Name} ({userSteamID}): {response}");

            if (response == AuthResponse.OK)
            {
                UserAuthenticated(userSteamID);
            }
            else
            {
                UserNotAuthenticated(userSteamID, response);
            }
        }

        #endregion

        #region Unity Functions
        private void Update()
        {
            if (Time.frameCount % 150 == 0)
                DisconnectOldConnections();

            SteamClient.RunCallbacks();

            try
            {
                if (socketServer != null)
                    socketServer.Receive();

                if (connectionToServer != null)
                    connectionToServer.Receive();
            }
            catch
            {
                Debug.Log("Error receiving data on socket/connection");
            }
        }

        private void OnDisable()
        {
            Leave();
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
                SetLobbyPrivacyMode(CurrentLobby, mode);
                CurrentLobby.SetJoinable(joinable);
                CurrentLobby.SetData(LOBBY_SERVER_VERSION, Application.version);

                OnLobbyCreated?.Invoke(CurrentLobby);

                if (await CurrentLobby.Join() != RoomEnter.Success)
                {
                    Debug.Log("Error joining own lobby!");
                }

                return true;
            }
            else
            {
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
            }
        }

        private static void LeaveCurrentLobby()
        {
            if (CurrentLobby.Id.IsValid)
            {
                CurrentLobby.Leave();
                OnLobbyLeft?.Invoke(CurrentLobby);
                CurrentLobby = default;
            }
        }
        #endregion

        #region Server
        /// <summary>
        /// Hosts a game.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="joinable"></param>
        public static async void Host(SteamLobbyPrivacyMode mode = SteamLobbyPrivacyMode.FRIENDS_ONLY, bool joinable = true)
        {
            Leave();

            CreateSteamSocketServer();
            ConnectToServer(SteamID);

            SteamNetworkingUtils.Timeout = 2000;

            if (!await CreateLobby(instance.maxPlayers, mode, joinable))
            {
                Debug.Log("Error starting server: Lobby not created successfully.");
                Leave();
                return;
            }
        }

        /// <summary>
        /// Leaves the server and the steam lobby. Stops the server if you are the host.
        /// </summary>
        public static void Leave()
        {
            try
            {
                if (IsServer)
                    StopServer();

                connectionToServer?.Close();
                connectionToServer = null;

                LeaveCurrentLobby();
                ResetValues();
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while trying to leave server! " + ex);
            }
        }


        private static void StopServer()
        {
            KickAllClients();
            socketServer?.Close();
            socketServer = null;
        }

        private static void KickAllClients()
        {
            foreach (Client client in clients.Values.ToList())
                client.Destroy();

            foreach (Client client in clientsPendingAuth.Values.ToList())
                client.Destroy();

            if (socketServer != null)
            {
                foreach (Connection connection in socketServer.Connected)
                    connection.Close();
                // Close all connections just in case
            }

            clients.Clear();
            clientsPendingAuth.Clear();

            connIDToSteamID.Clear();
            unverifiedSteamIDToConnID.Clear();
            // Clear mapping tables as well
        }

        private static void ResetValues()
        {
            clients.Clear();
            clientsPendingAuth.Clear();
            connIDToSteamID.Clear();
            unverifiedSteamIDToConnID.Clear();

            currentAuthTicket?.Cancel();
            currentAuthTicket = null;

            CurrentLobby = default;
            connectionToServer?.Close();
            connectionToServer = null;
            socketServer?.Close();
            socketServer = null;

            NetworkID.ResetNetIDs();
            DestroyAllRuntimeNetworkIDs();
        }
        #endregion

        #region Send
        public static bool SendMessageToServer(Message message)
        {
            if (message == null)
            {
                Debug.LogWarning("Cannot send null message!");
                return false;
            }

            try
            {
                // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
                int sizeOfMessage = message.WrittenLength;
                IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
                System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);
                Result success = connectionToServer.Connection.SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
                if (success == Result.OK)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                    NetStats.OnPacketSent(sizeOfMessage);
                    return true;
                }
                else
                {
                    // RETRY
                    Result retry = connectionToServer.Connection.SendMessage(intPtrMessage, sizeOfMessage, message.SendType);
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                    if (retry == Result.OK)
                    {
                        NetStats.OnPacketSent(sizeOfMessage);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Unable to send message to socket server: " + ex);
                return false;
            }
        }

        public static bool SendMessageToClient(SteamId id, Message message)
        {
            if (message == null)
            {
                Debug.LogWarning("Cannot send null message!");
                return false;
            }

            if (!IsServer)
            {
                Debug.LogWarning("Cannot send messages to clients as this machine is not currently a server!");
                return false;
            }

            if (!clients.ContainsKey(id))
            {
                Debug.LogWarning($"Tried to send message to {new Friend(id).Name}, but they weren't in the clients dict!");
                return false;
            }

            try
            {
                // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
                int sizeOfMessage = message.WrittenLength;
                IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
                System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);

                Connection conn = clients[id].connection;

                return SendDataToClient(intPtrMessage, conn, sizeOfMessage, message.SendType);
            }
            catch (Exception ex)
            {
                Debug.Log($"Unable to send message to {new Friend(id).Name}: " + ex);
                return false;
            }
        }

        public static bool SendMessageToAllClients(Message message)
        {
            if (message == null)
            {
                Debug.LogWarning("Cannot send null message!");
                return false;
            }

            if (!IsServer)
            {
                Debug.LogWarning("Cannot send messages to clients as this machine is not currently a server!");
                return false;
            }

            try
            {
                // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
                int sizeOfMessage = message.WrittenLength;
                IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
                System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);

                bool success = true;

                foreach (Client client in clients.Values)
                {
                    if (SendDataToClient(intPtrMessage, client.connection, sizeOfMessage, message.SendType) == false)
                        success = false;
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.Log($"Unable to send message to clients: " + ex);
                return false;
            }
        }

        public static bool SendMessageToAllClients(SteamId except, Message message)
        {
            if (message == null)
            {
                Debug.LogWarning("Cannot send null message!");
                return false;
            }

            if (!IsServer)
            {
                Debug.LogWarning("Cannot send messages to clients as this machine is not currently a server!");
                return false;
            }

            try
            {
                // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
                int sizeOfMessage = message.WrittenLength;
                IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
                System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);

                bool success = true;

                foreach (Client client in clients.Values)
                {
                    if (client.SteamID == except) continue;

                    if (SendDataToClient(intPtrMessage, client.connection, sizeOfMessage, message.SendType) == false)
                        success = false;
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.Log($"Unable to send message to clients: " + ex);
                return false;
            }
        }

        private static bool SendDataToClient(IntPtr data, Connection conn, int size, SendType sendType)
        {
            Result success = conn.SendMessage(data, size, sendType);
            if (success == Result.OK)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(data); // Free up memory at pointer
                NetStats.OnPacketSent(size);
                return true;
            }
            else
            {
                // RETRY
                Result retry = connectionToServer.Connection.SendMessage(data, size, sendType);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(data); // Free up memory at pointer
                if (retry == Result.OK)
                {
                    NetStats.OnPacketSent(size);
                    return true;
                }
                return false;
            }
        }
        #endregion

        #region Handle
        private static void HandleDataFromClient(Message message, SteamId fromClient)
        {
            NetStats.OnPacketReceived(message.WrittenLength);

            if (!clients.ContainsKey(fromClient))
            {
                Debug.LogWarning($"Received message from {new Friend(fromClient).Name}, but they are not in the clients dictionary!");
                return;
            }

            ushort id = message.GetUShort();

            if (!clients[fromClient].sceneLoaded && id != (ushort)InternalClientMessageIDs.SCENE_LOADED)
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

        private static void HandleDataFromServer(Message message)
        {
            NetStats.OnPacketReceived(message.WrittenLength);

            ushort id = message.GetUShort();

            if (Message.IsInternalMessage(id))
            {
                if (id == (ushort)InternalServerMessageIDs.REQUEST_AUTH)
                {
                    SendAuthToServer();
                    return;
                }

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

        //public static void DisconnectClient(SteamId clientID)
        //{
        //    if (!IsServer)
        //    {
        //        Debug.LogWarning("Tried to disconnect " + new Friend(clientID) + ", but this client is not the server!");
        //        return;
        //    }
        //
        //    if (!clients.ContainsKey(clientID))
        //    {
        //        if (serverShuttingDown && SteamID == clientID) return;
        //
        //        Debug.LogWarning("Tried to disconnect client with ID " + clientID + ", but clients dictionary does not contain that ID! (May be duplicate call)");
        //        if (SteamID == clientID) Debug.Log("^^^ This ID was your ID, may happen when closing the server.");
        //    }
        //
        //    InternalServerMessages.SendClientDisconnected(clientID);
        //
        //    OnClientDisconnect?.Invoke(clientID);
        //
        //    if (clients.TryGetValue(clientID, out Client client))
        //        client.Destroy();
        //
        //    SteamNetworking.CloseP2PSessionWithUser(clientID);
        //}

        //private void AcceptP2P(SteamId otherID)
        //{
        //    try
        //    {
        //        SteamNetworking.AcceptP2PSessionWithUser(otherID);
        //    }
        //    catch
        //    {
        //        Debug.Log("Unable to accept P2P Session with user " + otherID);
        //    }
        //}

        public static void OpenSteamOverlayLobbyInvite()
        {
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Id);
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
                return;
            }

            foreach (Client client in clients.Values)
            {
                client.sceneLoaded = false;
            }

            NetworkID.ResetNetIDs();
            //DestroyAllRuntimeNetworkIDs();

            instance.suppressNetworkIDDestroyMessages = true;

            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);

            instance.suppressNetworkIDDestroyMessages = false;

            InternalServerSend.SendChangeScene(scene.buildIndex);
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
            clients[id].OnSceneFinishedLoading();
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
        
            InternalServerSend.SendNetworkIDSpawn(networkID);
        }
        
        internal static void SpawnObject(NetworkID networkID, SteamId onlyTo)
        {
            if (!IsServer) return;
        
            InternalServerSend.SendNetworkIDSpawn(networkID, onlyTo);
        }
        
        internal static void DestroyObject(NetworkID networkID)
        {
            // Called in NetworkIDs OnDestroy(), no need to destroy it here, just send the destroy to clients
            if (!IsServer) return;

            if (instance.suppressNetworkIDDestroyMessages) return;
            // May backfire

            InternalServerSend.SendNetworkIDDestroy(networkID);
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
            // Used when disconnecting or server shutdown
            // Destroy messages not sent
        
            //if (IsServer) return;
        
            uint[] netIDsToDestroy = new uint[NetworkID.networkIDs.Count];
            int numNetworkIDsToDestroy = 0;
        
            foreach (NetworkID networkID in NetworkID.networkIDs.Values)
            {
                if (networkID != null && networkID.sceneID == 0)
                {
                    netIDsToDestroy[numNetworkIDsToDestroy++] = networkID.netID;
                }
            }

            instance.suppressNetworkIDDestroyMessages = true;

            for (uint i = 0; i < numNetworkIDsToDestroy; i++)
            {
                NetworkID networkID = NetworkID.networkIDs[netIDsToDestroy[i]];
                if (networkID != null)
                {
                    Debug.Log("Destroying NetworkID " + networkID.name);
                    Destroy(networkID.gameObject);
                }
            }

            instance.suppressNetworkIDDestroyMessages = false;

            NetworkID.ResetNetIDs();
        }
        #endregion

        #region Socket Methods

        internal static void HandleDataFromClient(Connection connection, NetIdentity identity, IntPtr data, int size)
        {
            if (!IsServer) return;

            if (!connIDToSteamID.ContainsKey(connection.Id))
            {
                if (clientsPendingAuth.TryGetValue(connection.Id, out Client unAuthedClient))
                {
                    try
                    {
                        Message message = Message.Create(data, size);

                        HandleDataFromUnauthedClient(message, connection);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Unable to process message from unauthed client! " + ex);
                    }
                }
                else
                {
                    Debug.Log($"Attempting to handle data from {new Friend(identity.SteamId).Name}, but cannot find that ID in any dictionary!");
                }

                return;
            }

            SteamId clientID = connIDToSteamID[connection.Id];

            try
            {
                Message message = Message.Create(data, size);

                HandleDataFromClient(message, clientID);
            }
            catch (Exception ex)
            {
                Debug.Log($"Unable to process message from {new Friend(clientID).Name}! " + ex);
            }
        }

        internal static void HandleDataFromServer(IntPtr data, int size)
        {
            try
            {
                Message message = Message.Create(data, size);

                HandleDataFromServer(message);
            }
            catch
            {
                Debug.Log("Unable to process message from server");
            }
        }

        private static void CreateSteamSocketServer()
        {
            socketServer = SteamNetworkingSockets.CreateRelaySocket<SteamSocketManager>(STEAM_VIRTUAL_PORT);
            //connectionToServer = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(SteamID);

            //Debug.Log($"Attempting connection to local server...");
        }

        private static void ConnectToServer(SteamId serverID)
        {
            Debug.Log($"Attempting connection to {new Friend(serverID).Name} ({serverID})...");
            connectionToServer = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(serverID, STEAM_VIRTUAL_PORT);
        }

        internal static void OnConnectionConnected(Connection connection, ConnectionInfo data)
        {
            //Debug.Log("Incoming Connection: " + data.Identity.ToString());

            if (clients.Count + clientsPendingAuth.Count >= MaxPlayers)
            {
                connection.Close();
                Debug.Log("Player managed to join full game. Disconnecting.");
                return;
            }

            Debug.Log("Client connected to server. Awaiting ready message.");

            Client client = instance.GetClient();
            client.OnCreate(connection, data.Identity.SteamId);
            //Client.Create(connection, data.Identity.SteamId);
        }

        internal static void OnConnectionDisconnected(Connection connection, ConnectionInfo data)
        {
            uint connID = connection.Id;

            if (connIDToSteamID.ContainsKey(connID))
            {
                Client client = clients[connIDToSteamID[connID]];
                if (client != null)
                {
                    OnClientDisconnected?.Invoke(client);
                    client.Destroy();
                }
            }
        }


        internal static void OnConnectedToServer(ConnectionInfo info)
        {
            Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalClientMessageIDs.CONNECTED);
            try
            {
                instance.AddSpawnData(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error adding client spawn data! " + ex);
            }
            SendMessageToServer(message);
        }

        #endregion

        private static async void SendAuthToServer()
        {
            Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalClientMessageIDs.AUTH_TICKET);
        
            currentAuthTicket = await SteamUser.GetAuthSessionTicketAsync();
        
            if (currentAuthTicket == null)
            {
                Debug.LogError("Could not generate valid auth ticket.");
                return;
            }
        
            Debug.Log("Sending auth ticket to server...");
            message.Add(SteamID).Add(currentAuthTicket.Data.Length).Add(currentAuthTicket.Data);
        
            SendMessageToServer(message);
        }


        private static void HandleDataFromUnauthedClient(Message receievedMessage, Connection connection)
        {
            if (!IsServer) return;
        
            NetStats.OnPacketReceived(receievedMessage.WrittenLength);
        
            ushort id = receievedMessage.GetUShort();
            if (!clientsPendingAuth.TryGetValue(connection.Id, out Client client))
            {
                Debug.LogWarning("Could not find client in the unAuthed dict!");
                connection.Close();
                return;
            }
        
            if (Message.IsInternalMessage(id))
            {
                if (id == (ushort)InternalClientMessageIDs.CONNECTED)
                {
                    client.connected = true;

                    try
                    {
                        client.GetSpawnData(receievedMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Error getting client spawn data! " + ex);
                    }

                    Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.REQUEST_AUTH);
        
                    int sizeOfMessage = message.WrittenLength;
                    IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
                    System.Runtime.InteropServices.Marshal.Copy(message.Bytes, 0, intPtrMessage, sizeOfMessage);
        
                    Debug.Log("Sending auth request to client...");
                    SendDataToClient(intPtrMessage, client.connection, sizeOfMessage, SendType.Reliable);
                }
        
                else if (id == (ushort)InternalClientMessageIDs.AUTH_TICKET)
                {
                    ReceivedAuthenticationDataFromClient(client, receievedMessage);
                }
            }
        }
        
        private static void ReceivedAuthenticationDataFromClient(Client client, Message message)
        {
            if (!IsServer) return;
        
            SteamId steamId = message.GetSteamId();
            int length = message.GetInt();
        
            if (steamId == 0)
            {
                Debug.Log("Received SteamID was zero.");
                client.Destroy();
                return;
            }

            if (unverifiedSteamIDToConnID.ContainsKey(steamId))
            {
                Debug.LogWarning($"Found pre-existing auth mapping for {steamId.SteamName()}!");
                // Right now, we dont know which (if any) are the true steam ID


                //Client other = clientsPendingAuth[unverifiedSteamIDToConnID[steamId]];
                //if (other != client)
                //{
                //    other.Destroy();
                //
                //}
            }

            unverifiedSteamIDToConnID[steamId] = client.connection.Id;
        
            SteamUser.BeginAuthSession(message.GetByteArray(length), steamId);
        }
        
        private static void UserAuthenticated(SteamId id)
        {
            if (!IsServer) return;

            Debug.Log("Successfully authenticated " + id.SteamName());

            uint connID = unverifiedSteamIDToConnID[id];
            Client client = clientsPendingAuth[connID];
            client.OnAuthorized(id);
            OnClientConnected?.Invoke(client);
            // Connect user
        }
        
        private static void UserNotAuthenticated(SteamId id, AuthResponse response)
        {
            if (!IsServer) return;

            Debug.Log($"Received bad authentication for {id.SteamName()}: {response}");

            uint connID = unverifiedSteamIDToConnID[id];
            Client client = clientsPendingAuth[connID];
            client.Destroy();
            // Disconnect user
        }


        private void DisconnectOldConnections()
        {
            List<uint> invalidIDs = clientsPendingAuth.Where(pair => Time.realtimeSinceStartup -
                pair.Value.TimeCreated > UNAUTHENTICATED_CLIENT_TIMEOUT)
                         .Select(pair => pair.Key)
                         .ToList();

            foreach (uint connID in invalidIDs)
            {
                Client client = clientsPendingAuth[connID];
                Debug.Log($"Removing connection {client.connection.Id}, supposed SteamID {new Friend(client.SteamID)} ({client.SteamID})");
                client.Destroy();
            }
        }

        #region Inspector Methods

        [ContextMenu("Host Server")]
        public void Inspector_HostServer()
        {
            Host();
        }

        [ContextMenu("Log Clients")]
        public void Inspector_LogClients()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            builder.AppendLine("Authenticated clients: ");
            foreach (Client client in clients.Values)
            {
                builder.AppendLine("\n  -" + client.SteamName);
            }

            builder.AppendLine();

            builder.AppendLine("ConnID -> SteamID mapping table: ");
            foreach (KeyValuePair<uint, SteamId> id in connIDToSteamID)
            {
                builder.AppendLine($"  -{id.Key} -> {id.Value.SteamName()}");
            }



            builder.AppendLine();
            builder.AppendLine();

            builder.AppendLine("Clients pending authentication: ");
            foreach (Client client in clientsPendingAuth.Values)
            {
                builder.AppendLine("\n  -" + client.connection.Id);
            }

            builder.AppendLine();
            builder.AppendLine("Supposed SteamID -> ConnID mapping table: ");
            foreach (KeyValuePair<SteamId, uint> id in unverifiedSteamIDToConnID)
            {
                builder.AppendLine($"  -{id.Key.SteamName()} -> {id.Value}");
            }

            Debug.Log(builder.ToString());

            builder.Clear();
        }

        #endregion

        /// <summary>
        /// Used for using a custom client type. By default, returns new Client(). Override with new Player(), or whatever you are using.
        /// </summary>
        /// <returns></returns>
        protected virtual Client GetClient()
        {
            return new Client();
        }

        /// <summary>
        /// Add any data you want to have on the server-side version of your client. Character type, favourite colour, etc. (Called client side)
        /// </summary>
        /// <param name="message">The message to add data to</param>
        protected virtual void AddSpawnData(Message message) { }
    }

    public enum SteamLobbyPrivacyMode
    {
        PUBLIC,
        PRIVATE,
        INVISIBLE,
        FRIENDS_ONLY
    }
}

#region Old Send & Handle
/*

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
*/
#endregion
