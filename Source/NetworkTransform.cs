using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using VirtualVoid.Networking.Steam.LLAPI;

namespace VirtualVoid.Networking.Steam
{
    public class NetworkTransform : NetworkBehavior
    {
        [Tooltip("Should the NetworkTransform try to sync every FixedUpdate?")]
        public bool syncWithFixedUpdate;
        [Min(0f)]
        [Tooltip("If not syncing with FixedUpdate, try to sync after this amount of time. 0 for instant (not recommended).")]
        public float syncTime = 0.1f;
        public NetworkTransformSettings settings;

        internal Vector3 lastPosition = Vector3.zero;
        internal Quaternion lastRotation = Quaternion.identity;
        internal Vector3 lastScale = Vector3.one;
        private float lastSyncTime = 0;

        private float syncDelay => syncWithFixedUpdate ? Time.fixedDeltaTime : syncTime;
        private float currentInterpolation
        {
            get
            {
                float difference = target.time - current.time;

                float elapsed = Time.time - target.time;
                return difference > 0 ? elapsed / difference : 0;
            }
        }

        private readonly TransformSnapshot current = new TransformSnapshot();
        internal readonly TransformSnapshot target = new TransformSnapshot();

        private const float SNAP_THRESHOLD_MULTIPLIER = 10;

        private void Start()
        {
            lastPosition = settings.useGlobalPosition ? transform.position : transform.localPosition;
            lastRotation = settings.useGlobalRotation ? transform.rotation : transform.localRotation;
            lastScale = transform.localScale;

            current.Update(lastPosition, lastRotation, lastScale, Time.time - syncDelay);
            target.Update(lastPosition, lastRotation, lastScale, Time.time);
        }

        private void Update()
        {
            if (syncWithFixedUpdate) return;

            if (SteamManager.IsServer && Time.time - lastSyncTime > syncTime)
            {
                lastSyncTime = Time.time;
                CheckTransform();
            }

            UpdateTransform();
        }

        private void FixedUpdate()
        {
            if (!syncWithFixedUpdate) return;

            CheckTransform();
            UpdateTransform();
        }

        private void CheckTransform()
        {
            if (!SteamManager.IsServer) return;

            TransformUpdateFlags flags = TransformUpdateFlags.NONE;

            if (settings.syncPosition && HasMoved())
                flags |= TransformUpdateFlags.POSITION;

            if (settings.syncRotation && HasRotated())
                flags |= TransformUpdateFlags.ROTATION;

            if (settings.syncScale && HasScaled())
                flags |= TransformUpdateFlags.SCALE;

            //Debug.Log(flags.ToString()); Works correctly

            if (flags == TransformUpdateFlags.NONE) return;

            InternalServerMessages.SendNetworkTransform(this, flags);
            //Message message = Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.NETWORK_TRANSFORM);
            //
            //message.Add((byte)flags);
            //if (flags.HasFlag(TransformUpdateFlags.POSITION)) message.Add(lastPosition);
            //if (flags.HasFlag(TransformUpdateFlags.ROTATION)) message.Add(lastRotation);
            //if (flags.HasFlag(TransformUpdateFlags.SCALE)) message.Add(lastScale);
            //
            //SteamManager.SendMessageToAllClients(message);
        }

        private void UpdateTransform()
        {
            if (SteamManager.IsServer) return;

            // Interpolate / Snap
            if (settings.syncPosition)
            {
                if (settings.interpolatePosition)
                {
                    if (settings.useGlobalPosition)
                        transform.position = Vector3.Lerp(current.position, target.position, currentInterpolation);
                    else
                        transform.localPosition = Vector3.Lerp(current.position, target.position, currentInterpolation);
                }
                else
                {
                    if (settings.useGlobalPosition)
                        transform.position = target.position;
                    else
                        transform.localPosition = target.position;
                }
            }

            if (settings.syncRotation)
            {
                if (settings.interpolateRotation)
                {
                    if (settings.useGlobalRotation)
                        transform.rotation = Quaternion.Slerp(current.rotation, target.rotation, currentInterpolation);
                    else
                        transform.localRotation = Quaternion.Slerp(current.rotation, target.rotation, currentInterpolation);
                }
                else
                {
                    if (settings.useGlobalRotation)
                        transform.rotation = target.rotation;
                    else
                        transform.localRotation = target.rotation;
                }
            }

            if (settings.syncScale)
            {
                if (settings.interpolateScale)
                {
                    transform.localScale = Vector3.Lerp(current.scale, target.scale, currentInterpolation);
                }
                else
                {
                    transform.localScale = target.scale;
                }
            }
        }

        private bool HasMoved()
        {
            Vector3 currentPos = settings.useGlobalPosition ? transform.position : transform.localPosition;
            bool changed = Vector3.Distance(lastPosition, currentPos) > settings.positionSyncSensitivity;
            if (changed)
                lastPosition = currentPos;

            return changed;
        }

        private bool HasRotated()
        {
            Quaternion currentRot = settings.useGlobalRotation ? transform.rotation : transform.localRotation;
            bool changed = Quaternion.Angle(lastRotation, currentRot) > settings.rotationSyncSensitivity;
            if (changed)
                lastRotation = currentRot;

            return changed;
        }

        private bool HasScaled()
        {
            Vector3 currentScale = transform.localScale;
            bool changed = Vector3.Distance(lastScale, currentScale) > settings.scaleSyncSensitivity;
            if (changed)
                lastScale = currentScale;

            return changed;
        }

        private bool ShouldSnap()
        {
            float currentTime = current == null ? Time.time - (syncWithFixedUpdate ? Time.fixedDeltaTime : syncTime) : current.time;
            float targetTime = target == null ? Time.time : target.time;
            float difference = targetTime - currentTime;
            float timeSinceGoalReceived = Time.time - targetTime;
            return timeSinceGoalReceived > difference * SNAP_THRESHOLD_MULTIPLIER;

            //if (current == null) return true;
            //
            //if (syncWithFixedUpdate)
            //    return target.time - current.time > Time.fixedDeltaTime * SNAP_THRESHOLD_MULTIPLIER;
            //else
            //    return target.time - current.time > syncTime * SNAP_THRESHOLD_MULTIPLIER;
        }

        internal void OnNewTransformReceived(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (ShouldSnap()) current.Update(target.position, target.rotation, target.scale, target.time);
            else current.Update(settings.useGlobalPosition ? transform.position : transform.localPosition,
                settings.useGlobalRotation ? transform.rotation : transform.localRotation, transform.localScale, Time.time - syncDelay);

            target.Update(position, rotation, scale, Time.time);
        }

        internal class TransformSnapshot
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float time;

            public void Update(Vector3 position, Quaternion rotation, Vector3 scale, float time)
            {
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
                this.time = time;
            }
        }

        [System.Flags]
        internal enum TransformUpdateFlags : byte
        {
            NONE = 0,
            POSITION = 1 << 0,
            ROTATION = 1 << 1,
            SCALE = 1 << 2,
        }

        // (targetEnum & Enum.Value) == Enum.Value
        // targetEnum.HasFlag(Enum.Value)
    }
}
