using System;
using System.Collections.Generic;
using TMPro;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI thông báo dành riêng cho Pháp Sư (Shaman) khi có người bị tấn công vào ban đêm.
/// 
/// Đảm bảo:
/// 1. Chỉ Shaman nhận được thông báo này (quyền riêng tư tuyệt đối).
/// 2. Shaman ĐƯỢC PHÉP tự cứu bản thân bằng Bùa Cứu nếu vẫn còn lượt sử dụng.
/// 3. Tiêu đề: "CÓ NGƯỜI ĐANG BỊ TẤN CÔNG!"
/// 4. Thông tin: Tên người bị tấn công, Avatar nếu có, câu hỏi phù hợp (tự cứu hoặc cứu người khác).
/// 5. Hai nút: "SỬ DỤNG BÙA CỨU" và "KHÔNG CỨU".
/// 6. Event-driven hoàn toàn (KHÔNG Update polling), tự ẩn khi chuyển Phase, reset sạch khi qua trận mới.
/// </summary>
public class NetworkShamanNotificationUI : MonoBehaviour
{
    public static NetworkShamanNotificationUI Instance { get; private set; }
    public const string ShamanPromptMessageName = "Loi.ShamanVictimPrompt";

    [Header("UI References (Optional - will auto-generate if null)")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI victimNameText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image victimAvatarImage;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button dismissButton;

    private int currentVictimId = -1;
    private int currentAttackEventId;
    private string currentVictimName = string.Empty;
    private GameObject dedicatedCanvasGO;
    private CustomMessagingManager registeredMessagingManager;
    private NetworkManager observedNetworkManager;
    private readonly HashSet<int> shownAttackEventIds = new();
    private bool isSubmittingSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeOnLoad()
    {
        EnsureInstance();
    }

    public static NetworkShamanNotificationUI EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("NetworkShamanNotificationUI");
            Instance = go.AddComponent<NetworkShamanNotificationUI>();
            DontDestroyOnLoad(go);
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        NetworkPlayerAction.OnShamanVictimPromptReceived -= HandleVictimPromptReceived;
        NetworkPlayerAction.OnShamanVictimPromptReceived += HandleVictimPromptReceived;

        NetworkPhaseSync.OnPhaseChangedGlobal -= HandlePhaseChangedGlobal;
        NetworkPhaseSync.OnPhaseChangedGlobal += HandlePhaseChangedGlobal;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        HookNetworkLifecycle();
        RegisterNetworkMessageHandler();
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnShamanVictimPromptReceived -= HandleVictimPromptReceived;
        NetworkPhaseSync.OnPhaseChangedGlobal -= HandlePhaseChangedGlobal;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        UnhookNetworkLifecycle();
        UnregisterNetworkMessageHandler();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetForNewMatch();
        HookNetworkLifecycle();
        RegisterNetworkMessageHandler();
    }

    private void HookNetworkLifecycle()
    {
        var net = GetActiveNetworkManager();
        if (observedNetworkManager == net) return;

        UnhookNetworkLifecycle();
        observedNetworkManager = net;
        if (observedNetworkManager != null)
        {
            observedNetworkManager.OnServerStarted += HandleNetworkStarted;
            observedNetworkManager.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void UnhookNetworkLifecycle()
    {
        if (observedNetworkManager != null)
        {
            observedNetworkManager.OnServerStarted -= HandleNetworkStarted;
            observedNetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            observedNetworkManager = null;
        }
    }

    private void HandleNetworkStarted()
    {
        RegisterNetworkMessageHandler();
    }

    private void HandleClientConnected(ulong clientId)
    {
        var net = GetActiveNetworkManager();
        if (net != null && clientId == net.LocalClientId)
        {
            RegisterNetworkMessageHandler();
        }
    }

    private static NetworkManager GetActiveNetworkManager()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null)
        {
            var sessionNet = session.GetNetworkManager();
            if (sessionNet != null) return sessionNet;
        }
        return NetworkManager.Singleton;
    }

