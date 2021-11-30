using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks.Data;

namespace VirtualVoid.Net
{
    public class NetworkAnimator : NetworkBehaviour
    {
        [Tooltip("When should this try to sync?")]
        public SyncUpdateLoop updateLoop;

        private bool syncsWithFixedUpdate => updateLoop == SyncUpdateLoop.FixedUpdate;

        [Min(0f)]
        [Tooltip("If not syncing with FixedUpdate, try to sync this many times per second. 0 for every frame (not recommended).")]
        public int syncsPerSecond = 0;
        private int lastSyncsPerSecond = 0;
        private float syncTime = 0.1f;
        public Animator target;
        //public NetworkAnimatorSettings settings;

        private float lastSyncTime = 0;

        private readonly Dictionary<int, AnimFloatBuffer> floatBuffers = new Dictionary<int, AnimFloatBuffer>(4);
        private readonly Dictionary<int, AnimBoolBuffer> boolBuffers = new Dictionary<int, AnimBoolBuffer>(4);
        private readonly Dictionary<int, AnimIntBuffer> intBuffers = new Dictionary<int, AnimIntBuffer>(4);
        private readonly Dictionary<int, AnimTriggerBuffer> triggerBuffers = new Dictionary<int, AnimTriggerBuffer>(4);
        private readonly Dictionary<int, AnimPlayBuffer> playBuffers = new Dictionary<int, AnimPlayBuffer>(4);

        // This entire class smells of unoptimization, 20 buffers?

        private void Start()
        {
            if (syncsPerSecond == 0) syncTime = 0; // Avoid division by zero
            else syncTime = 1f / syncsPerSecond;
        }

        private void Update()
        {
            if (syncsPerSecond != lastSyncsPerSecond)
            {
                lastSyncsPerSecond = syncsPerSecond;
                if (syncsPerSecond == 0) syncTime = 0; // Avoid division by zero
                else syncTime = 1f / syncsPerSecond;
            }

            if (updateLoop != SyncUpdateLoop.Update) return;

            UpdateTimesForFrameUpdate();
        }

        private void LateUpdate()
        {
            if (updateLoop != SyncUpdateLoop.LateUpdate) return;

            UpdateTimesForFrameUpdate();
        }

        private void UpdateTimesForFrameUpdate()
        {
            if (SteamManager.IsServer && Time.time - lastSyncTime > syncTime)
            {
                lastSyncTime = Time.time;
                SendCachedCommands();
            }
        }

        private void FixedUpdate()
        {
            if (updateLoop != SyncUpdateLoop.FixedUpdate) return;

            SendCachedCommands();
        }

        private void SendCachedCommands()
        {
            // Can possibly use byte
            ushort numCommands = (ushort)(floatBuffers.Keys.Count + boolBuffers.Keys.Count + intBuffers.Keys.Count + triggerBuffers.Keys.Count + playBuffers.Keys.Count);

            if (numCommands == 0) return;

            Message message = Message.CreateInternal(SendType.Reliable, (ushort)InternalServerMessageIDs.NETWORK_ANIMATOR);
            message.Add(this);
            message.Add(numCommands);

            foreach (var key in floatBuffers.Keys)
                message.Add(floatBuffers[key]);

            foreach (var key in boolBuffers.Keys)
                message.Add(boolBuffers[key]);

            foreach (var key in intBuffers.Keys)
                message.Add(intBuffers[key]);

            foreach (var key in triggerBuffers.Keys)
                message.Add(triggerBuffers[key]);

            foreach (var key in playBuffers.Keys)
                message.Add(playBuffers[key]);

            ClearBuffers();

            SteamManager.SendMessageToAllClients(message);
        }

        internal void OnCommandsReceived(Message message)
        {
            //Debug.Log("Received Animation Message of size " + message.WrittenLength);

            if (IsServer) return;

            ushort numCommands = message.GetUShort();

            for (ushort i = 0; i < numCommands; i++)
            {
                NetworkAnimatorFlags flags = (NetworkAnimatorFlags)message.PeekByte();

                if (flags.HasFlag(NetworkAnimatorFlags.FLOAT))
                    ApplyFloatBuffer(message.GetStruct<AnimFloatBuffer>());
                else if (flags.HasFlag(NetworkAnimatorFlags.BOOL))
                    ApplyBoolBuffer(message.GetStruct<AnimBoolBuffer>());
                else if (flags.HasFlag(NetworkAnimatorFlags.INT))
                    ApplyIntBuffer(message.GetStruct<AnimIntBuffer>());
                else if (flags.HasFlag(NetworkAnimatorFlags.TRIGGER))
                    ApplyTriggerBuffer(message.GetStruct<AnimTriggerBuffer>());
                else if (flags.HasFlag(NetworkAnimatorFlags.PLAY))
                    ApplyPlayBuffer(message.GetStruct<AnimPlayBuffer>());
            }
        }

        private void ClearBuffers()
        {
            floatBuffers.Clear();
            boolBuffers.Clear();
            intBuffers.Clear();
            triggerBuffers.Clear();
            playBuffers.Clear();
        }

