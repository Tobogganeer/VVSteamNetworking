using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking.Steam;
using VirtualVoid.Networking.Steam.LLAPI;
using UnityEngine.UI;

public class Testing : MonoBehaviour
{
    public static Testing instance;

    public GameObject lobbyObject;
    public GameObject menuObject;

    public Text lobbyText;

    public Transform[] spawnPoints;
    public static Transform[] SpawnPoints => instance.spawnPoints;

    //public InputField inputField;

    private void Awake()
    {
        SteamManager.OnLobbyCreated += SteamManager_OnLobbyCreated;
        SteamManager.OnLobbyJoined += SteamManager_OnLobbyJoined;
        SteamManager.OnLobbyLeft += SteamManager_OnLobbyLeft;
        SteamManager.OnLobbyMemberJoined += SteamManager_OnLobbyMemberJoined;
        SteamManager.OnLobbyMemberLeave += SteamManager_OnLobbyMemberLeave;

        instance = this;
    }

    private void OnDestroy()
    {
        SteamManager.OnLobbyCreated -= SteamManager_OnLobbyCreated;
        SteamManager.OnLobbyJoined -= SteamManager_OnLobbyJoined;
        SteamManager.OnLobbyLeft -= SteamManager_OnLobbyLeft;
        SteamManager.OnLobbyMemberJoined -= SteamManager_OnLobbyMemberJoined;
        SteamManager.OnLobbyMemberLeave -= SteamManager_OnLobbyMemberLeave;
    }

    private void SteamManager_OnLobbyMemberLeave(Steamworks.Data.Lobby lobby, Steamworks.Friend member)
    {
        Debug.Log($"SteamManager_OnLobbyMemberLeave invoked. (Friend: {member.Name})");
        OnLobbyMembersChange();
    }

    private void SteamManager_OnLobbyMemberJoined(Steamworks.Data.Lobby lobby, Steamworks.Friend member)
    {
        Debug.Log($"SteamManager_OnLobbyMemberJoined invoked. (Friend: {member.Name})");
        OnLobbyMembersChange();
    }

    private void OnLobbyMembersChange()
    {
        UpdateMemberText();
    }

    private void SteamManager_OnLobbyCreated(Steamworks.Data.Lobby arg1, bool arg2)
    {
        Debug.Log("SteamManager_OnLobbyCreated invoked. Success? " + arg2);
    }

    private void SteamManager_OnLobbyJoined(Steamworks.Data.Lobby obj)
    {
        Debug.Log("SteamManager_OnLobbyJoined invoked.");
        menuObject.SetActive(false);
        lobbyObject.SetActive(true);

        UpdateMemberText();
    }

    private void SteamManager_OnLobbyLeft(Steamworks.Data.Lobby obj)
    {
        Debug.Log("SteamManager_OnLobbyLeft invoked.");
        menuObject.SetActive(true);
        lobbyObject.SetActive(false);
    }

    // Called from UI
    public void Host()
    {
        SteamManager.HostServer();
    }

    // Called from UI
    public void LeaveLobby()
    {
        SteamManager.LeaveServer();
    }

    private void UpdateMemberText()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder("Friends:\n");

        foreach (var friend in SteamManager.CurrentLobby.Members)
        {
            builder.AppendLine(friend.Name);
        }

        lobbyText.text = builder.ToString();
    }
}
