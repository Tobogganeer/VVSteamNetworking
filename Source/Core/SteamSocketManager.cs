using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

namespace VirtualVoid.Net
{
    public class SteamSocketManager : SocketManager
    {
		public override void OnConnecting(Connection connection, ConnectionInfo data)
		{
			if (SteamManager.clients.Count + SteamManager.clientsPendingAuth.Count >= SteamManager.MaxPlayers)
            {
				Debug.Log($"Attempted connection from {data.Identity.SteamId}, but the server is full!");
				return;
            }

			base.OnConnecting(connection, data);//The base class will accept the connection
			Debug.Log("Incoming server connection...");// from " + new Friend(data.Identity.SteamId).Name);
		}

		public override void OnConnected(Connection connection, ConnectionInfo data)
		{
			base.OnConnected(connection, data);
			Debug.Log(new Friend(data.Identity.SteamId).Name + " connected to the server");
			//Debug.Log("Address: " + data.Identity);
			SteamManager.OnConnectionConnected(connection, data);
		}

		public override void OnDisconnected(Connection connection, ConnectionInfo data)
		{
			base.OnDisconnected(connection, data);
			Debug.Log(new Friend(data.Identity.SteamId).Name + " disconnected");
			SteamManager.OnConnectionDisconnected(connection, data);
		}

		public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			// Socket server received message, forward on message to all members of socket server
			//SteamManager.Instance.RelaySocketMessageReceived(data, size, connection.Id);
			//Debug.Log("Socket message received");

			SteamManager.HandleDataFromClient(connection, identity, data, size);
		}
	}
}
