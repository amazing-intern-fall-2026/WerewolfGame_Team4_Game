using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Chat Multiplayer 2 kênh (Chat Làng và Chat Monster) nhỏ gọn cho game Ma Sói màn hình ngang (Landscape).
/// - 2 Tab hoạt động độc lập: CHAT LÀNG và CHAT MONSTER.
/// - Tab CHAT MONSTER chỉ xuất hiện và cho phép sử dụng khi người chơi thuộc phe Monster hoặc Bạch Khuyển đã thức tỉnh.
/// - Lịch sử và vùng cuộn tin nhắn tách biệt hoàn toàn giữa 2 kênh.
/// - Nút Chat nổi 52 dp tại góc dưới bên trái khi thu gọn, tích hợp huy hiệu tin nhắn chưa đọc.
/// - Tự động nâng khung chat khi mở bàn phím ảo trên Android/iOS.
/// </summary>
public class NetworkChatUI : MonoBehaviour
{
    public static NetworkChatUI Instance { get; private set; }

    public enum ChatTab : byte
    {
        Village = 0,
        Monster = 1,
        Dead = 2
    }

    [Header("Panel Root & Toggle Button")]
    [SerializeField] private GameObject chatPanelRoot;
    [SerializeField] private Button floatingChatButton;
    [SerializeField] private Button minimizeButton;
    [SerializeField] private TextMeshProUGUI unreadBadgeText;
    [SerializeField] private GameObject unreadBadgeObj;

    [Header("Tab Controls")]
    [SerializeField] private GameObject tabBarObj;
    [SerializeField] private Button villageTabButton;
    [SerializeField] private Button monsterTabButton;
    [SerializeField] private Button deadTabButton;
    [SerializeField] private Image villageTabImage;
    [SerializeField] private Image monsterTabImage;
    [SerializeField] private Image deadTabImage;
    [SerializeField] private TextMeshProUGUI villageTabText;
    [SerializeField] private TextMeshProUGUI monsterTabText;
    [SerializeField] private TextMeshProUGUI deadTabText;
    [SerializeField] private GameObject monsterTabObj;
    [SerializeField] private GameObject monsterTabUnreadDot;
    [SerializeField] private GameObject deadTabObj;
    [SerializeField] private GameObject deadTabUnreadDot;

    [Header("Multi-Channel Scroll Views")]
    [SerializeField] private ScrollRect villageScrollRect;
    [SerializeField] private RectTransform villageContent;
    [SerializeField] private ScrollRect monsterScrollRect;
    [SerializeField] private RectTransform monsterContent;
    [SerializeField] private GameObject deadViewObj;
    [SerializeField] private ScrollRect deadScrollRect;
    [SerializeField] private RectTransform deadContent;
    [SerializeField] private Button deadMicButton;
    [SerializeField] private Image deadMicImage;
    [SerializeField] private TextMeshProUGUI deadMicText;
    [SerializeField] private Button deadSpeakerButton;
    [SerializeField] private Image deadSpeakerImage;
    [SerializeField] private TextMeshProUGUI deadSpeakerText;
    [SerializeField] private TextMeshProUGUI deadMembersText;

    [Header("Messages & Scroll (Backwards Compatibility)")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform messageContent;
    [SerializeField] private GameObject messageItemPrefab;

    [Header("Input Controls")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;

    [Header("Cấu hình kích thước & Vị trí")]
    [Range(0.25f, 0.40f)]
    [SerializeField] private float widthScreenRatio = 0.33f;

    [Range(0.30f, 0.45f)]
    [SerializeField] private float heightScreenRatio = 0.40f;

    private int unreadVillageCount = 0;
    private int unreadMonsterCount = 0;
    private int unreadDeadCount = 0;
    private int unreadMessagesCount = 0;
    private bool isOpen = false;
    public bool IsOpen => isOpen;
    public bool IsInputFocused => chatInputField != null && chatInputField.isFocused;
    public bool IsDeadTabVisible => deadTabObj != null && deadTabObj.activeSelf;
    public bool IsMonsterTabVisible => monsterTabObj != null && monsterTabObj.activeSelf;
    public bool IsDeadViewVisible => deadViewObj != null && deadViewObj.activeSelf;
    public string DeadMembersLabelText => deadMembersText != null ? deadMembersText.text : string.Empty;
    public string DeadMicLabelText => deadMicText != null ? deadMicText.text : string.Empty;
    public string DeadSpeakerLabelText => deadSpeakerText != null ? deadSpeakerText.text : string.Empty;

    private ChatTab activeTab = ChatTab.Village;
    public ChatTab ActiveTab => activeTab;

    private bool wasDeadPermitted = false;
    private bool hasCachedAvailability = false;
    private bool lastAvailabilityAllowed = true;
    private string lastAvailabilityDescription = string.Empty;

    public void SetCommunicationAvailability(bool allowed, string description)
    {
        bool deadActive = HasDeadChatPermission();
        bool changed = !hasCachedAvailability ||
                       lastAvailabilityAllowed != allowed ||
                       !string.Equals(lastAvailabilityDescription, description, StringComparison.Ordinal) ||
                       wasDeadPermitted != deadActive;

        hasCachedAvailability = true;
        lastAvailabilityAllowed = allowed;
        lastAvailabilityDescription = description ?? string.Empty;

        if (changed)
        {
            RefreshAllTabPermissions();
        }

        UpdateInputAvailabilityForActiveTab();
    }

    private RectTransform panelRect;
    private Vector2 basePanelPos;
    private float currentKeyboardOffset = 0f;

    private const string MainFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private TMP_FontAsset cachedFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        EnsureUIReferences();
    }

    private void Start()
    {
        SetupEvents();

        // Mặc định khởi đầu ở trạng thái thu gọn
        SetChatOpen(false);

        // Khởi tạo tab mặc định là Chat Làng
        SwitchToTab(ChatTab.Village);

        // Cập nhật hiển thị các Tab theo quyền thực tế của người chơi
        RefreshAllTabPermissions();

        // Nạp lại toàn bộ lịch sử tin nhắn
        ReloadHistory();
    }

    private void OnEnable()
    {
        NetworkChatManager.OnChatMessageReceived += HandleMessageReceived;
        NetworkChatManager.OnChatHistoryCleared += HandleHistoryCleared;
        NetworkChatManager.OnMonsterChatPermissionChanged += HandleMonsterPermissionChanged;
        NetworkChatManager.OnDeadChatPermissionChanged += HandleDeadPermissionChanged;
        NetworkChatManager.OnDeadChatMembersChanged += HandleDeadMembersChanged;

        NetworkVoiceManager.OnMicrophoneMuteChanged += HandleVoiceStateChanged;
        NetworkVoiceManager.OnOutputMuteChanged += HandleVoiceStateChanged;
        NetworkVoiceManager.OnLocalSpeakingChanged += HandleLocalSpeakingChanged;
        NetworkVoiceManager.OnParticipantSpeakingChanged += HandleParticipantSpeakingChanged;

        NetworkPlayerStateSync.OnLocalPlayerDeath += HandleLocalPlayerDiedEvent;
        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolvedEvent;
            DeathResolver.Instance.DeathResolved += HandleDeathResolvedEvent;
        }

        RefreshAllTabPermissions();
    }

    private void OnDisable()
    {
        NetworkChatManager.OnChatMessageReceived -= HandleMessageReceived;
        NetworkChatManager.OnChatHistoryCleared -= HandleHistoryCleared;
        NetworkChatManager.OnMonsterChatPermissionChanged -= HandleMonsterPermissionChanged;
        NetworkChatManager.OnDeadChatPermissionChanged -= HandleDeadPermissionChanged;
        NetworkChatManager.OnDeadChatMembersChanged -= HandleDeadMembersChanged;

        NetworkVoiceManager.OnMicrophoneMuteChanged -= HandleVoiceStateChanged;
        NetworkVoiceManager.OnOutputMuteChanged -= HandleVoiceStateChanged;
        NetworkVoiceManager.OnLocalSpeakingChanged -= HandleLocalSpeakingChanged;
        NetworkVoiceManager.OnParticipantSpeakingChanged -= HandleParticipantSpeakingChanged;

        NetworkPlayerStateSync.OnLocalPlayerDeath -= HandleLocalPlayerDiedEvent;
        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolvedEvent;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        HandleVirtualKeyboardOffset();
    }

