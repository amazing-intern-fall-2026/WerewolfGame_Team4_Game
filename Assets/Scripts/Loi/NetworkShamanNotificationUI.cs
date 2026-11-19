using System;
using TMPro;
using Thuong.Online;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI thông báo dành riêng cho Pháp Sư (Shaman) khi có người bị tấn công vào ban đêm.
/// 
/// Đảm bảo:
/// 1. Chỉ Shaman nhận được thông báo này (quyền riêng tư tuyệt đối).
/// 2. Shaman bị tấn công: KHÔNG hiển thị popup tự cứu.
/// 3. Tiêu đề: "CÓ NGƯỜI ĐANG BỊ TẤN CÔNG!"
/// 4. Thông tin: Tên người bị tấn công, Avatar nếu có, câu hỏi "Bạn có muốn sử dụng bùa cứu để cứu người chơi này không?".
/// 5. Hai nút: "SỬ DỤNG BÙA CỨU" và "KHÔNG CỨU".
/// 6. Tự động ẩn khi kết thúc đêm hoặc chuyển Phase, không làm treo game.
/// 7. Reset sạch sẽ khi bắt đầu trận mới hoặc về menu/lobby.
/// </summary>
public class NetworkShamanNotificationUI : MonoBehaviour
{
    public static NetworkShamanNotificationUI Instance { get; private set; }

    [Header("UI References (Optional - will auto-generate if null)")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI victimNameText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Image victimAvatarImage;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button dismissButton;

    private int currentVictimId = -1;
    private string currentVictimName = string.Empty;
    private GameObject dedicatedCanvasGO;

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
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnShamanVictimPromptReceived -= HandleVictimPromptReceived;
        NetworkPhaseSync.OnPhaseChangedGlobal -= HandlePhaseChangedGlobal;
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

    /// <summary>
    /// Xử lý khi nhận được event thông báo nạn nhân bị tấn công từ Server RPC.
    /// </summary>
    public void HandleVictimPromptReceived(int victimId, string victimName)
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
        if (NetworkRoleActionBridge.IsShamanSaveUsed(localId))
        {
            Debug.LogWarning("[NetworkShamanNotificationUI] Bỏ qua thông báo vì Bùa Cứu đã được sử dụng trước đó.");
            return;
        }

        // 4. QUY TẮC BẮT BUỘC: Shaman không được phép tự cứu bản thân!
        // Nếu chính Shaman là người bị tấn công -> KHÔNG xuất hiện lựa chọn tự cứu.
        if (localId == victimId)
        {
            Debug.LogWarning($"[NetworkShamanNotificationUI] Shaman (ID {localId}) là nạn nhân bị tấn công -> KHÔNG hiển thị UI tự cứu!");
            HidePrompt();
            return;
        }

        // 5. Hiển thị thông báo hợp lệ
        currentVictimId = victimId;
        currentVictimName = victimName;
        ShowPrompt(victimId, victimName);
    }

    /// <summary>
    /// Hiển thị bảng thông báo hỏi Shaman có muốn cứu mục tiêu hay không.
    /// </summary>
    public void ShowPrompt(int victimId, string victimName)
    {
        EnsureUI();

        if (victimNameText != null)
        {
            victimNameText.text = $"MỤC TIÊU: <color=#F1C40F>{victimName}</color>";
        }

        if (messageText != null)
        {
            messageText.text = "Bạn có muốn sử dụng bùa cứu để cứu người chơi này không?";
        }

        // Cập nhật Avatar nạn nhân nếu có
        UpdateVictimAvatar(victimId);

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
        }

        Debug.Log($"[NetworkShamanNotificationUI] Đang hiển thị thông báo cứu người cho Shaman | Nạn nhân: {victimName} (ID {victimId})");
    }

    /// <summary>
    /// Đóng bảng thông báo.
    /// </summary>
    public void HidePrompt()
    {
        currentVictimId = -1;
        currentVictimName = string.Empty;

        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }

    private void OnSaveButtonClicked()
    {
        if (currentVictimId < 0)
        {
            HidePrompt();
            return;
        }

        int localId = GetLocalPlayerId();
        if (localId == currentVictimId)
        {
            Debug.LogError("[NetworkShamanNotificationUI] Chặn click: Không thể tự cứu bản thân!");
            HidePrompt();
            return;
        }

        int targetToSave = currentVictimId;
        HidePrompt();

        // Gửi yêu cầu sử dụng Bùa Cứu lên Server (subActionId = 1)
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
        if (NetworkRoleSync.LocalInstance != null)
        {
            if (NetworkRoleSync.LocalInstance.LocalRole == RoleType.Shaman) return true;
        }

        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null)
        {
            int localId = GetLocalPlayerId();
            if (RoleManager.Instance.playerRoles.TryGetValue(localId, out var role) && role != null)
            {
                return role.roleType == RoleType.Shaman;
            }
        }

        if (PlayerManager.Instance != null)
        {
            int localId = GetLocalPlayerId();
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null && p.roleType == RoleType.Shaman) return true;
        }

        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.GetPrivateRole() == RoleType.Shaman;
        }

        return false;
    }

    private bool IsLocalPlayerAlive()
    {
        int localId = GetLocalPlayerId();
        if (PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null) return p.isAlive;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var pObj = NetworkManager.Singleton.LocalClient.PlayerObject;
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            return (int)NetworkManager.Singleton.LocalClientId;
        }

        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.LocalPlayerId;
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
        messageText.text = "Bạn có muốn sử dụng bùa cứu để cứu người chơi này không?";
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
        dismissText.text = "KHÔNG CỨU";
        dismissText.fontSize = 24;
        dismissText.fontStyle = FontStyles.Bold;
        dismissText.color = Color.white;
        dismissText.alignment = TextAlignmentOptions.Center;

        promptPanel.SetActive(false);
    }
}
