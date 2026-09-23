using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NetworkGamePlayerBootstrap : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log(
            "NETWORK GAME BOOTSTRAP: Awake()"
        );
    }

    private void Start()
    {
        Debug.Log(
            "NETWORK GAME BOOTSTRAP: Start()"
        );

        RegisterAllPlayers();
    }

    private void RegisterAllPlayers()
    {
        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "NETWORK GAME BOOTSTRAP: "
                + "Không tìm thấy NetworkManager."
            );

            return;
        }

        // Chỉ Server tạo PlayerData
        if (!networkManager.IsServer)
        {
            Debug.Log(
                "NETWORK GAME BOOTSTRAP: "
                + "Không phải Server → bỏ qua."
            );

            return;
        }

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
        {
            playerManager =
                FindFirstObjectByType<PlayerManager>();
        }

        if (playerManager == null)
        {
            Debug.LogError(
                "NETWORK GAME BOOTSTRAP: "
                + "Không tìm thấy PlayerManager."
            );

            return;
        }

        Debug.Log(
            "NETWORK GAME BOOTSTRAP: "
            + "Connected Clients = "
            + networkManager.ConnectedClientsIds.Count
        );

        foreach (
            ulong clientId
            in networkManager.ConnectedClientsIds)
        {
            RegisterPlayer(
                playerManager,
                clientId
            );
        }

        Debug.Log(
            "NETWORK GAME BOOTSTRAP: "
            + "PlayerManager hiện có "
            + playerManager.players.Count
            + " PlayerData."
        );
    }

    private void RegisterPlayer(
        PlayerManager playerManager,
        ulong clientId)
    {
        int playerID =
            (int)clientId;

        PlayerData existingPlayer =
            playerManager.GetplayerByID(
                playerID
            );

        if (existingPlayer != null)
        {
            Debug.Log(
                "NETWORK GAME BOOTSTRAP: "
                + "Player "
                + playerID
                + " đã tồn tại."
            );

            return;
        }

        PlayerData player =
            new PlayerData
            {
                playerID = playerID,

                playerName =
                    "Player " + playerID,

                isAlive = true,

                votePower = 1,

                hasVoted = false,

                hasUseNightAction = false,

                status = new PlayerStatus()
            };

        playerManager.RegisterPlayer(player);

        Debug.Log(
            "NETWORK GAME BOOTSTRAP: "
            + "Đã tạo PlayerData cho Player "
            + playerID
        );
    }
}