        private void ApplyFloatBuffer(AnimFloatBuffer buffer)
        {
            if (buffer.flags.HasFlag(NetworkAnimatorFlags.EXTRA))
                target.SetFloat(buffer.id, buffer.value, buffer.dampTime, buffer.deltaTime);
            else
                target.SetFloat(buffer.id, buffer.value);
        }

        private void ApplyBoolBuffer(AnimBoolBuffer buffer)
        {
            target.SetBool(buffer.id, buffer.value);
        }

        private void ApplyIntBuffer(AnimIntBuffer buffer)
        {
            target.SetInteger(buffer.id, buffer.value);
        }

        private void ApplyTriggerBuffer(AnimTriggerBuffer buffer)
        {
            if (buffer.flags.HasFlag(NetworkAnimatorFlags.EXTRA))
                target.ResetTrigger(buffer.id);
            else
                target.SetTrigger(buffer.id);
        }

        private void ApplyPlayBuffer(AnimPlayBuffer buffer)
        {
            if (buffer.flags.HasFlag(NetworkAnimatorFlags.LAYER))
                if (buffer.flags.HasFlag(NetworkAnimatorFlags.EXTRA))
                    target.Play(buffer.id, buffer.layer, buffer.normalizedTime);
                else
                    target.Play(buffer.id, buffer.layer);
            else
                target.Play(buffer.id);
        }

        #region Float

        // FLOAT
        public void SetFloat(int id, float value)
        {
            target.SetFloat(id, value);
            floatBuffers[id] = new AnimFloatBuffer(id, value);
        }

        // FLOAT
        public void SetFloat(string name, float value)
        {
            SetFloat(Animator.StringToHash(name), value);
            //target.SetFloat(name, value);
        }

        // FLOAT, EXTRA
        public void SetFloat(int id, float value, float dampTime, float deltaTime)
        {
            target.SetFloat(id, value, dampTime, deltaTime);
            floatBuffers[id] = new AnimFloatBuffer(id, value, dampTime, deltaTime);
        }

        // FLOAT, EXTRA
        public void SetFloat(string name, float value, float dampTime, float deltaTime)
        {
            SetFloat(Animator.StringToHash(name), value, dampTime, deltaTime);
            //target.SetFloat(name, value, dampTime, deltaTime);
        }

        #endregion

        #region Bool

        // BOOL
        public void SetBool(int id, bool value)
        {
            target.SetBool(id, value);
            boolBuffers[id] = new AnimBoolBuffer(id, value);
        }

        // BOOL
        public void SetBool(string name, bool value)
        {
            SetBool(Animator.StringToHash(name), value);
            //target.SetBool(name, value);
        }

        #endregion

        #region Int

        // INT
        public void SetInteger(int id, int value)
        {
            target.SetInteger(id, value);
            intBuffers[id] = new AnimIntBuffer(id, value);
        }

        // INT
        public void SetInteger(string name, int value)
        {
            SetInteger(Animator.StringToHash(name), value);
            //target.SetInteger(name, value);
        }

        #endregion

        #region Trigger

        // TRIGGER
        public void SetTrigger(int id)
        {
            target.SetTrigger(id);
            triggerBuffers[id] = new AnimTriggerBuffer(id, false);
        }

        // TRIGGER
        public void SetTrigger(string name)
        {
            SetTrigger(Animator.StringToHash(name));
            //target.SetTrigger(name);
        }

        // TRIGGER, EXTRA
        public void ResetTrigger(int id)
        {
            target.ResetTrigger(id);
            triggerBuffers[id] = new AnimTriggerBuffer(id, true);
        }

        // TRIGGER, EXTRA
        public void ResetTrigger(string name)
        {
            ResetTrigger(Animator.StringToHash(name));
            //target.SetTrigger(name);
        }

        #endregion

        #region Play

        //PLAY, ?LAYER, ?TIME
        public void Play(int stateNameHash, int layer = -1, float normalizedTime = 0)
        {
            target.Play(stateNameHash, layer, normalizedTime);
            playBuffers[stateNameHash] = new AnimPlayBuffer(stateNameHash, layer, normalizedTime);
        }

        //PLAY, ?LAYER, ?TIME
        public void Play(string stateName, int layer = -1, float normalizedTime = 0)
        {
            Play(Animator.StringToHash(stateName), layer, normalizedTime);
            //target.Play(stateName, layer, normalizedTime);
        }

        #endregion

        [System.Flags]
        internal enum NetworkAnimatorFlags : byte
        {
            NONE    = 0,
            FLOAT   = 1 << 0,
            BOOL    = 1 << 1,
            INT     = 1 << 2,
            TRIGGER = 1 << 3,
            PLAY    = 1 << 4,
            LAYER   = 1 << 5,
            EXTRA   = 1 << 6,
            //SET     = 1 << 7
        }

        //private interface IAnimBuffer
        //{
        //    public NetworkAnimatorFlags GetFlags();
        //}

        private struct AnimFloatBuffer : INetworkMessage//, IAnimBuffer
        {
            public NetworkAnimatorFlags flags;

