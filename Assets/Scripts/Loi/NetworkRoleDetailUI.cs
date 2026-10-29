using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkRoleDetailUI : NetworkBehaviour
{
    [Serializable]
    public class RoleImageEntry
    {
        public RoleType role;
        public Sprite image;
    }

    private static NetworkRoleDetailUI instance;
    public static NetworkRoleDetailUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<NetworkRoleDetailUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
        private set
        {
            instance = value;
        }
    }

    [Header("UI Panels")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button backdropCloseButton;
    [SerializeField] private Button closeButton;

    [Header("Role Detail Fields")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI roleNameText;
    [SerializeField] private TextMeshProUGUI factionText;
    [SerializeField] private Image roleImage;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillDescriptionText;
    [SerializeField] private TextMeshProUGUI restrictionsText;

    [Header("Role Images")]
    [SerializeField] private RoleImageEntry[] roleImages;
    [SerializeField] private Sprite mysteryRoleSprite;

    [Header("Debug Mode")]
    [SerializeField] private Button debugModeToggleButton;
    [SerializeField] private TextMeshProUGUI debugModeStatusText;
    [SerializeField] private bool isDebugMode = false;

    public bool IsDebugMode => isDebugMode;
    public GameObject PanelRoot => panelRoot;
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private int currentDisplayedPlayerId = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (backdropCloseButton != null)
        {
            backdropCloseButton.onClick.RemoveAllListeners();
            backdropCloseButton.onClick.AddListener(Close);
        }

        if (debugModeToggleButton != null)
        {
            debugModeToggleButton.onClick.RemoveAllListeners();
            debugModeToggleButton.onClick.AddListener(ToggleDebugMode);
        }

        UpdateDebugUI();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (instance == this)
        {
            instance = null;
        }
    }

    public void ToggleDebugMode()
    {
        isDebugMode = !isDebugMode;
        UpdateDebugUI();

        // Nếu đang mở panel xem người khác, làm mới lại thông tin theo chế độ mới
        if (panelRoot != null && panelRoot.activeSelf && currentDisplayedPlayerId >= 0)
        {
            ShowForPlayer(currentDisplayedPlayerId);
        }
    }

    private void UpdateDebugUI()
    {
        if (debugModeStatusText != null)
        {
            debugModeStatusText.text = isDebugMode ? "DEBUG: BẬT" : "DEBUG: TẮT";
            debugModeStatusText.color = isDebugMode ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
        }
    }

    public void OnPlayerDisconnected(int disconnectedPlayerId)
    {
        if (IsOpen && currentDisplayedPlayerId == disconnectedPlayerId)
        {
            Debug.Log($"[NetworkRoleDetailUI] Người chơi {disconnectedPlayerId} đang xem chi tiết đã ngắt kết nối -> Đóng Panel.");
            Close();
        }
    }

    /// <summary>
    /// Hiển thị thông tin Role của chính người chơi local (được cấp quyền 100%).
    /// </summary>
    public void ShowForLocalPlayer()
    {
        int localPlayerId = GetLocalPlayerId();
        string localName = GetPlayerNameById(localPlayerId);

        RoleType localRole = RoleType.Villager;
        if (NetworkRoleSync.LocalInstance != null)
        {
            localRole = NetworkRoleSync.LocalInstance.LocalRole;
        }

        currentDisplayedPlayerId = localPlayerId;
        DisplayRoleInfo(localName, localRole, isSecret: false);
    }

    /// <summary>
    /// Hiển thị thông tin Role trực tiếp cho việc kiểm thử UI.
    /// </summary>
    public void ShowRoleForTesting(RoleType role, string playerName, bool isSecret)
    {
        DisplayRoleInfo(playerName, role, isSecret);
    }

    /// <summary>
    /// Hiển thị thông tin Role của một người chơi bất kỳ (kiểm tra phân quyền nghiêm ngặt).
    /// </summary>
    public void ShowForPlayer(int targetPlayerId, string targetName = null)
    {
        currentDisplayedPlayerId = targetPlayerId;
        int localPlayerId = GetLocalPlayerId();

        if (string.IsNullOrEmpty(targetName))
        {
            targetName = GetPlayerNameById(targetPlayerId);
        }

        // 1. Nếu là chính mình -> hiển thị đầy đủ
        if (targetPlayerId == localPlayerId)
        {
            ShowForLocalPlayer();
            return;
        }

        // 2. Nếu đang ở giai đoạn Game Over hoặc Role Reveal -> công khai
        bool isPublicPhase = false;
        if (NetworkPhaseSync.Instance != null)
        {
            GamePhase phase = NetworkPhaseSync.Instance.CurrentPhase.Value;
            if (phase == GamePhase.GameOver || phase == GamePhase.RoleReveal)
            {
                isPublicPhase = true;
            }
        }

        // 3. Nếu là Server hoặc chế độ Debug đang bật trên Host
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && isDebugMode)
        {
            RoleType role = GetRoleFromServer(targetPlayerId);
            DisplayRoleInfo(targetName, role, isSecret: false);
            return;
        }

        // 4. Nếu là Client và Debug mode bật -> gửi yêu cầu ServerRpc kiểm tra
        if (isDebugMode || isPublicPhase)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                RequestRoleDetailServerRpc(targetPlayerId);
                return;
            }
        }

        // 5. Mặc định: Bí mật! Không tiết lộ thông tin vai trò của người chơi khác
        DisplaySecretInfo(targetName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRoleDetailServerRpc(int targetPlayerId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        bool isSelf = (int)senderClientId == targetPlayerId;
        bool isDebug = isDebugMode;
        bool isPublic = NetworkPhaseSync.Instance != null &&
                        (NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.GameOver ||
                         NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.RoleReveal);

        ClientRpcParams targetRpc = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new List<ulong> { senderClientId }
            }
        };

        if (isSelf || isDebug || isPublic)
        {
            RoleType role = GetRoleFromServer(targetPlayerId);
            ReceiveRoleDetailClientRpc(targetPlayerId, role, isSecret: false, targetRpc);
        }
        else
        {
            ReceiveRoleDetailClientRpc(targetPlayerId, RoleType.Villager, isSecret: true, targetRpc);
        }
    }

    [ClientRpc]
    private void ReceiveRoleDetailClientRpc(int targetPlayerId, RoleType role, bool isSecret, ClientRpcParams clientRpcParams = default)
    {
        if (currentDisplayedPlayerId != targetPlayerId)
            return;

        string targetName = GetPlayerNameById(targetPlayerId);
        if (isSecret)
        {
            DisplaySecretInfo(targetName);
        }
        else
        {
            DisplayRoleInfo(targetName, role, isSecret: false);
        }
    }

    private void DisplayRoleInfo(string playerName, RoleType role, bool isSecret)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }
        transform.SetAsLastSibling();

        RoleInfoData info = isSecret ? RoleInfoDatabase.GetSecretRoleInfo(playerName) : RoleInfoDatabase.GetRoleInfo(role);

        if (!isSecret && (role == RoleType.WhiteHound || role == RoleType.WhiteWolf) && IsDisplayedPlayerWhiteHoundAwakened())
        {
            info.DisplayName = "Bạch khuyển (Phe DogSpirit)";
            info.FactionName = "Phe Quái vật (DogSpirit)";
            info.Faction = FactionType.Monster;
            info.SkillName = "Tấn công ban đêm";
            info.SkillDescription = "Đã bị DogSpirit cắn và chuyển sang phe DogSpirit! Mỗi đêm có thể chọn một người chơi để tấn công.";
            info.Restrictions = "Sử dụng chức năng tấn công vào ban đêm cùng phe DogSpirit.";
        }

        if (headerText != null)
        {
            headerText.text = isSecret ? "THÔNG TIN NGƯỜI CHƠI" : "THÔNG TIN VAI TRÒ";
        }

        if (playerNameText != null)
        {
            playerNameText.text = $"Người chơi: <color=#FFDD44>{playerName}</color>";
        }

        if (roleNameText != null)
        {
            roleNameText.text = $"Vai trò: <color=#00FFAA>{info.DisplayName}</color>";
        }

        if (factionText != null)
        {
            Color factionColor = GetFactionColor(info.Faction, isSecret);
            string hexColor = ColorUtility.ToHtmlStringRGB(factionColor);
            factionText.text = $"Phe: <color=#{hexColor}>{info.FactionName}</color>";
        }

        if (roleImage != null)
        {
            Sprite sprite = isSecret ? mysteryRoleSprite : GetRoleSprite(role);
            roleImage.sprite = sprite;
            roleImage.enabled = sprite != null;
        }

        if (descriptionText != null)
        {
            descriptionText.text = info.Description;
        }

        if (skillNameText != null)
        {
            skillNameText.text = $"Kỹ năng: <color=#44CCFF>{info.SkillName}</color>";
        }

        if (skillDescriptionText != null)
        {
            skillDescriptionText.text = info.SkillDescription;
        }

        if (restrictionsText != null)
        {
            restrictionsText.text = $"Giới hạn & Điều kiện: <color=#FFAA44>{info.Restrictions}</color>";
        }
    }

    private void DisplaySecretInfo(string playerName)
    {
        DisplayRoleInfo(playerName, RoleType.Villager, isSecret: true);
    }

    private bool IsDisplayedPlayerWhiteHoundAwakened()
    {
        int localId = GetLocalPlayerId();
        if (currentDisplayedPlayerId == localId &&
            NetworkRoleSync.LocalInstance != null &&
            NetworkRoleSync.LocalInstance.IsWhiteHoundAwakened)
        {
            return true;
        }

        if (currentDisplayedPlayerId >= 0 && PlayerManager.Instance != null)
        {
            PlayerData p = PlayerManager.Instance.GetplayerByID(currentDisplayedPlayerId);
            if (p != null && p.isWhiteHoundAwakened)
                return true;
        }

        return false;
    }

    public void Close()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        currentDisplayedPlayerId = -1;
    }

    private int GetLocalPlayerId()
    {
        if (NetworkRoleSync.LocalInstance != null)
        {
            return (int)NetworkRoleSync.LocalInstance.OwnerClientId;
        }

        if (NetworkManager.Singleton != null)
        {
            return (int)NetworkManager.Singleton.LocalClientId;
        }

        return 0;
    }

    private string GetPlayerNameById(int playerId)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController p in players)
        {
            if ((int)p.OwnerClientId == playerId)
            {
                NetworkPlayerNameSync nameSync = p.GetComponent<NetworkPlayerNameSync>();
                if (nameSync != null && !string.IsNullOrEmpty(nameSync.GetPlayerName()))
                {
                    return nameSync.GetPlayerName();
                }
                break;
            }
        }

        return $"Player {playerId + 1}";
    }

    private RoleType GetRoleFromServer(int playerId)
    {
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null)
        {
            if (RoleManager.Instance.playerRoles.TryGetValue(playerId, out BaseRole role) && role != null)
            {
                return role.roleType;
            }
        }

        return RoleType.Villager;
    }

    private Sprite GetRoleSprite(RoleType role)
    {
        if (roleImages != null)
        {
            foreach (RoleImageEntry entry in roleImages)
            {
                if (entry != null && entry.role == role)
                    return entry.image;
            }
        }

        return null;
    }

    private Color GetFactionColor(FactionType faction, bool isSecret)
    {
        if (isSecret) return new Color(0.7f, 0.7f, 0.7f);

        switch (faction)
        {
            case FactionType.Villager:
                return new Color(0.3f, 0.8f, 1f); // Xanh dương nhạt
            case FactionType.Monster:
                return new Color(1f, 0.25f, 0.25f); // Đỏ
            case FactionType.Neutral:
                return new Color(1f, 0.85f, 0.2f); // Vàng
            default:
                return Color.white;
        }
    }
}
