using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý hiển thị thông báo riêng cho người chơi Khuyển Tinh (DogSpirit)
/// khi cắn trúng Bạch Khuyển (White Hound) và Server xác nhận Bạch Khuyển thức tỉnh thành công.
/// 
/// Đảm bảo:
/// 1. Chỉ người chơi sở hữu vai trò DogSpirit nhìn thấy thông báo này (Host hoặc Client).
/// 2. Tiêu đề: "BẠN ĐÃ CẮN TRÚNG BẠCH KHUYỂN!"
/// 3. Dòng phụ: "Đòn tấn công của bạn đã đánh thức bản năng Bạch Khuyển. Mục tiêu đã chuyển sang phe Sói Trắng!"
/// 4. Nhãn phụ: "MỤC TIÊU: WHITE HOUND"
/// 5. Có nút ĐÓNG và tự động ẩn sau khoảng 5 giây.
/// 6. Không che toàn bộ màn hình, giữ nguyên bố cục gameplay.
/// 7. Không gửi cho Bạch Khuyển hay các người chơi khác (hoạt động độc lập với popup Bạch Khuyển).
/// 8. Không tạo popup trùng lặp trong cùng một sự kiện / ván đấu.
/// 9. Reset trạng thái khi bắt đầu ván mới.
/// 10. Tuyệt đối không can thiệp code trong Assets/Scripts/Thuong/.
/// </summary>
public class NetworkDogSpiritNotificationUI : MonoBehaviour
{
    public static NetworkDogSpiritNotificationUI Instance { get; private set; }

    public const string DogSpiritBiteWhiteHoundMessageName = "loi.dogspirit.bitten.whitehound.notification.v1";

    public const string NotificationTitle = "BẠN ĐÃ CẮN TRÚNG BẠCH KHUYỂN!";
    public const string NotificationSubtitle = "Đòn tấn công của bạn đã đánh thức bản năng Bạch Khuyển. Mục tiêu đã chuyển sang phe Sói Trắng!";
    public const string TargetRoleLabel = "MỤC TIÊU: WHITE HOUND";

    private static bool hasNotifiedThisMatch = false;

    private GameObject notificationCanvasGO;
    private GameObject notificationPanelGO;
    private Coroutine autoHideCoroutine;
    private float autoHideDuration = 5.0f;
    private bool isVisible = false;

