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

    private void Awake()
    {
        playerIds = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        playerIds.OnListChanged += OnPlayerListChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            AddPlayer(NetworkManager.LocalClientId);

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                AddPlayer(clientId);
            }
        }

        UpdatePlayerList();
        UpdateStartButton();
    }

    public override void OnNetworkDespawn()
    {
        playerIds.OnListChanged -= OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ==========================================
    // PLAYER CONNECTED
    // ==========================================

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player " +
            clientId +
            " đã vào Lobby"
        );

        AddPlayer(clientId);
    }

    // ==========================================
    // PLAYER DISCONNECTED
    // ==========================================

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player " +
            clientId +
            " đã rời Lobby"
        );

        RemovePlayer(clientId);
    }

    // ==========================================
    // ADD PLAYER
    // ==========================================

    private void AddPlayer(ulong clientId)
    {
        if (playerIds.Contains(clientId))
            return;

        playerIds.Add(clientId);

        Debug.Log(
            "Đã thêm Player " +
            clientId +
            " vào Lobby."
        );
    }

    // ==========================================
    // REMOVE PLAYER
    // ==========================================

    private void RemovePlayer(ulong clientId)
    {
        if (!playerIds.Contains(clientId))
            return;

        playerIds.Remove(clientId);

        Debug.Log(
            "Đã xóa Player " +
            clientId +
            " khỏi Lobby."
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

        string text = "PLAYERS\n\n";

        for (int i = 0; i < playerIds.Count; i++)
        {
            text += "Player " + (i + 1);

            if (i < playerIds.Count - 1)
            {
                text += "\n";
            }
        }

        playerListText.text = text;
    }

    // ==========================================
    // UPDATE START BUTTON
    // ==========================================

    private void UpdateStartButton()
    {
        if (startGameButton == null)
            return;

        // Chỉ Host được thấy nút Start
        startGameButton.SetActive(IsServer);
    }

    // ==========================================
    // START GAME
    // ==========================================

    public void StartGame()
    {
        // Chỉ Server/Host được phép Start
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Host mới được Start Game!"
            );

            return;
        }

        Debug.Log("HOST START GAME!");

        NetworkManager.SceneManager.LoadScene(
            "Game",
            LoadSceneMode.Single
        );
    }
}