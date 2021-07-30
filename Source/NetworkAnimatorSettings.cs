using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking.Steam
{
    [CreateAssetMenu(menuName = "VVSteamNetworking/Network Animator Settings")]
    public class NetworkAnimatorSettings : ScriptableObject
    {
        [Header("Bool")]
        public bool autoSyncBools = false;
    }
}
