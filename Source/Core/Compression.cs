using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using BitPrecision = VirtualVoid.Net.NetworkTransformSettings.BitPrecision;

namespace VirtualVoid.Net
{
    public static class Compression
    {
        //public static void AddQuaternion(Message message, Quaternion quaternion)
        //{
        //    message.Add(Rotation.Compress(quaternion));
        //}

        //public static Quaternion GetQuaternion(Message message)
        //{
        //    return Rotation.Decompress(message.GetUInt());
        //}

        public static void AddVector3(Message message, Vector3 value, BitPrecision precision, Vector3 min, Vector3 max)
        {
            if (value.x < min.x || value.y < min.y || value.z < min.z)
                Debug.LogWarning($"Tried to quantize {value}, but one or more values were outside the min range ({min})! Clamping...");

            if (value.x > max.x || value.y > max.y || value.z > max.z)
                Debug.LogWarning($"Tried to quantize {value}, but one or more values were outside the max range ({max})! Clamping...");

            value = value.Clamp(min, max);

            // Disregarding bit packing for now (still size benefits for <= 16 bits, precision for <= 32 bits)

            if (precision <= BitPrecision.Eight)
            {
                message.Add(Vector.Quantize_8bit(value.x, min.x, max.x, (int)precision));
                message.Add(Vector.Quantize_8bit(value.y, min.y, max.y, (int)precision));
                message.Add(Vector.Quantize_8bit(value.z, min.z, max.z, (int)precision));

                return;
            }

            if (precision <= BitPrecision.Sixteen)
            {
                message.Add(Vector.Quantize_16bit(value.x, min.x, max.x, (int)precision));
                message.Add(Vector.Quantize_16bit(value.y, min.y, max.y, (int)precision));
                message.Add(Vector.Quantize_16bit(value.z, min.z, max.z, (int)precision));

                return;
            }

            if (precision <= BitPrecision.TwentyFour)
            {
                message.Add(Vector.Quantize_32bit(value.x, min.x, max.x, (int)precision));
                message.Add(Vector.Quantize_32bit(value.y, min.y, max.y, (int)precision));
                message.Add(Vector.Quantize_32bit(value.z, min.z, max.z, (int)precision));

                return;
            }
        }

        public static Vector3 GetVector3(Message message, BitPrecision precision, Vector3 min, Vector3 max)
        {
            float x = 0;
            float y = 0;
            float z = 0;

            if (precision <= BitPrecision.Eight)
            {
                x = Vector.Dequantize(message.GetByte(), min.x, max.x, (int)precision);
                y = Vector.Dequantize(message.GetByte(), min.y, max.y, (int)precision);
                z = Vector.Dequantize(message.GetByte(), min.z, max.z, (int)precision);

                return new Vector3(x, y, z);
            }

            if (precision <= BitPrecision.Sixteen)
            {
                x = Vector.Dequantize(message.GetUShort(), min.x, max.x, (int)precision);
                y = Vector.Dequantize(message.GetUShort(), min.y, max.y, (int)precision);
                z = Vector.Dequantize(message.GetUShort(), min.z, max.z, (int)precision);

                return new Vector3(x, y, z);
            }

            else if (precision <= BitPrecision.TwentyFour)
            {
                x = Vector.Dequantize(message.GetUInt(), min.x, max.x, (int)precision);
                y = Vector.Dequantize(message.GetUInt(), min.y, max.y, (int)precision);
                z = Vector.Dequantize(message.GetUInt(), min.z, max.z, (int)precision);

                return new Vector3(x, y, z);
            }

            return new Vector3(x, y, z);
        }

        public static class Rotation
        {
            // Taken from https://gist.github.com/fversnel/0497ad7ab3b81e0dc1dd

            private const float Minimum = -1.0f / 1.414214f; // note: 1.0f / sqrt(2)
            private const float Maximum = +1.0f / 1.414214f;

