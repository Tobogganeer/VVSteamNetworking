using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using VirtualVoid.Networking.Steam.LLAPI;

namespace VirtualVoid.Networking.Steam
{
    [RequireComponent(typeof(NetworkID))]
    public class NetworkBehavior : MonoBehaviour
    {
        private NetworkID _networkID;
        public NetworkID networkID
        {
            get
            {
                if (_networkID == null)
                    _networkID = GetComponent<NetworkID>();

                return _networkID;
            }
        }

        private static Dictionary<int, Type> networkBehaviorTypes = new Dictionary<int, Type>();

        public bool IsServer { get
            {
                return SteamManager.IsServer;
            } 
        }

        internal byte ComponentIndex { get; private set; }

        protected virtual void Awake()
        {
            ComponentIndex = (byte)Array.IndexOf(networkID.netBehaviors, this);
            
            if (!networkBehaviorTypes.ContainsKey(GetType().Name.GetStableHashCode()))
            {
                networkBehaviorTypes.Add(GetType().Name.GetStableHashCode(), GetType());
            }
        }

        internal static Type GetTypeFromHash(int hash)
        {
            if (!networkBehaviorTypes.TryGetValue(hash, out Type dictType)) return null;
            return dictType;
        }

        /// <summary>
        /// Sends a message to the server version of this object.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendCommand(Message message)
        {
            InternalClientMessages.SendNetworkBehaviorCommand(this, message);
        }

        /// <summary>
        /// Called on the server when a message is received the client with ID <paramref name="from"/>.
        /// </summary>
        /// <param name="message">The message received.</param>
        protected internal virtual void OnCommandReceived(SteamId from, Message message, ushort messageID)
        {

        }

        /// <summary>
        /// Sends a message to the client version of this object.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendRPC(Message message)
        {
            if (!IsServer) return;

            InternalServerMessages.SendNetworkBehaviorRPC(this, message);
        }

        /// <summary>
        /// Sends a message to the client version of this object, but only for the user with ID <paramref name="onlyTo"/>.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendRPC(Message message, SteamId onlyTo)
        {
            if (!IsServer) return;

            InternalServerMessages.SendNetworkBehaviorRPC(this, message, onlyTo);
        }

        /// <summary>
        /// Called on the client when a message is received from the server.
        /// </summary>
        /// <param name="message">The message received.</param>
        protected internal virtual void OnRPCReceived(Message message, ushort messageID)
        {

        }
    }
}
