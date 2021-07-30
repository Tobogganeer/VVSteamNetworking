using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using VirtualVoid.Networking.Steam.LLAPI;
using Steamworks;
using Steamworks.Data;

namespace VirtualVoid.Networking.Steam
{
    /// <summary>Represents a packet.</summary>
    public class Message
    {
        /// <summary>The message instance used for sending user messages.</summary>
        private static readonly Message send = new Message();
        /// <summary>The message instance used for sending internal messages.</summary>
        private static readonly Message sendInternal = new Message(MAX_INTERNAL_MESSAGE_SIZE);
        /// <summary>The message instance used for handling user messages.</summary>
        private static readonly Message handle = new Message();
        /// <summary>The message instance used for handling internal messages.</summary>
        private static readonly Message handleInternal = new Message(MAX_INTERNAL_MESSAGE_SIZE);

        /// <summary>The length in bytes of the data that can be read from the message.</summary>
        public int ReadableLength { get; private set; }
        /// <summary>The length in bytes of the unread data contained in the message.</summary>
        public int UnreadLength => ReadableLength - readPos;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        public int WrittenLength => writePos;
        /// <summary>How many more bytes can be written into the packet.</summary>
        private int UnwrittenLength => Bytes.Length - writePos;
        /// <summary>The message's send mode.</summary>
        public P2PSend SendType { get; private set; }
        /// <summary>The message's data.</summary>
        internal byte[] Bytes { get; set; }

        private const ushort LOWER_INTERNAL_ID = 2560;
        private const ushort UPPER_INTERNAL_ID = 2585;

        private const ushort MAX_INTERNAL_MESSAGE_SIZE = 256;

        /// <summary>The position in the byte array that the next bytes will be written to.</summary>
        private ushort writePos = 0;
        /// <summary>The position in the byte array that the next bytes will be read from.</summary>
        private ushort readPos = 0;

        public static bool IsInternalMessage(ushort id) => id >= LOWER_INTERNAL_ID && id <= UPPER_INTERNAL_ID;

        #region Constructors

        /// <summary>Initializes a reusable Message instance.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        private Message(ushort maxSize = 1500)
        {
            Bytes = new byte[maxSize];
        }

        /// <summary>Initializes a reusable Message instance with a pre-defined header type.</summary>
        /// <param name="maxSize">The maximum amount of bytes the message can contain.</param>
        /// <param name="sendType">The mode in which the message should be sent.</param>
        private Message(P2PSend sendType, ushort maxSize = 1500)
        {
            Bytes = new byte[maxSize];

            SendType = sendType;
        }

        /// <summary>Reinitializes the Message instance used for sending.</summary>
        /// <param name="sendType">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        public static Message Create(P2PSend sendType, ushort id)
        {
            if (IsInternalMessage(id))
            {
                // Should really never be called!
                Debug.LogWarning($"Tried to create message with ID {id}, but IDs in range {LOWER_INTERNAL_ID}-{UPPER_INTERNAL_ID} are used internally! Please use IDs outside this range!");
                return null;
            }

            Reinitialize(send, sendType);
            send.Add(id);
            return send;
        }

        /// <summary>Reinitializes the Message instance used for handling.</summary>
        /// <param name="sendType">The mode in which the message should be sent.</param>
        /// <param name="data">The bytes contained in the message.</param>
        /// <param name="dataLength">The length of the data that should be copied. 0 for all the data.</param>
        /// <returns>A message instance ready to be used for handling.</returns>
        internal static Message Create(P2PSend sendType, byte[] data)
        {
            Reinitialize(handle, sendType, data);
            return handle;
        }

        /// <summary>Reinitializes the Message instance used for sending internal messages.</summary>
        /// <param name="sendType">The mode in which the message should be sent.</param>
        /// <param name="id">The message ID.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        internal static Message CreateInternal(P2PSend sendType, ushort id)
        {
            if (!IsInternalMessage(id))
            {
                // Should really never be called!
                Debug.LogWarning($"Tried to create internal message with ID {id}, but IDs in range {LOWER_INTERNAL_ID}-{UPPER_INTERNAL_ID} are used internally! Please use an ID in this range!");
                return null;
            }

            Reinitialize(sendInternal, sendType);
            sendInternal.Add(id);
            return sendInternal;
        }

        /// <summary>Reinitializes the Message instance used for sending internal messages.</summary>
        /// <param name="headerType">The message's header type.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        private static Message CreateInternal(P2PSend sendType)
        {
            Reinitialize(sendInternal, sendType);
            return sendInternal;
        }

