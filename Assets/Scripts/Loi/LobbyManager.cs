using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private GameObject startGameButton;

    private NetworkList<ulong> playerIds;

    private NetworkRoleLobbyConfig roleLobbyConfig;


    private void Awake()
    {
        playerIds = new NetworkList<ulong>();
    }


    public override void OnNetworkSpawn()
    {
        playerIds.OnListChanged += OnPlayerListChanged;

        FindRoleLobbyConfig();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback +=
                OnClientConnected;

            NetworkManager.OnClientDisconnectCallback +=
                OnClientDisconnected;

            AddPlayer(
                NetworkManager.LocalClientId
            );

            foreach (
                ulong clientId
                in NetworkManager.ConnectedClientsIds
            )
            {
                AddPlayer(clientId);
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


    public override void OnNetworkDespawn()
    {
        playerIds.OnListChanged -=
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
            FindFirstObjectByType<NetworkRoleLobbyConfig>();

        if (roleLobbyConfig == null)
        {
            Debug.LogWarning(
                "LOBBY MANAGER | Không tìm thấy NetworkRoleLobbyConfig!"
            );

            return;
        }

        Debug.Log(
            "LOBBY MANAGER | Đã tìm thấy NetworkRoleLobbyConfig."
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
            + clientId
            + " đã vào Lobby"
        );

        AddPlayer(clientId);
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
            + clientId
            + " đã rời Lobby"
        );

        RemovePlayer(clientId);
    }


    // ==========================================
    // ADD PLAYER
    // ==========================================

    private void AddPlayer(
        ulong clientId)
    {
        if (playerIds.Contains(clientId))
            return;

        playerIds.Add(clientId);

        Debug.Log(
            "Đã thêm Player "
            + clientId
            + " vào Lobby."
        );
    }


    // ==========================================
    // REMOVE PLAYER
    // ==========================================

    private void RemovePlayer(
        ulong clientId)
    {
        if (!playerIds.Contains(clientId))
            return;

        playerIds.Remove(clientId);

        Debug.Log(
            "Đã xóa Player "
            + clientId
            + " khỏi Lobby."
        );
    }


    // ==========================================
    // PLAYER LIST CHANGED
    // ==========================================

    private void OnPlayerListChanged(
        NetworkListEvent<ulong> changeEvent)
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
            i < playerIds.Count;
            i++
        )
        {
            text +=
                "Player "
                + (i + 1);

            if (
                i <
                playerIds.Count - 1
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

        // Chỉ Host được thấy nút Start
        startGameButton.SetActive(
            IsServer
        );
    }


    // ==========================================
    // START GAME
    // ==========================================

    public void StartGame()
    {
        // Chỉ Server / Host
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Host mới được Start Game!"
            );

            return;
        }


        // Kiểm tra Role Config
        if (roleLobbyConfig == null)
        {
            FindRoleLobbyConfig();
        }


        if (roleLobbyConfig == null)
        {
            Debug.LogError(
                "Không thể Start Game: "
                + "NetworkRoleLobbyConfig không tồn tại!"
            );

            return;
        }


        // ======================================
        // CHECK PLAYER / ROLE
        // ======================================

        int playerCount =
            playerIds.Count;

        int roleCount =
            roleLobbyConfig.GetTotalRoleAmount();


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
                + ") phải bằng số Player ("
                + playerCount
                + ")."
            );

            return;
        }

        // ======================================
        // SAVE ROLE SETUP
        // ======================================

        NetworkGameRoleSetup.Save(
            roleLobbyConfig.GetRoleAmounts()
        );

        Debug.Log(
            "LOBBY START GAME | "
            + "Đã lưu Role Setup. "
            + "Total Roles = "
            + NetworkGameRoleSetup.GetTotalRoleAmount()
        );


        // ======================================
        // START
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