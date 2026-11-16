using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý hiển thị thông báo thức tỉnh riêng cho người chơi Bạch Khuyển (White Hound)
/// khi bị Khuyển Tinh (DogSpirit) cắn trúng trong đêm.
/// 
/// Đảm bảo:
/// 1. Chỉ người chơi sở hữu vai trò Bạch Khuyển nhìn thấy thông báo.
/// 2. Tiêu đề: "BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG! BẢN NĂNG BẠCH KHUYỂN ĐÃ THỨC TỈNH!"
/// 3. Dòng phụ: "Bạn đã chuyển sang phe Sói Trắng. Hãy tiếp tục trận đấu!"
/// 4. Tự động ẩn sau 5 giây hoặc người chơi bấm nút "Đóng".
/// 5. Đồng bộ an toàn cho cả Host và Client qua NetworkManager/CustomMessagingManager.
/// 6. Không tạo thông báo trùng lặp trong cùng một ván đấu.
/// 7. Tuyệt đối không can thiệp code trong Assets/Scripts/Thuong/.
/// </summary>
public class NetworkWhiteHoundNotificationUI : MonoBehaviour
{
    public static NetworkWhiteHoundNotificationUI Instance { get; private set; }

    public const string WhiteHoundAwakenedMessageName = "loi.whitehound.awakened.notification.v1";