        /// <summary>Reinitializes the Message instance used for handling internal messages.</summary>
        /// <param name="sendType">The message's send type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        /// <returns>A message instance ready to be used for sending.</returns>
        private static Message CreateInternal(P2PSend sendType, byte[] data)
        {
            Reinitialize(handleInternal, sendType, data);
            return handleInternal;
        }

        /// <summary>Reinitializes a message for sending.</summary>
        /// <param name="message">The message to initialize.</param>
        /// <param name="sendType">The message's send type.</param>
        private static void Reinitialize(Message message, P2PSend sendType)
        {
            message.SendType = sendType;
            message.writePos = 0;
            message.readPos = 0;
        }

        /// <summary>Reinitializes a message for handling.</summary>
        /// <param name="message">The message to initialize.</param>
        /// <param name="sendType">The message's send type.</param>
        /// <param name="data">The bytes contained in the message.</param>
        private static void Reinitialize(Message message, P2PSend sendType, byte[] data)
        {
            message.SendType = sendType;
            message.writePos = (ushort)data.Length;
            message.readPos = 0;

            if (data.Length > message.Bytes.Length)
            {
                Debug.LogError($"Can't fully handle {data.Length} bytes because it exceeds the maximum of {message.Bytes.Length}, message will contain incomplete data!");
                Array.Copy(data, 0, message.Bytes, 0, message.Bytes.Length);
                message.ReadableLength = message.Bytes.Length;
            }
            else
            {
                Array.Copy(data, 0, message.Bytes, 0, data.Length);
                message.ReadableLength = data.Length;
            }
        }

        #endregion

        #region Functions

        /// <summary>Resets the internal write position so the message be reused. Header type and send mode remain unchanged, but message contents can be rewritten.</summary>
        private void Reuse()
        {
            writePos = 0;
            readPos = 0;
        }
        #endregion

        #region Add & Retrieve Data
        #region Byte
        /// <summary>Adds a single <see langword="byte"/> to the message.</summary>
        /// <param name="value">The <see langword="byte"/> to add.</param>
        /// <returns>The Message instance that the <see langword="byte"/> was added to.</returns>
        public Message Add(byte value)
        {
            if (UnwrittenLength < 1)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte'!");

            Bytes[writePos++] = value;
            return this;
        }

        /// <summary>Retrieves a <see langword="byte"/> from the message.</summary>
        /// <returns>The <see langword="byte"/> that was retrieved.</returns>
        public byte GetByte()
        {
            if (UnreadLength < 1)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte', returning 0!");
                return 0;
            }

            return Bytes[readPos++]; // Get the byte at readPos' position
        }

        /// <summary>Adds a <see langword="byte"/> array to the message.</summary>
        /// <param name="value">The <see langword="byte"/> array to add.</param>
        /// <returns>The Message instance that the <see langword="byte"/> array was added to.</returns>
        public Message Add(byte[] value)
        {
            if (UnwrittenLength < value.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'byte[]'!");

            Array.Copy(value, 0, Bytes, writePos, value.Length);
            writePos += (ushort)value.Length;
            return this;
        }

        /// <summary>Retrieves a <see langword="byte"/> array from the message.</summary>
        /// <param name="length">The length of the <see langword="byte"/> array.</param>
        /// <returns>The <see langword="byte"/> array that was retrieved.</returns>
        public byte[] GetByteArray(int length)
        {
            byte[] value = new byte[length];

            if (UnreadLength < length)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'byte[]', array will contain default elements!");
                length = UnreadLength;
            }

            Array.Copy(Bytes, readPos, value, 0, length); // Copy the bytes at readPos' position to the array that will be returned
            readPos += (ushort)length;
            return value;
        }
        #endregion

