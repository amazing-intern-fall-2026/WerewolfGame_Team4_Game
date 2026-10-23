using System.Collections.Generic;
using System.Linq;
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

        if (networkManager.IsServer)
        {
            var connectedIds = networkManager.ConnectedClientsIds;
            var validActiveIds = new HashSet<ulong>();

            foreach (ulong cid in connectedIds)
            {
                if (!NetworkGameManager.LeftPlayerClientIds.Contains(cid) && NetworkGamePlayerSetup.IsClientInMatch(cid))
                {
                    validActiveIds.Add(cid);
                }
                else
                {
                    NetworkGameManager.LeftPlayerClientIds.Add(cid);
                    if (networkManager.ConnectedClients.ContainsKey(cid))
                    {
                        networkManager.DisconnectClient(cid);
                    }
                }
            }

            // 1. Tiêu hủy mọi PlayerController GameObject mồ côi (của client đã ngắt kết nối / văng game / không thuộc active session)
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc != null)
                {
                    ulong cid = pc.OwnerClientId;
                    if (!validActiveIds.Contains(cid) || NetworkGameManager.LeftPlayerClientIds.Contains(cid))
                    {
                        Debug.Log($"[NetworkGamePlayerBootstrap] Tiêu hủy PlayerController GameObject mồ côi ClientId={cid}");
                        var no = pc.GetComponent<NetworkObject>();
                        if (no != null && no.IsSpawned)
                        {
                            no.Despawn(true);
                        }
                        Destroy(pc.gameObject);
                    }
                }
            }

            playerManager.disconnectedPlayers?.Clear();

            playerManager.players.RemoveAll(p => p == null 
                || !validActiveIds.Contains((ulong)p.playerID) 
                || NetworkGameManager.LeftPlayerClientIds.Contains((ulong)p.playerID)
                || p.isDisconnected);

            Debug.Log(
                "NETWORK GAME BOOTSTRAP: "
                + "Valid Active Clients = "
                + validActiveIds.Count
            );

            foreach (ulong clientId in validActiveIds)
            {
                RegisterPlayer(playerManager, clientId);
            }

            // Đồng bộ toàn bộ người chơi đang kết nối về Alive
            NetworkPlayerStateSync.ResetAllPlayersAlive();

            // Đồng bộ danh sách active và left players sang tất cả Client
            if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.IsSpawned)
            {
                NetworkPhaseSync.Instance.SyncActiveSessionClientRpc(
                    validActiveIds.ToArray(),
                    NetworkGameManager.LeftPlayerClientIds.ToArray()
                );
            }
        }
        else
        {
            // Client: Tiêu hủy mọi PlayerController GameObject của client đã bị đánh dấu Left hoặc không trong NetworkGamePlayerSetup
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc != null && (NetworkGameManager.LeftPlayerClientIds.Contains(pc.OwnerClientId) || !NetworkGamePlayerSetup.IsClientInMatch(pc.OwnerClientId)))
                {
                    Debug.Log($"[NetworkGamePlayerBootstrap] Client tiêu hủy PlayerController mồ côi ClientId={pc.OwnerClientId}");
                    Destroy(pc.gameObject);
                }
            }

            playerManager.disconnectedPlayers?.Clear();
            controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Debug.Log($"NETWORK GAME BOOTSTRAP (Client): Tìm thấy {controllers.Length} PlayerControllers.");
            var activeIds = new HashSet<ulong>();
            foreach (var pc in controllers)
            {
                if (pc != null && !NetworkGameManager.LeftPlayerClientIds.Contains(pc.OwnerClientId) && NetworkGamePlayerSetup.IsClientInMatch(pc.OwnerClientId))
                {
                    activeIds.Add(pc.OwnerClientId);
                }
            }

            playerManager.players.RemoveAll(p => p == null 
                || !activeIds.Contains((ulong)p.playerID) 
                || NetworkGameManager.LeftPlayerClientIds.Contains((ulong)p.playerID)
                || (NetworkGamePlayerSetup.HasActiveMatchSetup && !NetworkGamePlayerSetup.IsClientInMatch((ulong)p.playerID))
                || p.isDisconnected);

            foreach (var pc in controllers)
            {
                if (pc != null && !NetworkGameManager.LeftPlayerClientIds.Contains(pc.OwnerClientId) && NetworkGamePlayerSetup.IsClientInMatch(pc.OwnerClientId))
                {
                    RegisterPlayer(playerManager, pc.OwnerClientId);
                }
            }
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
                + (playerID + 1)
                + " đã tồn tại."
            );

            return;
        }

        // Lấy tên đã nhập từ Lobby
        string playerName =
    NetworkGamePlayerNameSetup.GetName(
        clientId
    );

        if (
            NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client
            )
            && client.PlayerObject != null
        )
        {
            NetworkPlayerNameSync nameSync =
                client.PlayerObject.GetComponent<NetworkPlayerNameSync>();

            if (nameSync != null)
            {
                nameSync.SetPlayerName(playerName);
            }
        }

        PlayerData player =
            new PlayerData
            {
                playerID = playerID,
                playerName = playerName,
                isAlive = true,
                votePower = 1,
                hasVoted = false,
                hasUseNightAction = false,
                status = new PlayerStatus()
            };



        playerManager.RegisterPlayer(
            player
        );

        Debug.Log(
            "NETWORK GAME BOOTSTRAP: "
            + "Đã tạo PlayerData"
            + " | Player = "
            + (playerID + 1)
            + " | Name = "
            + playerName
        );
    }
}