    public const string NotificationTitle = "BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG! BẢN NĂNG BẠCH KHUYỂN ĐÃ THỨC TỈNH!";
    public const string NotificationSubtitle = "Bạn đã chuyển sang phe Sói Trắng. Hãy tiếp tục trận đấu!";

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
        var existing = FindAnyObjectByType<NetworkWhiteHoundNotificationUI>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("[NetworkWhiteHoundNotificationUI]");
        Instance = go.AddComponent<NetworkWhiteHoundNotificationUI>();
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
    }

    private void OnDestroy()
    {
        UnregisterNetworkMessageHandler();
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged += HandleLocalWhiteHoundAwakened;
        RegisterNetworkMessageHandler();
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= HandleLocalWhiteHoundAwakened;
        UnregisterNetworkMessageHandler();
    }

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
                net.CustomMessagingManager.UnregisterNamedMessageHandler(WhiteHoundAwakenedMessageName);
                net.CustomMessagingManager.RegisterNamedMessageHandler(WhiteHoundAwakenedMessageName, ReceiveAwakenedNotification);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkWhiteHoundNotificationUI] Lỗi đăng ký handler: {ex.Message}");
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
                net.CustomMessagingManager.UnregisterNamedMessageHandler(WhiteHoundAwakenedMessageName);
            }
            catch { }
        }
    }

    private void ReceiveAwakenedNotification(ulong senderClientId, FastBufferReader reader)
    {
        Debug.Log("[NetworkWhiteHoundNotificationUI] Nhận gói tin thông báo thức tỉnh từ Server!");
        ShowNotification();
    }

    /// <summary>
    /// Gửi thông báo thức tỉnh riêng đến Client của người chơi Bạch Khuyển.
    /// </summary>
    public static void NotifyPlayerAwakened(int playerId)
    {
        EnsureInstance();

        // Kiểm tra xem máy hiện tại có phải là người chơi Bạch Khuyển đó không
        bool isLocal = false;
        ulong targetClientId = ulong.MaxValue;

        var net = NetworkManager.Singleton;
        if (net != null)
        {
            // Tìm ClientId của playerId
            if (PlayerManager.Instance?.players != null)
            {
                var targetPlayer = PlayerManager.Instance.GetplayerByID(playerId);
                if (targetPlayer != null)
                {
                    targetClientId = (ulong)targetPlayer.playerID;
                }
            }

            if (net.IsServer && targetClientId != ulong.MaxValue)
            {
                if (targetClientId == net.LocalClientId)
                {
                    isLocal = true;
                }
                else if (net.ConnectedClients.ContainsKey(targetClientId))
                {
                    // Gửi tin nhắn riêng đến client mục tiêu
                    using var writer = new FastBufferWriter(16, Allocator.Temp);
                    writer.WriteValueSafe(playerId);
                    net.CustomMessagingManager.SendNamedMessage(WhiteHoundAwakenedMessageName, targetClientId, writer, NetworkDelivery.ReliableSequenced);
                    Debug.Log($"[NetworkWhiteHoundNotificationUI] Đã gửi thông báo thức tỉnh riêng tới Client {targetClientId} (Player {playerId + 1}).");
                }
            }
            else
            {
                isLocal = true;
            }
        }
        else
        {
            isLocal = true;
        }

        if (isLocal)
        {
            Instance?.ShowNotification();
        }
    }

    private void HandleLocalWhiteHoundAwakened(bool awakened)
    {
        if (awakened)
        {
            ShowNotification();
        }
    }

    /// <summary>
    /// Hiển thị popup thông báo thức tỉnh cho người chơi cục bộ.
    /// </summary>
    public void ShowNotification()
    {
        if (hasNotifiedThisMatch)
        {
            Debug.Log("[NetworkWhiteHoundNotificationUI] Đã phát thông báo trong ván này, bỏ qua để tránh trùng lặp.");
            return;
        }

        hasNotifiedThisMatch = true;
        isVisible = true;

        Debug.Log($"[NetworkWhiteHoundNotificationUI] HIỂN THỊ THÔNG BÁO:\n- Tiêu đề: {NotificationTitle}\n- Phụ: {NotificationSubtitle}");

        BuildUI();

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
        }
        if (autoHideDuration > 0f)
        {
            autoHideCoroutine = StartCoroutine(AutoHideRoutine(autoHideDuration));
        }
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

        notificationCanvasGO = new GameObject("WhiteHoundNotificationCanvas");
        notificationCanvasGO.hideFlags = HideFlags.DontSave;

        Canvas canvas = notificationCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;

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

        // Panel thông báo trung tâm
        notificationPanelGO = new GameObject("NotificationPanel", typeof(RectTransform), typeof(Image));
        notificationPanelGO.transform.SetParent(notificationCanvasGO.transform, false);

        RectTransform panelRect = notificationPanelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(880, 360);

        Image panelImg = notificationPanelGO.GetComponent<Image>();
        panelImg.color = new Color(0.10f, 0.08f, 0.16f, 0.98f);

        Outline outline = notificationPanelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.25f, 0.25f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Header icon / cảnh báo
        CreateLabel(notificationPanelGO.transform, "AlertHeader", "⚠️ CẢNH BÁO THỨC TỈNH ⚠️",
            new Vector2(0, 125), new Vector2(800, 45), 26, new Color(1f, 0.4f, 0.4f), FontStyles.Bold);

        // Tiêu đề chính
        CreateLabel(notificationPanelGO.transform, "TitleText", NotificationTitle,
            new Vector2(0, 55), new Vector2(820, 80), 28, Color.yellow, FontStyles.Bold);

        // Dòng phụ
        CreateLabel(notificationPanelGO.transform, "SubtitleText", NotificationSubtitle,
            new Vector2(0, -25), new Vector2(800, 55), 22, new Color(0.9f, 0.95f, 1f), FontStyles.Normal);

        // Nút Đóng
        CreateCloseButton(notificationPanelGO.transform, new Vector2(0, -115), new Vector2(220, 60));
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
        img.color = new Color(0.85f, 0.25f, 0.25f, 1f);

        Outline btnOutline = btnGO.AddComponent<Outline>();
        btnOutline.effectColor = Color.white;
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
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    // =========================================================
    // DỰ PHÒNG OnGUI
    // =========================================================
    private void OnGUI()
    {
        if (!isVisible) return;
        if (notificationPanelGO != null) return;

        GUI.depth = -3000;
        float w = 620;
        float h = 220;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "");
        GUI.Box(new Rect(x, y, w, h), "=== BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG! ===");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(x + 10, y + 35, w - 20, 60), NotificationTitle, titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 10, y + 105, w - 20, 35), NotificationSubtitle, subStyle);

        if (GUI.Button(new Rect(x + (w - 140) / 2f, y + 155, 140, 45), "ĐÓNG"))
        {
            HideNotification();
        }
    }
}
