using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking.Steam.LLAPI
{
    internal class InternalMessages
    {
        internal static void Initialize()
        {
            InternalClientMessages.Initialize();
            InternalServerMessages.Initialize();
        }

        internal static void AddNetworkBehaviorAndIDIntoMessage(ushort messageID, NetworkBehavior networkBehavior, Message message)
        {
            //System.Text.StringBuilder builder = new System.Text.StringBuilder("Before copy: ");
            //for (int i = 0; i < 20; i++)
            //{
            //    builder.Append(message.Bytes[i] + " ");
            //}
            //Debug.Log(builder.ToString());
            //
            //builder.Clear();
            //builder.Append("After copy: ");
            //
            //Array.Copy(message.Bytes, 0, message.Bytes, 7, message.WrittenLength);
            //for (int i = 0; i < 20; i++)
            //{
            //    builder.Append(message.Bytes[i] + " ");
            //}
            //Debug.Log(builder.ToString());

            int totalLength = message.WrittenLength + 7; // copied into pos 7 / shifting entire message 7 to the right

            // Prepend proper message header before other message contents
            // 2 bytes for proper id, 4 bytes for netID, 1 byte for comp index
            Array.Copy(message.Bytes, 0, message.Bytes, 7, message.WrittenLength);

            message.writePos = 0;
            message.Add(messageID);
            message.Add(networkBehavior);
            message.writePos = (ushort)totalLength;

            //builder.Clear();
            //builder.Append("After add data: ");
            //
            //for (int i = 0; i < 20; i++)
            //{
            //    builder.Append(message.Bytes[i] + " ");
            //}
            //Debug.Log(builder.ToString());

            //Array.Copy(SteamManager.tempMessageByteBuffer, 0, message.Bytes, 0, totalLength);
        }
    }

    internal class InternalClientMessages
    {
        internal static void Initialize()
        {
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.PONG, OnServerPong);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.DISCONNECT, OnClientDisconnected);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.SCENE_CHANGE, OnChangeScene);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID, OnNetworkIDSpawn);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.DESTROY_NETWORK_ID, OnNetworkIDDestroy);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.NETWORK_TRANSFORM, OnNetworkTransform);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.NETWORK_ANIMATOR, OnNetworkAnimator);
        }

        #region Send
        internal static void SendPing()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.PING));
        }

        internal static void SendDisconnect()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.DISCONNECTED));
        }

        internal static void SendSceneLoaded()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.SCENE_LOADED));
        }

        internal static void SendNetworkBehaviorCommand(NetworkBehavior networkBehavior, Message message)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalClientMessageIDs.NETWORK_BEHAVIOR_COMMAND, networkBehavior, message);

            SteamManager.SendMessageToServer(message);
        }
        #endregion

        #region Receive
        private static void OnServerPong(Message message)
        {
            NetStats.OnPongReceived();
        }

        private static void OnClientDisconnected(Message message)
        {
            SteamId id = message.GetSteamId();

            if (id == SteamManager.SteamID || id == SteamManager.ServerID)
            {
                SteamManager.LeaveServer();
            }

            // handle other client disconnect?
            // can maybe use event from lobby
        }

        private static void OnChangeScene(Message message)
        {
            if (SteamManager.IsServer) return; // Already changed scene from server method

            NetworkID.ResetNetIDs();

            int buildIndex = message.GetUShort();
            UnityEngine.SceneManagement.SceneManager.LoadScene(buildIndex);
            SendSceneLoaded();
        }

        private static void OnNetworkIDSpawn(Message message)
        {
            if (SteamManager.IsServer) return; // Already spawned object

            NetworkObjectIDMessage spawnMessage = message.GetStruct<NetworkObjectIDMessage>();
            if (spawnMessage.objType == NetworkObjectType.SCENE_OBJECT)
            {
                if (!NetworkID.sceneIDs.TryGetValue(spawnMessage.sceneID, out NetworkID sceneNetID))
                {
                    Debug.Log("Tried to assign netID to scene object, but no scene object with ID " + spawnMessage.sceneID + " was in the dictionary!");
                    return;
                }
                sceneNetID.netID = spawnMessage.netID;
                NetworkID.networkIDs[spawnMessage.netID] = sceneNetID;
            }
            else if (spawnMessage.objType == NetworkObjectType.RUNTIME_OBJECT)
            {
                if (!SteamManager.registeredPrefabs.TryGetValue(spawnMessage.assetID, out GameObject obj))
                {
                    Debug.LogWarning($"Received netID for a prefab with assetID {spawnMessage.netID}, but SteamManager.registeredPrefabs does not contain a prefab with that assetID! Did you register that prefab?");
                    return;
                }

                NetworkID spawnedObjID = UnityEngine.Object.Instantiate(obj).GetComponent<NetworkID>();
                spawnedObjID.netID = spawnMessage.netID;
                NetworkID.networkIDs[spawnMessage.netID] = spawnedObjID;
            }

            //if (spawnMessage.includeTransform)
            //{
            //    if (NetworkID.networkIDs.TryGetValue(spawnMessage.netID, out NetworkID networkID))
            //    {
            //        Transform netIDTransform = networkID.transform;
            //        netIDTransform.localPosition = spawnMessage.localPos;
            //        netIDTransform.localRotation = spawnMessage.localRot;
            //        netIDTransform.localScale = spawnMessage.localScale;
            //    }
            //}
        }

        private static void OnNetworkIDDestroy(Message message)
        {
            if (SteamManager.IsServer) return; // Already destroyed object

            uint netID = message.GetUInt();
            if (!NetworkID.networkIDs.TryGetValue(netID, out NetworkID networkID))
            {
                Debug.LogWarning("Tried to destroy NetworkID with netID " + netID + ", but that netID was not present in the networkIDs dictionary!");
                return;
            }

            UnityEngine.Object.Destroy(networkID);
            NetworkID.networkIDs.Remove(netID);
        }

        private static void OnNetworkTransform(Message message)
        {
            if (SteamManager.IsServer) return;

            NetworkTransform.TransformUpdateFlags flags = (NetworkTransform.TransformUpdateFlags)message.GetByte();
            if (flags == NetworkTransform.TransformUpdateFlags.PARENT)
            {
                NetworkID targetID = message.GetNetworkID();
                if (targetID == null)
                {
                    Debug.LogWarning("Received null NetworkTransform, returning...");
                    return;
                }

                uint parentNetID = message.GetUInt();
                if (parentNetID == 0)
                    targetID.transform.SetParent(null);
                else
                {
                    if (!NetworkID.networkIDs.TryGetValue(parentNetID, out NetworkID newParent))
                    {
                        Debug.Log("Could not find new parent NetworkID!");
                        return;
                    }

                    targetID.transform.SetParent(newParent.transform);
                }

                return;

            }

            NetworkTransform networkTransform = message.GetNetworkBehavior<NetworkTransform>();
            if (networkTransform == null)
            {
                Debug.LogWarning("Received null NetworkTransform, returning...");
                return;
            }

            bool isTargetNull = networkTransform.target == null;

            Vector3 newPos = flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION) ? message.GetVector3() : isTargetNull ? networkTransform.settings.useGlobalPosition ?
                networkTransform.transform.position : networkTransform.transform.localPosition : networkTransform.target.position;
            Quaternion newRot = flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION) ? message.GetQuaternion() : isTargetNull ? networkTransform.settings.useGlobalRotation ?
                networkTransform.transform.rotation : networkTransform.transform.localRotation : networkTransform.target.rotation;
            Vector3 newScale = flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE) ? message.GetVector3() :
                isTargetNull ? networkTransform.transform.localScale : networkTransform.target.scale;

            networkTransform.OnNewTransformReceived(newPos, newRot, newScale);
        }

        private static void OnNetworkAnimator(Message message)
        {
            NetworkAnimator anim = message.GetNetworkBehavior<NetworkAnimator>();
            //NetworkAnimator anim = message.GetNetworkBehavior<NetworkAnimator>();

            if (anim == null)
            {
                Debug.LogWarning("Read null NetworkAnimator!");
                return;
            }

            anim.OnCommandsReceived(message);
        }
        #endregion
    }

    internal class InternalServerMessages
    {
        internal static void Initialize()
        {
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.PING, OnClientPing);
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.DISCONNECTED, OnClientDisconnect);
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.SCENE_LOADED, OnClientSceneLoaded);
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.NETWORK_BEHAVIOR_COMMAND, OnNetworkBehaviorCommand);
        }

        #region Send
        internal static void SendPong(SteamId id)
        {
            if (SteamManager.IsServer)
                SteamManager.SendMessageToClient(id, Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.PONG));
        }

        internal static void SendClientDisconnected(SteamId id)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.DISCONNECT).Add(id));
        }

        internal static void SendChangeScene(int buildIndex)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.SCENE_CHANGE).Add((ushort)buildIndex));
            // could send a byte but trying to account for lots of scenes + scenes dont change that often so the performance here is fine
        }

        internal static void SendNetworkIDSpawn(NetworkID networkID)
        {
            NetworkObjectIDMessage spawnMessage = new NetworkObjectIDMessage(networkID);//, !networkID.gameObject.isStatic);
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID).Add(spawnMessage));
        }

        internal static void SendNetworkIDSpawn(NetworkID networkID, SteamId onlyTo)
        {
            NetworkObjectIDMessage spawnMessage = new NetworkObjectIDMessage(networkID);//, !networkID.gameObject.isStatic);
            SteamManager.SendMessageToClient(onlyTo, Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID).Add(spawnMessage));
        }

        internal static void SendNetworkIDDestroy(NetworkID networkID)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.DESTROY_NETWORK_ID).Add(networkID.netID));
        }

        internal static void SendNetworkTransform(NetworkTransform networkTransform, NetworkTransform.TransformUpdateFlags flags)
        {
            Message message = Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.NETWORK_TRANSFORM);

            message.Add((byte)flags);
            message.Add(networkTransform);
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION)) message.Add(networkTransform.lastPosition);
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION)) message.Add(networkTransform.lastRotation);
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE)) message.Add(networkTransform.lastScale);

            SteamManager.SendMessageToAllClients(message);
        }

        internal static void SendNetworkBehaviorRPC(NetworkBehavior networkBehavior, Message message)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalServerMessageIDs.NETWORK_BEHAVIOR_RPC, networkBehavior, message);

            SteamManager.SendMessageToAllClients(message);
        }

        internal static void SendNetworkBehaviorRPC(NetworkBehavior networkBehavior, Message message, SteamId onlyTo)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalServerMessageIDs.NETWORK_BEHAVIOR_RPC, networkBehavior, message);

            SteamManager.SendMessageToClient(onlyTo, message);
        }
        #endregion

        #region Receive
        private static void OnClientPing(SteamId clientSteamID, Message message)
        {
            SendPong(clientSteamID);
        }

        private static void OnClientDisconnect(SteamId clientSteamID, Message message)
        {
            SteamManager.DisconnectClient(clientSteamID);
        }

        private static void OnClientSceneLoaded(SteamId clientSteamID, Message message)
        {
            SteamManager.ClientSceneLoaded(clientSteamID);
        }

        private static void OnNetworkBehaviorCommand(SteamId clientSteamID, Message message)
        {
            NetworkBehavior networkBehavior = message.GetNetworkBehavior<NetworkBehavior>();
            if (networkBehavior == null)
            {
                Debug.LogWarning("Tried to read NetworkBehavior from command, but could not!");
                return;
            }

            ushort messageID = message.GetUShort();

            networkBehavior.OnCommandReceived(clientSteamID, message, messageID);
        }
        #endregion
    }

    internal struct NetworkObjectIDMessage : INetworkMessage
    {
        public NetworkObjectType objType;
        public uint netID;
        public uint sceneID;
        public Guid assetID;

        //public bool includeTransform;
        //public Vector3 localPos;
        //public Quaternion localRot;
        //public Vector3 localScale;

        public NetworkObjectIDMessage(NetworkID networkID)//, bool includeTransform)
        {
            this.objType = NetworkObjectType.RUNTIME_OBJECT;
            this.netID = networkID.netID;
            this.sceneID = networkID.sceneID;
            this.assetID = networkID.assetID;

            //this.includeTransform = includeTransform;
            //
            //Transform transform = networkID.transform;
            //localPos = transform.localPosition;
            //localRot = transform.localRotation;
            //localScale = transform.localScale;

            if (this.sceneID != 0)
            {
                if (this.assetID != Guid.Empty)
                {
                    Debug.LogWarning("Object " + networkID.name + " has a sceneID and an assetID!");
                    return;
                }
                this.objType = NetworkObjectType.SCENE_OBJECT;
            }
        }

        public void AddToMessage(Message message)
        {
            message.Add((byte)objType);
            message.Add(netID);

            switch (objType)
            {
                case NetworkObjectType.SCENE_OBJECT:
                    message.Add(sceneID);
                    break;
                case NetworkObjectType.RUNTIME_OBJECT:
                    message.Add(assetID);
                    break;
            }

            //message.Add(includeTransform);
            //if (includeTransform)
            //{
            //    message.Add(localPos);
            //    message.Add(localRot);
            //    message.Add(localScale);
            //}
        }

        public void Deserialize(Message message)
        {
            objType = (NetworkObjectType)message.GetByte();
            netID = message.GetUInt();

            switch (objType)
            {
                case NetworkObjectType.SCENE_OBJECT:
                    sceneID = message.GetUInt();
                    assetID = Guid.Empty;
                    break;
                case NetworkObjectType.RUNTIME_OBJECT:
                    sceneID = 0;
                    assetID = message.GetGuid();
                    break;
            }

            //includeTransform = message.GetBool();
            //if (includeTransform)
            //{
            //    localPos = message.GetVector3();
            //    localRot = message.GetQuaternion();
            //    localScale = message.GetVector3();
            //}
        }

        public byte GetMaxSize()
        {
            return Util.BYTE_LENGTH + Util.INT_LENGTH + Util.LONG_LENGTH + Util.BOOL_LENGTH + Util.VECTOR3_LENGTH + Util.VECTOR3_LENGTH + Util.VECTOR3_LENGTH;
            // type + netid + guid + includeTransform + pos + rot (euler) + scale
        }
    }

    internal enum NetworkObjectType : byte
    {
        SCENE_OBJECT,
        RUNTIME_OBJECT
    }
}
