using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking.Steam.LLAPI;
using System.Linq;
using UnityEngine.UI;
using System;

namespace VirtualVoid.Networking.Steam
{
    public class NetStats : MonoBehaviour
    {
        private static NetStats instance;
        private void Awake()
        {
            instance = this;
        }

        public GameObject statsPanel;
        public Text pingText;
        public Text packetsDownText;
        public Text packetsUpText;
        public Text bytesDownText;
        public Text bytesUpText;

        private static readonly System.Diagnostics.Stopwatch pingTimer = new System.Diagnostics.Stopwatch();
        private static readonly List<long> pings = new List<long>(MAX_PINGS);
        private const byte MAX_PINGS = 5;

        public bool uiEnabled = true;

        private void OnEnable()
        {
            InvokeRepeating(nameof(SlowUpdate), 1f, 3f);
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void SlowUpdate()
        {
            pingTimer.Start();
            InternalClientMessages.SendPing();

            UpdateUI();
            ClearSettings();
        }

        public static void OnPongReceived()
        {
            if (pings.Count >= MAX_PINGS) pings.RemoveAt(0);

            pingTimer.Stop();

            pings.Add(pingTimer.ElapsedMilliseconds);

            pingTimer.Reset();

            Ping = pings.Average();
        }

        public static int PacketsReceived { get; private set; }
        public static int PacketsSent { get; private set; }

        public static int BytesReceived { get; private set; }
        public static int BytesSent { get; private set; }

        public static double Ping { get; private set; }

        public static void ClearSettings()
        {
            PacketsReceived = 0;
            PacketsSent = 0;
            BytesReceived = 0;
            BytesSent = 0;
        }

        public static void OnPacketSent(int size)
        {
            BytesSent += size;
            PacketsSent++;

            instance?.UpdateUI();
        }

        public static void OnPacketReceived(int size)
        {
            BytesReceived += size;
            PacketsReceived++;

            instance?.UpdateUI();
        }

        private void UpdateUI()
        {
            if (!uiEnabled) return;

            try
            {
                pingText.text = "Ping: " + Math.Round(Ping).ToString() + "ms";
                packetsDownText.text = "Packets Down: " + PacketsReceived;
                packetsUpText.text = "Packets Up: " + PacketsSent;
                bytesDownText.text = "Bytes Down: " + BytesReceived;
                bytesUpText.text = "Bytes Up: " + BytesSent;
            }
            catch
            {
                Debug.Log("Caught NullReferenceException from missing texts on game close.");
            }
        }
    }
}
