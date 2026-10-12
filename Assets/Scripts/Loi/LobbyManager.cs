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

    [Header("Speaker UI (Optional)")]
    [SerializeField]
    private SpeakerToggleSwitch speakerToggle;

    [Header("Mic UI (Optional)")]
    [SerializeField]
    private MicToggleSwitch micToggle;

    private void Awake()
    {
        players = new NetworkList<PlayerLobbyData>();
    }

    private void Start()
    {
        EnsureVoiceUI();
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
        }

        // Tự động tham gia voice khi vào Lobby
        JoinLobbyVoice();

        players.OnListChanged += OnPlayerListChanged;
        FindRoleLobbyConfig();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            AddPlayer(
                NetworkManager.LocalClientId,
                "Player " + (NetworkManager.LocalClientId + 1)
            );

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                AddPlayer(
                    clientId,
                    "Player " + (clientId + 1)
                );
            }
        }

        UpdatePlayerList();
        UpdateStartButton();

        Debug.Log(
            "LOBBY MANAGER | NetworkRoleLobbyConfig = "
            + (roleLobbyConfig != null ? "FOUND" : "NULL")
        );
    }

    public override void OnNetworkDespawn()
    {
        players.OnListChanged -= OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void JoinLobbyVoice()
    {
        if (NetworkVoiceManager.Instance != null)
        {
            StartCoroutine(
                NetworkVoiceManager.Instance.WaitAndJoinLobbyVoice()
            );
        }
        else
        {
            Debug.LogWarning(
                "LOBBY | Không tìm thấy NetworkVoiceManager để vào kênh."
            );
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
            "Lobby: Player " + (clientId + 1) + " đã vào Lobby"
        );

        AddPlayer(clientId, "Player " + (clientId + 1));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            "Lobby: Player " + (clientId + 1) + " đã rời Lobby"
        );

        RemovePlayer(clientId);
    }

    private void AddPlayer(ulong clientId, string playerName)
    {
        if (FindPlayerIndex(clientId) >= 0)
            return;

        players.Add(new PlayerLobbyData(clientId, playerName));

        Debug.Log(
            "Đã thêm Player "
            + (clientId + 1)
        );
    }

    private void RemovePlayer(ulong clientId)
    {
        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        players.RemoveAt(index);

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
            return;
        }

        PlayerLobbyData data = players[index];

        data.PlayerName = new FixedString64Bytes(playerName);
        players[index] = data;

        NetworkGamePlayerNameSetup.Save(clientId, playerName);

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
    }

    private void UpdatePlayerList()
    {
        if (playerListText == null)
            return;

        string text = "PLAYERS\n\n";

        for (int i = 0; i < players.Count; i++)
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

            if (i < players.Count - 1)
                text += "\n";
        }

        playerListText.text = text;
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

        NetworkManager.SceneManager.LoadScene(
            "Game",
            LoadSceneMode.Single
        );
    }

    // =========================================================
    // VOICE CHAT: BẬT / TẮT MIC VÀ LOA
    // =========================================================

    // Hàm được gọi từ JoinVoiceButton trong Inspector hoặc sự kiện UI
    public void JoinLobbyVoiceButton()
    {
        if (NetworkVoiceManager.Instance == null)
        {
            Debug.LogError(
                "VOICE | Không tìm thấy NetworkVoiceManager."
            );
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

        Vector2 panelPos = new Vector2(718, 110);
        Transform parentTransform = canvas.transform;

        // Tạo Panel Micro (VoiceMicPanel)
        GameObject micPanelObj = new GameObject("VoiceMicPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        micPanelObj.transform.SetParent(parentTransform, false);

        RectTransform panelRect = micPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelPos;
        panelRect.sizeDelta = new Vector2(242, 56);

        UnityEngine.UI.Image panelBg = micPanelObj.GetComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        // Tiêu đề "Micro"
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(0, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(75, 7);
        titleRect.sizeDelta = new Vector2(120, 22);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "Micro (Mic)";
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;

        // Switch Container
        GameObject switchObj = new GameObject("MicToggleSwitch", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(MicToggleSwitch));
        switchObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform switchRect = switchObj.GetComponent<RectTransform>();
        switchRect.anchorMin = new Vector2(1, 0.5f);
        switchRect.anchorMax = new Vector2(1, 0.5f);
        switchRect.pivot = new Vector2(0.5f, 0.5f);
        switchRect.anchoredPosition = new Vector2(-46, 0);
        switchRect.sizeDelta = new Vector2(62, 30);

        UnityEngine.UI.Image switchBg = switchObj.GetComponent<UnityEngine.UI.Image>();
        switchBg.color = new Color(0.2f, 0.8f, 0.35f, 1f);

        // Knob / Handle
        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        handleObj.transform.SetParent(switchObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(16, 0);
        handleRect.sizeDelta = new Vector2(24, 24);

        UnityEngine.UI.Image handleImg = handleObj.GetComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;

        // Status Label (BẬT / TẮT)
        GameObject statusLabelObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusLabelObj.transform.SetParent(micPanelObj.transform, false);
        RectTransform statusRect = statusLabelObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(0, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(75, -13);
        statusRect.sizeDelta = new Vector2(120, 18);

        TextMeshProUGUI statusLabel = statusLabelObj.GetComponent<TextMeshProUGUI>();
        statusLabel.text = "BẬT";
        statusLabel.fontSize = 13;
        statusLabel.fontStyle = FontStyles.Bold;
        statusLabel.color = new Color(0.2f, 0.85f, 0.45f, 1f);
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // Cài đặt MicToggleSwitch
        micToggle = switchObj.GetComponent<MicToggleSwitch>();
        var handleField = typeof(MicToggleSwitch).GetField("handleRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bgField = typeof(MicToggleSwitch).GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handleImgField = typeof(MicToggleSwitch).GetField("handleImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statusTextField = typeof(MicToggleSwitch).GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (handleField != null) handleField.SetValue(micToggle, handleRect);
        if (bgField != null) bgField.SetValue(micToggle, switchBg);
        if (handleImgField != null) handleImgField.SetValue(micToggle, handleImg);
        if (statusTextField != null) statusTextField.SetValue(micToggle, statusLabel);

        // Click trên toàn bộ panel sẽ kích hoạt toggle
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

        // Kiểm tra xem đã có VoiceSpeakerPanel trong Canvas chưa
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

        // Tìm VoiceMicPanel để đồng bộ vị trí và giao diện
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
                panelPos = new Vector2(micRect.anchoredPosition.x, micRect.anchoredPosition.y - 64);
            }
        }

        // Tạo Panel Loa (VoiceSpeakerPanel) với phong cách đồng nhất Mic
        GameObject speakerPanelObj = new GameObject("VoiceSpeakerPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        speakerPanelObj.transform.SetParent(parentTransform, false);

        RectTransform panelRect = speakerPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelPos;
        panelRect.sizeDelta = new Vector2(242, 56);

        UnityEngine.UI.Image panelBg = speakerPanelObj.GetComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        // Tiêu đề "Loa"
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(0, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(75, 7);
        titleRect.sizeDelta = new Vector2(120, 22);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "Loa (Speaker)";
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;

        // Switch Container
        GameObject switchObj = new GameObject("SpeakerToggleSwitch", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(SpeakerToggleSwitch));
        switchObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform switchRect = switchObj.GetComponent<RectTransform>();
        switchRect.anchorMin = new Vector2(1, 0.5f);
        switchRect.anchorMax = new Vector2(1, 0.5f);
        switchRect.pivot = new Vector2(0.5f, 0.5f);
        switchRect.anchoredPosition = new Vector2(-46, 0);
        switchRect.sizeDelta = new Vector2(62, 30);

        UnityEngine.UI.Image switchBg = switchObj.GetComponent<UnityEngine.UI.Image>();
        switchBg.color = new Color(0.2f, 0.8f, 0.35f, 1f); // Xanh lá khi bật

        // Knob / Handle
        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        handleObj.transform.SetParent(switchObj.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(16, 0);
        handleRect.sizeDelta = new Vector2(24, 24);

        UnityEngine.UI.Image handleImg = handleObj.GetComponent<UnityEngine.UI.Image>();
        handleImg.color = Color.white;

        // Status Label (BẬT / TẮT)
        GameObject statusLabelObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusLabelObj.transform.SetParent(speakerPanelObj.transform, false);
        RectTransform statusRect = statusLabelObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(0, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(75, -13);
        statusRect.sizeDelta = new Vector2(120, 18);

        TextMeshProUGUI statusLabel = statusLabelObj.GetComponent<TextMeshProUGUI>();
        statusLabel.text = "BẬT";
        statusLabel.fontSize = 13;
        statusLabel.fontStyle = FontStyles.Bold;
        statusLabel.color = new Color(0.2f, 0.85f, 0.45f, 1f);
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // Cài đặt SpeakerToggleSwitch
        speakerToggle = switchObj.GetComponent<SpeakerToggleSwitch>();
        var handleField = typeof(SpeakerToggleSwitch).GetField("handleRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bgField = typeof(SpeakerToggleSwitch).GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handleImgField = typeof(SpeakerToggleSwitch).GetField("handleImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statusTextField = typeof(SpeakerToggleSwitch).GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (handleField != null) handleField.SetValue(speakerToggle, handleRect);
        if (bgField != null) bgField.SetValue(speakerToggle, switchBg);
        if (handleImgField != null) handleImgField.SetValue(speakerToggle, handleImg);
        if (statusTextField != null) statusTextField.SetValue(speakerToggle, statusLabel);

        // Click trên toàn bộ panel sẽ kích hoạt toggle
        UnityEngine.UI.Button panelButton = speakerPanelObj.GetComponent<UnityEngine.UI.Button>();
        panelButton.onClick.AddListener(() =>
        {
            if (speakerToggle != null)
            {
                speakerToggle.Toggle();
            }
        });
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
