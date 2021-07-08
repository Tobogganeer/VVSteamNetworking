using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking.Steam;
using VirtualVoid.Networking.Steam.LLAPI;
using UnityEngine.UI;

public class Testing : MonoBehaviour
{
    public GameObject lobbyObject;
    public GameObject menuObject;

    public Text lobbyText;

    public InputField inputField;

    private void Awake()
    {
        SteamManager.OnLobbyCreated += SteamManager_OnLobbyCreated;
        SteamManager.OnLobbyJoined += SteamManager_OnLobbyJoined;
        SteamManager.OnLobbyLeft += SteamManager_OnLobbyLeft;

        SteamManager.OnLobbyMemberJoined += OnLobbyMembersChange;
        SteamManager.OnLobbyMemberLeave += OnLobbyMembersChange;
    }

    private void OnLobbyMembersChange(Steamworks.Data.Lobby arg1, Steamworks.Friend arg2)
    {
        UpdateMemberText();
    }

    private void SteamManager_OnLobbyCreated(Steamworks.Data.Lobby arg1, bool arg2)
    {
        Debug.Log("SteamManager_OnLobbyCreated invoked.");
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

    public void Host()
    {
        SteamManager.HostServer();
    }

    public void LeaveLobby()
    {
        SteamManager.LeaveServer();
    }

    private void OnDestroy()
    {
        SteamManager.OnLobbyCreated -= SteamManager_OnLobbyCreated;
        SteamManager.OnLobbyJoined -= SteamManager_OnLobbyJoined;
        SteamManager.OnLobbyLeft -= SteamManager_OnLobbyLeft;
    }

    private void UpdateMemberText()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder("Friends:");

        foreach (var friend in SteamManager.CurrentLobby.Members)
        {
            builder.AppendLine(friend.Name);
        }

        lobbyText.text = builder.ToString();
    }
}
