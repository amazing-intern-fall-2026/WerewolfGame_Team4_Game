using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    public static readonly System.Collections.Generic.HashSet<ulong> LeftPlayerClientIds = new System.Collections.Generic.HashSet<ulong>();
    private static readonly System.Collections.Generic.HashSet<ulong> processedDisconnectClientIds = new System.Collections.Generic.HashSet<ulong>();

    public static void ResetProcessedDisconnects()
    {
        processedDisconnectClientIds.Clear();
    }

    public static void ResetSessionData()
    {
        LeftPlayerClientIds.Clear();
        processedDisconnectClientIds.Clear();
        Debug.Log("[NetworkGameManager] ResetSessionData: Đã dọn sạch LeftPlayerClientIds và processedDisconnectClientIds.");
    }
    public static bool IsPlayerLeft(ulong clientId)
    {
        if (clientId == 0 || (Unity.Netcode.NetworkManager.Singleton != null && clientId == Unity.Netcode.NetworkManager.ServerClientId))
            return false;
        return LeftPlayerClientIds.Contains(clientId);
    }

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

        EnsureInGameLeaveButton();

        Debug.Log(
            "NetworkGameManager Spawned | Current State = "
            + CurrentState.Value
        );
    }

    private void Start()
    {
        EnsureInGameLeaveButton();
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnGameStateChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base.OnDestroy();
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveRoomServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != clientId)
        {
            Debug.LogWarning($"[NetworkGameManager] RequestLeaveRoomServerRpc sender mismatch: sender={senderId}, param={clientId}");
            clientId = senderId;
        }

        Debug.Log($"[NetworkGameManager] Client {clientId} yêu cầu rời phòng qua ServerRpc.");
        HandleServerPlayerDisconnect(clientId);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && clientId != NetworkManager.ServerClientId)
        {
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            HandleServerPlayerDisconnect(clientId);
        }
        else
        {
            // Neu la Client va Server (Host) da shutdown hoac local client bi ngat ket noi
            if (clientId == NetworkManager.ServerClientId || (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId))
            {
                Debug.Log($"[NetworkGameManager] Client mat ket noi toi Server (clientId={clientId}) -> Ve MainMenu.");
                if (clientId == NetworkManager.ServerClientId)
                {
                    SceneLoader.DisconnectNoticeMessage = "Host \u0111\u00e3 r\u1eddi ph\u00f2ng. Tr\u1eadn \u0111\u1ea5u \u0111\u00e3 k\u1ebft th\u00fac.";
                    if (NetworkGameOverManager.Instance != null)
                    {
                        NetworkGameOverManager.Instance.ShowGameOverUI("Host Disconnected");
                    }
                }
                SceneLoader.Instance?.LeaveRoomToMainMenu();
            }
        }
    }

    public void HandleServerPlayerDisconnect(ulong clientId)
    {
        if (processedDisconnectClientIds.Contains(clientId))
        {
            Debug.Log($"[NetworkGameManager] Client {clientId} da duoc xu ly disconnect truoc do (idempotent deduplication).");
            return;
        }
        processedDisconnectClientIds.Add(clientId);
        LeftPlayerClientIds.Add(clientId);
        int playerId = (int)clientId;
        Debug.Log($"[NetworkGameManager] SERVER XU LY DISCONNECT: ClientId={clientId}, PlayerId={playerId}");

        // 1. Tim, despawn va tieu huy hoan toan GameObject cua PlayerController tuong ung
        try
        {
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc != null && pc.OwnerClientId == clientId)
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
                    Destroy(pc.gameObject);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NetworkGameManager] Loi khi don dep PlayerController cho Client {clientId}: {ex.Message}");
        }

        // 2. Xoa Player khoi danh sach active trong PlayerManager
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RemoveDisconnectedPlayer(playerId);
        }

        // 3. Xoa ten khoi bo nho tam
        NetworkGamePlayerNameSetup.Remove(clientId);

        // 4. Huy cac action dang cho xu ly cua Player do
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(playerId);
        NetworkPlayerAction.RemoveDisconnectedPlayerAction(clientId);
        if (NightManager.Instance != null)
        {
            NightManager.Instance.CancelDisconnectedPlayerActions(playerId);
        }
        NetworkRoleActionBridge.CancelDisconnectedPlayerActions(playerId);

        // 5. Reset cac moi lien he dac biet (Lover / Hunter)
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

        // 6. Gui ClientRpc thong bao cho tat ca Client con ket noi
        try
        {
            NotifyPlayerDisconnectedClientRpc(clientId);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NetworkGameManager] Khong the gui NotifyPlayerDisconnectedClientRpc: {ex.Message}");
        }

        // 7. Cap nhat UI tren Server
        RefreshAllGameUI(playerId);

        // 8. Neu dang trong Voting Phase, kiem tra xem so phieu con lai da du de ket thuc vote chua
        NetworkPlayerVote.CheckAndResolveIfAllVoted();

        // 9. Cap nhat dieu kien thang/thua theo luat hien co
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
        LeftPlayerClientIds.Add(disconnectedClientId);
        Debug.Log($"[NetworkGameManager] CLIENT NHẬN THÔNG BÁO DISCONNECT: PlayerId={playerId}");

        // Tiêu hủy GameObject của player ngắt kết nối trên máy Client
        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc != null && pc.OwnerClientId == disconnectedClientId)
            {
                Destroy(pc.gameObject);
            }
        }

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

    public static void EnsureInGameLeaveButton()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("InGameLeaveButton");
        GameObject btnObj;
        if (existing == null)
        {
            btnObj = new GameObject("InGameLeaveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvas.transform, false);
        }
        else
        {
            btnObj = existing.gameObject;
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20, -435);
        rt.sizeDelta = new Vector2(280, 72);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.48f, 0.12f, 0.12f, 0.95f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            Debug.Log("[NetworkGameManager] InGameLeaveButton clicked -> LeaveRoomToMainMenu");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                if (Instance != null && Instance.IsSpawned)
                {
                    Instance.RequestLeaveRoomServerRpc(localId);
                }
            }
            SceneLoader.Instance?.LeaveRoomToMainMenu();
        });

        Transform txtChild = btnObj.transform.Find("Text (TMP)");
        TextMeshProUGUI txt;
        if (txtChild == null)
        {
            GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnObj.transform, false);
            txt = txtObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            txt = txtChild.GetComponent<TextMeshProUGUI>();
        }

        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var sceneTmp = FindFirstObjectByType<TextMeshProUGUI>();
        if (sceneTmp != null && sceneTmp.font != null) txt.font = sceneTmp.font;
        txt.fontSize = 24;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        txt.text = "THOÁT PHÒNG";
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