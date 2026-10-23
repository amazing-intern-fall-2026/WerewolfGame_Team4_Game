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
    public static bool IsPlayerLeft(ulong clientId) => LeftPlayerClientIds.Contains(clientId);

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
        LeftPlayerClientIds.Add(clientId);
        Debug.Log($"[NetworkGameManager] SERVER XỬ LÝ DISCONNECT: ClientId={clientId}, PlayerId={playerId}");

        // 1. Tìm, despawn và tiêu hủy hoàn toàn GameObject của PlayerController tương ứng
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