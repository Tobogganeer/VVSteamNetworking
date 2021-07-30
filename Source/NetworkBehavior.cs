using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    }
}
