using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Net
{
    [CreateAssetMenu(menuName = "VVSteamNetworking/Network Transform Settings")]
    public class NetworkTransformSettings : ScriptableObject
    {
        /*
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
        */

        [HideInInspector] public bool foldout = false;
        [HideInInspector] public bool posFold = false;
        [HideInInspector] public bool rotFold = false;
        [HideInInspector] public bool scaleFold = false;

        public PositionSettings position;
        public RotationSettings rotation;
        public ScaleSettings scale;

        [System.Serializable]
        public class PositionSettings
        {
            [Tooltip("Should position be synced?")]
            public bool sync = true;
            [Tooltip("Only send position if changes exceed this value.")]
            [Min(0.01f)]
            public float sensitivity = 0.01f;
            [Tooltip("Should position changes be interpolated?")]
            public bool interpolate = true;
            [Tooltip("Sync global position instead of local position?")]
            public bool useGlobal = false;
            // Dont need tooltips because custom inspector

            public bool quantize = false;
            public BitPrecision quantizationPrecision = BitPrecision.Sixteen;
            public Vector3Int quantizationRangeMin = new Vector3Int(-256, -256, -256);
            public Vector3Int quantizationRangeMax = new Vector3Int(256, 256, 256);
            public bool visualizeQuantization = false;

        }

        [System.Serializable]
        public class RotationSettings
        {
            [Tooltip("Should rotation be synced?")]
            public bool sync = true;
            [Tooltip("Only send rotation if angle changes exceed this value.")]
            [Min(0.01f)]
            public float sensitivity = 0.01f;
            [Tooltip("Should rotation changes be interpolated?")]
            public bool interpolate = true;
            [Tooltip("Sync global rotation instead of local rotation?")]
            public bool useGlobal = false;
        }

        [System.Serializable]
        public class ScaleSettings
        {
            [Tooltip("Should scale be synced?")]
            public bool sync = true;
            [Tooltip("Only send scale if changes exceed this value.")]
            [Min(0.01f)]
            public float sensitivity = 0.01f;
            [Tooltip("Should scale changes be interpolated?")]
            public bool interpolate = true;

            public bool quantize = false;
            public BitPrecision quantizationPrecision;
            public Vector3 quantizationSizeMin = new Vector3(0.01f, 0.01f, 0.01f);
            public Vector3 quantizationSizeMax = new Vector3(100f, 100f, 100f);
            public bool visualizeQuantization = false;
        }

        public enum BitPrecision : byte
        {
            Five = 5,       // >> 15/16 bits >> 2 bytes
            Eight = 8,      // >> 24/24 bits >> 3 bytes
            Ten = 10,       // >> 30/32 bits >> 4 bytes
            Thirteen = 13,  // >> 39/40 bits >> 5 bytes
            Sixteen = 16,   // >> 48/48 bits >> 6 bytes
            Eighteen = 18,  // >> 54/56 bits >> 7 bytes
            TwentyOne = 21, // >> 63/64 bits >> 8 bytes
            TwentyFour = 24,// >> 72/72 bits >> 9 bytes
            //TwentySix = 26, // >> 78/80 bits >> 10 bytes
            //TwentyNine = 29,// >> 87/88 bits >> 11 bytes
            // Going past 9 bytes doesnt seem worth it :P
        }
        // Again, PascalCase because it looks nice in editor.

        //[System.Flags]
        //public enum TestFlags
        //{
        //    VALUE_1 = 1 << 0,
        //    VALUE_2 = 1 << 1,
        //    VALUE_3 = 1 << 2,
        //    VALUE_4 = 1 << 3,
        //    VALUE_5 = 1 << 4,
        //}
    }
}