    public static bool HasNotifiedThisMatch => hasNotifiedThisMatch;
    public bool IsVisible => isVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<NetworkDogSpiritNotificationUI>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("[NetworkDogSpiritNotificationUI]");
        Instance = go.AddComponent<NetworkDogSpiritNotificationUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
        RegisterNetworkMessageHandler();
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnregisterNetworkMessageHandler();
        UnsubscribeEvents();
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RegisterNetworkMessageHandler();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnregisterNetworkMessageHandler();
    }

    private void SubscribeEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        SubscribeDeathResolver();
    }

    private void UnsubscribeEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeDeathResolver();
        RegisterNetworkMessageHandler();

        // Reset trạng thái khi rời trận đấu về sảnh hoặc menu
        string sceneName = scene.name;
        if (sceneName == "MainMenu" || sceneName == "OnlineSceneLobby" || sceneName == "SceneLobby")
        {
            ResetMatchNotificationState();
        }
    }

    private void SubscribeDeathResolver()
    {
        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
            DeathResolver.Instance.DeathResolved += HandleDeathResolved;
        }
    }

    /// <summary>
    /// Xử lý sự kiện kết quả tấn công từ DeathResolver.
    /// Chỉ khi DogSpirit tấn công Bạch Khuyển và Server xác nhận thức tỉnh thành công,
    /// kích hoạt gửi thông báo riêng tới DogSpirit.
    /// </summary>
    public void HandleDeathResolved(DeathResult result)
    {
        // Yêu cầu: Loại bỏ thông báo cho DogSpirit biết đã cắn trúng Bạch Khuyển
        return;
    }

    /// <summary>
    /// Xác định chính xác PlayerId của DogSpirit thực hiện đòn tấn công.
    /// </summary>
    public static int ResolveDogSpiritPlayerId(DeathResult result)
    {
        // 1. Kiểm tra request source
        if (result.Request.Source != null &&
            (result.Request.Source.roleType == RoleType.DogSpirit || result.Request.SourceRole == RoleType.DogSpirit))
        {
            return result.Request.Source.playerID;
        }

        // 2. Tra cứu qua PlayerManager
        if (PlayerManager.Instance?.players != null)
        {
            var aliveDog = PlayerManager.Instance.players.Find(p => p != null && p.roleType == RoleType.DogSpirit && p.isAlive);
            if (aliveDog != null) return aliveDog.playerID;

            var anyDog = PlayerManager.Instance.players.Find(p => p != null && p.roleType == RoleType.DogSpirit);
            if (anyDog != null) return anyDog.playerID;
        }

        // 3. Tra cứu qua RoleManager
        if (RoleManager.Instance?.playerRoles != null)
        {
            foreach (var kvp in RoleManager.Instance.playerRoles)
            {
                if (kvp.Value != null && kvp.Value.roleType == RoleType.DogSpirit)
                {
                    return kvp.Key;
                }
            }
        }

        // 4. Tra cứu qua ThuongOnlineSession
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.TryGetPlayerIdForRole(RoleType.DogSpirit, out int dsId))
        {
            return dsId;
        }

        // 5. Fallback: Nếu local player là DogSpirit (trong môi trường mock/offline)
        if (IsLocalPlayerDogSpirit())
        {
            return GetLocalPlayerId();
        }

        return -1;
    }

    /// <summary>
    /// Đặt lại trạng thái thông báo khi sang ván đấu mới.
    /// </summary>
    public static void ResetMatchNotificationState()
    {
        hasNotifiedThisMatch = false;
        if (Instance != null)
        {
            Instance.HideNotification();
        }
    }

    private void RegisterNetworkMessageHandler()
    {
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && net.CustomMessagingManager != null)
        {
            try
            {
                net.CustomMessagingManager.UnregisterNamedMessageHandler(DogSpiritBiteWhiteHoundMessageName);
                net.CustomMessagingManager.RegisterNamedMessageHandler(DogSpiritBiteWhiteHoundMessageName, ReceiveDogSpiritBiteNotification);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkDogSpiritNotificationUI] Lỗi đăng ký handler: {ex.Message}");
            }
        }
    }

    private void UnregisterNetworkMessageHandler()
    {
        var net = NetworkManager.Singleton;
        if (net != null && net.CustomMessagingManager != null)
        {
            try
            {
                net.CustomMessagingManager.UnregisterNamedMessageHandler(DogSpiritBiteWhiteHoundMessageName);
            }
            catch { }
        }
    }

    private void ReceiveDogSpiritBiteNotification(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[NetworkDogSpiritNotificationUI] Bỏ qua thông báo không đến từ Server.");
            return;
        }

        reader.ReadValueSafe(out int targetPlayerId);
        if (targetPlayerId < 0 || targetPlayerId != GetLocalPlayerId())
        {
            Debug.LogWarning("[NetworkDogSpiritNotificationUI] Bỏ qua thông báo không dành cho người chơi cục bộ.");
            return;
        }

        Debug.Log("[NetworkDogSpiritNotificationUI] Client nhận được gói tin cắn trúng Bạch Khuyển từ Server!");
        ShowNotification();
    }

    public static int GetLocalPlayerId()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.LocalPlayerId;
        }

        if (NetworkManager.Singleton != null)
        {
            return (int)NetworkManager.Singleton.LocalClientId;
        }

        return -1;
    }

    public static bool IsLocalPlayerDogSpirit()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            return session.GetPrivateRole() == RoleType.DogSpirit;
        }

        int localId = GetLocalPlayerId();
        if (localId >= 0 && PlayerManager.Instance?.players != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localId);
            if (p != null && p.roleType == RoleType.DogSpirit)
            {
                return true;
            }
        }

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.LocalRole == RoleType.DogSpirit)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gửi thông báo riêng đến Client của người chơi Khuyển Tinh.
    /// </summary>
    public static void NotifyDogSpiritBitten(int dogSpiritPlayerId, int whiteHoundPlayerId)
    {
        // Yêu cầu: Loại bỏ thông báo cho DogSpirit biết đã cắn trúng Bạch Khuyển
        return;
    }

    /// <summary>
    /// Hiển thị popup thông báo cho DogSpirit cục bộ (đã bị vô hiệu hóa theo yêu cầu).
    /// </summary>
    public void ShowNotification()
    {
        // Yêu cầu: Loại bỏ thông báo cho DogSpirit biết đã cắn trúng Bạch Khuyển
        return;
    }

    public void HideNotification()
    {
        isVisible = false;
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (notificationPanelGO != null)
        {
            if (Application.isPlaying) Destroy(notificationPanelGO);
            else DestroyImmediate(notificationPanelGO);
            notificationPanelGO = null;
        }

        if (notificationCanvasGO != null)
        {
            if (Application.isPlaying) Destroy(notificationCanvasGO);
            else DestroyImmediate(notificationCanvasGO);
            notificationCanvasGO = null;
        }
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideNotification();
    }

    private void BuildUI()
    {
        if (notificationCanvasGO != null)
        {
            notificationCanvasGO.SetActive(true);
            return;
        }

        notificationCanvasGO = new GameObject("DogSpiritBiteNotificationCanvas");
        notificationCanvasGO.hideFlags = HideFlags.DontSave;

        Canvas canvas = notificationCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32001;

        CanvasScaler scaler = notificationCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        notificationCanvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem
        var existingEventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingEventSystem == null)
        {
            var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        // Panel trung tâm phong cách Ma Sói
        notificationPanelGO = new GameObject("NotificationPanel", typeof(RectTransform), typeof(Image));
        notificationPanelGO.transform.SetParent(notificationCanvasGO.transform, false);

        RectTransform panelRect = notificationPanelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900, 390);

        Image panelImg = notificationPanelGO.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.05f, 0.08f, 0.98f);

        Outline outline = notificationPanelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.35f, 0.15f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Header icon / nhãn trạng thái Ma Sói
        CreateLabel(notificationPanelGO.transform, "AlertHeader", "🐺 KHUYỂN TINH TẤN CÔNG THÀNH CÔNG 🐺",
            new Vector2(0, 140), new Vector2(820, 45), 24, new Color(1f, 0.45f, 0.35f), FontStyles.Bold);

        // Tiêu đề chính
        CreateLabel(notificationPanelGO.transform, "TitleText", NotificationTitle,
            new Vector2(0, 75), new Vector2(840, 70), 30, new Color(1f, 0.88f, 0.2f), FontStyles.Bold);

        // Nhãn mục tiêu White Hound
        CreateBadgeLabel(notificationPanelGO.transform, "TargetBadge", TargetRoleLabel,
            new Vector2(0, 15), new Vector2(340, 38));

        // Dòng phụ
        CreateLabel(notificationPanelGO.transform, "SubtitleText", NotificationSubtitle,
            new Vector2(0, -45), new Vector2(820, 65), 21, new Color(0.92f, 0.95f, 1f), FontStyles.Normal);

        // Nút Đóng
        CreateCloseButton(notificationPanelGO.transform, new Vector2(0, -130), new Vector2(220, 56));
    }

    private void CreateBadgeLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size)
    {
        GameObject badgeGO = new GameObject(name, typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(parent, false);

        RectTransform rt = badgeGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = badgeGO.GetComponent<Image>();
        img.color = new Color(0.35f, 0.08f, 0.12f, 0.9f);

        Outline outline = badgeGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.4f, 0.3f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(badgeGO.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.75f, 0.65f);
        tmp.raycastTarget = false;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void CreateLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void CreateCloseButton(Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject btnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.75f, 0.18f, 0.22f, 1f);

        Outline btnOutline = btnGO.AddComponent<Outline>();
        btnOutline.effectColor = new Color(1f, 0.8f, 0.8f, 0.9f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(HideNotification);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "ĐÓNG";
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
