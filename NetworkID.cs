using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
#endif

namespace VirtualVoid.Networking.Steam
{
    public class NetworkID : MonoBehaviour
    {
        private bool spawnedDuringRuntime;
        private bool hasSpawned;
        public uint netID; //{ get; private set; }
        public uint sceneID; //{ get; private set; }

        private bool destroyed;

        private static Dictionary<uint, NetworkID> networkIDs = new Dictionary<uint, NetworkID>();
        private static Dictionary<uint, NetworkID> sceneIDs = new Dictionary<uint, NetworkID>();

        private NetworkBehavior[] _netBehaviors;

        public NetworkBehavior[] netBehaviors
        {
            get
            {
                if (_netBehaviors == null)
                {
                    _netBehaviors = GetComponents<NetworkBehavior>();

                    if (_netBehaviors.Length > byte.MaxValue)
                        throw new IndexOutOfRangeException($"Cannot have more than {byte.MaxValue} NetworkBehaviors on one gameobject! (" + name + ")");
                }

                return _netBehaviors;
            }
        }

        private static uint nextNetworkId = 1;
        internal static uint NextNetID() => nextNetworkId++;

        public static void ResetNetIDs() => nextNetworkId = 1;

        public bool IsServer
        {
            get
            {
                return SteamManager.IsServer;
            }
        }

        private void Awake()
        {
            if (hasSpawned)
            {
                Debug.LogError($"{name} has already spawned. Don't call Instantiate for NetworkIDs that were in the scene since the beginning (aka scene objects). Destroying...");
                spawnedDuringRuntime = true;
                Destroy(gameObject);

                return;
            }
            hasSpawned = true;
        }

        private void Start()
        {
            if (netID == 0)
                netID = NextNetID();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            hasSpawned = false; // OnValidate not called from Instantiate()

#if UNITY_EDITOR
            AssignIDs();
#endif
        }

#if UNITY_EDITOR
        private void AssignIDs()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }
            else
            {
                AssignSceneID();
            }
        }

        private void AssignSceneID()
        {
            bool duplicate = sceneIDs.TryGetValue(sceneID, out NetworkID existing) && existing != null && existing != this;

            if (sceneID == 0 || duplicate)
            {
                sceneID = 0;

                uint newID = Util.GetRandomUInt();

                Undo.RecordObject(this, "Generated SceneID");

                sceneID = Util.GetRandomUInt();

                duplicate = sceneIDs.TryGetValue(newID, out existing) && existing != null && existing != this;

                if (!duplicate)
                {
                    sceneID = newID;
                }
            }

            if (sceneIDs.ContainsKey(sceneID)) sceneIDs[sceneID] = this;
            else sceneIDs.Add(sceneID, this);
        }
#endif

        void OnDestroy()
        {
            if (spawnedDuringRuntime)
                return;

            if (IsServer && !destroyed)
            {
                SteamManager.DestroyObject(this);
                destroyed = true;
            }

            if (networkIDs.ContainsKey(netID)) networkIDs.Remove(netID);
        }

        internal void Destroy()
        {
            if (IsServer && !destroyed) Destroy(gameObject);
        }
    }
}