    // =========================================================
    // 1. QUẢN LÝ TRẠNG THÁI MỞ / THU GỌN & CHUYỂN TAB
    // =========================================================

    public void ToggleChat()
    {
        SetChatOpen(!isOpen);
    }

    public void SetChatOpen(bool open)
    {
        isOpen = open;

        if (chatPanelRoot != null)
        {
            chatPanelRoot.SetActive(isOpen);
        }

        if (floatingChatButton != null)
        {
            floatingChatButton.gameObject.SetActive(!isOpen);
        }

        if (isOpen)
        {
            Thuong.Online.ThuongOnlineSession.Active?.PauseForChat();

            // Cập nhật quyền hiển thị các Tab khi mở chat
            RefreshAllTabPermissions();

            // Xóa unread badge của tab đang mở
            if (activeTab == ChatTab.Village) unreadVillageCount = 0;
            else if (activeTab == ChatTab.Monster) unreadMonsterCount = 0;
            else if (activeTab == ChatTab.Dead)
            {
                unreadDeadCount = 0;
                if (deadTabUnreadDot != null) deadTabUnreadDot.SetActive(false);
            }
            unreadMessagesCount = unreadVillageCount + unreadMonsterCount + unreadDeadCount;
            UpdateUnreadBadge();

            StartCoroutine(ScrollToBottomCoroutine());

            if (chatInputField != null && chatInputField.interactable)
            {
                chatInputField.ActivateInputField();
            }
        }
    }

    public void SwitchToTab(ChatTab tab)
    {
        if (tab == ChatTab.Monster && !HasMonsterChatPermission())
        {
            Debug.LogWarning("[CHAT UI] Bạn không thuộc phe Monster còn sống, không thể mở Tab Monster!");
            return;
        }
        if (tab == ChatTab.Dead && !HasDeadChatPermission())
        {
            Debug.LogWarning("[CHAT UI] Người chơi còn sống không thể truy cập Tab Người Chết!");
            return;
        }

        activeTab = tab;

        // Bật/tắt ScrollView tương ứng
        if (villageScrollRect != null)
        {
            villageScrollRect.gameObject.SetActive(activeTab == ChatTab.Village);
        }
        if (monsterScrollRect != null)
        {
            monsterScrollRect.gameObject.SetActive(activeTab == ChatTab.Monster);
        }
        if (deadViewObj != null)
        {
            deadViewObj.SetActive(activeTab == ChatTab.Dead);
        }

        // Cập nhật con trỏ ScrollRect và Content hoạt động
        if (activeTab == ChatTab.Dead)
        {
            scrollRect = deadScrollRect;
            messageContent = deadContent;
            RefreshDeadVoiceVisuals();
            RefreshDeadMembersDisplay();
        }
        else if (activeTab == ChatTab.Monster)
        {
            scrollRect = monsterScrollRect;
            messageContent = monsterContent;
        }
        else
        {
            scrollRect = villageScrollRect;
            messageContent = villageContent;
        }

        // Xóa unread của tab vừa chuyển tới
        if (activeTab == ChatTab.Village) unreadVillageCount = 0;
        else if (activeTab == ChatTab.Monster)
        {
            unreadMonsterCount = 0;
            if (monsterTabUnreadDot != null) monsterTabUnreadDot.SetActive(false);
        }
        else if (activeTab == ChatTab.Dead)
        {
            unreadDeadCount = 0;
            if (deadTabUnreadDot != null) deadTabUnreadDot.SetActive(false);
        }
        unreadMessagesCount = unreadVillageCount + unreadMonsterCount + unreadDeadCount;
        UpdateUnreadBadge();

        // Cập nhật giao diện Tab và Input
        UpdateTabStyles();
        UpdateInputControlsForActiveTab();
        UpdateInputAvailabilityForActiveTab();

        StartCoroutine(ScrollToBottomCoroutine());
    }

