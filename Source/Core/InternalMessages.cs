using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks.Data;

namespace VirtualVoid.Net
{
    internal class InternalMessages
    {
        internal static void AddNetworkBehaviorAndIDIntoMessage(ushort messageID, NetworkBehaviour networkBehavior, Message message)
        {
            int totalLength = message.WrittenLength + 7; // copied into pos 7 / shifting entire message 7 to the right
        
            // Prepend proper message header before other message contents
            // 2 bytes for proper id, 4 bytes for netID, 1 byte for comp index
            Array.Copy(message.Bytes, 0, message.Bytes, 7, message.WrittenLength);
        
            message.writePos = 0;
            message.Add(messageID);
            message.Add(networkBehavior);
            message.writePos = (ushort)totalLength;
        }
        
        internal static void AddIDIntoMessage(ushort messageID, Message message)
        {
            int totalLength = message.WrittenLength + 2; // 2 bytes for id
            Array.Copy(message.Bytes, 0, message.Bytes, 2, message.WrittenLength);
            message.writePos = 0;
            message.Add(messageID);
            message.writePos = (ushort)totalLength;
        }
    }

    internal static class InternalClientSend
    {
        internal static void SendPing()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(SendType.Reliable, (ushort)InternalClientMessageIDs.PING));
        }

        internal static void SendSceneLoaded()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(SendType.Reliable, (ushort)InternalClientMessageIDs.SCENE_LOADED));
        }

        internal static void SendNetworkBehaviorCommand(NetworkBehaviour networkBehavior, Message message)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalClientMessageIDs.NETWORK_BEHAVIOR_COMMAND, networkBehavior, message);
        
            SteamManager.SendMessageToServer(message);
        }

        //internal static void SendClientCommand(Client client, Message message)
        //{
        //    InternalMessages.AddIDIntoMessage((ushort)InternalClientMessageIDs.CLIENT_COMMAND, message);
        //
        //    SteamManager.SendMessageToServer(message);
        //}

        internal static void SendNetworkIDRespawnRequest(uint netID)
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(SendType.Reliable, (ushort)InternalClientMessageIDs.REQUEST_NETWORK_ID_RESPAWN).Add(netID));
        }
    }

    internal static class InternalClientHandle
    {
        static InternalClientHandle()
        {
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.PONG, OnServerPong);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.SCENE_CHANGE, OnChangeScene);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.SPAWN_NETWORK_ID, OnNetworkIDSpawn);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.DESTROY_NETWORK_ID, OnNetworkIDDestroy);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.NETWORK_TRANSFORM, OnNetworkTransform);
            SteamManager.RegisterInternalMessageHandler_FromServer(InternalServerMessageIDs.NETWORK_ANIMATOR, OnNetworkAnimator);
        }

        private static void OnServerPong(Message message)
        {
            NetStats.OnPongReceived();
        }

        private static void OnChangeScene(Message message)
        {
            if (SteamManager.IsServer) return; // Already changed scene from server method

            //NetworkID.ResetNetIDs();

            int buildIndex = message.GetUShort();
            UnityEngine.SceneManagement.SceneManager.LoadScene(buildIndex);
            InternalClientSend.SendSceneLoaded();
        }

        private static void OnNetworkIDSpawn(Message message)
        {
            if (SteamManager.IsServer) return; // Already spawned object
        
            NetworkObjectIDMessage spawnMessage = message.GetStruct<NetworkObjectIDMessage>();
            if (spawnMessage.objType == NetworkObjectType.SCENE_OBJECT)
            {
                if (spawnMessage.sceneID == 0) return;
        
                if (!NetworkID.sceneIDs.TryGetValue(spawnMessage.sceneID, out NetworkID sceneNetID))
                {
                    Debug.Log("Tried to assign netID to scene object, but no scene object with ID " + spawnMessage.sceneID + " was in the dictionary!");
                    return;
                }
                if (sceneNetID == null)
                {
        
                }
        
                sceneNetID.netID = spawnMessage.netID;
                Debug.Log($"Set {sceneNetID.name} netID to {spawnMessage.netID}");
                NetworkID.networkIDs[spawnMessage.netID] = sceneNetID;
            }
            else if (spawnMessage.objType == NetworkObjectType.RUNTIME_OBJECT)
            {
                if (!SteamManager.registeredPrefabs.TryGetValue(spawnMessage.assetID, out GameObject obj))
                {
                    Debug.LogWarning($"Received netID for a prefab with assetID {spawnMessage.assetID}, but SteamManager.registeredPrefabs does not contain a prefab with that assetID! Did you register that prefab?");
                    return;
                }
        
                NetworkID spawnedObjID = UnityEngine.Object.Instantiate(obj).GetComponent<NetworkID>();
                spawnedObjID.netID = spawnMessage.netID;
                NetworkID.networkIDs[spawnMessage.netID] = spawnedObjID;
            }
        
            NetworkID networkID = NetworkID.networkIDs[spawnMessage.netID];
        
            if (!networkID.UseSpawnData) return;
        
            foreach (NetworkBehaviour networkBehaviour in networkID.netBehaviors)
            {
                if (networkBehaviour == null) continue;
        
                try
                {
                    networkBehaviour.GetSpawnData(message);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Caught error trying to get spawn data from {networkBehaviour.GetType().Name} ({networkBehaviour.gameObject.name}): " + ex);
                    break;
                }
            }
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
        
            UnityEngine.Object.Destroy(networkID.gameObject);
            NetworkID.networkIDs.Remove(netID);
        }
        
        private static void OnNetworkTransform(Message message)
        {
            if (SteamManager.IsServer) return;
        
            NetworkTransform.TransformUpdateFlags flags = (NetworkTransform.TransformUpdateFlags)message.GetByte();
            if (flags == NetworkTransform.TransformUpdateFlags.PARENT)
            {
                NetworkID targetID = message.GetNetworkID(out uint netID);
                if (targetID == null)
                {
                    Debug.LogWarning("Received null NetworkTransform, requesting respawn...");
                    InternalClientSend.SendNetworkIDRespawnRequest(netID);
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
        
            NetworkTransform networkTransform = message.GetNetworkBehavior<NetworkTransform>(out uint transformNetID);
            if (networkTransform == null)
            {
                Debug.LogWarning("Received null NetworkTransform, requesting respawn...");
                InternalClientSend.SendNetworkIDRespawnRequest(transformNetID);
                return;
            }
        
            bool isTargetNull = networkTransform.target == null;
        
            NetworkTransformSettings settings = networkTransform.settings;
        
            Vector3 newPos;
            Quaternion newRot;
            Vector3 newScale;
        
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION))
            {
                if (settings.position.quantize)
                    newPos = Compression.GetVector3(message, settings.position.quantizationPrecision, settings.position.quantizationRangeMin, settings.position.quantizationRangeMax);
                else
                    newPos = message.GetVector3();
            }
            else
            {
                if (isTargetNull)
                {
                    if (settings.position.useGlobal)
                        newPos = networkTransform.transform.position;
                    else
                        newPos = networkTransform.transform.localPosition;
                }
                else
                    newPos = networkTransform.target.position;
            }
        
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION))
            {
                newRot = message.GetQuaternion();
            }
            else
            {
                if (isTargetNull)
                {
                    if (settings.rotation.useGlobal)
                        newRot = networkTransform.transform.rotation;
                    else
                        newRot = networkTransform.transform.localRotation;
                }
                else
                    newRot = networkTransform.target.rotation;
            }
        
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE))
            {
                if (settings.scale.quantize)
                    newScale = Compression.GetVector3(message, settings.scale.quantizationPrecision, settings.scale.quantizationSizeMin, settings.scale.quantizationSizeMax);
                else
                    newScale = message.GetVector3();
            }
            else
            {
                if (isTargetNull)
                    newScale = networkTransform.transform.localScale;
                else
                    newScale = networkTransform.target.scale;
            }
        
            //Vector3 newPos = flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION) ? message.GetVector3() : isTargetNull ? networkTransform.settings.position.useGlobal ?
            //    networkTransform.transform.position : networkTransform.transform.localPosition : networkTransform.target.position;
            //Quaternion newRot = flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION) ? message.GetQuaternion() : isTargetNull ? networkTransform.settings.rotation.useGlobal ?
            //    networkTransform.transform.rotation : networkTransform.transform.localRotation : networkTransform.target.rotation;
            //Vector3 newScale = flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE) ? message.GetVector3() :
            //    isTargetNull ? networkTransform.transform.localScale : networkTransform.target.scale;
        
            networkTransform.OnNewTransformReceived(newPos, newRot, newScale);
        }
        
        private static void OnNetworkAnimator(Message message)
        {
            if (SteamManager.IsServer) return;
        
            NetworkAnimator anim = message.GetNetworkBehavior<NetworkAnimator>(out uint netID);
            //NetworkAnimator anim = message.GetNetworkBehavior<NetworkAnimator>();
        
            if (anim == null)
            {
                Debug.LogWarning("Read null NetworkAnimator! Requesting respawn...");
                InternalClientSend.SendNetworkIDRespawnRequest(netID);
                return;
            }
        
            anim.OnCommandsReceived(message);
        }
    }


    internal static class InternalServerSend
    {
        internal static void SendPong(SteamId id)
        {
            if (SteamManager.IsServer)
                SteamManager.SendMessageToClient(id, Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.PONG));
        }

        internal static void SendChangeScene(int buildIndex)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.SCENE_CHANGE).Add((ushort)buildIndex));
        }

        private static Message GenNetworkIDSpawnMessage(NetworkID networkID)
        {
            if (networkID == null) return null;
        
            Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID);
        
            NetworkObjectIDMessage spawnMessage = new NetworkObjectIDMessage(networkID);//, !networkID.gameObject.isStatic);
        
            message.Add(spawnMessage);
            //message.Add((byte)networkID.netBehaviors.Length);
        
            if (!networkID.UseSpawnData) return message;
        
            foreach (NetworkBehaviour networkBehaviour in networkID.netBehaviors)
            {
                if (networkBehaviour == null) continue;
        
                try
                {
                    //message.Add(networkBehaviour);
                    networkBehaviour.AddSpawnData(message);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Caught error trying to add spawn data from {networkBehaviour.gameObject.name}: " + ex);
                    break;
                }
            }
        
            return message;
        }
        
        internal static void SendNetworkIDSpawn(NetworkID networkID)
        {
            Message message = GenNetworkIDSpawnMessage(networkID);
        
            if (message == null) return;
        
            SteamManager.SendMessageToAllClients(Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID));
        }
        
        internal static void SendNetworkIDSpawn(NetworkID networkID, SteamId onlyTo)
        {
            Message message = GenNetworkIDSpawnMessage(networkID);
        
            if (message == null) return;
        
            SteamManager.SendMessageToClient(onlyTo, Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.SPAWN_NETWORK_ID));
        }
        
        internal static void SendNetworkIDDestroy(NetworkID networkID)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.DESTROY_NETWORK_ID).Add(networkID.netID));
        }
        

        internal static Message GetNetworkTransformMessage(NetworkTransform networkTransform, NetworkTransform.TransformUpdateFlags flags)
        {
            if (networkTransform == null || networkTransform.settings == null) return null;

            Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.NETWORK_TRANSFORM);
            NetworkTransformSettings settings = networkTransform.settings;

            message.Add((byte)flags);
            message.Add(networkTransform);
            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION))
            {
                if (settings.position.quantize)
                {
                    Compression.AddVector3(message, networkTransform.lastPosition,
                        settings.position.quantizationPrecision, settings.position.quantizationRangeMin, settings.position.quantizationRangeMax);
                }
                else
                {
                    message.Add(networkTransform.lastPosition);
                }
            }

            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION))
                message.Add(networkTransform.lastRotation);

            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE))
            {
                if (settings.scale.quantize)
                {
                    Compression.AddVector3(message, networkTransform.lastScale,
                        settings.scale.quantizationPrecision, settings.scale.quantizationSizeMin, settings.scale.quantizationSizeMax);
                }
                else
                {
                    message.Add(networkTransform.lastScale);
                }
            }

            return message;
        }

        internal static void SendNetworkTransform(NetworkTransform networkTransform, NetworkTransform.TransformUpdateFlags flags)
        {
            SteamManager.SendMessageToAllClients(GetNetworkTransformMessage(networkTransform, flags));
        }

        internal static void SendNetworkTransform(NetworkTransform networkTransform, NetworkTransform.TransformUpdateFlags flags, SteamId except)
        {
            SteamManager.SendMessageToAllClients(except, GetNetworkTransformMessage(networkTransform, flags));
        }


        internal static void SendNetworkBehaviorRPC(NetworkBehaviour networkBehavior, Message message)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalServerMessageIDs.NETWORK_BEHAVIOR_RPC, networkBehavior, message);
        
            SteamManager.SendMessageToAllClients(message);
        }
        
        internal static void SendNetworkBehaviorRPC(NetworkBehaviour networkBehavior, Message message, SteamId onlyTo)
        {
            InternalMessages.AddNetworkBehaviorAndIDIntoMessage((ushort)InternalServerMessageIDs.NETWORK_BEHAVIOR_RPC, networkBehavior, message);
        
            SteamManager.SendMessageToClient(onlyTo, message);
        }
    }

    internal static class InternalServerHandle
    {
        static InternalServerHandle()
        {
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.PING, OnClientPing);
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.SCENE_LOADED, OnClientSceneLoaded);
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.NETWORK_BEHAVIOR_COMMAND, OnNetworkBehaviorCommand);
            //SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.CLIENT_COMMAND, OnClientCommand);
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.REQUEST_NETWORK_ID_RESPAWN, OnRequestNetworkIDRespawn);
            SteamManager.RegisterInternalMessageHandler_FromClient(InternalClientMessageIDs.C_NETWORK_TRANSFORM, OnC_NetworkTransform);
        }

        private static void OnClientPing(SteamId clientSteamID, Message message)
        {
            InternalServerSend.SendPong(clientSteamID);
        }

        private static void OnClientSceneLoaded(SteamId clientSteamID, Message message)
        {
            SteamManager.ClientSceneLoaded(clientSteamID);
        }

        private static void OnNetworkBehaviorCommand(SteamId clientSteamID, Message message)
        {
            NetworkBehaviour networkBehavior = message.GetNetworkBehavior<NetworkBehaviour>();
            if (networkBehavior == null)
            {
                Debug.LogWarning("Tried to read NetworkBehavior from command, but could not!");
                return;
            }
        
            ushort messageID = message.GetUShort();
        
            networkBehavior.OnCommandReceived(clientSteamID, message, messageID);
        }

        //private static void OnClientCommand(SteamId clientSteamID, Message message)
        //{
        //    if (!SteamManager.clients.TryGetValue(clientSteamID, out Client client))
        //    {
        //        Debug.LogWarning($"Received message from {new Friend(clientSteamID).Name}, but they are not in the clients dictionary!");
        //        return;
        //    }
        //    
        //    ushort messageID = message.GetUShort();
        //    
        //    client.OnCommandReceived(message, messageID);
        //}

        internal static void OnRequestNetworkIDRespawn(SteamId clientSteamID, Message message)
        {
            uint id = message.GetUInt();
        
            if (NetworkID.networkIDs.TryGetValue(id, out NetworkID netID))
            {
                InternalServerSend.SendNetworkIDSpawn(netID, clientSteamID);
            }
            else
            {
                Debug.Log($"{new Friend(clientSteamID).Name} requested a respawn of netID {id}, but that netID doesn't exist!");
            }
        }

        internal static void OnC_NetworkTransform(SteamId clientSteamID, Message message)
        {
            NetworkTransform.TransformUpdateFlags flags = (NetworkTransform.TransformUpdateFlags)message.GetByte();
            if (flags == NetworkTransform.TransformUpdateFlags.PARENT)
            {
                NetworkID targetID = message.GetNetworkID();
                if (targetID == null)
                {
                    Debug.LogWarning("Received null NetworkTransform!");
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
                Debug.LogWarning("Received null NetworkTransform!");
                return;
            }

            bool isTargetNull = networkTransform.target == null;

            NetworkTransformSettings settings = networkTransform.settings;

            Vector3 newPos;
            Quaternion newRot;
            Vector3 newScale;

            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION))
            {
                if (settings.position.quantize)
                    newPos = Compression.GetVector3(message, settings.position.quantizationPrecision, settings.position.quantizationRangeMin, settings.position.quantizationRangeMax);
                else
                    newPos = message.GetVector3();
            }
            else
            {
                if (isTargetNull)
                {
                    if (settings.position.useGlobal)
                        newPos = networkTransform.transform.position;
                    else
                        newPos = networkTransform.transform.localPosition;
                }
                else
                    newPos = networkTransform.target.position;
            }

            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION))
            {
                newRot = message.GetQuaternion();
            }
            else
            {
                if (isTargetNull)
                {
                    if (settings.rotation.useGlobal)
                        newRot = networkTransform.transform.rotation;
                    else
                        newRot = networkTransform.transform.localRotation;
                }
                else
                    newRot = networkTransform.target.rotation;
            }

            if (flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE))
            {
                if (settings.scale.quantize)
                    newScale = Compression.GetVector3(message, settings.scale.quantizationPrecision, settings.scale.quantizationSizeMin, settings.scale.quantizationSizeMax);
                else
                    newScale = message.GetVector3();
            }
            else
            {
                if (isTargetNull)
                    newScale = networkTransform.transform.localScale;
                else
                    newScale = networkTransform.target.scale;
            }

            //Vector3 newPos = flags.HasFlag(NetworkTransform.TransformUpdateFlags.POSITION) ? message.GetVector3() : isTargetNull ? networkTransform.settings.position.useGlobal ?
            //    networkTransform.transform.position : networkTransform.transform.localPosition : networkTransform.target.position;
            //Quaternion newRot = flags.HasFlag(NetworkTransform.TransformUpdateFlags.ROTATION) ? message.GetQuaternion() : isTargetNull ? networkTransform.settings.rotation.useGlobal ?
            //    networkTransform.transform.rotation : networkTransform.transform.localRotation : networkTransform.target.rotation;
            //Vector3 newScale = flags.HasFlag(NetworkTransform.TransformUpdateFlags.SCALE) ? message.GetVector3() :
            //    isTargetNull ? networkTransform.transform.localScale : networkTransform.target.scale;

            networkTransform.OnNewTransformReceived(newPos, newRot, newScale);
        }
    }

    internal struct NetworkObjectIDMessage : INetworkMessage
    {
        public NetworkObjectType objType;
        public uint netID;
        public uint sceneID;
        public Guid assetID;
    
        public NetworkObjectIDMessage(NetworkID networkID)
        {
            this.objType = NetworkObjectType.RUNTIME_OBJECT;
            this.netID = networkID.netID;
            this.sceneID = networkID.sceneID;
            this.assetID = networkID.assetID;
    
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
        }
    }

    internal enum NetworkObjectType : byte
    {
        SCENE_OBJECT,
        RUNTIME_OBJECT
    }
}
