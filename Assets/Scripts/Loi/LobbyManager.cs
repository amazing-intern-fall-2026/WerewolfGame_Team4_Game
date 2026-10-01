using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField]
    private TextMeshProUGUI playerListText;

    [SerializeField]
    private GameObject startGameButton;

    [Header("Player Name")]
    [SerializeField]
    private TMP_InputField playerNameInput;

    private NetworkList<PlayerLobbyData> players;

    private NetworkRoleLobbyConfig roleLobbyConfig;

    // ==========================================
    // AWAKE
    // ==========================================

    private void Awake()
    {
        players =
            new NetworkList<PlayerLobbyData>();
    }

    // ==========================================
    // NETWORK SPAWN
    // ==========================================

    public override void OnNetworkSpawn()
    {
        players.OnListChanged +=
            OnPlayerListChanged;

        FindRoleLobbyConfig();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback +=
                OnClientConnected;

            NetworkManager.OnClientDisconnectCallback +=
                OnClientDisconnected;

            AddPlayer(
                NetworkManager.LocalClientId,
                "Player " +
                (NetworkManager.LocalClientId + 1)
            );

            foreach (
                ulong clientId
                in NetworkManager.ConnectedClientsIds
            )
            {
                AddPlayer(
                    clientId,
                    "Player " +
                    (clientId + 1)
                );
            }
        }

        UpdatePlayerList();
        UpdateStartButton();

        Debug.Log(
            "LOBBY MANAGER | NetworkRoleLobbyConfig = "
            + (
                roleLobbyConfig != null
                    ? "FOUND"
                    : "NULL"
            )
        );
    }

    // ==========================================
    // NETWORK DESPAWN
    // ==========================================

    public override void OnNetworkDespawn()
    {
        players.OnListChanged -=
            OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -=
                OnClientConnected;

            NetworkManager.OnClientDisconnectCallback -=
                OnClientDisconnected;
        }
    }

    // ==========================================
    // FIND ROLE CONFIG
    // ==========================================

    private void FindRoleLobbyConfig()
    {
        roleLobbyConfig =
            FindFirstObjectByType<
                NetworkRoleLobbyConfig
            >();

        if (roleLobbyConfig == null)
        {
            Debug.LogWarning(
                "LOBBY MANAGER | "
                + "Không tìm thấy NetworkRoleLobbyConfig!"
            );

            return;
        }

        Debug.Log(
            "LOBBY MANAGER | "
            + "Đã tìm thấy NetworkRoleLobbyConfig."
        );

        Debug.Log(
            "LOBBY MANAGER | Total Roles = "
            + roleLobbyConfig.GetTotalRoleAmount()
        );
    }

    // ==========================================
    // PLAYER CONNECTED
    // ==========================================

    private void OnClientConnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player "
            + (clientId + 1)
            + " đã vào Lobby"
        );

        AddPlayer(
            clientId,
            "Player " +
            (clientId + 1)
        );
    }

    // ==========================================
    // PLAYER DISCONNECTED
    // ==========================================

    private void OnClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player "
            + (clientId + 1)
            + " đã rời Lobby"
        );

        RemovePlayer(clientId);
    }

    // ==========================================
    // ADD PLAYER
    // ==========================================

    private void AddPlayer(
        ulong clientId,
        string playerName)
    {
        if (FindPlayerIndex(clientId) >= 0)
            return;

        players.Add(
            new PlayerLobbyData(
                clientId,
                playerName
            )
        );

        Debug.Log(
            "Đã thêm Player "
            + (clientId + 1)
            + " | Name = "
            + playerName
        );
    }

    // ==========================================
    // REMOVE PLAYER
    // ==========================================

    private void RemovePlayer(
        ulong clientId)
    {
        int index =
            FindPlayerIndex(clientId);

        if (index < 0)
            return;

        players.RemoveAt(index);

        Debug.Log(
            "Đã xóa Player "
            + (clientId + 1)
            + " khỏi Lobby."
        );
    }

    // ==========================================
    // FIND PLAYER
    // ==========================================

    private int FindPlayerIndex(
        ulong clientId)
    {
        for (
            int i = 0;
            i < players.Count;
            i++
        )
        {
            if (
                players[i].ClientId ==
                clientId
            )
            {
                return i;
            }
        }

        return -1;
    }

    // ==========================================
    // SUBMIT PLAYER NAME
    // ==========================================

    public void SubmitPlayerName()
    {
        if (
            NetworkManager.Singleton ==
            null
        )
        {
            return;
        }

        string playerName =
            "Player " +
            (
                NetworkManager.Singleton
                    .LocalClientId + 1
            );

        if (playerNameInput != null)
        {
            string input =
                playerNameInput.text.Trim();

            if (
                !string.IsNullOrWhiteSpace(
                    input
                )
            )
            {
                playerName = input;
            }
        }

        SubmitPlayerNameServerRpc(
            playerName
        );
    }

    // ==========================================
    // SUBMIT NAME SERVER RPC
    // ==========================================

    [ServerRpc(
        RequireOwnership = false
    )]
    private void SubmitPlayerNameServerRpc(
        string playerName,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId =
            rpcParams.Receive.SenderClientId;

        playerName =
            playerName.Trim();

        if (
            string.IsNullOrWhiteSpace(
                playerName
            )
        )
        {
            playerName =
                "Player " +
                (clientId + 1);
        }

        if (playerName.Length > 20)
        {
            playerName =
                playerName.Substring(
                    0,
                    20
                );
        }

        int index =
            FindPlayerIndex(clientId);

        if (index < 0)
        {
            AddPlayer(
                clientId,
                playerName
            );

            return;
        }

        PlayerLobbyData data =
            players[index];

        data.PlayerName =
            new FixedString64Bytes(
                playerName
            );

        players[index] =
            data;

        NetworkGamePlayerNameSetup.Save(
       clientId,
           playerName
        );

        Debug.Log(
            "LOBBY NAME | Player "
            + (clientId + 1)
            + " → "
            + playerName
        );
    }

    // ==========================================
    // PLAYER LIST CHANGED
    // ==========================================

    private void OnPlayerListChanged(
        NetworkListEvent<PlayerLobbyData>
            changeEvent)
    {
        UpdatePlayerList();
        UpdateStartButton();
    }

    // ==========================================
    // UPDATE PLAYER LIST UI
    // ==========================================

    private void UpdatePlayerList()
    {
        if (playerListText == null)
            return;

        string text =
            "PLAYERS\n\n";

        for (
            int i = 0;
            i < players.Count;
            i++
        )
        {
            PlayerLobbyData player =
                players[i];

            string playerName;

            if (
                player.PlayerName.Length ==
                0
            )
            {
                playerName =
                    "Player " +
                    (player.ClientId + 1);
            }
            else
            {
                playerName =
                    player.PlayerName.ToString();
            }

            text +=
                "Player "
                + (player.ClientId + 1)
                + " - "
                + playerName;

            if (
                i <
                players.Count - 1
            )
            {
                text += "\n";
            }
        }

        playerListText.text =
            text;
    }

    // ==========================================
    // UPDATE START BUTTON
    // ==========================================

    private void UpdateStartButton()
    {
        if (startGameButton == null)
            return;

        startGameButton.SetActive(
            IsServer
        );
    }

    // ==========================================
    // START GAME
    // ==========================================

    public void StartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Host mới được Start Game!"
            );

            return;
        }

        if (roleLobbyConfig == null)
        {
            FindRoleLobbyConfig();
        }

        if (roleLobbyConfig == null)
        {
            Debug.LogError(
                "Không thể Start Game: "
                + "NetworkRoleLobbyConfig "
                + "không tồn tại!"
            );

            return;
        }

        // ======================================
        // CHECK PLAYER / ROLE
        // ======================================

        int playerCount =
            players.Count;

        int roleCount =
            roleLobbyConfig
                .GetTotalRoleAmount();

        Debug.Log(
            "LOBBY START CHECK | "
            + "Players = "
            + playerCount
            + " | Roles = "
            + roleCount
        );

        if (playerCount != roleCount)
        {
            Debug.LogWarning(
                "Không thể Start Game! "
                + "Số Role ("
                + roleCount
                + ") "
                + "phải bằng số Player ("
                + playerCount
                + ")."
            );

            return;
        }

        // ======================================
        // SAVE ROLE SETUP
        // ======================================

        NetworkGameRoleSetup.Save(
            roleLobbyConfig
                .GetRoleAmounts()
        );

        Debug.Log(
            "LOBBY START GAME | "
            + "Đã lưu Role Setup. "
            + "Total Roles = "
            + NetworkGameRoleSetup
                .GetTotalRoleAmount()
        );

        // ======================================
        // START GAME
        // ======================================

        Debug.Log(
            "HOST START GAME!"
        );

        NetworkManager.SceneManager.LoadScene(
            "Game",
            LoadSceneMode.Single
        );
    }
}


// ==================================================
// PLAYER LOBBY DATA
// ==================================================

public struct PlayerLobbyData :
    INetworkSerializable,
    IEquatable<PlayerLobbyData>
{
    public ulong ClientId;

    public FixedString64Bytes PlayerName;

    public PlayerLobbyData(
        ulong clientId,
        string playerName)
    {
        ClientId =
            clientId;

        PlayerName =
            new FixedString64Bytes(
                playerName
            );
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(
            ref ClientId
        );

        serializer.SerializeValue(
            ref PlayerName
        );
    }

    public bool Equals(
        PlayerLobbyData other)
    {
        return
            ClientId ==
            other.ClientId
            &&
            PlayerName ==
            other.PlayerName;
    }
}