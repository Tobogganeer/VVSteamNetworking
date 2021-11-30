using System.Security.Cryptography;
using System;
using UnityEngine;

namespace VirtualVoid.Net
{
    internal static class Util
    {
        public const byte BYTE_LENGTH = 1;
        public const byte BOOL_LENGTH = sizeof(bool);
        public const byte SHORT_LENGTH = sizeof(short);
        public const byte INT_LENGTH = sizeof(int);
        public const byte LONG_LENGTH = sizeof(long);
        public const byte FLOAT_LENGTH = sizeof(float);
        public const byte DOUBLE_LENGTH = sizeof(double);
        public const byte VECTOR2_LENGTH = FLOAT_LENGTH * 2;
        public const byte VECTOR3_LENGTH = FLOAT_LENGTH * 3;
        //public const byte QUATERNION_LENGTH = FLOAT_LENGTH * 4;
        public const byte GUID_LENGTH = 16;


        // These functions are all ripped from Mirror

        internal static int GetStableHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        internal static uint GetRandomUInt()
        {
            // use Crypto RNG to avoid having time based duplicates
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }

        internal static Vector3 Clamp(this Vector3 v, Vector3 min, Vector3 max)
        {
            return new Vector3(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y), Mathf.Clamp(v.z, min.z, max.z));
        }

        /// <summary>
        /// Returns true if <paramref name="obj"/> is a prefab in the project (Not in the scene!)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal static bool IsGameObjectPrefab(GameObject obj)
        {
#if UNITY_EDITOR
            return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj);
#else
            return false;
#endif
        }

        internal static bool IsSceneObjectWithPrefabParent(GameObject gameObject, out GameObject prefab)
        {
            prefab = null;

#if UNITY_EDITOR
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }
            prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif

            if (prefab == null)
            {
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        internal static ushort ID(this InternalClientMessageIDs id) => (ushort)id;
        internal static ushort ID(this InternalServerMessageIDs id) => (ushort)id;

        internal static string SteamName(this Steamworks.SteamId id) => new Steamworks.Friend(id).Name;
    }
}