    private void UpdateTabStyles()
    {
        bool isVillage = activeTab == ChatTab.Village;
        bool isMonster = activeTab == ChatTab.Monster;
        bool isDead = activeTab == ChatTab.Dead;

        // Style Tab Làng
        if (villageTabImage != null)
        {
            villageTabImage.color = isVillage
                ? new Color(0.18f, 0.45f, 0.75f, 1f)
                : new Color(0.10f, 0.14f, 0.22f, 0.85f);
        }
        if (villageTabText != null)
        {
            villageTabText.color = isVillage ? Color.white : new Color(0.6f, 0.7f, 0.85f, 0.9f);
            villageTabText.fontStyle = isVillage ? FontStyles.Bold : FontStyles.Normal;
        }

        // Style Tab Monster
        if (monsterTabImage != null)
        {
            monsterTabImage.color = isMonster
                ? new Color(0.70f, 0.15f, 0.20f, 1f)
                : new Color(0.22f, 0.08f, 0.10f, 0.85f);
        }
        if (monsterTabText != null)
        {
            monsterTabText.color = isMonster ? new Color(1f, 0.9f, 0.4f, 1f) : new Color(0.85f, 0.55f, 0.55f, 0.9f);
            monsterTabText.fontStyle = isMonster ? FontStyles.Bold : FontStyles.Normal;
        }

        // Style Tab Người Chết
        if (deadTabImage != null)
        {
            deadTabImage.color = isDead
                ? new Color(0.42f, 0.24f, 0.68f, 1f)
                : new Color(0.14f, 0.11f, 0.22f, 0.88f);
        }
        if (deadTabText != null)
        {
            deadTabText.color = isDead ? new Color(0.95f, 0.90f, 1f, 1f) : new Color(0.70f, 0.62f, 0.85f, 0.9f);
            deadTabText.fontStyle = isDead ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void UpdateInputControlsForActiveTab()
    {
        if (chatInputField != null && chatInputField.placeholder is TMP_Text hint)
        {
            if (activeTab == ChatTab.Dead)
                hint.text = "Nhập tin nhắn Người Chết...";
            else if (activeTab == ChatTab.Monster)
                hint.text = "Nhập tin nhắn Monster (chỉ phe sói)...";
            else
                hint.text = HasDeadChatPermission() ? "Bạn đã chết — chỉ xem Chat Làng" : "Nhập tin nhắn làng...";
        }

        if (sendButton != null)
        {
            var sImg = sendButton.GetComponent<Image>();
            if (sImg != null)
            {
                sImg.color = activeTab == ChatTab.Dead
                    ? new Color(0.48f, 0.28f, 0.78f, 0.95f)
                    : activeTab == ChatTab.Monster
                        ? new Color(0.75f, 0.15f, 0.20f, 0.95f)
                        : new Color(0.15f, 0.55f, 0.95f, 0.95f);
            }
        }
    }

    private void UpdateInputAvailabilityForActiveTab()
    {
        bool deadPermitted = HasDeadChatPermission();
        bool allowed;
        string titleText;

        if (deadPermitted)
        {
            if (activeTab == ChatTab.Dead)
            {
                allowed = Thuong.Online.ThuongOnlineSession.Active == null || lastAvailabilityAllowed;
                titleText = "KÊNH NGƯỜI CHẾT (DEAD CHAT)";
            }
            else
            {
                allowed = false;
                titleText = "BẠN ĐÃ BỊ LOẠI (CHỈ XEM)";
            }
        }
        else if (activeTab == ChatTab.Monster)
        {
            allowed = HasMonsterChatPermission();
            titleText = "CHAT MONSTER";
        }
        else
        {
            allowed = !hasCachedAvailability || lastAvailabilityAllowed;
            titleText = hasCachedAvailability && !string.IsNullOrEmpty(lastAvailabilityDescription)
                ? lastAvailabilityDescription
                : "TRÒ CHUYỆN";
        }

        if (chatInputField != null)
        {
            chatInputField.interactable = allowed;
            if (!allowed && chatInputField.isFocused) chatInputField.DeactivateInputField();
            if (chatInputField.placeholder is TMP_Text hint && !allowed && hasCachedAvailability && !deadPermitted && !string.IsNullOrEmpty(lastAvailabilityDescription))
            {
                hint.text = lastAvailabilityDescription;
            }
        }
        if (sendButton != null) sendButton.interactable = allowed;

        var title = chatPanelRoot != null ? chatPanelRoot.transform.Find("HeaderBar/TitleText")?.GetComponent<TMP_Text>() : null;
        if (title != null) title.text = titleText;
    }

    public void RefreshAllTabPermissions()
    {
        RefreshMonsterTabVisibility();
        RefreshDeadTabVisibility();
        UpdateInputControlsForActiveTab();
        UpdateInputAvailabilityForActiveTab();
    }

    public void RefreshDeadTabVisibility()
    {
        bool hasDeadPerm = HasDeadChatPermission();
        bool justBecameDead = !wasDeadPermitted && hasDeadPerm;
        wasDeadPermitted = hasDeadPerm;

        if (deadTabObj != null)
        {
            deadTabObj.SetActive(hasDeadPerm);
        }
        else if (deadTabButton != null)
        {
            deadTabButton.gameObject.SetActive(hasDeadPerm);
        }

        if (justBecameDead)
        {
            SwitchToTab(ChatTab.Dead);
            RefreshDeadMembersDisplay();
            RefreshDeadVoiceVisuals();
        }
        else if (!hasDeadPerm && activeTab == ChatTab.Dead)
        {
            SwitchToTab(ChatTab.Village);
        }
        else if (hasDeadPerm && activeTab == ChatTab.Dead)
        {
            RefreshDeadMembersDisplay();
            RefreshDeadVoiceVisuals();
        }
    }

    public static bool HasDeadChatPermission()
    {
        return NetworkChatManager.HasLocalDeadChatPermission();
    }

    public void RefreshMonsterTabVisibility()
    {
        bool hasPermission = HasMonsterChatPermission();

        if (monsterTabObj != null)
        {
            monsterTabObj.SetActive(hasPermission);
        }
        else if (monsterTabButton != null)
        {
            monsterTabButton.gameObject.SetActive(hasPermission);
        }

        // Nếu mất quyền mà đang đứng ở Tab Monster -> tự động chuyển về Tab phù hợp
        if (!hasPermission && activeTab == ChatTab.Monster)
        {
            SwitchToTab(HasDeadChatPermission() ? ChatTab.Dead : ChatTab.Village);
        }
    }

    public static bool HasMonsterChatPermission()
    {
        if (HasDeadChatPermission()) return false;

        // 1. Bạch Khuyển không nằm trong kênh chat của Monster theo yêu cầu
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            if (!session.IsLocalPlayerAliveInMatch()) return false;
            var role = session.GetPrivateRole();
            if (role == RoleType.WhiteHound || role == RoleType.WhiteWolf) return false;
        }

        if (NetworkRoleSync.LocalInstance != null)
        {
            var role = NetworkRoleSync.LocalInstance.LocalRole;
            if (role == RoleType.WhiteHound || role == RoleType.WhiteWolf) return false;
        }

        if (NetworkChatManager.HasLocalMonsterChatPermission())
        {
            return true;
        }

        var net = Unity.Netcode.NetworkManager.Singleton;
        if (net != null)
        {
            ulong localClientId = net.LocalClientId;

            if (session != null && session.IsServerPlayerMonster(localClientId))
            {
                return true;
            }

            if (PlayerManager.Instance?.players != null)
            {
                foreach (var p in PlayerManager.Instance.players)
                {
                    if (p != null && (ulong)p.playerID == localClientId)
                    {
                        if (!p.isAlive || p.isDisconnected) return false;
                        if (p.roleType == RoleType.WhiteHound || p.roleType == RoleType.WhiteWolf) return false;
                        if (p.roleType == RoleType.DogSpirit || p.roleType == RoleType.SerpentSpirit || p.roleType == RoleType.Ogre) return true;
                        if (p.faction == FactionType.Monster) return true;
                    }
                }
            }
        }

        return false;
    }

    private void HandleMonsterPermissionChanged(bool hasPermission)
    {
        RefreshAllTabPermissions();
        if (hasPermission && !isOpen && monsterTabUnreadDot != null)
        {
            monsterTabUnreadDot.SetActive(true);
        }
    }

    private void HandleDeadPermissionChanged(bool hasPermission)
    {
        RefreshAllTabPermissions();
        if (hasPermission)
        {
            ReloadHistory();
            if (!isOpen && deadTabUnreadDot != null)
            {
                deadTabUnreadDot.SetActive(true);
            }
        }
    }

    private void HandleDeadMembersChanged(IReadOnlyList<NetworkChatManager.DeadMemberInfo> members)
    {
        RefreshDeadMembersDisplay();
    }

    private void HandleVoiceStateChanged(bool muted)
    {
        RefreshDeadVoiceVisuals();
    }

    private void HandleLocalSpeakingChanged(bool speaking)
    {
        RefreshDeadVoiceVisuals();
        RefreshDeadMembersDisplay();
    }

    private void HandleParticipantSpeakingChanged(string participantKey, bool speaking)
    {
        RefreshDeadMembersDisplay();
    }

    private void HandleLocalPlayerDiedEvent()
    {
        NetworkChatManager.RefreshLocalMonsterChatPermission();
        NetworkChatManager.RefreshLocalDeadChatPermission();
        RefreshAllTabPermissions();
    }

    private void HandleDeathResolvedEvent(DeathResult result)
    {
        NetworkChatManager.RefreshLocalMonsterChatPermission();
        NetworkChatManager.RefreshLocalDeadChatPermission();
        RefreshAllTabPermissions();
    }

    public void OnDeadMicClicked()
    {
        if (!HasDeadChatPermission())
        {
            Debug.LogWarning("[DEAD VOICE] Chỉ người chơi đã chết mới dùng được Micro kênh Người Chết.");
            return;
        }
        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.ToggleMicrophone();
        }
        RefreshDeadVoiceVisuals();
    }