        #region Bool
        /// <summary>Adds a <see langword="bool"/> to the message.</summary>
        /// <param name="value">The <see langword="bool"/> to add.</param>
        /// <returns>The Message instance that the <see langword="bool"/> was added to.</returns>
        public Message Add(bool value)
        {
            if (UnwrittenLength < Util.BOOL_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool'!");

            Bytes[writePos++] = value ? (byte)1 : (byte)0;
            return this;
        }

        /// <summary>Retrieves a <see langword="bool"/> from the message.</summary>
        /// <returns>The <see langword="bool"/> that was retrieved.</returns>
        public bool GetBool()
        {
            if (UnreadLength < Util.BOOL_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool', returning false!");
                return false;
            }

            return Bytes[readPos++] == 1; // Convert the byte at readPos' position to a bool
        }

        /// <summary>Adds a <see langword="bool"/> array to the message.</summary>
        /// <param name="array">The <see langword="bool"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="bool"/> array was added to.</returns>
        public Message Add(bool[] array, bool includeLength = true)
        {
            ushort byteLength = (ushort)(array.Length / 8 + (array.Length % 8 == 0 ? 0 : 1));
            if (UnwrittenLength < byteLength)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'bool[]'!");

            if (includeLength)
                Add((ushort)array.Length);

            BitArray bits = new BitArray(array);
            bits.CopyTo(Bytes, writePos);
            writePos += byteLength;
            return this;
        }

        /// <summary>Retrieves a <see langword="bool"/> array from the message.</summary>
        /// <returns>The <see langword="bool"/> array that was retrieved.</returns>
        public bool[] GetBoolArray()
        {
            return GetBoolArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="bool"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="bool"/> array that was retrieved.</returns>
        public bool[] GetBoolArray(ushort length)
        {
            ushort byteLength = (ushort)(length / 8 + (length % 8 == 0 ? 0 : 1));
            if (UnreadLength < byteLength)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'bool[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.SHORT_LENGTH);
            }

            BitArray bits = new BitArray(GetByteArray(byteLength));
            bool[] array = new bool[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = bits.Get(i);

            return array;
        }
        #endregion

        #region Short & UShort
        /// <summary>Adds a <see langword="short"/> to the message.</summary>
        /// <param name="value">The <see langword="short"/> to add.</param>
        /// <returns>The Message instance that the <see langword="short"/> was added to.</returns>
        public Message Add(short value)
        {
            if (UnwrittenLength < Util.SHORT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short'!");

            Write((ushort)value);
            return this;
        }

        /// <summary>Adds a <see langword="ushort"/> to the message.</summary>
        /// <param name="value">The <see langword="ushort"/> to add.</param>
        /// <returns>The Message instance that the <see langword="ushort"/> was added to.</returns>
        public Message Add(ushort value)
        {
            if (UnwrittenLength < Util.SHORT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort'!");

            Write(value);
            return this;
        }

        /// <summary>Converts a given <see langword="ushort"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see langword="ushort"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(ushort value)
        {
#if BIG_ENDIAN
        Bytes[writePos + 1] = (byte)value;
        Bytes[writePos    ] = (byte)(value >> 8);
#else
            Bytes[writePos] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
#endif
            writePos += Util.SHORT_LENGTH;
        }

        /// <summary>Retrieves a <see langword="short"/> from the message.</summary>
        /// <returns>The <see langword="short"/> that was retrieved.</returns>
        public short GetShort()
        {
            if (UnreadLength < Util.SHORT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short', returning 0!");
                return 0;
            }

            return (short)ReadUShort(); // Convert the bytes at readPos' position to a short
        }

        /// <summary>Retrieves a <see langword="ushort"/> from the message.</summary>
        /// <returns>The <see langword="ushort"/> that was retrieved.</returns>
        public ushort GetUShort()
        {
            if (UnreadLength < Util.SHORT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort', returning 0!");
                return 0;
            }

            return ReadUShort(); // Convert the bytes at readPos' position to a ushort
        }

        /// <summary>Retrieves a <see langword="ushort"/> from the next 2 bytes, starting at the read position.</summary>
        /// <returns>The converted <see langword="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort()
        {
#if BIG_ENDIAN
        ushort value = (ushort)(Bytes[readPos + 1] | (Bytes[readPos    ] << 8));
#else
            ushort value = (ushort)(Bytes[readPos] | (Bytes[readPos + 1] << 8));
#endif
            readPos += Util.SHORT_LENGTH;
            return value;
        }

        /// <summary>Retrieves a <see langword="ushort"/> from the message without moving the read position, allowing the same bytes to be read again.</summary>
        internal ushort PeekUShort()
        {
            if (UnreadLength < Util.SHORT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to peek type 'ushort', returning 0!");
                return 0;
            }

#if BIG_ENDIAN
        return (ushort)((Bytes[readPos + 1] << 8) | Bytes[readPos]); // Convert the bytes to a ushort
#else
            return (ushort)(Bytes[readPos] | (Bytes[readPos + 1] << 8)); // Convert the bytes to a ushort
#endif
        }

        /// <summary>Adds a <see langword="short"/> array to the message.</summary>
        /// <param name="array">The <see langword="short"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="short"/> array was added to.</returns>
        public Message Add(short[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.SHORT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'short[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see langword="ushort"/> array to the message.</summary>
        /// <param name="array">The <see langword="ushort"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="ushort"/> array was added to.</returns>
        public Message Add(ushort[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.SHORT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ushort[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see langword="short"/> array from the message.</summary>
        /// <returns>The <see langword="short"/> array that was retrieved.</returns>
        public short[] GetShortArray()
        {
            return GetShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="short"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="short"/> array that was retrieved.</returns>
        public short[] GetShortArray(ushort length)
        {
            short[] array = new short[length];

            if (UnreadLength < length * Util.SHORT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'short[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.SHORT_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetShort();

            return array;
        }

        /// <summary>Retrieves a <see langword="ushort"/> array from the message.</summary>
        /// <returns>The <see langword="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray()
        {
            return GetUShortArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="ushort"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="ushort"/> array that was retrieved.</returns>
        public ushort[] GetUShortArray(ushort length)
        {
            ushort[] array = new ushort[length];

            if (UnreadLength < length * Util.SHORT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ushort[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.SHORT_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUShort();

            return array;
        }
        #endregion

        #region Int & UInt
        /// <summary>Adds an <see langword="int"/> to the message.</summary>
        /// <param name="value">The <see langword="int"/> to add.</param>
        /// <returns>The Message instance that the <see langword="int"/> was added to.</returns>
        public Message Add(int value)
        {
            if (UnwrittenLength < Util.INT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int'!");

            Write(value);
            return this;
        }

        /// <summary>Adds a <see langword="uint"/> to the message.</summary>
        /// <param name="value">The <see langword="uint"/> to add.</param>
        /// <returns>The Message instance that the <see langword="uint"/> was added to.</returns>
        public Message Add(uint value)
        {
            if (UnwrittenLength < Util.INT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint'!");

            Write((int)value);
            return this;
        }

        /// <summary>Converts a given <see langword="int"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see langword="int"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(int value)
        {
#if BIG_ENDIAN
        Bytes[writePos + 3] = (byte)value;
        Bytes[writePos + 2] = (byte)(value >> 8);
        Bytes[writePos + 1] = (byte)(value >> 16);
        Bytes[writePos    ] = (byte)(value >> 24);
#else
            Bytes[writePos] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
            Bytes[writePos + 2] = (byte)(value >> 16);
            Bytes[writePos + 3] = (byte)(value >> 24);
#endif
            writePos += Util.INT_LENGTH;
        }

        /// <summary>Retrieves an <see langword="int"/> from the message.</summary>
        /// <returns>The <see langword="int"/> that was retrieved.</returns>
        public int GetInt()
        {
            if (UnreadLength < Util.INT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int', returning 0!");
                return 0;
            }

            return ReadInt(); // Convert the bytes at readPos' position to an int
        }

        /// <summary>Retrieves a <see langword="uint"/> from the message.</summary>
        /// <returns>The <see langword="uint"/> that was retrieved.</returns>
        public uint GetUInt()
        {
            if (UnreadLength < Util.INT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint', returning 0!");
                return 0;
            }

            return (uint)ReadInt(); // Convert the bytes at readPos' position to a uint
        }

        /// <summary>Retrieves an <see langword="int"/> from the next 4 bytes, starting at the read position.</summary>
        /// <returns>The converted <see langword="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadInt()
        {
#if BIG_ENDIAN
        int value = Bytes[readPos + 3] | (Bytes[readPos + 2] << 8) | (Bytes[readPos + 1] << 16) | (Bytes[readPos    ] << 32);
#else
            int value = Bytes[readPos] | (Bytes[readPos + 1] << 8) | (Bytes[readPos + 2] << 16) | (Bytes[readPos + 3] << 24);
#endif
            readPos += Util.INT_LENGTH;
            return value;
        }

        /// <summary>Adds an <see langword="int"/> array message.</summary>
        /// <param name="array">The <see langword="int"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="int"/> array was added to.</returns>
        public Message Add(int[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.INT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'int[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see langword="uint"/> array to the message.</summary>
        /// <param name="array">The <see langword="uint"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="uint"/> array was added to.</returns>
        public Message Add(uint[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.INT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'uint[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves an <see langword="int"/> array from the message.</summary>
        /// <returns>The <see langword="int"/> array that was retrieved.</returns>
        public int[] GetIntArray()
        {
            return GetIntArray(GetUShort());
        }
        /// <summary>Retrieves an <see langword="int"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="int"/> array that was retrieved.</returns>
        public int[] GetIntArray(ushort length)
        {
            int[] array = new int[length];

            if (UnreadLength < length * Util.INT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'int[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.INT_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetInt();

            return array;
        }

        /// <summary>Retrieves a <see langword="uint"/> array from the message.</summary>
        /// <returns>The <see langword="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray()
        {
            return GetUIntArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="uint"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="uint"/> array that was retrieved.</returns>
        public uint[] GetUIntArray(ushort length)
        {
            uint[] array = new uint[length];

            if (UnreadLength < length * Util.INT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'uint[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.INT_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetUInt();

            return array;
        }
        #endregion

        #region Long & ULong
        /// <summary>Adds a <see langword="long"/> to the message.</summary>
        /// <param name="value">The <see langword="long"/> to add.</param>
        /// <returns>The Message instance that the <see langword="long"/> was added to.</returns>
        public Message Add(long value)
        {
            if (UnwrittenLength < Util.LONG_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long'!");

            Write(value);
            return this;
        }

        /// <summary>Adds a <see langword="ulong"/> to the message.</summary>
        /// <param name="value">The <see langword="ulong"/> to add.</param>
        /// <returns>The Message instance that the <see langword="ulong"/> was added to.</returns>
        public Message Add(ulong value)
        {
            if (UnwrittenLength < Util.LONG_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong'!");

            Write((long)value);
            return this;
        }

        /// <summary>Converts a given <see langword="long"/> to bytes and adds them to the message's contents.</summary>
        /// <param name="value">The <see langword="long"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(long value)
        {
#if BIG_ENDIAN
        Bytes[writePos + 7] = (byte)value;
        Bytes[writePos + 6] = (byte)(value >> 8);
        Bytes[writePos + 5] = (byte)(value >> 16);
        Bytes[writePos + 4] = (byte)(value >> 24);
        Bytes[writePos + 3] = (byte)(value >> 32);
        Bytes[writePos + 2] = (byte)(value >> 40);
        Bytes[writePos + 1] = (byte)(value >> 48);
        Bytes[writePos    ] = (byte)(value >> 56);
#else
            Bytes[writePos] = (byte)value;
            Bytes[writePos + 1] = (byte)(value >> 8);
            Bytes[writePos + 2] = (byte)(value >> 16);
            Bytes[writePos + 3] = (byte)(value >> 24);
            Bytes[writePos + 4] = (byte)(value >> 32);
            Bytes[writePos + 5] = (byte)(value >> 40);
            Bytes[writePos + 6] = (byte)(value >> 48);
            Bytes[writePos + 7] = (byte)(value >> 56);
#endif
            writePos += Util.LONG_LENGTH;
        }

        /// <summary>Retrieves a <see langword="long"/> from the message.</summary>
        /// <returns>The <see langword="long"/> that was retrieved.</returns>
        public long GetLong()
        {
            if (UnreadLength < Util.LONG_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a long
#if BIG_ENDIAN
        Array.Reverse(Bytes, readPos, longLength);
#endif
            long value = BitConverter.ToInt64(Bytes, readPos);
            readPos += Util.LONG_LENGTH;
            return value;
        }

        /// <summary>Retrieves a <see langword="ulong"/> from the message.</summary>
        /// <returns>The <see langword="ulong"/> that was retrieved.</returns>
        public ulong GetULong()
        {
            if (UnreadLength < Util.LONG_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a ulong
#if BIG_ENDIAN
        Array.Reverse(Bytes, readPos, longLength);
#endif
            ulong value = BitConverter.ToUInt64(Bytes, readPos);
            readPos += Util.LONG_LENGTH;
            return value;
        }

        /// <summary>Adds a <see langword="long"/> array to the message.</summary>
        /// <param name="array">The array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="long"/> array was added to.</returns>
        public Message Add(long[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.LONG_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'long[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Adds a <see langword="ulong"/> array to the message.</summary>
        /// <param name="array">The <see langword="ulong"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="ulong"/> array was added to.</returns>
        public Message Add(ulong[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.LONG_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'ulong[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see langword="long"/> array from the message.</summary>
        /// <returns>The <see langword="long"/> array that was retrieved.</returns>
        public long[] GetLongArray()
        {
            return GetLongArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="long"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="long"/> array that was retrieved.</returns>
        public long[] GetLongArray(ushort length)
        {
            long[] array = new long[length];

            if (UnreadLength < length * Util.LONG_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'long[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.LONG_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetLong();

            return array;
        }

        /// <summary>Retrieves a <see langword="ulong"/> array from the message.</summary>
        /// <returns>The <see langword="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray()
        {
            return GetULongArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="ulong"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="ulong"/> array that was retrieved.</returns>
        public ulong[] GetULongArray(ushort length)
        {
            ulong[] array = new ulong[length];

            if (UnreadLength < length * Util.LONG_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'ulong[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.LONG_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetULong();

            return array;
        }
        #endregion

        #region Float
        /// <summary>Adds a <see langword="float"/> to the message.</summary>
        /// <param name="value">The <see langword="float"/> to add.</param>
        /// <returns>The Message instance that the <see langword="float"/> was added to.</returns>
        public Message Add(float value)
        {
            if (UnwrittenLength < Util.FLOAT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float'!");

            FloatConverter converter = new FloatConverter { floatValue = value };
#if BIG_ENDIAN
        Bytes[writePos + 3] = converter.byte0;
        Bytes[writePos + 2] = converter.byte1;
        Bytes[writePos + 1] = converter.byte2;
        Bytes[writePos    ] = converter.byte3;
#else
            Bytes[writePos] = converter.byte0;
            Bytes[writePos + 1] = converter.byte1;
            Bytes[writePos + 2] = converter.byte2;
            Bytes[writePos + 3] = converter.byte3;
#endif
            writePos += Util.FLOAT_LENGTH;
            return this;
        }

        /// <summary>Retrieves a <see langword="float"/> from the message.</summary>
        /// <returns>The <see langword="float"/> that was retrieved.</returns>
        public float GetFloat()
        {
            if (UnreadLength < Util.FLOAT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a float
#if BIG_ENDIAN
        FloatConverter converter = new FloatConverter { byte3 = Bytes[readPos], byte2 = Bytes[readPos + 1], byte1 = Bytes[readPos + 2], byte0 = Bytes[readPos + 3] };
#else
            FloatConverter converter = new FloatConverter { byte0 = Bytes[readPos], byte1 = Bytes[readPos + 1], byte2 = Bytes[readPos + 2], byte3 = Bytes[readPos + 3] };
#endif
            readPos += Util.FLOAT_LENGTH;
            return converter.floatValue;
        }

        /// <summary>Adds a <see langword="float"/> array to the message.</summary>
        /// <param name="array">The <see langword="float"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="float"/> array was added to.</returns>
        public Message Add(float[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.FLOAT_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'float[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see langword="float"/> array from the message.</summary>
        /// <returns>The <see langword="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray()
        {
            return GetFloatArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="float"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="float"/> array that was retrieved.</returns>
        public float[] GetFloatArray(ushort length)
        {
            float[] array = new float[length];

            if (UnreadLength < length * Util.FLOAT_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'float[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.FLOAT_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetFloat();

            return array;
        }
        #endregion

        #region Double
        /// <summary>Adds a <see langword="double"/> to the message.</summary>
        /// <param name="value">The <see langword="double"/> to add.</param>
        /// <returns>The Message instance that the <see langword="double"/> was added to.</returns>
        public Message Add(double value)
        {
            if (UnwrittenLength < Util.DOUBLE_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double'!");

            DoubleConverter converter = new DoubleConverter { doubleValue = value };
#if BIG_ENDIAN
        Bytes[writePos + 7] = converter.byte0;
        Bytes[writePos + 6] = converter.byte1;
        Bytes[writePos + 5] = converter.byte2;
        Bytes[writePos + 4] = converter.byte3;
        Bytes[writePos + 3] = converter.byte4;
        Bytes[writePos + 2] = converter.byte5;
        Bytes[writePos + 1] = converter.byte6;
        Bytes[writePos    ] = converter.byte7;
#else
            Bytes[writePos] = converter.byte0;
            Bytes[writePos + 1] = converter.byte1;
            Bytes[writePos + 2] = converter.byte2;
            Bytes[writePos + 3] = converter.byte3;
            Bytes[writePos + 4] = converter.byte4;
            Bytes[writePos + 5] = converter.byte5;
            Bytes[writePos + 6] = converter.byte6;
            Bytes[writePos + 7] = converter.byte7;
#endif
            writePos += Util.DOUBLE_LENGTH;
            return this;
        }

        /// <summary>Retrieves a <see langword="double"/> from the message.</summary>
        /// <returns>The <see langword="double"/> that was retrieved.</returns>
        public double GetDouble()
        {
            if (UnreadLength < Util.DOUBLE_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double', returning 0!");
                return 0;
            }

            // Convert the bytes at readPos' position to a double
#if BIG_ENDIAN
        Array.Reverse(Bytes, readPos, doubleLength);
#endif
            double value = BitConverter.ToDouble(Bytes, readPos);
            readPos += Util.DOUBLE_LENGTH;
            return value;
        }

        /// <summary>Adds a <see langword="double"/> array to the message.</summary>
        /// <param name="array">The <see langword="double"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="double"/> array was added to.</returns>
        public Message Add(double[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            if (UnwrittenLength < array.Length * Util.DOUBLE_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'double[]'!");

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a<see langword="double"/> array from the message.</summary>
        /// <returns>The <see langword="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray()
        {
            return GetDoubleArray(GetUShort());
        }
        /// <summary>Retrieves a<see langword="double"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="double"/> array that was retrieved.</returns>
        public double[] GetDoubleArray(ushort length)
        {
            double[] array = new double[length];

            if (UnreadLength < length * Util.DOUBLE_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'double[]', array will contain default elements!");
                length = (ushort)(UnreadLength / Util.DOUBLE_LENGTH);
            }

            for (int i = 0; i < length; i++)
                array[i] = GetDouble();

            return array;
        }
        #endregion

        #region String
        /// <summary>Adds a <see langword="string"/> to the message.</summary>
        /// <param name="value">The <see langword="string"/> to add.</param>
        /// <returns>The Message instance that the <see langword="string"/> was added to.</returns>
        public Message Add(string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            Add((ushort)stringBytes.Length); // Add the length of the string (in bytes) to the message

            if (UnwrittenLength < stringBytes.Length)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'string'!");

            Add(stringBytes); // Add the string itself
            return this;
        }

        /// <summary>Retrieves a <see langword="string"/> from the message.</summary>
        /// <returns>The <see langword="string"/> that was retrieved.</returns>
        public string GetString()
        {
            ushort length = GetUShort(); // Get the length of the string (in bytes, NOT characters)
            if (UnreadLength < length)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'string', result will be truncated!");
                length = (ushort)UnreadLength;
            }

            string value = Encoding.UTF8.GetString(Bytes, readPos, length); // Convert the bytes at readPos' position to a string
            readPos += length;
            return value;
        }

        /// <summary>Adds a <see langword="string"/> array to the message.</summary>
        /// <param name="array">The <see langword="string"/> array to add.</param>
        /// <param name="includeLength">Whether or not to include the length of the array in the message.</param>
        /// <returns>The Message instance that the <see langword="string"/> array was added to.</returns>
        public Message Add(string[] array, bool includeLength = true)
        {
            if (includeLength)
                Add((ushort)array.Length);

            for (int i = 0; i < array.Length; i++)
                Add(array[i]);

            return this;
        }

        /// <summary>Retrieves a <see langword="string"/> array from the message.</summary>
        /// <returns>The <see langword="string"/> array that was retrieved.</returns>
        public string[] GetStringArray()
        {
            return GetStringArray(GetUShort());
        }
        /// <summary>Retrieves a <see langword="string"/> array from the message.</summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>The <see langword="string"/> array that was retrieved.</returns>
        public string[] GetStringArray(ushort length)
        {
            string[] array = new string[length];
            for (int i = 0; i < array.Length; i++)
                array[i] = GetString();

            return array;
        }
        #endregion

        #region Vector2
        public Message Add(Vector2 value)
        {
            if (UnwrittenLength < Util.VECTOR2_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'Vector2'!");

            Add(value.x);
            Add(value.y);

            return this;
        }

        public Vector2 GetVector2()
        {
            if (UnreadLength < Util.VECTOR2_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'Vector2', returning Vector2.zero!");
                return Vector2.zero;
            }

            return new Vector2(GetFloat(), GetFloat());
        }
        #endregion

        #region Vector3
        public Message Add(Vector3 value)
        {
            if (UnwrittenLength < Util.VECTOR3_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'Vector3'!");

            Add(value.x);
            Add(value.y);
            Add(value.z);

            return this;
        }

        public Vector3 GetVector3()
        {
            if (UnreadLength < Util.VECTOR3_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'Vector3', returning Vector3.zero!");
                return Vector3.zero;
            }

            return new Vector3(GetFloat(), GetFloat(), GetFloat());
        }
        #endregion

        #region Quaternion
        public Message Add(Quaternion value)
        {
            if (UnwrittenLength < Util.QUATERNION_LENGTH)
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type 'Quaternion'!");

            Add(value.eulerAngles); // Should switch to smallest three in the future, but idc rn

            return this;
        }

        public Quaternion GetQuaternion()
        {
            if (UnreadLength < Util.QUATERNION_LENGTH)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type 'Quaternion', returning Quaternion.identity!");
                return Quaternion.identity;
            }

            return Quaternion.Euler(GetVector3()); // Should switch to smallest three in the future, but idc rn
        }
        #endregion

        #region Guid
        public Message Add(Guid guid)
        {
            Add(guid.ToByteArray());

            return this;
        }

        public Guid GetGuid()
        {
            return new Guid(GetByteArray(16));
        }
        #endregion

        #region Struct
        public Message Add<T>(T str) where T : struct, INetworkMessage
        {
            if (UnwrittenLength < str.GetMaxSize())
                throw new Exception($"Message has insufficient remaining capacity ({UnwrittenLength}) to add type '" + typeof(T) + "'!");

            //Add(Serialization.SerializeINetworkMessage(str));
            str.AddToMessage(this);
            return this;
        }

        public T GetStruct<T>() where T : struct, INetworkMessage
        {
            byte size = default(T).GetMaxSize();

            if (UnreadLength < size)
            {
                Debug.LogError($"Message contains insufficient unread bytes ({UnreadLength}) to read type '" + typeof(T) + "', returning default(T)!");
                return default;
            }

            readPos += size;
            //return Serialization.DeserializeINetworkMessage<T>(new ArraySegment<byte>(Bytes, readPos - size, size));
            return Serialization.DeserializeINetworkMessage<T>(this);
        }
        #endregion

        #region NetworkID
        public Message Add(NetworkID networkID)
        {
            if (networkID == null)
            {
                Debug.LogWarning("Tried to add NetworkID to message, but it was null!");
                return this;
            }

            Add(networkID.netID);
            return this;
        }

        public NetworkID GetNetworkID()
        {
            uint netID = GetUInt();

            if (!NetworkID.networkIDs.TryGetValue(netID, out NetworkID networkID))
            {
                Debug.LogWarning("Tried to read NetworkID from message, but no NetworkID with netID " + netID + " exists!");
                return null;
            }

            return networkID;
        }
        #endregion

        #region NetworkBehavior
        public Message Add(NetworkBehavior networkBehavior)
        {
            if (networkBehavior == null)
            {
                Debug.LogWarning("Tried to add NetworkBehavior to message, but it was null!");
                return this;
            }

            Add(networkBehavior.networkID);
            Add(networkBehavior.ComponentIndex);

            return this;
        }

        public T GetNetworkBehavior<T>() where T : NetworkBehavior
        {
            NetworkID networkID = GetNetworkID();
            byte compIndex = GetByte();

            if (networkID == null)
            {
                Debug.LogWarning($"Tried to get NetworkBehavior ({nameof(T)}), but could not get NetworkID from the message!");
                return null;
            }

            if (!(networkID.netBehaviors[compIndex] is T comp))
            {
                Debug.LogWarning($"Tried to get NetworkBehavior ({nameof(T)}), but the NetworkBehavior at index {compIndex} was not of type {nameof(T)}!");
                return null;
            }

            return comp;
        }
        #endregion

        public SteamId GetSteamId()
        {
            return GetULong();
        }

        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    struct FloatConverter
    {
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;

        [FieldOffset(0)]
        public float floatValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct DoubleConverter
    {
        [FieldOffset(0)]
        public byte byte0;
        [FieldOffset(1)]
        public byte byte1;
        [FieldOffset(2)]
        public byte byte2;
        [FieldOffset(3)]
        public byte byte3;
        [FieldOffset(4)]
        public byte byte4;
        [FieldOffset(5)]
        public byte byte5;
        [FieldOffset(6)]
        public byte byte6;
        [FieldOffset(7)]
        public byte byte7;

        [FieldOffset(0)]
        public double doubleValue;
    }
}
