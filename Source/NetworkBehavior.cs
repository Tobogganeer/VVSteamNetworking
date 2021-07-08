using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking.Steam
{
    [RequireComponent(typeof(NetworkID))]
    public class NetworkBehavior : MonoBehaviour
    {
        public NetworkID networkID { get; private set; }

        private static Dictionary<int, Type> networkBehaviorTypes = new Dictionary<int, Type>();

        public bool IsServer { get
            {
                return SteamManager.IsServer;
            } 
        }

        public byte ComponentIndex { get; private set; }

        protected virtual void Awake()
        {
            networkID = GetComponent<NetworkID>();

            ComponentIndex = (byte)Array.IndexOf(networkID.netBehaviors, this);
            
            if (!networkBehaviorTypes.ContainsKey(GetType().Name.GetStableHashCode()))
            {
                networkBehaviorTypes.Add(GetType().Name.GetStableHashCode(), GetType());
            }
        }

        public static Type GetTypeFromHash(int hash)
        {
            if (!networkBehaviorTypes.TryGetValue(hash, out Type dictType)) return null;
            return dictType;
        }
    }
}