    public void OnDeadSpeakerClicked()
    {
        if (!HasDeadChatPermission())
        {
            Debug.LogWarning("[DEAD VOICE] Chỉ người chơi đã chết mới dùng được Loa kênh Người Chết.");
            return;
        }
        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.ToggleOutputAudio();
        }
        RefreshDeadVoiceVisuals();
    }

    public void RefreshDeadVoiceVisuals()
    {
        var voice = NetworkVoiceManager.Existing ?? NetworkVoiceManager.Instance;
        bool micMuted = voice == null || voice.IsMicrophoneMuted;
        bool localSpeaking = !micMuted && voice != null && voice.IsLocalSpeaking;
        bool speakerMuted = voice != null && voice.IsOutputMuted;

        if (deadMicImage != null)
        {
            deadMicImage.color = micMuted
                ? new Color(0.42f, 0.16f, 0.19f, 0.95f)
                : localSpeaking
                    ? new Color(0.16f, 0.68f, 0.36f, 1f)
                    : new Color(0.12f, 0.48f, 0.28f, 0.95f);
        }
        if (deadMicText != null)
        {
            deadMicText.text = micMuted
                ? "[MIC] TẮT"
                : localSpeaking
                    ? "[MIC] ● ĐANG NÓI"
                    : "[MIC] BẬT";
            deadMicText.color = micMuted
                ? new Color(1f, 0.72f, 0.72f, 1f)
                : localSpeaking
                    ? new Color(1f, 0.94f, 0.45f, 1f)
                    : new Color(0.85f, 1f, 0.90f, 1f);
        }

        if (deadSpeakerImage != null)
        {
            deadSpeakerImage.color = speakerMuted
                ? new Color(0.48f, 0.28f, 0.12f, 0.95f)
                : new Color(0.12f, 0.38f, 0.58f, 0.95f);
        }
        if (deadSpeakerText != null)
        {
            deadSpeakerText.text = speakerMuted ? "[LOA] TẮT" : "[LOA] BẬT";
            deadSpeakerText.color = speakerMuted
                ? new Color(1f, 0.80f, 0.58f, 1f)
                : new Color(0.82f, 0.95f, 1f, 1f);
        }
    }

    public void RefreshDeadMembersDisplay()
    {
        if (deadMembersText == null) return;
        if (!HasDeadChatPermission())
        {
            deadMembersText.text = string.Empty;
            return;
        }

        var members = NetworkChatManager.Instance != null ? NetworkChatManager.Instance.DeadMembers : null;
        if (members == null || members.Count == 0)
        {
            deadMembersText.text = "<color=#9A94B8>Người chết (0): Chưa có thành viên khác</color>";
            return;
        }

        var voice = NetworkVoiceManager.Existing;
        ulong localClientId = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
        int localPlayerId = Thuong.Online.ThuongOnlineSession.Active != null
            ? Thuong.Online.ThuongOnlineSession.Active.LocalPlayerId
            : NetworkWhiteHoundNotificationUI.GetLocalPlayerId();

        var sb = new System.Text.StringBuilder();
        sb.Append($"<color=#C9B3FF><b>Kênh Người Chết ({members.Count}):</b></color> ");
        for (int i = 0; i < members.Count; i++)
        {
            if (i > 0) sb.Append("  |  ");
            var m = members[i];
            bool isSelf = (localClientId != ulong.MaxValue && m.ClientId == localClientId) ||
                          (localPlayerId >= 0 && m.PlayerId == localPlayerId);
            bool speaking = voice != null && (
                (isSelf && voice.IsLocalSpeaking) ||
                voice.IsParticipantSpeaking(m.Name) ||
                voice.IsParticipantSpeaking(m.ClientId.ToString())
            );
            string dot = speaking ? "<color=#4DFF88>●</color> " : string.Empty;
            string nameColor = isSelf ? "#B9F3FF" : "#EAE6FA";
            string suffix = isSelf ? " (Bạn)" : string.Empty;
            sb.Append($"{dot}<color={nameColor}>{m.Name}{suffix}</color>");
        }
        deadMembersText.text = sb.ToString();
    }

    // =========================================================
    // 2. GỬI & NHẬN TIN NHẮN THEO TAB
    // =========================================================

    public void OnSendClicked()
    {
        if (chatInputField == null) return;

        string text = chatInputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (activeTab == ChatTab.Dead)
        {
            if (!HasDeadChatPermission())
            {
                Debug.LogWarning("[CHAT UI] Người chơi còn sống không thể gửi vào kênh Người Chết!");
                return;
            }
            if (NetworkChatManager.Instance != null)
            {
                NetworkChatManager.Instance.SendDeadChatMessage(text);
            }
            else
            {
                Debug.LogWarning("[CHAT UI] Chưa có NetworkChatManager trong scene!");
            }
        }
        else if (HasDeadChatPermission())
        {
            Debug.LogWarning("[CHAT UI] Người chơi đã chết chỉ được phép gửi tin nhắn trong kênh Người Chết!");
            return;
        }
        else if (activeTab == ChatTab.Monster)
        {
            // Gửi qua kênh Monster Chat riêng biệt
            if (NetworkChatManager.Instance != null)
            {
                NetworkChatManager.Instance.SendMonsterChatMessage(text);
            }
            else
            {
                Debug.LogWarning("[CHAT UI] Chưa có NetworkChatManager trong scene!");
            }
        }
        else
        {
            // Gửi qua kênh Chat Làng
            if (Thuong.Online.ThuongOnlineSession.Active != null)
            {
                if (!Thuong.Online.ThuongOnlineSession.Active.SendChatText(text)) return;
            }
            else if (NetworkChatManager.Instance != null)
            {
                NetworkChatManager.Instance.SendChatMessage(text, ChatChannel.Village);
            }
            else
            {
                Debug.LogWarning("[CHAT UI] Chưa có NetworkChatManager trong scene!");
            }
        }

        chatInputField.text = string.Empty;
        if (chatInputField.interactable)
        {
            chatInputField.ActivateInputField();
        }
    }

    private void HandleMessageReceived(NetworkChatManager.ChatMessageEntry entry)
    {
        bool isDeadMsg = entry.Channel == (byte)ChatChannel.Dead;
        bool isMonsterMsg = entry.Channel == (byte)ChatChannel.Monster;

        if (isDeadMsg)
        {
            if (!HasDeadChatPermission()) return;

            AddMessageItem(entry, deadContent);
            while (deadContent != null && deadContent.childCount > NetworkChatManager.MaxHistoryCount)
            {
                var oldest = deadContent.GetChild(0);
                oldest.SetParent(null);
                Destroy(oldest.gameObject);
            }

            if (!isOpen || activeTab != ChatTab.Dead)
            {
                unreadDeadCount++;
                if (deadTabUnreadDot != null) deadTabUnreadDot.SetActive(true);
            }
        }
        else if (isMonsterMsg)
        {
            // Nếu là tin nhắn Monster nhưng máy cục bộ không có quyền Monster (hoặc đã chết), tuyệt đối bỏ qua
            if (!HasMonsterChatPermission()) return;

            AddMessageItem(entry, monsterContent);

            // Giới hạn lịch sử
            while (monsterContent != null && monsterContent.childCount > NetworkChatManager.MaxHistoryCount)
            {
                var oldest = monsterContent.GetChild(0);
                oldest.SetParent(null);
                Destroy(oldest.gameObject);
            }

            if (!isOpen || activeTab != ChatTab.Monster)
            {
                unreadMonsterCount++;
                if (monsterTabUnreadDot != null) monsterTabUnreadDot.SetActive(true);
            }
        }
        else
        {
            AddMessageItem(entry, villageContent);

            // Giới hạn lịch sử
            while (villageContent != null && villageContent.childCount > NetworkChatManager.MaxHistoryCount)
            {
                var oldest = villageContent.GetChild(0);
                oldest.SetParent(null);
                Destroy(oldest.gameObject);
            }

            if (!isOpen || activeTab != ChatTab.Village)
            {
                unreadVillageCount++;
            }
        }

        unreadMessagesCount = unreadVillageCount + unreadMonsterCount + unreadDeadCount;
        UpdateUnreadBadge();

        if ((isDeadMsg && activeTab == ChatTab.Dead) ||
            (isMonsterMsg && activeTab == ChatTab.Monster) ||
            (!isDeadMsg && !isMonsterMsg && activeTab == ChatTab.Village))
        {
            StartCoroutine(ScrollToBottomCoroutine());
        }
    }

    private void HandleHistoryCleared()
    {
        if (villageContent != null)
        {
            for (int i = villageContent.childCount - 1; i >= 0; i--)
            {
                Destroy(villageContent.GetChild(i).gameObject);
            }
        }

        if (monsterContent != null)
        {
            for (int i = monsterContent.childCount - 1; i >= 0; i--)
            {
                Destroy(monsterContent.GetChild(i).gameObject);
            }
        }

        if (deadContent != null)
        {
            for (int i = deadContent.childCount - 1; i >= 0; i--)
            {
                Destroy(deadContent.GetChild(i).gameObject);
            }
        }

        unreadVillageCount = 0;
        unreadMonsterCount = 0;
        unreadDeadCount = 0;
        unreadMessagesCount = 0;
        if (monsterTabUnreadDot != null) monsterTabUnreadDot.SetActive(false);
        if (deadTabUnreadDot != null) deadTabUnreadDot.SetActive(false);
        UpdateUnreadBadge();
    }

    private void ReloadHistory()
    {
        if (NetworkChatManager.Instance == null) return;

        // 1. Tải lịch sử Chat Làng
        if (villageContent != null)
        {
            for (int i = villageContent.childCount - 1; i >= 0; i--)
                Destroy(villageContent.GetChild(i).gameObject);

            foreach (var msg in NetworkChatManager.Instance.VillageHistory)
            {
                AddMessageItem(msg, villageContent);
            }
        }

        // 2. Tải lịch sử Chat Monster (nếu có quyền)
        if (monsterContent != null && HasMonsterChatPermission())
        {
            for (int i = monsterContent.childCount - 1; i >= 0; i--)
                Destroy(monsterContent.GetChild(i).gameObject);

            foreach (var msg in NetworkChatManager.Instance.MonsterHistory)
            {
                AddMessageItem(msg, monsterContent);
            }
        }

        // 3. Tải lịch sử Chat Người Chết (chỉ khi đã chết)
        if (deadContent != null)
        {
            for (int i = deadContent.childCount - 1; i >= 0; i--)
                Destroy(deadContent.GetChild(i).gameObject);

            if (HasDeadChatPermission())
            {
                foreach (var msg in NetworkChatManager.Instance.DeadHistory)
                {
                    AddMessageItem(msg, deadContent);
                }
            }
        }

        StartCoroutine(ScrollToBottomCoroutine());
    }

    private void AddMessageItem(NetworkChatManager.ChatMessageEntry entry, RectTransform targetContent)
    {
        if (targetContent == null) return;

        GameObject itemObj = null;
        if (messageItemPrefab != null)
        {
            itemObj = Instantiate(messageItemPrefab, targetContent);
        }
        else
        {
            itemObj = new GameObject("ChatMessageItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            itemObj.transform.SetParent(targetContent, false);
            var tmp = itemObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) tmp.font = cachedFont;
            tmp.fontSize = 14;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
        }

        var textComp = itemObj.GetComponent<TextMeshProUGUI>();
        if (textComp != null)
        {
            if (cachedFont != null && textComp.font == null)
            {
                textComp.font = cachedFont;
            }

            ulong localClientId = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
            bool isMe = entry.SenderClientId == localClientId;
            string timeTag = entry.TimestampTicks > 0
                ? $"<color=#8892A8>[{new DateTime(entry.TimestampTicks, DateTimeKind.Utc).ToLocalTime():HH:mm}]</color> "
                : string.Empty;

            if (entry.IsSystem)
            {
                textComp.text = $"{timeTag}<color=#FFD700><i>[Hệ thống] {entry.MessageText}</i></color>";
            }
            else if (entry.Channel == (byte)ChatChannel.Dead)
            {
                string nameColor = isMe ? "#88EEFF" : "#D4B8FF";
                string prefix = isMe ? "Bạn" : entry.SenderName.ToString();
                textComp.text = $"{timeTag}<color=#B27AFF><b>[Người Chết]</b></color> <color={nameColor}><b>{prefix}:</b></color> <color=#EFEAFF>{entry.MessageText}</color>";
            }
            else if (entry.Channel == (byte)ChatChannel.Monster)
            {
                string nameColor = isMe ? "#FF8888" : "#FF5555";
                string prefix = isMe ? "Bạn" : entry.SenderName.ToString();
                textComp.text = $"{timeTag}<color=#FF2222><b>[Sói]</b></color> <color={nameColor}><b>{prefix}:</b></color> <color=#FFE5E5>{entry.MessageText}</color>";
            }
            else
            {
                string nameColor = isMe ? "#55DDFF" : "#FFCC44";
                string prefix = isMe ? "Bạn" : entry.SenderName.ToString();
                textComp.text = $"{timeTag}<color={nameColor}><b>{prefix}:</b></color> <color=#FFFFFF>{entry.MessageText}</color>";
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(targetContent);
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void UpdateUnreadBadge()
    {
        if (unreadBadgeObj != null)
        {
            unreadBadgeObj.SetActive(unreadMessagesCount > 0 && !isOpen);
        }

        if (unreadBadgeText != null && unreadMessagesCount > 0)
        {
            unreadBadgeText.text = unreadMessagesCount > 99 ? "99+" : unreadMessagesCount.ToString();
        }
    }

    // =========================================================
    // 3. XỬ LÝ BÀN PHÍM ẢO TRÊN ANDROID / IOS
    // =========================================================

    private void HandleVirtualKeyboardOffset()
    {
        if (!isOpen || panelRect == null) return;

        float targetOffset = 0f;

#if UNITY_ANDROID || UNITY_IOS
        if (TouchScreenKeyboard.visible)
        {
            Rect area = TouchScreenKeyboard.area;
            if (area.height > 0)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                if (scale <= 0.001f) scale = 1f;

                targetOffset = area.height / scale;
            }
        }
#endif

        currentKeyboardOffset = Mathf.Lerp(currentKeyboardOffset, targetOffset, Time.unscaledDeltaTime * 14f);
        panelRect.anchoredPosition = new Vector2(basePanelPos.x, basePanelPos.y + currentKeyboardOffset);
    }

    // =========================================================
    // 4. THIẾT LẬP SỰ KIỆN & GIAO DIỆN
    // =========================================================

    private void SetupEvents()
    {
        if (floatingChatButton != null)
        {
            floatingChatButton.onClick.RemoveAllListeners();
            floatingChatButton.onClick.AddListener(ToggleChat);
        }

        if (minimizeButton != null)
        {
            minimizeButton.onClick.RemoveAllListeners();
            minimizeButton.onClick.AddListener(() => SetChatOpen(false));
        }

        if (villageTabButton != null)
        {
            villageTabButton.onClick.RemoveAllListeners();
            villageTabButton.onClick.AddListener(() => SwitchToTab(ChatTab.Village));
        }

        if (monsterTabButton != null)
        {
            monsterTabButton.onClick.RemoveAllListeners();
            monsterTabButton.onClick.AddListener(() => SwitchToTab(ChatTab.Monster));
        }

        if (deadTabButton != null)
        {
            deadTabButton.onClick.RemoveAllListeners();
            deadTabButton.onClick.AddListener(() => SwitchToTab(ChatTab.Dead));
        }

        if (deadMicButton != null)
        {
            deadMicButton.onClick.RemoveAllListeners();
            deadMicButton.onClick.AddListener(OnDeadMicClicked);
        }

        if (deadSpeakerButton != null)
        {
            deadSpeakerButton.onClick.RemoveAllListeners();
            deadSpeakerButton.onClick.AddListener(OnDeadSpeakerClicked);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(OnSendClicked);
        }

        if (chatInputField != null)
        {
            chatInputField.characterLimit = NetworkChatManager.MaxMessageLength;
            chatInputField.onSubmit.RemoveAllListeners();
            chatInputField.onSubmit.AddListener((val) => OnSendClicked());
        }
    }

    public void EnsureUIReferences()
    {
        if (cachedFont == null)
        {
            cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
#if UNITY_EDITOR
            if (cachedFont == null)
            {
                cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
            }
#endif
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = FindAnyObjectByType<Canvas>();
            if (parentCanvas != null && transform.parent == null)
            {
                transform.SetParent(parentCanvas.transform, false);
            }
        }

        Transform canvasTrans = parentCanvas != null ? parentCanvas.transform : transform;

        // Tìm FloatingChatButton
        if (floatingChatButton == null)
        {
            Transform fbTrans = canvasTrans.Find("FloatingChatButton");
            if (fbTrans != null)
            {
                floatingChatButton = fbTrans.GetComponent<Button>();
            }
        }

        // Tìm ChatPanelRoot
        if (chatPanelRoot == null)
        {
            Transform pTrans = canvasTrans.Find("CompactChatPanel");
            if (pTrans != null)
            {
                chatPanelRoot = pTrans.gameObject;
            }
        }

        // Nếu cả hai đều chưa có, tạo mới hoàn chỉnh tại runtime
        if (floatingChatButton == null || chatPanelRoot == null)
        {
            BuildRuntimeChatUI(canvasTrans);
        }
        else
        {
            // Nếu đã có chatPanelRoot (từ scene hoặc prefab), đảm bảo cấu trúc 2 Tab và 2 ScrollView đã được cài đặt
            EnsureTabsAndDualScrollViews(chatPanelRoot.transform);
        }

        if (chatPanelRoot != null)
        {
            panelRect = chatPanelRoot.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                basePanelPos = panelRect.anchoredPosition;
            }
        }
    }

    /// <summary>
    /// Đảm bảo cấu trúc 2 Tab và 2 ScrollRect (Làng + Monster) tồn tại bên trong ChatPanelRoot.
    /// </summary>
    private void EnsureTabsAndDualScrollViews(Transform panelTrans)
    {
        // 1. Kiểm tra / tạo TabBar
        Transform tbTrans = panelTrans.Find("TabBar");
        if (tbTrans == null)
        {
            GameObject tbObj = new GameObject("TabBar", typeof(RectTransform));
            tbObj.transform.SetParent(panelTrans, false);
            RectTransform tbRt = tbObj.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 1);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.pivot = new Vector2(0.5f, 1);
            tbRt.anchoredPosition = new Vector2(0, -32);
            tbRt.sizeDelta = new Vector2(-16, 28);
            tabBarObj = tbObj;
            tbTrans = tbObj.transform;

            // Tab Làng
            GameObject vTab = CreateChannelTab(tbObj.transform, "VillageTab", "CHAT LÀNG",
                new Color(0.18f, 0.45f, 0.75f, 1f), Color.white, out villageTabImage, out villageTabButton, out villageTabText);

            // Tab Monster
            CreateChannelTab(tbObj.transform, "MonsterTab", "CHAT MONSTER",
                new Color(0.22f, 0.08f, 0.10f, 0.85f), new Color(0.85f, 0.55f, 0.55f, 0.9f),
                out monsterTabImage, out monsterTabButton, out monsterTabText, out monsterTabUnreadDot, new Color(1f, 0.2f, 0.2f, 1f));
            monsterTabObj = monsterTabButton.gameObject;

            // Tab Người Chết
            CreateChannelTab(tbObj.transform, "DeadTab", "NGƯỜI CHẾT",
                new Color(0.14f, 0.11f, 0.22f, 0.88f), new Color(0.70f, 0.62f, 0.85f, 0.9f),
                out deadTabImage, out deadTabButton, out deadTabText, out deadTabUnreadDot, new Color(0.75f, 0.42f, 1f, 1f));
            deadTabObj = deadTabButton.gameObject;
        }
        else
        {
            tabBarObj = tbTrans.gameObject;
            Transform vt = tbTrans.Find("VillageTab");
            if (vt != null)
            {
                villageTabButton = vt.GetComponent<Button>();
                villageTabImage = vt.GetComponent<Image>();
                villageTabText = vt.GetComponentInChildren<TextMeshProUGUI>();
            }
            Transform mt = tbTrans.Find("MonsterTab");
            if (mt != null)
            {
                monsterTabObj = mt.gameObject;
                monsterTabButton = mt.GetComponent<Button>();
                monsterTabImage = mt.GetComponent<Image>();
                monsterTabText = mt.GetComponentInChildren<TextMeshProUGUI>();
                Transform dot = mt.Find("UnreadDot");
                if (dot != null) monsterTabUnreadDot = dot.gameObject;
            }
            Transform dt = tbTrans.Find("DeadTab");
            if (dt == null)
            {
                CreateChannelTab(tbTrans, "DeadTab", "NGƯỜI CHẾT",
                    new Color(0.14f, 0.11f, 0.22f, 0.88f), new Color(0.70f, 0.62f, 0.85f, 0.9f),
                    out deadTabImage, out deadTabButton, out deadTabText, out deadTabUnreadDot, new Color(0.75f, 0.42f, 1f, 1f));
                deadTabObj = deadTabButton.gameObject;
            }
            else
            {
                deadTabObj = dt.gameObject;
                deadTabButton = dt.GetComponent<Button>();
                deadTabImage = dt.GetComponent<Image>();
                deadTabText = dt.GetComponentInChildren<TextMeshProUGUI>();
                Transform dot = dt.Find("UnreadDot");
                if (dot != null) deadTabUnreadDot = dot.gameObject;
            }
        }

        // Bảo đảm TabBar tự chia đều bề rộng cho các tab đang hiển thị (1, 2 hoặc 3 tab)
        var tabLayout = tabBarObj.GetComponent<HorizontalLayoutGroup>();
        if (tabLayout == null) tabLayout = tabBarObj.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 4f;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        // 2. Tìm hoặc liên kết VillageScrollView
        Transform svTrans = panelTrans.Find("MessageScrollView");
        if (svTrans != null)
        {
            villageScrollRect = svTrans.GetComponent<ScrollRect>();
            RectTransform svRt = svTrans.GetComponent<RectTransform>();
            svRt.offsetMax = new Vector2(-8, -64); // Chừa chỗ cho Header (32) + TabBar (28) + margin (4)

            Transform vp = svTrans.Find("Viewport");
            if (vp != null)
            {
                Transform cnt = vp.Find("Content");
                if (cnt != null) villageContent = cnt.GetComponent<RectTransform>();
            }
        }

        // 3. Tìm hoặc tạo MonsterScrollView
        Transform msvTrans = panelTrans.Find("MonsterScrollView");
        if (msvTrans == null && villageScrollRect != null)
        {
            GameObject msvObj = Instantiate(villageScrollRect.gameObject, panelTrans, false);
            msvObj.name = "MonsterScrollView";
            msvObj.SetActive(false);

            monsterScrollRect = msvObj.GetComponent<ScrollRect>();
            Transform mvp = msvObj.transform.Find("Viewport");
            if (mvp != null)
            {
                Transform mcnt = mvp.Find("Content");
                if (mcnt != null)
                {
                    monsterContent = mcnt.GetComponent<RectTransform>();
                    for (int i = monsterContent.childCount - 1; i >= 0; i--)
                        Destroy(monsterContent.GetChild(i).gameObject);
                }
            }
        }
        else if (msvTrans != null)
        {
            monsterScrollRect = msvTrans.GetComponent<ScrollRect>();
            Transform mvp = msvTrans.Find("Viewport");
            if (mvp != null)
            {
                Transform mcnt = mvp.Find("Content");
                if (mcnt != null) monsterContent = mcnt.GetComponent<RectTransform>();
            }
        }

        // 4. Tìm hoặc tạo DeadView (Voice Bar + Danh sách thành viên Người Chết + DeadScrollView)
        EnsureDeadChatView(panelTrans);

        // Đồng bộ con trỏ mặc định
        scrollRect = villageScrollRect;
        messageContent = villageContent;
    }

    private GameObject CreateChannelTab(
        Transform parent,
        string objName,
        string title,
        Color bgColor,
        Color textColor,
        out Image bgImg,
        out Button btn,
        out TextMeshProUGUI label)
    {
        GameObject tab = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        tab.transform.SetParent(parent, false);
        bgImg = tab.GetComponent<Image>();
        bgImg.color = bgColor;
        btn = tab.GetComponent<Button>();
        btn.targetGraphic = bgImg;

        GameObject txt = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txt.transform.SetParent(tab.transform, false);
        RectTransform tRt = txt.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;

        label = txt.GetComponent<TextMeshProUGUI>();
        if (cachedFont != null) label.font = cachedFont;
        label.text = title;
        label.fontSize = 11;
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return tab;
    }

    private void CreateChannelTab(
        Transform parent,
        string objName,
        string title,
        Color bgColor,
        Color textColor,
        out Image bgImg,
        out Button btn,
        out TextMeshProUGUI label,
        out GameObject unreadDot,
        Color dotColor)
    {
        GameObject tab = CreateChannelTab(parent, objName, title, bgColor, textColor, out bgImg, out btn, out label);
        GameObject dotObj = new GameObject("UnreadDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dotObj.transform.SetParent(tab.transform, false);
        RectTransform dotRt = dotObj.GetComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(1, 1);
        dotRt.anchorMax = new Vector2(1, 1);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = new Vector2(-4, -4);
        dotRt.sizeDelta = new Vector2(10, 10);
        Image dotImg = dotObj.GetComponent<Image>();
        dotImg.color = dotColor;
        unreadDot = dotObj;
        unreadDot.SetActive(false);
    }

    private void EnsureDeadChatView(Transform panelTrans)
    {
        Transform existingDead = panelTrans.Find("DeadView");
        if (existingDead == null)
        {
            GameObject dv = new GameObject("DeadView", typeof(RectTransform));
            dv.transform.SetParent(panelTrans, false);
            RectTransform dvRt = dv.GetComponent<RectTransform>();
            dvRt.anchorMin = new Vector2(0, 0);
            dvRt.anchorMax = new Vector2(1, 1);
            dvRt.offsetMin = new Vector2(8, 46);
            dvRt.offsetMax = new Vector2(-8, -64);
            deadViewObj = dv;

            // 1. Voice Bar (Nút Micro & Nút Loa tách biệt rõ ràng)
            GameObject voiceBar = new GameObject("DeadVoiceBar", typeof(RectTransform));
            voiceBar.transform.SetParent(dv.transform, false);
            RectTransform vbRt = voiceBar.GetComponent<RectTransform>();
            vbRt.anchorMin = new Vector2(0, 1);
            vbRt.anchorMax = new Vector2(1, 1);
            vbRt.pivot = new Vector2(0.5f, 1);
            vbRt.anchoredPosition = Vector2.zero;
            vbRt.sizeDelta = new Vector2(0, 28);

            CreateVoiceToggleButton(voiceBar.transform, "DeadMicButton", "[MIC] TẮT",
                new Vector2(0f, 0f), new Vector2(0.49f, 1f),
                new Color(0.42f, 0.16f, 0.19f, 0.95f), out deadMicButton, out deadMicImage, out deadMicText);

            CreateVoiceToggleButton(voiceBar.transform, "DeadSpeakerButton", "[LOA] BẬT",
                new Vector2(0.51f, 0f), new Vector2(1f, 1f),
                new Color(0.12f, 0.38f, 0.58f, 0.95f), out deadSpeakerButton, out deadSpeakerImage, out deadSpeakerText);

            // 2. Danh sách thành viên Kênh Người Chết
            GameObject membersBar = new GameObject("DeadMembersBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            membersBar.transform.SetParent(dv.transform, false);
            RectTransform mbRt = membersBar.GetComponent<RectTransform>();
            mbRt.anchorMin = new Vector2(0, 1);
            mbRt.anchorMax = new Vector2(1, 1);
            mbRt.pivot = new Vector2(0.5f, 1);
            mbRt.anchoredPosition = new Vector2(0, -31);
            mbRt.sizeDelta = new Vector2(0, 28);
            membersBar.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.14f, 0.9f);

            GameObject mTxtObj = new GameObject("MembersText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            mTxtObj.transform.SetParent(membersBar.transform, false);
            RectTransform mtRt = mTxtObj.GetComponent<RectTransform>();
            mtRt.anchorMin = Vector2.zero;
            mtRt.anchorMax = Vector2.one;
            mtRt.offsetMin = new Vector2(6, 2);
            mtRt.offsetMax = new Vector2(-6, -2);
            deadMembersText = mTxtObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) deadMembersText.font = cachedFont;
            deadMembersText.fontSize = 11;
            deadMembersText.fontSizeMin = 9;
            deadMembersText.fontSizeMax = 12;
            deadMembersText.enableAutoSizing = true;
            deadMembersText.textWrappingMode = TextWrappingModes.Normal;
            deadMembersText.alignment = TextAlignmentOptions.MidlineLeft;
            deadMembersText.raycastTarget = false;

            // 3. DeadScrollView
            if (villageScrollRect != null)
            {
                GameObject dsvObj = Instantiate(villageScrollRect.gameObject, dv.transform, false);
                dsvObj.name = "DeadScrollView";
                dsvObj.SetActive(true);
                RectTransform dsvRt = dsvObj.GetComponent<RectTransform>();
                dsvRt.anchorMin = Vector2.zero;
                dsvRt.anchorMax = Vector2.one;
                dsvRt.offsetMin = Vector2.zero;
                dsvRt.offsetMax = new Vector2(0, -62);

                deadScrollRect = dsvObj.GetComponent<ScrollRect>();
                Transform dvp = dsvObj.transform.Find("Viewport");
                if (dvp != null)
                {
                    Transform dcnt = dvp.Find("Content");
                    if (dcnt != null)
                    {
                        deadContent = dcnt.GetComponent<RectTransform>();
                        for (int i = deadContent.childCount - 1; i >= 0; i--)
                            Destroy(deadContent.GetChild(i).gameObject);
                    }
                }
            }

            deadViewObj.SetActive(false);
        }
        else
        {
            deadViewObj = existingDead.gameObject;
            deadMicButton = existingDead.Find("DeadVoiceBar/DeadMicButton")?.GetComponent<Button>();
            deadMicImage = deadMicButton != null ? deadMicButton.GetComponent<Image>() : null;
            deadMicText = deadMicButton != null ? deadMicButton.GetComponentInChildren<TextMeshProUGUI>() : null;

            deadSpeakerButton = existingDead.Find("DeadVoiceBar/DeadSpeakerButton")?.GetComponent<Button>();
            deadSpeakerImage = deadSpeakerButton != null ? deadSpeakerButton.GetComponent<Image>() : null;
            deadSpeakerText = deadSpeakerButton != null ? deadSpeakerButton.GetComponentInChildren<TextMeshProUGUI>() : null;

            deadMembersText = existingDead.Find("DeadMembersBar/MembersText")?.GetComponent<TextMeshProUGUI>();
            deadScrollRect = existingDead.Find("DeadScrollView")?.GetComponent<ScrollRect>();
            deadContent = deadScrollRect != null ? deadScrollRect.transform.Find("Viewport/Content")?.GetComponent<RectTransform>() : null;
        }
    }

    private void CreateVoiceToggleButton(
        Transform parent,
        string name,
        string initialLabel,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color bgColor,
        out Button button,
        out Image bgImg,
        out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        bgImg = go.GetComponent<Image>();
        bgImg.color = bgColor;
        button = go.GetComponent<Button>();
        button.targetGraphic = bgImg;

        GameObject txt = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        RectTransform tRt = txt.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(4, 0);
        tRt.offsetMax = new Vector2(-4, 0);

        label = txt.GetComponent<TextMeshProUGUI>();
        if (cachedFont != null) label.font = cachedFont;
        label.text = initialLabel;
        label.fontSize = 11;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    /// <summary>
    /// Xây dựng tự động hệ thống Chat UI 2 kênh chuẩn nếu scene chưa được setup trước qua Editor.
    /// </summary>
    public void BuildRuntimeChatUI(Transform canvasTrans)
    {
        // 1. Nút Chat nổi (FloatingChatButton) ở góc dưới bên trái
        if (floatingChatButton == null)
        {
            GameObject fbObj = new GameObject("FloatingChatButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            fbObj.transform.SetParent(canvasTrans, false);

            RectTransform fbRt = fbObj.GetComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0f, 0f);
            fbRt.anchorMax = new Vector2(0f, 0f);
            fbRt.pivot = new Vector2(0f, 0f);
            fbRt.anchoredPosition = new Vector2(16, 16);
            fbRt.sizeDelta = new Vector2(56, 56);

            Image fbImg = fbObj.GetComponent<Image>();
            fbImg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
            floatingChatButton = fbObj.GetComponent<Button>();
            floatingChatButton.targetGraphic = fbImg;

            // Icon văn bản 'CHAT'
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(fbObj.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI iconTmp = iconObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) iconTmp.font = cachedFont;
            iconTmp.text = "CHAT";
            iconTmp.fontSize = 13;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.color = new Color(0.35f, 0.85f, 1f, 1f);
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.raycastTarget = false;

            // Unread badge
            GameObject badgeObj = new GameObject("UnreadBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.transform.SetParent(fbObj.transform, false);
            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(-4, -4);
            badgeRt.sizeDelta = new Vector2(22, 22);

            Image badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.color = new Color(0.95f, 0.2f, 0.25f, 1f);

            GameObject badgeTxtObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            badgeTxtObj.transform.SetParent(badgeObj.transform, false);
            RectTransform btRt = badgeTxtObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;

            unreadBadgeText = badgeTxtObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) unreadBadgeText.font = cachedFont;
            unreadBadgeText.fontSize = 11;
            unreadBadgeText.fontStyle = FontStyles.Bold;
            unreadBadgeText.color = Color.white;
            unreadBadgeText.alignment = TextAlignmentOptions.Center;
            unreadBadgeText.raycastTarget = false;

            unreadBadgeObj = badgeObj;
            unreadBadgeObj.SetActive(false);
        }

        // 2. Khung chat nhỏ gọn (CompactChatPanel)
        if (chatPanelRoot == null)
        {
            GameObject pObj = new GameObject("CompactChatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pObj.transform.SetParent(canvasTrans, false);
            chatPanelRoot = pObj;

            panelRect = pObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(widthScreenRatio, heightScreenRatio);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.offsetMin = new Vector2(16, 16);
            panelRect.offsetMax = new Vector2(-6, -6);
            basePanelPos = panelRect.anchoredPosition;

            Image pImg = pObj.GetComponent<Image>();
            pImg.color = new Color(0.06f, 0.09f, 0.14f, 0.88f);
            pImg.raycastTarget = true;

            // --- Header Bar ---
            GameObject headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerObj.transform.SetParent(pObj.transform, false);
            RectTransform hRt = headerObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0, 1);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.pivot = new Vector2(0.5f, 1);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(0, 32);

            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);

            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(headerObj.transform, false);
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0, 0);
            tRt.anchorMax = new Vector2(1, 1);
            tRt.offsetMin = new Vector2(12, 0);
            tRt.offsetMax = new Vector2(-40, 0);

            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) titleTmp.font = cachedFont;
            titleTmp.text = "TRÒ CHUYỆN";
            titleTmp.fontSize = 14;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.4f, 0.85f, 1f);
            titleTmp.alignment = TextAlignmentOptions.Left;
            titleTmp.raycastTarget = false;

            // Minimize Button
            GameObject minBtnObj = new GameObject("MinimizeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            minBtnObj.transform.SetParent(headerObj.transform, false);
            RectTransform mbRt = minBtnObj.GetComponent<RectTransform>();
            mbRt.anchorMin = new Vector2(1, 0.5f);
            mbRt.anchorMax = new Vector2(1, 0.5f);
            mbRt.pivot = new Vector2(1, 0.5f);
            mbRt.anchoredPosition = new Vector2(-6, 0);
            mbRt.sizeDelta = new Vector2(28, 24);

            Image mbImg = minBtnObj.GetComponent<Image>();
            mbImg.color = new Color(0.25f, 0.3f, 0.4f, 0.8f);
            minimizeButton = minBtnObj.GetComponent<Button>();
            minimizeButton.targetGraphic = mbImg;

            GameObject mbTxtObj = new GameObject("CloseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            mbTxtObj.transform.SetParent(minBtnObj.transform, false);
            RectTransform mbtRt = mbTxtObj.GetComponent<RectTransform>();
            mbtRt.anchorMin = Vector2.zero;
            mbtRt.anchorMax = Vector2.one;
            mbtRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI mbTmp = mbTxtObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) mbTmp.font = cachedFont;
            mbTmp.text = "✕";
            mbTmp.fontSize = 14;
            mbTmp.fontStyle = FontStyles.Bold;
            mbTmp.color = Color.white;
            mbTmp.alignment = TextAlignmentOptions.Center;
            mbTmp.raycastTarget = false;

            // --- ScrollView Chat Làng ---
            GameObject scrollObj = new GameObject("MessageScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(pObj.transform, false);
            RectTransform svRt = scrollObj.GetComponent<RectTransform>();
            svRt.anchorMin = new Vector2(0, 0);
            svRt.anchorMax = new Vector2(1, 1);
            svRt.offsetMin = new Vector2(8, 46);
            svRt.offsetMax = new Vector2(-8, -64);

            Image svImg = scrollObj.GetComponent<Image>();
            svImg.color = new Color(0, 0, 0, 0.25f);
            villageScrollRect = scrollObj.GetComponent<ScrollRect>();
            villageScrollRect.horizontal = false;
            villageScrollRect.vertical = true;
            villageScrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRt = viewportObj.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;

            Image vpImg = viewportObj.GetComponent<Image>();
            vpImg.color = Color.white;
            Mask mask = viewportObj.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            villageContent = contentObj.GetComponent<RectTransform>();
            villageContent.anchorMin = new Vector2(0, 1);
            villageContent.anchorMax = new Vector2(1, 1);
            villageContent.pivot = new Vector2(0.5f, 1);
            villageContent.offsetMin = Vector2.zero;
            villageContent.offsetMax = Vector2.zero;

            var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var csf = contentObj.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            villageScrollRect.viewport = vpRt;
            villageScrollRect.content = villageContent;

            // --- Input Row (Bottom Bar) ---
            GameObject inputRow = new GameObject("InputRow", typeof(RectTransform));
            inputRow.transform.SetParent(pObj.transform, false);
            RectTransform irRt = inputRow.GetComponent<RectTransform>();
            irRt.anchorMin = new Vector2(0, 0);
            irRt.anchorMax = new Vector2(1, 0);
            irRt.pivot = new Vector2(0.5f, 0);
            irRt.anchoredPosition = new Vector2(0, 6);
            irRt.sizeDelta = new Vector2(-16, 36);

            // Send Button
            GameObject sendBtnObj = new GameObject("SendButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            sendBtnObj.transform.SetParent(inputRow.transform, false);
            RectTransform sRt = sendBtnObj.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(1, 0);
            sRt.anchorMax = new Vector2(1, 1);
            sRt.pivot = new Vector2(1, 0.5f);
            sRt.anchoredPosition = Vector2.zero;
            sRt.sizeDelta = new Vector2(62, 0);

            Image sImg = sendBtnObj.GetComponent<Image>();
            sImg.color = new Color(0.15f, 0.55f, 0.95f, 0.95f);
            sendButton = sendBtnObj.GetComponent<Button>();
            sendButton.targetGraphic = sImg;

            GameObject sendTxtObj = new GameObject("SendText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            sendTxtObj.transform.SetParent(sendBtnObj.transform, false);
            RectTransform stRt = sendTxtObj.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI sendTmp = sendTxtObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) sendTmp.font = cachedFont;
            sendTmp.text = "GỬI";
            sendTmp.fontSize = 14;
            sendTmp.fontStyle = FontStyles.Bold;
            sendTmp.color = Color.white;
            sendTmp.alignment = TextAlignmentOptions.Center;
            sendTmp.raycastTarget = false;

            // TMP_InputField
            GameObject inputObj = new GameObject("ChatInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            inputObj.transform.SetParent(inputRow.transform, false);
            RectTransform inpRt = inputObj.GetComponent<RectTransform>();
            inpRt.anchorMin = new Vector2(0, 0);
            inpRt.anchorMax = new Vector2(1, 1);
            inpRt.offsetMin = Vector2.zero;
            inpRt.offsetMax = new Vector2(-68, 0);

            Image inpImg = inputObj.GetComponent<Image>();
            inpImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
            chatInputField = inputObj.GetComponent<TMP_InputField>();

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(8, 2);
            taRt.offsetMax = new Vector2(-8, -2);

            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            phObj.transform.SetParent(textArea.transform, false);
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI phTmp = phObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) phTmp.font = cachedFont;
            phTmp.text = "Nhập tin nhắn làng...";
            phTmp.fontSize = 14;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.75f);
            phTmp.alignment = TextAlignmentOptions.Left;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(textArea.transform, false);
            RectTransform tCompRt = textObj.GetComponent<RectTransform>();
            tCompRt.anchorMin = Vector2.zero;
            tCompRt.anchorMax = Vector2.one;
            tCompRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI mainTmp = textObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) mainTmp.font = cachedFont;
            mainTmp.fontSize = 14;
            mainTmp.color = Color.white;
            mainTmp.alignment = TextAlignmentOptions.Left;

            chatInputField.textViewport = taRt;
            chatInputField.placeholder = phTmp;
            chatInputField.textComponent = mainTmp;
            chatInputField.characterLimit = NetworkChatManager.MaxMessageLength;

            // Thêm TabBar và MonsterScrollView
            EnsureTabsAndDualScrollViews(pObj.transform);
        }

        SetupEvents();
        Debug.Log("[CHAT UI] Đã khởi tạo hoàn chỉnh hệ thống Chat UI 2 kênh (Làng & Monster) tại góc dưới bên trái.");
    }
}
