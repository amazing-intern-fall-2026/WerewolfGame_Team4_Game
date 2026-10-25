using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    // Mã phòng 6 chữ số — Host đồng bộ cho mọi Client
    private NetworkVariable<FixedString64Bytes> roomCodeNetwork =
        new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes(string.Empty),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Kênh Voice riêng cho mỗi lobby — Host tạo, Client đọc
    private NetworkVariable<FixedString64Bytes> voiceChannelId =
        new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes(string.Empty),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Room Code UI")]
    [SerializeField]
    private TextMeshProUGUI roomCodeDisplayText;

    [Header("Speaker UI (Optional)")]
    [SerializeField]
    private SpeakerToggleSwitch speakerToggle;

    [Header("Mic UI (Optional)")]
    [SerializeField]
    private MicToggleSwitch micToggle;

    public static LobbyManager Instance { get; private set; }
    public static int CurrentPlayerCount => Instance != null && Instance.players != null ? Instance.players.Count : 0;

    private void Awake()
    {
        Instance = this;
        players = new NetworkList<PlayerLobbyData>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        EnsureVoiceUI();
        EnsureRoomCodeUI();
    }

    private void EnsureVoiceUI()
    {
        EnsureMicToggleUI();
        EnsureSpeakerToggleUI();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkGamePlayerNameSetup.Clear();
            NetworkGameManager.LeftPlayerClientIds.Clear();

            // Khởi tạo Room Code 6 chữ số
            string code = RoomManager.CurrentRoomCode;
            if (string.IsNullOrEmpty(code))
            {
                code = RoomManager.GenerateRoomCode();
                RoomManager.CurrentRoomCode = code;
            }
            roomCodeNetwork.Value = new FixedString64Bytes(code);

            // Tạo Voice Channel duy nhất gắn liền với mã phòng của Lobby này
            string uniqueVoice = RoomManager.CurrentVoiceChannelId;
            if (string.IsNullOrEmpty(uniqueVoice))
            {
                uniqueVoice = "ww-" + code + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                RoomManager.CurrentVoiceChannelId = uniqueVoice;
            }
            voiceChannelId.Value = new FixedString64Bytes(uniqueVoice);
            Debug.Log($"LOBBY MANAGER | Room Code: {code} | Voice Channel ID: {uniqueVoice}");
        }

        // Đăng ký lắng nghe khi mã phòng hoặc kênh voice thay đổi
        roomCodeNetwork.OnValueChanged += OnRoomCodeChanged;
        voiceChannelId.OnValueChanged += OnVoiceChannelIdChanged;

        players.OnListChanged += OnPlayerListChanged;
        FindRoleLobbyConfig();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            SyncPlayersWithConnectedClients();
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        EnsureRoomCodeUI();
        EnsureLobbyLeaveButton();
        UpdateRoomCodeUI(roomCodeNetwork.Value.ToString());

        UpdatePlayerList();
        UpdateStartButton();

        // Tự động xác nhận người chơi vào Lobby ngay khi kết nối (không bắt buộc phải bấm nút SET NAME)
        AutoRegisterLocalPlayer();

        // Join voice sau khi channel ID đã sẵn sàng
        JoinLobbyVoice();

        Debug.Log(
            "LOBBY MANAGER | NetworkRoleLobbyConfig = "
            + (roleLobbyConfig != null ? "FOUND" : "NULL")
        );
    }

    private float autoSyncTimer = 0f;

    private void Update()
    {
        if (!IsServer || !IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        autoSyncTimer += Time.deltaTime;
        if (autoSyncTimer >= 1.0f)
        {
            autoSyncTimer = 0f;
            int connectedCount = NetworkManager.Singleton.ConnectedClientsIds?.Count ?? 0;
            if (players == null || players.Count != connectedCount || (players.Count == 0 && connectedCount > 0))
            {
                SyncPlayersWithConnectedClients();
            }
        }
    }

    private void AutoRegisterLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        string defaultName = "Player " + (localId + 1);

        if (playerNameInput != null)
        {
            playerNameInput.text = defaultName;
        }

        Debug.Log($"LOBBY MANAGER | Tự động xác nhận người chơi Local Client {localId} với tên: '{defaultName}'");
        SubmitPlayerNameServerRpc(defaultName);
    }

    public override void OnNetworkDespawn()
    {
        roomCodeNetwork.OnValueChanged -= OnRoomCodeChanged;
        voiceChannelId.OnValueChanged -= OnVoiceChannelIdChanged;
        players.OnListChanged -= OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnRoomCodeChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        string code = newVal.ToString();
        Debug.Log("LOBBY MANAGER | Nhận Room Code từ Host: " + code);
        UpdateRoomCodeUI(code);
    }

    private void UpdateRoomCodeUI(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        RoomManager.CurrentRoomCode = code;

        if (roomCodeDisplayText != null)
        {
            roomCodeDisplayText.text = "PHÒNG: " + code;
        }
    }

    private void JoinLobbyVoice()
    {
        if (NetworkVoiceManager.Instance == null)
            return;

        string channel = voiceChannelId.Value.ToString();

        // Nếu Host chưa set (trường hợp Client spawn rất nhanh trước Host), chờ
        if (string.IsNullOrEmpty(channel))
        {
            StartCoroutine(NetworkVoiceManager.Instance.WaitAndJoinLobbyVoice());
        }
        else
        {
            StartCoroutine(NetworkVoiceManager.Instance.WaitAndJoinVoiceChannel(channel));
        }
    }

    private void OnVoiceChannelIdChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        string channel = newVal.ToString();
        if (string.IsNullOrEmpty(channel))
            return;

        Debug.Log("LOBBY MANAGER | Nhận Voice Channel ID từ Host: " + channel);

        if (NetworkVoiceManager.Instance != null)
        {
            StartCoroutine(NetworkVoiceManager.Instance.WaitAndJoinVoiceChannel(channel));
        }
    }

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

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player " + (clientId + 1) + " đã vào Lobby (ClientId=" + clientId + ")"
        );

        NetworkGameManager.LeftPlayerClientIds.Remove(clientId);
        SyncPlayersWithConnectedClients();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLeaveLobbyServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != clientId)
        {
            Debug.LogWarning($"[LobbyManager] RequestLeaveLobbyServerRpc sender mismatch: sender={senderId}, param={clientId}");
            clientId = senderId;
        }

        Debug.Log($"LOBBY MANAGER | Client {clientId} yêu cầu rời Lobby qua ServerRpc.");
        NetworkGameManager.LeftPlayerClientIds.Add(clientId);
        RemovePlayer(clientId);
        NetworkGamePlayerNameSetup.Remove(clientId);
        SyncPlayersWithConnectedClients(clientId);

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
            Debug.Log($"Lobby: Player {clientId + 1} đã rời Lobby (ClientId={clientId})");
            NetworkGameManager.LeftPlayerClientIds.Add(clientId);
            RemovePlayer(clientId);
            NetworkGamePlayerNameSetup.Remove(clientId);
            SyncPlayersWithConnectedClients(clientId);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
        }
        else
        {
            // Nếu là Client và Host bị disconnect hoặc local client bị ngắt kết nối
            if (clientId == NetworkManager.ServerClientId || (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId))
            {
                Debug.Log($"Lobby: Client nhận disconnect khỏi Host (clientId={clientId}) -> Về MainMenu.");
                SceneLoader.Instance?.LeaveRoomToMainMenu();
            }
        }
    }

    public void SyncPlayersWithConnectedClients(ulong excludedClientId = ulong.MaxValue)
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        var connectedSet = new HashSet<ulong>(NetworkManager.Singleton.ConnectedClientsIds ?? System.Array.Empty<ulong>());

        // 1. Loại bỏ các player không còn kết nối thực tế, hoặc là excludedClientId
        for (int i = players.Count - 1; i >= 0; i--)
        {
            ulong cid = players[i].ClientId;
            if (cid == excludedClientId || !connectedSet.Contains(cid) || (cid != NetworkManager.ServerClientId && NetworkGameManager.LeftPlayerClientIds.Contains(cid)))
            {
                Debug.Log($"LOBBY MANAGER | Xóa player ClientId {cid} khỏi Lobby vì không còn kết nối / đã ngắt kết nối.");
                players.RemoveAt(i);
            }
        }

        // 2. Thêm các client đang kết nối thực tế mà chưa có trong players (bỏ qua excludedClientId)
        foreach (ulong cid in connectedSet)
        {
            if (cid == excludedClientId)
                continue;

            // Client đang kết nối thực tế thì không thể bị tính là đã rời phòng
            NetworkGameManager.LeftPlayerClientIds.Remove(cid);

            if (FindPlayerIndex(cid) < 0)
            {
                string name = NetworkGamePlayerNameSetup.GetName(cid);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Player " + (cid + 1);
                }
                players.Add(new PlayerLobbyData(cid, name));
                NetworkGamePlayerNameSetup.Save(cid, name);
                Debug.Log($"LOBBY MANAGER | Thêm player ClientId {cid} ({name}) vào Lobby.");
            }
        }

        // 3. Cập nhật Registry số lượng người chơi chính xác
        string code = roomCodeNetwork.Value.ToString();
        if (!string.IsNullOrEmpty(code))
        {
            RoomManager.SetPlayerCountInRegistry(code, players.Count);
        }

        // 4. Đồng bộ vai mặc định nếu Host chưa tùy chỉnh tay
        if (roleLobbyConfig == null)
            FindRoleLobbyConfig();

        if (roleLobbyConfig != null)
        {
            roleLobbyConfig.InitializeDefaultRolesForPlayerCount(players.Count);
        }

        UpdatePlayerList();
        UpdateStartButton();
    }

    private void AddPlayer(ulong clientId, string playerName)
    {
        if (FindPlayerIndex(clientId) >= 0)
            return;

        players.Add(new PlayerLobbyData(clientId, playerName));
        NetworkGamePlayerNameSetup.Save(clientId, playerName);

        string code = roomCodeNetwork.Value.ToString();
        if (!string.IsNullOrEmpty(code))
        {
            RoomManager.SetPlayerCountInRegistry(code, players.Count);
        }

        Debug.Log(
            "Đã thêm Player "
            + (clientId + 1)
            + $" ({playerName})"
        );
    }

    private void RemovePlayer(ulong clientId)
    {
        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        players.RemoveAt(index);
        string code = roomCodeNetwork.Value.ToString();
        if (!string.IsNullOrEmpty(code))
        {
            RoomManager.SetPlayerCountInRegistry(code, players.Count);
        }

        Debug.Log(
            "Đã xóa Player " + (clientId + 1) + " khỏi Lobby."
        );
    }

    private int FindPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
                return i;
        }

        return -1;
    }

    public void SubmitPlayerName()
    {
        if (NetworkManager.Singleton == null)
            return;

        string playerName =
            "Player " + (NetworkManager.Singleton.LocalClientId + 1);

        if (playerNameInput != null)
        {
            string input = playerNameInput.text.Trim();

            if (!string.IsNullOrWhiteSpace(input))
                playerName = input;
        }

        SubmitPlayerNameServerRpc(playerName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerNameServerRpc(
        string playerName,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        playerName = playerName.Trim();

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player " + (clientId + 1);

        if (playerName.Length > 20)
            playerName = playerName.Substring(0, 20);

        int index = FindPlayerIndex(clientId);

        if (index < 0)
        {
            AddPlayer(clientId, playerName);
        }
        else
        {
            PlayerLobbyData data = players[index];
            data.PlayerName = new FixedString64Bytes(playerName);
            players[index] = data;
            NetworkGamePlayerNameSetup.Save(clientId, playerName);
        }

        Debug.Log(
            "LOBBY NAME | Player "
            + (clientId + 1)
            + " → "
            + playerName
        );
    }

    private void OnPlayerListChanged(
        NetworkListEvent<PlayerLobbyData> changeEvent)
    {
        UpdatePlayerList();
        UpdateStartButton();

        if (NetworkRoleLobbyUI.Instance != null)
        {
            NetworkRoleLobbyUI.Instance.RefreshRoleCount();
        }
    }

    private void UpdatePlayerList()
    {
        if (playerListText == null)
            return;

        int count = players != null ? players.Count : 0;
        string text = $"PLAYERS ({count})\n\n";

        for (int i = 0; i < count; i++)
        {
            PlayerLobbyData player = players[i];

            string playerName;

            if (player.PlayerName.Length == 0)
            {
                playerName = "Player " + (player.ClientId + 1);
            }
            else
            {
                playerName = player.PlayerName.ToString();
            }

            text +=
                "Player "
                + (player.ClientId + 1)
                + " - "
                + playerName;

            if (i < count - 1)
                text += "\n";
        }

        playerListText.text = text;

        if (NetworkRoleLobbyUI.Instance != null)
        {
            NetworkRoleLobbyUI.Instance.RefreshRoleCount();
        }
    }

    private void UpdateStartButton()
    {
        if (startGameButton == null)
            return;

        startGameButton.SetActive(IsServer);
    }

    public void StartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Chỉ Host mới được Start Game!");
            return;
        }

        // Đồng bộ sĩ số kết nối thực tế ngay trước khi bắt đầu
        SyncPlayersWithConnectedClients();

        // Fallback: nếu danh sách players vẫn rỗng nhưng ConnectedClientsIds có người chơi
        if (players.Count == 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
        {
            foreach (ulong cid in NetworkManager.Singleton.ConnectedClientsIds)
            {
                NetworkGameManager.LeftPlayerClientIds.Remove(cid);
                string name = NetworkGamePlayerNameSetup.GetName(cid);
                players.Add(new PlayerLobbyData(cid, name));
            }
            UpdatePlayerList();
        }

        if (players.Count <= 0)
        {
            Debug.LogWarning("Không thể Start Game! Chưa có người chơi nào trong phòng.");
            return;
        }

        // 1. Thu thập danh sách ClientId thực tế đang có mặt trong Lobby
        var lobbyClientIds = new HashSet<ulong>();
        for (int i = 0; i < players.Count; i++)
        {
            lobbyClientIds.Add(players[i].ClientId);
        }

        // 2. Ngắt kết nối dứt điểm mọi client kết nối mồ côi không có trong Lobby (ngoại trừ Host)
        if (NetworkManager.Singleton != null)
        {
            foreach (ulong cid in NetworkManager.Singleton.ConnectedClientsIds.ToArray())
            {
                if (cid != NetworkManager.ServerClientId && !lobbyClientIds.Contains(cid))
                {
                    Debug.Log($"LOBBY START | Ngắt kết nối client {cid} vì không hợp lệ / không có trong Lobby.");
                    NetworkGameManager.LeftPlayerClientIds.Add(cid);
                    NetworkManager.Singleton.DisconnectClient(cid);
                }
            }
        }

        // 3. Thiết lập danh sách người chơi chính thức cho ván đấu
        NetworkGamePlayerSetup.SetActiveMatchClientIds(lobbyClientIds);

        if (roleLobbyConfig == null)
            FindRoleLobbyConfig();

        if (roleLobbyConfig == null)
        {
            Debug.LogError(
                "Không thể Start Game: NetworkRoleLobbyConfig không tồn tại!"
            );
            return;
        }

        int playerCount = players.Count;
        int roleCount = roleLobbyConfig.GetTotalRoleAmount();

        // Tự động cân bằng vai trò nếu số lượng role chưa khớp sĩ số người chơi thực tế
        if (playerCount != roleCount)
        {
            Debug.Log($"LOBBY START | Tự động cân bằng vai trò cho {playerCount} người chơi.");
            roleLobbyConfig.InitializeDefaultRolesForPlayerCount(playerCount);
            roleCount = roleLobbyConfig.GetTotalRoleAmount();
        }

        Debug.Log(
            "LOBBY START CHECK | Players = "
            + playerCount
            + " | Roles = "
            + roleCount
        );

        if (playerCount != roleCount)
        {
            Debug.LogWarning(
                "Không thể Start Game! Số Role ("
                + roleCount
                + ") phải bằng số Player ("
                + playerCount
                + ")."
            );
            return;
        }

        NetworkGameRoleSetup.Save(
            roleLobbyConfig.GetRoleAmounts()
        );

        Debug.Log(
            "LOBBY START GAME | Đã lưu Role Setup. Total Roles = "
            + NetworkGameRoleSetup.GetTotalRoleAmount()
        );

        Debug.Log("HOST START GAME!");

        NetworkPlayerVote.ResetLocalStaticVotes();

        NetworkManager.SceneManager.LoadScene(
            "Game",
            LoadSceneMode.Single
        );
    }

    // =========================================================
    // VOICE CHAT: BẬT / TẮT MIC VÀ LOA
    // =========================================================

    public void JoinLobbyVoiceButton()
    {
        if (NetworkVoiceManager.Instance == null)
        {
            Debug.LogError("VOICE | Không tìm thấy NetworkVoiceManager.");
            return;
        }

        if (!NetworkVoiceManager.Instance.IsInLobbyVoice)
        {
            NetworkVoiceManager.Instance.JoinLobbyVoice();
        }
        else
        {
            NetworkVoiceManager.Instance.ToggleMicrophone();
        }
    }

    public void ToggleMicButton()
    {
        if (micToggle != null)
        {
            micToggle.Toggle();
        }
        else if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.ToggleMicrophone();
        }
    }

    public void ToggleSpeakerButton()
    {
        if (speakerToggle != null)
        {
            speakerToggle.Toggle();
        }
        else if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.ToggleOutputAudio();
        }
    }

    // =========================================================
    // TỰ ĐỘNG TẠO / LIÊN KẾT GIAO DIỆN BẬT TẮT MICRO VÀ LOA
    // =========================================================

    private void EnsureMicToggleUI()
    {
        if (micToggle != null)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existingMic = canvas.transform.Find("VoiceMicPanel");
        if (existingMic != null)
        {
            micToggle = existingMic.GetComponentInChildren<MicToggleSwitch>(true);
            if (micToggle != null)
                return;
        }
        else
        {
            MicToggleSwitch foundSwitch = canvas.GetComponentInChildren<MicToggleSwitch>(true);
            if (foundSwitch != null)
            {
                micToggle = foundSwitch;
                return;
            }
        }

        Vector2 panelPos = new Vector2(700, -30);
        Transform parentTransform = canvas.transform;

        GameObject micPanelObj = new GameObject("VoiceMicPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        micPanelObj.transform.SetParent(parentTransform, false);

        RectTransform panelRect = micPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelPos;
        panelRect.sizeDelta = new Vector2(320, 78);

        UnityEngine.UI.Image panelBg = micPanelObj.GetComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(0, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(90, 8);
        titleRect.sizeDelta = new Vector2(170, 32);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "Micro (Mic)";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject switchObj = new GameObject("MicToggleSwitch", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(MicToggleSwitch));
        switchObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform switchRect = switchObj.GetComponent<RectTransform>();
        switchRect.anchorMin = new Vector2(1, 0.5f);
        switchRect.anchorMax = new Vector2(1, 0.5f);
        switchRect.pivot = new Vector2(0.5f, 0.5f);
        switchRect.anchoredPosition = new Vector2(-46, 0);
        switchRect.sizeDelta = new Vector2(76, 38);

        UnityEngine.UI.Image switchBg = switchObj.GetComponent<UnityEngine.UI.Image>();
        switchBg.color = new Color(0.2f, 0.8f, 0.35f, 1f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        handleObj.transform.SetParent(switchObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(18, 0);
        handleRect.sizeDelta = new Vector2(30, 30);

        UnityEngine.UI.Image handleImg = handleObj.GetComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;

        GameObject statusLabelObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusLabelObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform statusRect = statusLabelObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(0, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(90, -18);
        statusRect.sizeDelta = new Vector2(170, 26);

        TextMeshProUGUI statusLabel = statusLabelObj.GetComponent<TextMeshProUGUI>();
        statusLabel.text = "BẬT";
        statusLabel.fontSize = 18;
        statusLabel.fontStyle = FontStyles.Bold;
        statusLabel.color = new Color(0.2f, 0.85f, 0.45f, 1f);
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;

        micToggle = switchObj.GetComponent<MicToggleSwitch>();
        var handleField = typeof(MicToggleSwitch).GetField("handleRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bgField = typeof(MicToggleSwitch).GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handleImgField = typeof(MicToggleSwitch).GetField("handleImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statusTextField = typeof(MicToggleSwitch).GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (handleField != null) handleField.SetValue(micToggle, handleRect);
        if (bgField != null) bgField.SetValue(micToggle, switchBg);
        if (handleImgField != null) handleImgField.SetValue(micToggle, handleImg);
        if (statusTextField != null) statusTextField.SetValue(micToggle, statusLabel);

        UnityEngine.UI.Button panelButton = micPanelObj.GetComponent<UnityEngine.UI.Button>();
        panelButton.onClick.AddListener(() =>
        {
            if (micToggle != null)
            {
                micToggle.Toggle();
            }
        });
    }

    private void EnsureSpeakerToggleUI()
    {
        if (speakerToggle != null)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existingSpeaker = canvas.transform.Find("VoiceSpeakerPanel");
        if (existingSpeaker != null)
        {
            speakerToggle = existingSpeaker.GetComponentInChildren<SpeakerToggleSwitch>(true);
            if (speakerToggle != null)
                return;
        }
        else
        {
            SpeakerToggleSwitch foundSwitch = canvas.GetComponentInChildren<SpeakerToggleSwitch>(true);
            if (foundSwitch != null)
            {
                speakerToggle = foundSwitch;
                return;
            }
        }

        Transform micPanelT = canvas.transform.Find("VoiceMicPanel");
        if (micPanelT == null && micToggle != null)
        {
            micPanelT = micToggle.transform.parent != null && micToggle.transform.parent != canvas.transform
                ? micToggle.transform.parent
                : micToggle.transform;
        }

        Vector2 panelPos = new Vector2(718, 46);
        Transform parentTransform = canvas.transform;

        if (micPanelT != null)
        {
            parentTransform = micPanelT.parent != null ? micPanelT.parent : canvas.transform;
            RectTransform micRect = micPanelT.GetComponent<RectTransform>();
            if (micRect != null)
            {
                panelPos = new Vector2(micRect.anchoredPosition.x, micRect.anchoredPosition.y - 104);
            }
        }

        GameObject speakerPanelObj = new GameObject("VoiceSpeakerPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        speakerPanelObj.transform.SetParent(parentTransform, false);

        RectTransform panelRect = speakerPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelPos;
        panelRect.sizeDelta = new Vector2(320, 78);

        UnityEngine.UI.Image panelBg = speakerPanelObj.GetComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(0, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(90, 8);
        titleRect.sizeDelta = new Vector2(170, 32);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "Loa (Speaker)";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject switchObj = new GameObject("SpeakerToggleSwitch", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(SpeakerToggleSwitch));
        switchObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform switchRect = switchObj.GetComponent<RectTransform>();
        switchRect.anchorMin = new Vector2(1, 0.5f);
        switchRect.anchorMax = new Vector2(1, 0.5f);
        switchRect.pivot = new Vector2(0.5f, 0.5f);
        switchRect.anchoredPosition = new Vector2(-46, 0);
        switchRect.sizeDelta = new Vector2(76, 38);

        UnityEngine.UI.Image switchBg = switchObj.GetComponent<UnityEngine.UI.Image>();
        switchBg.color = new Color(0.2f, 0.8f, 0.35f, 1f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        handleObj.transform.SetParent(switchObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(18, 0);
        handleRect.sizeDelta = new Vector2(30, 30);

        UnityEngine.UI.Image handleImg = handleObj.GetComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;

        GameObject statusLabelObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusLabelObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform statusRect = statusLabelObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(0, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(90, -18);
        statusRect.sizeDelta = new Vector2(170, 26);

        TextMeshProUGUI statusLabel = statusLabelObj.GetComponent<TextMeshProUGUI>();
        statusLabel.text = "BẬT";
        statusLabel.fontSize = 18;
        statusLabel.fontStyle = FontStyles.Bold;
        statusLabel.color = new Color(0.2f, 0.85f, 0.45f, 1f);
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;

        speakerToggle = switchObj.GetComponent<SpeakerToggleSwitch>();
        var handleField = typeof(SpeakerToggleSwitch).GetField("handleRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bgField = typeof(SpeakerToggleSwitch).GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handleImgField = typeof(SpeakerToggleSwitch).GetField("handleImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statusTextField = typeof(SpeakerToggleSwitch).GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (handleField != null) handleField.SetValue(speakerToggle, handleRect);
        if (bgField != null) bgField.SetValue(speakerToggle, switchBg);
        if (handleImgField != null) handleImgField.SetValue(speakerToggle, handleImg);
        if (statusTextField != null) statusTextField.SetValue(speakerToggle, statusLabel);

        UnityEngine.UI.Button panelButton = speakerPanelObj.GetComponent<UnityEngine.UI.Button>();
        panelButton.onClick.AddListener(() =>
        {
            if (speakerToggle != null)
            {
                speakerToggle.Toggle();
            }
        });
    }

    // =========================================================
    // ROOM CODE UI (HIỂN THỊ VÀ SAO CHÉP MÃ PHÒNG 6 SỐ)
    // =========================================================

    private void EnsureRoomCodeUI()
    {
        if (roomCodeDisplayText != null)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existingBadge = canvas.transform.Find("RoomCodeBadge");
        if (existingBadge != null)
        {
            roomCodeDisplayText = existingBadge.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        GameObject badgeObj = new GameObject("RoomCodeBadge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        badgeObj.transform.SetParent(canvas.transform, false);

        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0, -90);
        badgeRect.sizeDelta = new Vector2(420, 76);

        UnityEngine.UI.Image badgeBg = badgeObj.GetComponent<UnityEngine.UI.Image>();
        badgeBg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

        // Text hiển thị mã phòng
        GameObject textObj = new GameObject("RoomCodeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(badgeObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(0.62f, 1);
        textRect.offsetMin = new Vector2(16, 0);
        textRect.offsetMax = new Vector2(0, 0);

        roomCodeDisplayText = textObj.GetComponent<TextMeshProUGUI>();
        roomCodeDisplayText.fontSize = 28;
        roomCodeDisplayText.fontStyle = FontStyles.Bold;
        roomCodeDisplayText.color = new Color(1f, 0.85f, 0.2f, 1f);
        roomCodeDisplayText.alignment = TextAlignmentOptions.MidlineLeft;
        roomCodeDisplayText.text = "PHÒNG: ......";

        // Nút Sao Chép
        GameObject copyBtnObj = new GameObject("CopyButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        copyBtnObj.transform.SetParent(badgeObj.transform, false);
        RectTransform copyRect = copyBtnObj.GetComponent<RectTransform>();
        copyRect.anchorMin = new Vector2(0.63f, 0.12f);
        copyRect.anchorMax = new Vector2(0.97f, 0.88f);
        copyRect.offsetMin = Vector2.zero;
        copyRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image copyBg = copyBtnObj.GetComponent<UnityEngine.UI.Image>();
        copyBg.color = new Color(0.2f, 0.55f, 0.9f, 1f);

        GameObject copyTextObj = new GameObject("CopyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        copyTextObj.transform.SetParent(copyBtnObj.transform, false);
        RectTransform copyTextRect = copyTextObj.GetComponent<RectTransform>();
        copyTextRect.anchorMin = Vector2.zero;
        copyTextRect.anchorMax = Vector2.one;
        copyTextRect.offsetMin = Vector2.zero;
        copyTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI copyLabel = copyTextObj.GetComponent<TextMeshProUGUI>();
        copyLabel.text = "CHÉP MÃ";
        copyLabel.fontSize = 22;
        copyLabel.fontStyle = FontStyles.Bold;
        copyLabel.color = Color.white;
        copyLabel.alignment = TextAlignmentOptions.Center;

        UnityEngine.UI.Button copyBtn = copyBtnObj.GetComponent<UnityEngine.UI.Button>();
        copyBtn.onClick.AddListener(() =>
        {
            CopyRoomCode(copyLabel);
        });
    }

    public void CopyRoomCode(TextMeshProUGUI label = null)
    {
        string code = roomCodeNetwork.Value.ToString();
        if (string.IsNullOrEmpty(code))
            code = RoomManager.CurrentRoomCode;

        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log("LOBBY MANAGER | Đã sao chép mã phòng: " + code);
            if (label != null)
            {
                StartCoroutine(FlashCopiedFeedback(label));
            }
        }
    }

    private System.Collections.IEnumerator FlashCopiedFeedback(TextMeshProUGUI label)
    {
        string prev = label.text;
        label.text = "ĐÃ CHÉP!";
        yield return new WaitForSecondsRealtime(1.5f);
        if (label != null) label.text = prev;
    }

    public static void EnsureLobbyLeaveButton()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("LobbyLeaveButton");
        GameObject btnObj;
        if (existing == null)
        {
            btnObj = new GameObject("LobbyLeaveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
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
        rt.anchoredPosition = new Vector2(25, -25);
        rt.sizeDelta = new Vector2(200, 56);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.48f, 0.12f, 0.12f, 0.95f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            Debug.Log("[LobbyManager] LobbyLeaveButton clicked -> LeaveRoomToMainMenu");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                if (Instance != null && Instance.IsSpawned)
                {
                    Instance.RequestLeaveLobbyServerRpc(localId);
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
        txt.fontSize = 22;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        txt.text = "THOÁT PHÒNG";
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

    public PlayerLobbyData(ulong clientId, string playerName)
    {
        ClientId = clientId;
        PlayerName = new FixedString64Bytes(playerName);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(PlayerLobbyData other)
    {
        return ClientId == other.ClientId
            && PlayerName == other.PlayerName;
    }
}