            public int id;
            public float value;
            public float dampTime;
            public float deltaTime;

            public AnimFloatBuffer(int id, float value)
            {
                flags = NetworkAnimatorFlags.FLOAT;

                this.id = id;
                this.value = value;
                this.dampTime = 0;
                this.deltaTime = 0;
            }

            public AnimFloatBuffer(int id, float value, float dampTime, float deltaTime)
            {
                this.flags = NetworkAnimatorFlags.FLOAT | NetworkAnimatorFlags.EXTRA;

                this.id = id;
                this.value = value;
                this.dampTime = dampTime;
                this.deltaTime = deltaTime;
            }

            public void AddToMessage(Message message)
            {
                message.Add((byte)flags);
                message.Add(id);
                message.Add(value);

                if (flags.HasFlag(NetworkAnimatorFlags.EXTRA))
                {
                    message.Add(dampTime);
                    message.Add(deltaTime);
                }
            }

            public void Deserialize(Message message)
            {
                flags = (NetworkAnimatorFlags)message.GetByte();
                id = message.GetInt();
                value = message.GetFloat();

                if (flags.HasFlag(NetworkAnimatorFlags.EXTRA))
                {
                    dampTime = message.GetFloat();
                    deltaTime = message.GetFloat();
                }
            }

            public byte GetMaxSize()
            {
                return 10;
            }
        }

        private struct AnimBoolBuffer : INetworkMessage//, IAnimBuffer
        {
            public NetworkAnimatorFlags flags;

            public int id;
            public bool value;

            public AnimBoolBuffer(int id, bool value)
            {
                this.flags = NetworkAnimatorFlags.BOOL;

                this.id = id;
                this.value = value;
            }

            public void AddToMessage(Message message)
            {
                message.Add((byte)flags);
                message.Add(id);
                message.Add(value);
            }

            public void Deserialize(Message message)
            {
                flags = (NetworkAnimatorFlags)message.GetByte();
                id = message.GetInt();
                value = message.GetBool();
            }

            public byte GetMaxSize()
            {
                return 10;
            }
        }

        private struct AnimIntBuffer : INetworkMessage//, IAnimBuffer
        {
            public NetworkAnimatorFlags flags;

            public int id;
            public int value;

            public AnimIntBuffer(int id, int value)
            {
                this.flags = NetworkAnimatorFlags.BOOL;

                this.id = id;
                this.value = value;
            }

            public void AddToMessage(Message message)
            {
                message.Add((byte)flags);
                message.Add(id);
                message.Add(value);
            }

            public void Deserialize(Message message)
            {
                flags = (NetworkAnimatorFlags)message.GetByte();
                id = message.GetInt();
                value = message.GetInt();
            }

            public byte GetMaxSize()
            {
                return 10;
            }
        }

        private struct AnimTriggerBuffer : INetworkMessage//, IAnimBuffer
        {
            public NetworkAnimatorFlags flags;

            public int id;

            public AnimTriggerBuffer(int id, bool reset)
            {
                this.flags = NetworkAnimatorFlags.TRIGGER | (reset ? NetworkAnimatorFlags.EXTRA : NetworkAnimatorFlags.NONE);

                this.id = id;
            }

            public void AddToMessage(Message message)
            {
                message.Add((byte)flags);
                message.Add(id);
            }

            public void Deserialize(Message message)
            {
                flags = (NetworkAnimatorFlags)message.GetByte();
                id = message.GetInt();
            }

            public byte GetMaxSize()
            {
                return 10;
            }
        }

        private struct AnimPlayBuffer : INetworkMessage//, IAnimBuffer
        {
            public NetworkAnimatorFlags flags;

            public int id;
            public int layer;
            public float normalizedTime;

            public AnimPlayBuffer(int id, int layer = -1, float normalizedTime = 0)
            {
                this.flags = NetworkAnimatorFlags.PLAY | (layer == -1 ? NetworkAnimatorFlags.NONE : NetworkAnimatorFlags.LAYER)
                    | (normalizedTime == 0 ? NetworkAnimatorFlags.NONE : NetworkAnimatorFlags.EXTRA);

                this.id = id;
                this.layer = layer;
                this.normalizedTime = normalizedTime;
            }

            public void AddToMessage(Message message)
            {
                message.Add((byte)flags);
                message.Add(id);

                if (flags.HasFlag(NetworkAnimatorFlags.LAYER)) message.Add(layer);
                if (flags.HasFlag(NetworkAnimatorFlags.EXTRA)) message.Add(normalizedTime);
            }

            public void Deserialize(Message message)
            {
                flags = (NetworkAnimatorFlags)message.GetByte();
                id = message.GetInt();

                if (flags.HasFlag(NetworkAnimatorFlags.LAYER)) layer = message.GetInt();
                if (flags.HasFlag(NetworkAnimatorFlags.EXTRA)) normalizedTime = message.GetFloat();
            }

            public byte GetMaxSize()
            {
                return 10;
            }
        }

        // If you want to add speed / layer weight or any other changes, do so.

        //private struct Anim_Buffer : INetworkMessage { }
    }
}
