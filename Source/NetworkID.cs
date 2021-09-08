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
    // Most of this code is ripped straight from Mirror. https://github.com/vis2k/Mirror
    // The people working on it are smarter than I am and they have figured out all this complex stuff so :/

    [DisallowMultipleComponent]
    public class NetworkID : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("If enabled, NetworkBehaviours can add to the spawn data. Causes _netBehaviors to be loaded.")]
        private bool useSpawnData = true;

        internal bool UseSpawnData => useSpawnData;

        private bool copyOfSceneObj;
        [SerializeField, HideInInspector] private bool hasSpawned;
        public uint netID; //{ get; private set; }
        public uint sceneID; //{ get; private set; }

        private bool destroyed;

        internal static readonly Dictionary<uint, NetworkID> networkIDs = new Dictionary<uint, NetworkID>();
        internal static readonly Dictionary<uint, NetworkID> sceneIDs = new Dictionary<uint, NetworkID>();

        private NetworkBehaviour[] _netBehaviors;

        public NetworkBehaviour[] netBehaviors
        {
            get
            {
                if (_netBehaviors == null)
                {
                    _netBehaviors = GetComponents<NetworkBehaviour>();

                    if (_netBehaviors.Length > byte.MaxValue)
                        throw new IndexOutOfRangeException($"Cannot have more than {byte.MaxValue} NetworkBehaviors on one gameobject! (" + name + ")");
                }

                return _netBehaviors;
            }
        }

        private static uint nextNetworkId = 1;
        internal static uint NextNetID() => nextNetworkId++;

        public static void ResetNetIDs()
        {
            nextNetworkId = 1;
            networkIDs.Clear();
        }

        public Guid assetID
        {
            get
            {
#if UNITY_EDITOR
                // This is important because sometimes OnValidate does not run (like when adding view to prefab with no child links)
                if (string.IsNullOrEmpty(assetIDString))
                    AssignIDs();
#endif
                // convert string to Guid and use .Empty to avoid exception if
                // we would use 'new Guid("")'
                return string.IsNullOrEmpty(assetIDString) ? Guid.Empty : new Guid(assetIDString);
            }
            internal set
            {
                string newAssetIdString = value == Guid.Empty ? string.Empty : value.ToString("N");
                string oldAssetIdSrting = assetIDString;

                // they are the same, do nothing
                if (oldAssetIdSrting == newAssetIdString)
                {
                    return;
                }

                // new is empty
                if (string.IsNullOrEmpty(newAssetIdString))
                {
                    Debug.LogError($"Can not set AssetId to empty guid on NetworkID '{name}', old assetId '{oldAssetIdSrting}'");
                    return;
                }

                // old not empty
                if (!string.IsNullOrEmpty(oldAssetIdSrting))
                {
                    Debug.LogError($"Can not Set AssetId on NetworkIdentity '{name}' because it already had an assetId, current assetId '{oldAssetIdSrting}', attempted new assetId '{newAssetIdString}'");
                    return;
                }

                // old is empty
                assetIDString = newAssetIdString;
                // Debug.Log($"Settings AssetId on NetworkIdentity '{name}', new assetId '{newAssetIdString}'");
            }
        }
        [SerializeField, HideInInspector] string assetIDString;

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
                copyOfSceneObj = true;
                Destroy(gameObject);

                return;
            }
            hasSpawned = true;

            SteamManager.OnServerStart += SteamManager_OnServerStart;
        }

        private void SteamManager_OnServerStart()
        {
            if (!IsServer) return;

            if (netID == 0)
                netID = NextNetID();

            networkIDs[netID] = this;

            SteamManager.SpawnObject(this);
        }

        private void Start()
        {
            if (!IsServer) return;
        
            if (netID == 0)
                netID = NextNetID();
        
            networkIDs[netID] = this;
        
            SteamManager.SpawnObject(this);
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
        private void AssignAssetID(string path) => assetIDString = AssetDatabase.AssetPathToGUID(path);
        private void AssignAssetID(GameObject prefab) => AssignAssetID(AssetDatabase.GetAssetPath(prefab));

        private void AssignIDs()
        {
            if (Util.IsGameObjectPrefab(gameObject))
            {
                // force 0 for prefabs
                sceneID = 0;
                AssignAssetID(gameObject);
            }

            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
                {
                    // force 0 for prefabs
                    sceneID = 0;

                    string path = PrefabStageUtility.GetCurrentPrefabStage().assetPath;

                    AssignAssetID(path);
                }
            }
            else if (Util.IsSceneObjectWithPrefabParent(gameObject, out GameObject prefab))
            {
                AssignSceneID();
                AssignAssetID(prefab);
            }
            else
            {
                AssignSceneID();

                if (!EditorApplication.isPlaying)
                {
                    assetIDString = "";
                }
            }
        }

        private void AssignSceneID()
        {
            bool duplicate = sceneIDs.TryGetValue(sceneID, out NetworkID existing) && existing != null && existing != this;

            if (sceneID == 0 || duplicate)
            {
                sceneID = 0;

                if (BuildPipeline.isBuildingPlayer)
                    throw new InvalidOperationException("Scene " + gameObject.scene.path + " needs to be opened and resaved before building, because the scene object " + name + " has no valid sceneId yet.");

                Undo.RecordObject(this, "Generated SceneID");

                uint newID = Util.GetRandomUInt();

                duplicate = sceneIDs.TryGetValue(newID, out existing) && existing != null && existing != this;

                if (!duplicate)
                {
                    sceneID = newID;
                }
            }

            sceneIDs[sceneID] = this;
        }
#endif

        void OnDestroy()
        {
            if (copyOfSceneObj)
                return;

            if (IsServer && !destroyed)
            {
                SteamManager.OnServerStart -= SteamManager_OnServerStart;
                SteamManager.DestroyObject(this);
                destroyed = true;
            }

            if (networkIDs.ContainsKey(netID)) networkIDs.Remove(netID);
        }

        [ContextMenu("Log IDs")]
        public void LogIDs()
        {
            Debug.Log($"IDs for GameObject {name}\n-SceneID: {sceneID}\n-AssetID: {assetID}\n-NetID: {netID}");
        }
    }
}
