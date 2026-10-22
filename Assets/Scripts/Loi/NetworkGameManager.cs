using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;

    [Header("Current Network Game State")]
    public NetworkVariable<NetworkGameState> CurrentState =
        new NetworkVariable<NetworkGameState>(
            NetworkGameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentState.OnValueChanged += OnGameStateChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        Debug.Log(
            "NetworkGameManager Spawned | Current State = "
            + CurrentState.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnGameStateChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnGameStateChanged(
        NetworkGameState oldState,
        NetworkGameState newState)
    {
        Debug.Log(
            "NETWORK GAME STATE: "
            + oldState
            + " → "
            + newState
        );
    }

    // =========================================================
    // DISCONNECT HANDLING
    // =========================================================

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            HandleServerPlayerDisconnect(clientId);
        }
        else
        {
            // Nếu là Client và Server (Host) đã shutdown hoặc local client bị ngắt kết nối
            if (clientId == NetworkManager.ServerClientId || (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId))
            {
                Debug.Log($"[NetworkGameManager] Client mất kết nối tới Server (clientId={clientId}) -> Về MainMenu.");
                SceneLoader.Instance?.LeaveRoomToMainMenu();
            }
        }
    }

    private void HandleServerPlayerDisconnect(ulong clientId)
    {
        int playerId = (int)clientId;
        Debug.Log($"[NetworkGameManager] SERVER XỬ LÝ DISCONNECT: ClientId={clientId}, PlayerId={playerId}");

        // 1. Tìm và despawn NetworkObject của PlayerController tương ứng
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            var stateSync = client.PlayerObject.GetComponent<NetworkPlayerStateSync>();
            if (stateSync != null)
            {
                stateSync.SetDisconnected();
            }
            if (client.PlayerObject.IsSpawned)
            {
                client.PlayerObject.Despawn(true);
            }
        }
        else
        {
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc.OwnerClientId == clientId)
                {
                    var stateSync = pc.GetComponent<NetworkPlayerStateSync>();
                    if (stateSync != null)
                    {
                        stateSync.SetDisconnected();
                    }
                    var no = pc.GetComponent<NetworkObject>();
                    if (no != null && no.IsSpawned)
                    {
                        no.Despawn(true);
                    }
                    break;
                }
            }
        }

        // 2. Xóa Player khỏi danh sách active trong PlayerManager
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RemoveDisconnectedPlayer(playerId);
        }

        // 3. Xóa tên khỏi bộ nhớ tạm
        NetworkGamePlayerNameSetup.Remove(clientId);

        // 4. Hủy các action đang chờ xử lý của Player đó
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(playerId);
        NetworkPlayerAction.RemoveDisconnectedPlayerAction(clientId);
        if (NightManager.Instance != null)
        {
            NightManager.Instance.CancelDisconnectedPlayerActions(playerId);
        }

        // 5. Reset các mối liên hệ đặc biệt (Lover / Hunter)
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null)
                {
                    if (p.loverID == playerId) p.loverID = -1;
                    if (p.hunterTargetID == playerId) p.hunterTargetID = -1;
                }
            }
        }

        // 6. Gửi ClientRpc thông báo cho tất cả Client còn kết nối
        NotifyPlayerDisconnectedClientRpc(clientId);

        // 7. Cập nhật UI trên Server
        RefreshAllGameUI(playerId);

        // 8. Cập nhật điều kiện thắng/thua theo luật hiện có
        if (NetworkGameOverManager.Instance != null)
        {
            NetworkGameOverManager.Instance.CheckServerWinCondition();
        }
        if (WinConditionManager.Instance != null)
        {
            WinConditionManager.Instance.CheckWinCondition();
        }
    }

    [ClientRpc]
    private void NotifyPlayerDisconnectedClientRpc(ulong disconnectedClientId)
    {
        if (IsServer) return;

        int playerId = (int)disconnectedClientId;
        Debug.Log($"[NetworkGameManager] CLIENT NHẬN THÔNG BÁO DISCONNECT: PlayerId={playerId}");

        // 1. Cập nhật PlayerManager trên Client
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RemoveDisconnectedPlayer(playerId);
        }

        // 2. Hủy các vote liên quan
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(playerId);

        // 3. Reset liên hệ
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null)
                {
                    if (p.loverID == playerId) p.loverID = -1;
                    if (p.hunterTargetID == playerId) p.hunterTargetID = -1;
                }
            }
        }

        // 4. Cập nhật UI trên Client
        RefreshAllGameUI(playerId);
    }

    public static void RefreshAllGameUI(int disconnectedPlayerId)
    {
        if (NetworkTargetListUI.Instance != null)
        {
            NetworkTargetListUI.Instance.Refresh();
        }

        if (NetworkVoteUI.Instance != null)
        {
            NetworkVoteUI.Instance.Refresh();
        }

        if (NetworkRoleDetailUI.Instance != null)
        {
            NetworkRoleDetailUI.Instance.OnPlayerDisconnected(disconnectedPlayerId);
        }

        ThuongMeetingRosterUI[] rosters = FindObjectsByType<ThuongMeetingRosterUI>(FindObjectsSortMode.None);
        foreach (var r in rosters)
        {
            if (r != null) r.Refresh();
        }
    }

    // Chỉ Server được quyền đổi NetworkGameState
    public void ChangeGameState(NetworkGameState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Server mới được phép đổi NetworkGameState!"
            );

            return;
        }

        Debug.Log(
            "SERVER NETWORK STATE: "
            + CurrentState.Value
            + " → "
            + newState
        );

        CurrentState.Value = newState;

        UpdateAllPlayerStates();
    }

    private void UpdateAllPlayerStates()
    {
        if (!IsServer)
            return;

        PlayerController[] players =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );

        foreach (PlayerController player in players)
        {
            player.ApplyStateFromGameState();
        }
    }
}