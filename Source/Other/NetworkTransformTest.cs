using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using VirtualVoid.Networking.Steam.LLAPI;

namespace VirtualVoid.Networking.Steam
{
    public class NetworkTransformTest : NetworkBehavior
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
                //return elapsed;
                return difference > 0 ? elapsed / difference : 0;
            }
        }

        private readonly TransformSnapshot current = new TransformSnapshot();
        internal readonly TransformSnapshot target = new TransformSnapshot();

        private const float SNAP_THRESHOLD_MULTIPLIER = 10;

        public Transform applyChangesTo;
        public int simulatedLatencyMS = 250;

        private void Start()
        {
            lastPosition = settings.useGlobalPosition ? applyChangesTo.position : applyChangesTo.localPosition;
            lastRotation = settings.useGlobalRotation ? applyChangesTo.rotation : applyChangesTo.localRotation;
            lastScale = applyChangesTo.localScale;

            current.Update(lastPosition, lastRotation, lastScale, Time.time - syncDelay);
            target.Update(lastPosition, lastRotation, lastScale, Time.time);
        }

        private void Update()
        {
            if (syncWithFixedUpdate) return;

            if (Time.time - lastSyncTime > syncTime)
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
            TransformUpdateFlags flags = TransformUpdateFlags.NONE;

            if (settings.syncPosition && HasMoved())
                flags |= TransformUpdateFlags.POSITION;

            if (settings.syncRotation && HasRotated())
                flags |= TransformUpdateFlags.ROTATION;

            if (settings.syncScale && HasScaled())
                flags |= TransformUpdateFlags.SCALE;

            //Debug.Log(flags.ToString()); Works correctly

            if (flags == TransformUpdateFlags.NONE) return;

            this.flags = flags;

            if (flags.HasFlag(TransformUpdateFlags.POSITION)) mesPos = lastPosition;
            if (flags.HasFlag(TransformUpdateFlags.ROTATION)) mesRot = lastRotation;
            if (flags.HasFlag(TransformUpdateFlags.SCALE)) mesScale = lastScale;

            Invoke(nameof(SimulateLatency), simulatedLatencyMS * 0.001f);
        }

        private TransformUpdateFlags flags;
        private Vector3 mesPos;
        private Quaternion mesRot;
        private Vector3 mesScale;

        private void SimulateLatency()
        {
            Vector3 newPos = flags.HasFlag(TransformUpdateFlags.POSITION) ? mesPos : target.position;
            Quaternion newRot = flags.HasFlag(TransformUpdateFlags.ROTATION) ? mesRot : target.rotation;
            Vector3 newScale = flags.HasFlag(TransformUpdateFlags.SCALE) ? mesScale : target.scale;

            OnNewTransformReceived(newPos, newRot, newScale);
        }

        private void UpdateTransform()
        {
            // Interpolate / Snap
            if (settings.syncPosition)
            {
                if (settings.interpolatePosition)
                {
                    if (settings.useGlobalPosition)
                        applyChangesTo.position = Vector3.Lerp(current.position, target.position, currentInterpolation);
                    else
                        //applyChangesTo.localPosition = Vector3.Lerp(applyChangesTo.localPosition, target.position, Time.deltaTime * 10);
                        applyChangesTo.localPosition = Vector3.Lerp(current.position, target.position, currentInterpolation);
                    //Debug.Log(currentInterpolation);
                }
                else
                {
                    if (settings.useGlobalPosition)
                        applyChangesTo.position = target.position;
                    else
                        applyChangesTo.localPosition = target.position;
                }
            }

            if (settings.syncRotation)
            {
                if (settings.interpolateRotation)
                {
                    if (settings.useGlobalRotation)
                        applyChangesTo.rotation = Quaternion.Slerp(current.rotation, target.rotation, currentInterpolation);
                    else
                        applyChangesTo.localRotation = Quaternion.Slerp(current.rotation, target.rotation, currentInterpolation);
                }
                else
                {
                    if (settings.useGlobalRotation)
                        applyChangesTo.rotation = target.rotation;
                    else
                        applyChangesTo.localRotation = target.rotation;
                }
            }

            if (settings.syncScale)
            {
                if (settings.interpolateScale)
                {
                    applyChangesTo.localScale = Vector3.Lerp(current.scale, target.scale, currentInterpolation);
                }
                else
                {
                    applyChangesTo.localScale = target.scale;
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
            float currentTime = current == null ? Time.time - syncDelay : current.time;
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
            else current.Update(settings.useGlobalPosition ? applyChangesTo.position : applyChangesTo.localPosition,
                settings.useGlobalRotation ? applyChangesTo.rotation : applyChangesTo.localRotation, applyChangesTo.localScale, Time.time - syncDelay);
            //current.Update(settings.useGlobalPosition ? applyChangesTo.position : applyChangesTo.localPosition,
            //    settings.useGlobalRotation ? applyChangesTo.rotation : applyChangesTo.localRotation, applyChangesTo.localScale, Time.time - syncDelay);

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
