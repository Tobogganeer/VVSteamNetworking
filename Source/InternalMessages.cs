using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VirtualVoid.Networking.Steam.LLAPI
{
    internal class InternalMessages
    {
        internal static void Initialize()
        {
            InternalClientMessages.Initialize();
            InternalServerMessages.Initialize();
        }
    }

    internal class InternalClientMessages
    {
        internal static void Initialize()
        {
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.PONG, OnServerPong);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.DISCONNECT, OnClientDisconnected);
            SteamManager.RegisterInternalMessageHandler_FromServer((ushort)InternalServerMessageIDs.SCENE_CHANGE, OnChangeScene);
        }

        #region Send
        internal static void SendPing()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.PING));
        }

        internal static void SendDisconnect()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.DISCONNECTED));
        }

        internal static void SendSceneLoaded()
        {
            SteamManager.SendMessageToServer(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalClientMessageIDs.SCENE_LOADED));
        }
        #endregion

        #region Receive
        private static void OnServerPong(ushort messageID, Message message)
        {
            NetStats.OnPongReceived();
        }

        private static void OnClientDisconnected(ushort messageID, Message message)
        {
            SteamId id = message.GetSteamId();

            if (id == SteamManager.SteamID)
            {
                SteamManager.LeaveServer();
            }

            // handle other client disconnect?
        }

        private static void OnChangeScene(ushort messageID, Message message)
        {
            string sceneName = message.GetString();
            SceneManager.LoadScene(sceneName);
            SendSceneLoaded();
        }
        #endregion
    }

    internal class InternalServerMessages
    {
        internal static void Initialize()
        {
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.PING, OnClientPing);
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.DISCONNECTED, OnClientDisconnect);
            SteamManager.RegisterInternalMessageHandler_FromClient((ushort)InternalClientMessageIDs.SCENE_LOADED, OnClientSceneLoaded);
        }

        #region Send
        internal static void SendPong(SteamId id)
        {
            if (SteamManager.IsServer)
                SteamManager.SendMessageToClient(id, Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.PONG));
        }

        internal static void SendClientDisconnected(SteamId id)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.DISCONNECT).Add(id));
        }

        internal static void SendChangeScene(string sceneId)
        {
            SteamManager.SendMessageToAllClients(Message.CreateInternal(P2PSend.Reliable, (ushort)InternalServerMessageIDs.SCENE_CHANGE).Add(sceneId));
        }
        #endregion

        #region Receive
        private static void OnClientPing(ushort messageID, SteamId clientSteamID, Message message)
        {
            SendPong(clientSteamID);
        }

        private static void OnClientDisconnect(ushort messageID, SteamId clientSteamID, Message message)
        {
            SteamManager.DisconnectClient(clientSteamID);
        }

        private static void OnClientSceneLoaded(ushort messageID, SteamId clientSteamID, Message message)
        {
            SteamManager.ClientSceneLoaded(clientSteamID);
        }
        #endregion
    }
}