    public void RegisterOnMessagingManager(CustomMessagingManager messaging)
    {
        if (messaging == null) return;
        if (registeredMessagingManager == messaging) return;

        try
        {
            if (registeredMessagingManager != null)
            {
                registeredMessagingManager.UnregisterNamedMessageHandler(ShamanPromptMessageName);
            }
            messaging.UnregisterNamedMessageHandler(ShamanPromptMessageName);
            messaging.RegisterNamedMessageHandler(ShamanPromptMessageName, ReceiveShamanPromptMessage);
            registeredMessagingManager = messaging;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkShamanNotificationUI] Lỗi đăng ký handler: {ex.Message}");
        }
    }

    public void UnregisterFromMessagingManager(CustomMessagingManager messaging)
    {
        if (messaging != null)
        {
            try
            {
                messaging.UnregisterNamedMessageHandler(ShamanPromptMessageName);
            }
            catch { }
        }
        if (registeredMessagingManager == messaging)
        {
            registeredMessagingManager = null;
        }
    }

    private void RegisterNetworkMessageHandler()
    {
        var net = GetActiveNetworkManager();
        if (net != null && net.IsListening && net.CustomMessagingManager != null)
        {
            RegisterOnMessagingManager(net.CustomMessagingManager);
        }
    }

    private void UnregisterNetworkMessageHandler()
    {
        if (registeredMessagingManager != null)
        {
            UnregisterFromMessagingManager(registeredMessagingManager);
        }

        var net = GetActiveNetworkManager();
        if (net != null && net.CustomMessagingManager != null)
        {
            UnregisterFromMessagingManager(net.CustomMessagingManager);
        }
    }

    private void ReceiveShamanPromptMessage(ulong senderClientId, FastBufferReader reader)
    {
        var net = GetActiveNetworkManager();
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[NetworkShamanNotificationUI] Bỏ qua thông báo không đến từ Server.");
            return;
        }

        reader.ReadValueSafe(out int victimId);
        reader.ReadValueSafe(out int attackEventId);
        reader.ReadValueSafe(out FixedString128Bytes nameFs);
        string victimName = nameFs.ToString();

        HandleVictimPromptReceived(victimId, victimName, attackEventId);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (dedicatedCanvasGO != null)
        {
            Destroy(dedicatedCanvasGO);
        }
    }

    private void HandlePhaseChangedGlobal(GamePhase phase)
    {
        if (phase != GamePhase.Night)
        {
            HidePrompt();
        }
    }

    public void ResetForNewMatch()
    {
        shownAttackEventIds.Clear();
        HidePrompt();
    }

    /// <summary>
    /// Xử lý khi nhận được event thông báo nạn nhân bị tấn công từ Server.
    /// </summary>
    public void HandleVictimPromptReceived(int victimId, string victimName)
    {
        HandleVictimPromptReceived(victimId, victimName, 0);
    }

    public void HandleVictimPromptReceived(int victimId, string victimName, int attackEventId)
    {
        // 1. Kiểm tra xem người chơi cục bộ có phải là Shaman không
        if (!IsLocalPlayerShaman())
        {
            Debug.LogWarning("[NetworkShamanNotificationUI] Bỏ qua thông báo vì Local Player không phải Pháp Sư.");
            return;
        }

        // 2. Kiểm tra xem Shaman có còn sống không
        if (!IsLocalPlayerAlive())
        {
            Debug.LogWarning("[NetworkShamanNotificationUI] Bỏ qua thông báo vì Pháp Sư đã bị loại.");
            return;
        }

        // 3. Kiểm tra xem Shaman đã sử dụng bùa cứu chưa
        int localId = GetLocalPlayerId();
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            if (!session.HasGoodCharm)
            {
                Debug.LogWarning("[NetworkShamanNotificationUI] Bùa Cứu đã được sử dụng trong ván này.");
                return;
            }
        }
        else if (NetworkRoleActionBridge.IsShamanSaveUsed(localId))
        {
            Debug.LogWarning("[NetworkShamanNotificationUI] Bỏ qua thông báo vì Bùa Cứu đã được sử dụng trước đó.");
            return;
        }

        // 4. Tránh hiển thị trùng lặp cho cùng một sự kiện tấn công
        if (attackEventId > 0 && !shownAttackEventIds.Add(attackEventId))
        {
            return;
        }

        // 5. Hiển thị thông báo (bao gồm cả trường hợp chính Shaman bị tấn công để tự cứu)
        currentVictimId = victimId;
        currentAttackEventId = attackEventId;
        currentVictimName = victimName;
        isSubmittingSave = false;
        ShowPrompt(victimId, victimName, attackEventId);
    }

    /// <summary>
    /// Hiển thị bảng thông báo hỏi Shaman có muốn cứu mục tiêu hay không.
    /// </summary>
    public void ShowPrompt(int victimId, string victimName, int attackEventId = 0)
    {
        EnsureUI();

        currentVictimId = victimId;
        if (attackEventId > 0) currentAttackEventId = attackEventId;
        currentVictimName = victimName;
        isSubmittingSave = false;

        int localId = GetLocalPlayerId();
        bool isSelfAttacked = localId == victimId;

        if (victimNameText != null)
        {
            victimNameText.text = isSelfAttacked
                ? $"BẠN (<color=#F1C40F>{victimName}</color>)"
                : $"MỤC TIÊU: <color=#F1C40F>{victimName}</color>";
        }

        if (messageText != null)
        {
            messageText.text = isSelfAttacked
                ? "Bạn đang bị tấn công! Bạn có muốn sử dụng bùa cứu để tự cứu mình không?"
                : "Người chơi này đang bị tấn công. Bạn có muốn sử dụng bùa cứu không?";
        }

        if (saveButton != null)
        {
            saveButton.interactable = true;
        }

        // Cập nhật Avatar nạn nhân nếu có
        UpdateVictimAvatar(victimId);

        if (dedicatedCanvasGO != null)
        {
            dedicatedCanvasGO.SetActive(true);
            var canvas = dedicatedCanvasGO.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = true;
        }

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
        }

        Debug.Log($"[NetworkShamanNotificationUI] Hiển thị thông báo cứu người cho Shaman | Nạn nhân: {victimName} (ID {victimId}, Self={isSelfAttacked}, EventId={currentAttackEventId})");
    }

    /// <summary>
    /// Đóng bảng thông báo.
    /// </summary>
    public void HidePrompt()
    {
        currentVictimId = -1;
        currentAttackEventId = 0;
        currentVictimName = string.Empty;
        isSubmittingSave = false;

        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }

    private void OnSaveButtonClicked()
    {
        if (currentVictimId < 0 || isSubmittingSave)
        {
            HidePrompt();
            return;
        }

        isSubmittingSave = true;
        int targetToSave = currentVictimId;
        int eventIdToSave = currentAttackEventId;
        HidePrompt();

        // 1. Nếu đang trong ThuongOnlineSession
        var session = ThuongOnlineSession.Active;
        if (session != null)
        {
            session.RequestAbility(targetToSave, eventIdToSave > 0 ? eventIdToSave : -1, shaman: true, goodCharm: true);
            Debug.Log($"[NetworkShamanNotificationUI] Shaman gửi yêu cầu Bùa Cứu qua ThuongOnlineSession cho Player {targetToSave + 1} (EventId={eventIdToSave})");
            return;
        }

        // 2. Fallback nếu chạy ở scene cũ dùng NetworkPlayerAction (subActionId = 1)
        NetworkPlayerAction[] actions = FindObjectsByType<NetworkPlayerAction>(FindObjectsSortMode.None);
        foreach (var action in actions)
        {
            if (action != null && action.IsOwner)
            {
                action.RequestActionServerRpc((ulong)targetToSave, 1);
                Debug.Log($"[NetworkShamanNotificationUI] Shaman gửi yêu cầu Bùa Cứu lên Server cho Player {targetToSave + 1}");
                break;
            }
        }
    }

    private void OnDismissButtonClicked()
    {
        Debug.Log("[NetworkShamanNotificationUI] Shaman đã chọn: KHÔNG CỨU mục tiêu.");
        HidePrompt();
    }

    private void UpdateVictimAvatar(int victimId)
    {
        if (victimAvatarImage == null) return;

        // Thử tìm Avatar của mục tiêu từ RoleInfoDatabase
        Sprite avatarSprite = RoleInfoDatabase.GetRoleSprite(RoleType.Villager);
        if (PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(victimId);
            if (p != null)
            {
                Sprite customSprite = RoleInfoDatabase.GetRoleSprite(p.roleType);
                if (customSprite != null) avatarSprite = customSprite;
            }
        }

        if (avatarSprite != null)
        {
            victimAvatarImage.sprite = avatarSprite;
            victimAvatarImage.gameObject.SetActive(true);
        }
        else
        {
            victimAvatarImage.gameObject.SetActive(false);
        }
    }

    private bool IsLocalPlayerShaman()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.GetPrivateRole() == RoleType.Shaman;
        }

        if (NetworkRoleSync.LocalInstance != null)
        {
            if (NetworkRoleSync.LocalInstance.LocalRole == RoleType.Shaman) return true;
        }

        int localId = GetLocalPlayerId();
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null)
        {
            if (RoleManager.Instance.playerRoles.TryGetValue(localId, out var role) && role != null)
            {
                return role.roleType == RoleType.Shaman;
            }
        }

        if (PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null && p.roleType == RoleType.Shaman) return true;
        }

        return false;
    }

    private bool IsLocalPlayerAlive()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return (session.GetPrivateFlags() & 1) != 0;
        }

        int localId = GetLocalPlayerId();
        if (PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null) return p.isAlive;
        }

        var net = GetActiveNetworkManager();
        if (net != null && net.LocalClient != null)
        {
            var pObj = net.LocalClient.PlayerObject;
            if (pObj != null)
            {
                var pState = pObj.GetComponent<NetworkPlayerStateSync>();
                if (pState != null) return pState.State.Value == NetworkPlayerStateType.Alive;
            }
        }

        return true;
    }

    private int GetLocalPlayerId()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.LocalPlayerId;
        }

        var net = GetActiveNetworkManager();
        if (net != null && net.LocalClient != null)
        {
            return (int)net.LocalClientId;
        }

        return 0;
    }

    private void EnsureUI()
    {
        if (promptPanel != null) return;

        // Tạo Dedicated Canvas cho Shaman Notification
        dedicatedCanvasGO = new GameObject("ShamanNotificationCanvas");
        DontDestroyOnLoad(dedicatedCanvasGO);

        Canvas canvas = dedicatedCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 29500; // Nằm trên HUD game thông thường

        var scaler = dedicatedCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        dedicatedCanvasGO.AddComponent<GraphicRaycaster>();

        // Container Panel
        promptPanel = new GameObject("ShamanPromptPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        promptPanel.transform.SetParent(dedicatedCanvasGO.transform, false);

        RectTransform panelRect = promptPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(700, 420);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = promptPanel.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.10f, 0.15f, 0.96f);

        TMP_FontAsset uiFont = UnityEngine.Object.FindAnyObjectByType<GameHUD>()?.phaseText?.font ?? TMP_Settings.defaultFontAsset;

        // Viền đỏ cảnh báo
        var outlineObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        outlineObj.transform.SetParent(promptPanel.transform, false);
        var outlineRect = outlineObj.GetComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.sizeDelta = new Vector2(6, 6);
        outlineObj.transform.SetAsFirstSibling();
        var outlineImg = outlineObj.GetComponent<Image>();
        outlineImg.color = new Color(0.85f, 0.22f, 0.22f, 0.8f);
        outlineImg.raycastTarget = false;

        // Header Title: "CÓ NGƯỜI ĐANG BỊ TẤN CÔNG!"
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(promptPanel.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -22);
        titleRect.sizeDelta = new Vector2(-40, 50);

        titleText = titleObj.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) titleText.font = uiFont;
        titleText.text = "CÓ NGƯỜI ĐANG BỊ TẤN CÔNG!";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.95f, 0.25f, 0.25f); // Đỏ nổi bật
        titleText.alignment = TextAlignmentOptions.Center;

        // Avatar Image (optional placeholder)
        GameObject avatarObj = new GameObject("VictimAvatar", typeof(RectTransform), typeof(Image));
        avatarObj.transform.SetParent(promptPanel.transform, false);
        RectTransform avatarRect = avatarObj.GetComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0.5f, 0.65f);
        avatarRect.anchorMax = new Vector2(0.5f, 0.65f);
        avatarRect.pivot = new Vector2(0.5f, 0.5f);
        avatarRect.sizeDelta = new Vector2(70, 70);
        avatarRect.anchoredPosition = new Vector2(0, 10);
        victimAvatarImage = avatarObj.GetComponent<Image>();

        // Victim Name Text
        GameObject nameObj = new GameObject("VictimNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(promptPanel.transform, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.46f);
        nameRect.anchorMax = new Vector2(1, 0.58f);
        nameRect.pivot = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(-60, 0);

        victimNameText = nameObj.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) victimNameText.font = uiFont;
        victimNameText.fontSize = 26;
        victimNameText.fontStyle = FontStyles.Bold;
        victimNameText.color = Color.white;
        victimNameText.alignment = TextAlignmentOptions.Center;

        // Message Text
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgObj.transform.SetParent(promptPanel.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0, 0.32f);
        msgRect.anchorMax = new Vector2(1, 0.44f);
        msgRect.pivot = new Vector2(0.5f, 0.5f);
        msgRect.anchoredPosition = Vector2.zero;
        msgRect.sizeDelta = new Vector2(-60, 0);

        messageText = msgObj.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) messageText.font = uiFont;
        messageText.text = "Người chơi này đang bị tấn công. Bạn có muốn sử dụng bùa cứu không?";
        messageText.fontSize = 22;
        messageText.color = new Color(0.85f, 0.88f, 0.92f);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.textWrappingMode = TextWrappingModes.Normal;

        // Button Container
        GameObject btnContainer = new GameObject("ButtonContainer", typeof(RectTransform));
        btnContainer.transform.SetParent(promptPanel.transform, false);
        RectTransform btnContainerRect = btnContainer.GetComponent<RectTransform>();
        btnContainerRect.anchorMin = new Vector2(0, 0);
        btnContainerRect.anchorMax = new Vector2(1, 0.30f);
        btnContainerRect.pivot = new Vector2(0.5f, 0);
        btnContainerRect.anchoredPosition = new Vector2(0, 20);
        btnContainerRect.sizeDelta = new Vector2(-60, 0);

        // Nút SỬ DỤNG BÙA CỨU (Xanh lá)
        GameObject saveBtnObj = new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        saveBtnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform saveBtnRect = saveBtnObj.GetComponent<RectTransform>();
        saveBtnRect.anchorMin = new Vector2(0.02f, 0.1f);
        saveBtnRect.anchorMax = new Vector2(0.48f, 0.9f);
        saveBtnRect.offsetMin = Vector2.zero;
        saveBtnRect.offsetMax = Vector2.zero;

        Image saveImg = saveBtnObj.GetComponent<Image>();
        saveImg.color = new Color(0.15f, 0.65f, 0.32f); // Xanh lá sáng

        saveButton = saveBtnObj.GetComponent<Button>();
        saveButton.onClick.AddListener(OnSaveButtonClicked);

        GameObject saveTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        saveTextObj.transform.SetParent(saveBtnObj.transform, false);
        RectTransform saveTextRect = saveTextObj.GetComponent<RectTransform>();
        saveTextRect.anchorMin = Vector2.zero;
        saveTextRect.anchorMax = Vector2.one;
        saveTextRect.offsetMin = Vector2.zero;
        saveTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI saveText = saveTextObj.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) saveText.font = uiFont;
        saveText.text = "SỬ DỤNG BÙA CỨU";
        saveText.fontSize = 24;
        saveText.fontStyle = FontStyles.Bold;
        saveText.color = Color.white;
        saveText.alignment = TextAlignmentOptions.Center;

        // Nút KHÔNG CỨU (Xám)
        GameObject dismissBtnObj = new GameObject("DismissButton", typeof(RectTransform), typeof(Image), typeof(Button));
        dismissBtnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform dismissBtnRect = dismissBtnObj.GetComponent<RectTransform>();
        dismissBtnRect.anchorMin = new Vector2(0.52f, 0.1f);
        dismissBtnRect.anchorMax = new Vector2(0.98f, 0.9f);
        dismissBtnRect.offsetMin = Vector2.zero;
        dismissBtnRect.offsetMax = Vector2.zero;

        Image dismissImg = dismissBtnObj.GetComponent<Image>();
        dismissImg.color = new Color(0.45f, 0.48f, 0.52f); // Xám

        dismissButton = dismissBtnObj.GetComponent<Button>();
        dismissButton.onClick.AddListener(OnDismissButtonClicked);

        GameObject dismissTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        dismissTextObj.transform.SetParent(dismissBtnObj.transform, false);
        RectTransform dismissTextRect = dismissTextObj.GetComponent<RectTransform>();
        dismissTextRect.anchorMin = Vector2.zero;
        dismissTextRect.anchorMax = Vector2.one;
        dismissTextRect.offsetMin = Vector2.zero;
        dismissTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI dismissText = dismissTextObj.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) dismissText.font = uiFont;
        dismissText.text = "KHÔNG CỨU";
        dismissText.fontSize = 24;
        dismissText.fontStyle = FontStyles.Bold;
        dismissText.color = Color.white;
        dismissText.alignment = TextAlignmentOptions.Center;

        promptPanel.SetActive(false);
    }

    /// <summary>
    /// Tra cứu ClientId theo PlayerId.
    /// </summary>
    public static ulong GetClientIdForPlayerId(int playerId)
    {
        var session = ThuongOnlineSession.Active;
        if (session != null && session.TryGetClientIdForPlayer(playerId, out ulong sessionClientId))
        {
            return sessionClientId;
        }

        var controllers = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (controllers != null)
        {
            foreach (var pc in controllers)
            {
                if (pc != null && (int)pc.OwnerClientId == playerId)
                {
                    return pc.OwnerClientId;
                }
            }
        }

        var net = GetActiveNetworkManager();
        if (net != null)
        {
            ulong uPlayerId = (ulong)playerId;
            if (net.ConnectedClients.ContainsKey(uPlayerId))
            {
                return uPlayerId;
            }
        }

        return ulong.MaxValue;
    }

    /// <summary>
    /// Server gọi hàm này ngay khi phát hiện có sự kiện tấn công ban đêm (Event-driven)
    /// để gửi thông báo riêng đến Shaman (bao gồm cả trường hợp chính Shaman bị tấn công).
    /// </summary>
    public static void NotifyVictimUnderAttack(int victimId, string victimName = null, int attackEventId = 0)
    {
        if (PlayerManager.Instance == null) return;

        var victim = PlayerManager.Instance.GetplayerByID(victimId);
        if (victim == null || !victim.isAlive) return;

        if (string.IsNullOrEmpty(victimName))
        {
            victimName = !string.IsNullOrEmpty(victim.playerName) ? victim.playerName : $"Player {victimId + 1}";
        }

        // Tìm Shaman còn sống
        PlayerData shaman = null;
        if (PlayerManager.Instance.players != null)
        {
            shaman = PlayerManager.Instance.players.Find(p => p != null && p.roleType == RoleType.Shaman && p.isAlive);
        }

        if (shaman == null)
        {
            Debug.Log("[NetworkShamanNotificationUI] Không có Shaman còn sống trong trận.");
            return;
        }

        // Kiểm tra xem Shaman còn bùa cứu không
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(shaman.playerID, out var baseRole) && baseRole is ShamanRole sRole)
        {
            if (!sRole.HasGoodCharm)
            {
                Debug.Log("[NetworkShamanNotificationUI] Shaman đã hết bùa cứu.");
                return;
            }
        }
        else if (ThuongOnlineSession.Active == null && NetworkRoleActionBridge.IsShamanSaveUsed(shaman.playerID))
        {
            Debug.Log("[NetworkShamanNotificationUI] Shaman đã dùng bùa cứu trước đó.");
            return;
        }

        ulong shamanClientId = GetClientIdForPlayerId(shaman.playerID);
        var net = GetActiveNetworkManager();
        var ui = EnsureInstance();
        if (net != null && net.IsServer)
        {
            if (shamanClientId == net.LocalClientId)
            {
                ui?.HandleVictimPromptReceived(victimId, victimName, attackEventId);
            }
            else if (shamanClientId != ulong.MaxValue && net.CustomMessagingManager != null && net.ConnectedClients.ContainsKey(shamanClientId))
            {
                string safeName = victimName.Length > 40 ? victimName.Substring(0, 40) : victimName;
                using var writer = new FastBufferWriter(256, Allocator.Temp);
                writer.WriteValueSafe(victimId);
                writer.WriteValueSafe(attackEventId);
                FixedString128Bytes nameFs = new FixedString128Bytes(safeName);
                writer.WriteValueSafe(nameFs);
                net.CustomMessagingManager.SendNamedMessage(ShamanPromptMessageName, shamanClientId, writer);
                Debug.Log($"[NetworkShamanNotificationUI] Đã gửi thông báo bị tấn công đến Shaman (ClientId {shamanClientId}) cho nạn nhân {safeName} (ID {victimId}, EventId {attackEventId})");
            }
        }
        else
        {
            ui?.HandleVictimPromptReceived(victimId, victimName, attackEventId);
        }
    }
}

/// <summary>
/// Event-driven coordinator giữ vòng đời thông báo Shaman mà không dùng Update polling.
/// </summary>
public class ThuongOnlineShamanRuntime : MonoBehaviour
{
    private static ThuongOnlineShamanRuntime instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureRuntimeExists()
    {
        ThuongOnlineShamanAdapter.EnsureInstalled();
        if (instance != null) return;
        var existing = UnityEngine.Object.FindAnyObjectByType<ThuongOnlineShamanRuntime>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[Loi_ShamanRuntime]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<ThuongOnlineShamanRuntime>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        ThuongOnlineShamanAdapter.EnsureInstalled();
    }
}
