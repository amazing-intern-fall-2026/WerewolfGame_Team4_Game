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
/// 7. Không tạo popup nếu DogSpirit chỉ chọn mục tiêu nhưng Server chưa xác nhận tấn công thành công.
/// 8. Tuyệt đối không can thiệp code trong Assets/Scripts/Thuong/.
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

        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= HandleLocalWhiteHoundAwakened;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged += HandleLocalWhiteHoundAwakened;

        SubscribeDeathResolver();
        SubscribePlayerDataEvents();
    }

    private void UnsubscribeEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= HandleLocalWhiteHoundAwakened;

        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
        }

        UnsubscribePlayerDataEvents();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeDeathResolver();
        SubscribePlayerDataEvents();
        RegisterNetworkMessageHandler();
        CheckLocalPlayerAwakened();
    }

    private void SubscribeDeathResolver()
    {
        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
            DeathResolver.Instance.DeathResolved += HandleDeathResolved;
        }
    }

    private readonly List<PlayerData> subscribedPlayers = new();

    private void SubscribePlayerDataEvents()
    {
        UnsubscribePlayerDataEvents();

        if (PlayerManager.Instance?.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null)
                {
                    p.Changed += HandlePlayerDataChanged;
                    subscribedPlayers.Add(p);
                }
            }
        }
    }

    private void UnsubscribePlayerDataEvents()
    {
        foreach (var p in subscribedPlayers)
        {
            if (p != null)
            {
                p.Changed -= HandlePlayerDataChanged;
            }
        }
        subscribedPlayers.Clear();
    }

    private void HandlePlayerDataChanged(PlayerData player)
    {
        if (player == null) return;
        int localId = GetLocalPlayerId();
        if (localId >= 0 && player.playerID == localId)
        {
            bool isWhiteHound = player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf;
            if (isWhiteHound && player.isWhiteHoundAwakened)
            {
                ShowNotification();
            }
        }
    }

    public void HandleDeathResolved(DeathResult result)
    {
        if (result.Request.Target == null) return;
        PlayerData target = result.Request.Target;
        bool isWhiteHound = target.roleType == RoleType.WhiteHound || target.roleType == RoleType.WhiteWolf;
        bool bittenByDogSpirit = result.Request.SourceRole == RoleType.DogSpirit ||
                                 (result.Request.Source != null && result.Request.Source.roleType == RoleType.DogSpirit);

        // Chỉ khi DogSpirit thực sự cắn trúng Bạch Khuyển và Server đã xác nhận thức tỉnh thành công
        if (isWhiteHound && bittenByDogSpirit && target.isWhiteHoundAwakened)
        {
            Debug.Log($"[NetworkWhiteHoundNotificationUI] DeathResolver xác nhận DogSpirit cắn trúng WhiteHound (Player {target.playerID + 1}) -> Gửi thông báo thức tỉnh!");
            NotifyPlayerAwakened(target.playerID);
        }
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
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[NetworkWhiteHoundNotificationUI] Bỏ qua thông báo thức tỉnh không đến từ Server.");
            return;
        }

        reader.ReadValueSafe(out int targetPlayerId);
        if (targetPlayerId < 0 || targetPlayerId != GetLocalPlayerId())
        {
            Debug.LogWarning("[NetworkWhiteHoundNotificationUI] Bỏ qua thông báo không dành cho người chơi cục bộ.");
            return;
        }

        Debug.Log("[NetworkWhiteHoundNotificationUI] Client nhận được gói tin thức tỉnh riêng từ Server!");
        ShowNotification();
    }

    /// <summary>
    /// Tra cứu ClientId theo PlayerId.
    /// </summary>
    public static ulong GetClientIdForPlayerId(int playerId)
    {
        // 1. Kiểm tra qua ThuongOnlineSession nếu đang trong Thương Online
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.TryGetClientIdForPlayer(playerId, out ulong sessionClientId))
        {
            return sessionClientId;
        }

        // 2. Kiểm tra qua PlayerController
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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

        // 3. Fallback: Netcode connected clients
        var net = NetworkManager.Singleton;
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

    private void CheckLocalPlayerAwakened()
    {
        if (hasNotifiedThisMatch) return;

        int localId = GetLocalPlayerId();
        if (localId < 0) return;

        PlayerData localPlayer = PlayerManager.Instance?.GetplayerByID(localId);
        if (localPlayer != null)
        {
            bool isWhiteHound = localPlayer.roleType == RoleType.WhiteHound || localPlayer.roleType == RoleType.WhiteWolf;
            if (isWhiteHound && localPlayer.isWhiteHoundAwakened)
            {
                ShowNotification();
            }
        }
    }

    /// <summary>
    /// Gửi thông báo thức tỉnh riêng đến Client của người chơi Bạch Khuyển.
    /// </summary>
    public static void NotifyPlayerAwakened(int playerId)
    {
        EnsureInstance();

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            // Môi trường offline hoặc unit test
            Instance?.ShowNotification();
            return;
        }

        if (net.IsServer)
        {
            ulong targetClientId = GetClientIdForPlayerId(playerId);
            Debug.Log($"[NetworkWhiteHoundNotificationUI] Server xác nhận Bạch Khuyển (Player {playerId + 1}) thức tỉnh! Target ClientId = {targetClientId}");

            if (targetClientId == net.LocalClientId)
            {
                // Host là người chơi Bạch Khuyển
                Instance?.ShowNotification();
            }
            else if (targetClientId != ulong.MaxValue && net.ConnectedClients.ContainsKey(targetClientId))
            {
                // Client từ xa là Bạch Khuyển -> gửi tin nhắn riêng qua mạng
                using var writer = new FastBufferWriter(16, Allocator.Temp);
                writer.WriteValueSafe(playerId);
                net.CustomMessagingManager.SendNamedMessage(
                    WhiteHoundAwakenedMessageName,
                    targetClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced
                );
                Debug.Log($"[NetworkWhiteHoundNotificationUI] Đã gửi thông báo thức tỉnh riêng tới Client {targetClientId} (Player {playerId + 1}).");
            }
            else
            {
                // Nếu không phân biệt được ClientId (ví dụ standalone mock), chỉ hiển thị nếu local player đúng là player này
                int localId = GetLocalPlayerId();
                if (localId == playerId)
                {
                    Instance?.ShowNotification();
                }
            }
        }
        else
        {
            // Phía Client: nếu được gọi trực tiếp, kiểm tra xem có phải bản thân không
            int localId = GetLocalPlayerId();
            if (localId == playerId)
            {
                Instance?.ShowNotification();
            }
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
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
