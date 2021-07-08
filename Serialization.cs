using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace VirtualVoid.Networking.Steam.LLAPI
{
    internal static class Serialization
    {
        //internal static byte[] SerializeINetworkMessage<T>(T str) where T : struct, INetworkMessage
        //{
        //    return str.Serialize();
        //}

        internal static T DeserializeINetworkMessage<T>(byte[] bytes, ushort bytesOffset) where T : struct, INetworkMessage
        {
            T str = default;
            str.Deserialize(new ArraySegment<byte>(bytes, bytesOffset, str.GetSize()));
            return str;
        }

        internal static T DeserializeINetworkMessage<T>(ArraySegment<byte> bytesSegment) where T : struct, INetworkMessage
        {
            T str = default;
            str.Deserialize(bytesSegment);
            return str;
        }


        /// <summary>
        /// Serializes an unmanaged struct.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static byte[] SerializeUnmanagedStruct<T>(T str) where T : unmanaged
        {
            var size = Marshal.SizeOf(typeof(T));
            var array = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, array, 0, size);
            }
            catch (System.Exception)
            {
                Debug.Log("Error serializing struct of type " + typeof(T) + "!");
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return array;
        }

        /// <summary>
        /// Deserializes an unmanaged struct.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="struct"></param>
        /// <returns></returns>
        internal static T DeserializeUnmanagedStruct<T>(byte[] array) where T : unmanaged
        {
            var size = Marshal.SizeOf(typeof(T));
            var ptr = Marshal.AllocHGlobal(size);

            T s;

            try
            {
                Marshal.Copy(array, 0, ptr, size);
                s = (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            catch (System.Exception)
            {
                Debug.Log("Error deserializing struct of type " + typeof(T) + "!");
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return s;
        }

        /// <summary>
        /// Serializes an unmanaged, non-blittable struct (eg. no arrays) with one less allocation than the other method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static byte[] SerializeUnmanagedStructNonBlittable<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);

            byte[] arr = new byte[size];

            GCHandle h = default;

            try
            {
                h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                Marshal.StructureToPtr<T>(str, h.AddrOfPinnedObject(), false);
            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return arr;
        }

        /// <summary>
        /// Deserializes an unmanaged, non-blittable struct (eg. no arrays) with one less allocation than the other method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="struct"></param>
        /// <returns></returns>
        internal static T DeserializeUnmanagedStructNonBlittable<T>(byte[] array) where T : struct
        {
            T str = default;

            GCHandle h = default;

            try
            {
                h = GCHandle.Alloc(array, GCHandleType.Pinned);

                str = Marshal.PtrToStructure<T>(h.AddrOfPinnedObject());

            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return str;
        }
    }
}
