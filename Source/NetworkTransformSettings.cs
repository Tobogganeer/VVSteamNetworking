using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking.Steam
{
    [CreateAssetMenu(menuName = "VVSteamNetworking/Network Transform Settings")]
    public class NetworkTransformSettings : ScriptableObject
    {
        [Header("Position")]
        [Tooltip("Should the position of this object be synced?")]
        public bool syncPosition = true;
        [Tooltip("Position will only be sent if the changes exceed this value.")]
        public float positionSyncSensitivity = 0.01f;
        [Tooltip("Should position updates be smoothly interpolated, rather than snapping immediately?")]
        public bool interpolatePosition = true;
        [Tooltip("Should the global position be used rather than the local position?")]
        public bool useGlobalPosition;

        [Header("Rotation")]
        [Tooltip("Should the rotation of this object be synced?")]
        public bool syncRotation = true;
        [Tooltip("Rotation will only be sent if the angle change exceeds this value.")]
        public float rotationSyncSensitivity = 0.01f;
        [Tooltip("Should rotation updates be smoothly interpolated, rather than snapping immediately?")]
        public bool interpolateRotation = true;
        [Tooltip("Should the global rotation be used rather than the local rotation?")]
        public bool useGlobalRotation;

        [Header("Scale")]
        [Tooltip("Should the scale of this object be synced?")]
        public bool syncScale = true;
        [Tooltip("Scale will only be sent if the changes exceed this value.")]
        public float scaleSyncSensitivity = 0.01f;
        [Tooltip("Should scale updates be smoothly interpolated, rather than snapping immediately?")]
        public bool interpolateScale = true;
    }
}
