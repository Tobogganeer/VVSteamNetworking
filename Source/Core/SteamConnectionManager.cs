using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

namespace VirtualVoid.Net
{
    public class SteamConnectionManager : ConnectionManager
    {
        public override void OnConnected(ConnectionInfo info)
        {
            base.OnConnected(info);
            Debug.Log($"Connected to {new Friend(info.Identity.SteamId).Name}");
            SteamManager.OnConnectedToServer(info);
        }

        public override void OnConnecting(ConnectionInfo info)
        {
            base.OnConnecting(info);
            Debug.Log($"Connecting to {new Friend(info.Identity.SteamId).Name}");
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            base.OnDisconnected(info);
            Debug.Log($"Disconnected from {new Friend(info.Identity.SteamId).Name}");
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            SteamManager.HandleDataFromServer(data, size);
        }
    }
}
