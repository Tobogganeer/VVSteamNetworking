using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using VirtualVoid.Networking.Steam.LLAPI;
using System.Threading.Tasks;

namespace VirtualVoid.Networking.Steam
{
    public class ConnectedClient
    {
        public SteamId steamId;
        public bool sceneLoaded = true;

        public ConnectedClient(SteamId steamId)
        {
            this.steamId = steamId;
        }

        public void SendMessage(Message message)
        {
            SteamManager.SendMessageToClient(steamId, message);
        }

        public void Disconnect()
        {
            SteamManager.DisconnectClient(this);
        }
    }
}