            public static uint Compress(Quaternion rotation)
            {
                float absX = Mathf.Abs(rotation.x),
                    absY = Mathf.Abs(rotation.y),
                    absZ = Mathf.Abs(rotation.z),
                    absW = Mathf.Abs(rotation.w);

                var largestComponent = new LargestComponent(ComponentType.X, absX);
                if (absY > largestComponent.Value)
                {
                    largestComponent.Value = absY;
                    largestComponent.ComponentType = ComponentType.Y;
                }
                if (absZ > largestComponent.Value)
                {
                    largestComponent.Value = absZ;
                    largestComponent.ComponentType = ComponentType.Z;
                }
                if (absW > largestComponent.Value)
                {
                    largestComponent.Value = absW;
                    largestComponent.ComponentType = ComponentType.W;
                }

                float a, b, c;
                switch (largestComponent.ComponentType)
                {
                    case ComponentType.X:
                        if (rotation.x >= 0)
                        {
                            a = rotation.y;
                            b = rotation.z;
                            c = rotation.w;
                        }
                        else
                        {
                            a = -rotation.y;
                            b = -rotation.z;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.Y:
                        if (rotation.y >= 0)
                        {
                            a = rotation.x;
                            b = rotation.z;
                            c = rotation.w;
                        }
                        else
                        {
                            a = -rotation.x;
                            b = -rotation.z;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.Z:
                        if (rotation.z >= 0)
                        {
                            a = rotation.x;
                            b = rotation.y;
                            c = rotation.w;
                        }
                        else
                        {
                            a = -rotation.x;
                            b = -rotation.y;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.W:
                        if (rotation.w >= 0)
                        {
                            a = rotation.x;
                            b = rotation.y;
                            c = rotation.z;
                        }
                        else
                        {
                            a = -rotation.x;
                            b = -rotation.y;
                            c = -rotation.z;
                        }
                        break;
                    default:
                        // Should never happen!
                        throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                              largestComponent.ComponentType);
                }

                float normalizedA = (a - Minimum) / (Maximum - Minimum),
                    normalizedB = (b - Minimum) / (Maximum - Minimum),
                    normalizedC = (c - Minimum) / (Maximum - Minimum);

                uint integerA = (uint)Mathf.FloorToInt(normalizedA * 1024.0f + 0.5f),
                    integerB = (uint)Mathf.FloorToInt(normalizedB * 1024.0f + 0.5f),
                    integerC = (uint)Mathf.FloorToInt(normalizedC * 1024.0f + 0.5f);

                return (((uint)largestComponent.ComponentType) << 30) | (integerA << 20) | (integerB << 10) | integerC;
            }

            public static Quaternion Decompress(uint compressedRotation)
            {
                var largestComponentType = (ComponentType)(compressedRotation >> 30);
                uint integerA = (compressedRotation >> 20) & ((1 << 10) - 1),
                    integerB = (compressedRotation >> 10) & ((1 << 10) - 1),
                    integerC = compressedRotation & ((1 << 10) - 1);

                float a = integerA / 1024.0f * (Maximum - Minimum) + Minimum,
                    b = integerB / 1024.0f * (Maximum - Minimum) + Minimum,
                    c = integerC / 1024.0f * (Maximum - Minimum) + Minimum;

                Quaternion rotation;
                switch (largestComponentType)
                {
                    case ComponentType.X:
                        // (?) y z w
                        rotation.y = a;
                        rotation.z = b;
                        rotation.w = c;
                        rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                                   - rotation.z * rotation.z
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.Y:
                        // x (?) z w
                        rotation.x = a;
                        rotation.z = b;
                        rotation.w = c;
                        rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.z * rotation.z
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.Z:
                        // x y (?) w
                        rotation.x = a;
                        rotation.y = b;
                        rotation.w = c;
                        rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.y * rotation.y
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.W:
                        // x y z (?)
                        rotation.x = a;
                        rotation.y = b;
                        rotation.z = c;
                        rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.y * rotation.y
                                                   - rotation.z * rotation.z);
                        break;
                    default:
                        // Should never happen!
                        throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                              largestComponentType);
                }

                return rotation;
            }

            private enum ComponentType : uint
            {
                X = 0,
                Y = 1,
                Z = 2,
                W = 3
            }

            private struct LargestComponent
            {
                public ComponentType ComponentType;
                public float Value;

                public LargestComponent(ComponentType componentType, float value)
                {
                    ComponentType = componentType;
                    Value = value;
                }
            }
        }

        public static class Vector
        {
            public static byte Quantize_8bit(float value, float min, float max, int bitLength)
            {
                return (byte)((value - min) / (max - min) * Mathf.Pow(2, bitLength));
            }

            public static ushort Quantize_16bit(float value, float min, float max, int bitLength)
            {
                return (ushort)((value - min) / (max - min) * Mathf.Pow(2, bitLength));
            }

            public static uint Quantize_32bit(float value, float min, float max, int bitLength)
            {
                return (uint)((value - min) / (max - min) * Mathf.Pow(2, bitLength));
            }

            public static float Dequantize(byte value, float min, float max, int bitLength)
            {
                return Dequantize((uint)value, min, max, bitLength);
            }

            public static float Dequantize(ushort value, float min, float max, int bitLength)
            {
                return Dequantize((uint)value, min, max, bitLength);
            }

            public static float Dequantize(uint value, float min, float max, int bitLength)
            {
                return value / Mathf.Pow(2, bitLength) * (max - min) + min;
            }

            private struct BitVector5
            {
                public bool bit1;
                public bool bit2;
                public bool bit3;
                public bool bit4;
                public bool bit5;
            }

            private struct BitVector5_3
            {
                public BitVector5 vector1;
                public BitVector5 vector2;
                public BitVector5 vector3;

                public ushort GetValue()
                {
                    ushort returnValue = 0;
                    Shift(ref returnValue, ref vector1);
                    Shift(ref returnValue, ref vector2);
                    Shift(ref returnValue, ref vector3);

                    return returnValue;
                }

                public void SetValue(Vector3 value, Vector3 min, Vector3 max)
                {

                }

                private void Shift(ref ushort value, ref BitVector5 vector)
                {
                    value |= (ushort)(vector.bit1 ? 1 : 0);
                    value <<= 1;

                    value |= (ushort)(vector.bit2 ? 1 : 0);
                    value <<= 1;

                    value |= (ushort)(vector.bit3 ? 1 : 0);
                    value <<= 1;

                    value |= (ushort)(vector.bit4 ? 1 : 0);
                    value <<= 1;

                    value |= (ushort)(vector.bit5 ? 1 : 0);
                    value <<= 1;
                }
            }

            /*

            EXAMPLE:
            Range = [-1, 1]
            Steps = 20

            GET QUANT (0.12)

            Subtract the bottom of the range from the number: 0.12 - (-1) = 1.12
            Divide by the size of the range: 1.12 / 2 = 0.56
            Multiply by the number of discrete values: 0.56 * 20 = 11.2
            Round to the nearest integer: 11


            GET VALUE (11)

            Divide the quantized number by the number of discrete values: 11 / 20 = 0.55
            Multiply by the size of the range: 0.55 * 2 = 1.1
            Add the bottom of the range: 1.1 + (-1) = 0.1
            */
        }
    }
}
