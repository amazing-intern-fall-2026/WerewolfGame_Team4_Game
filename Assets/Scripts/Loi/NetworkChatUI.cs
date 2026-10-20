using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Chat Multiplayer nhỏ gọn cho game Ma Sói màn hình ngang (Landscape).
/// - Khung chat nằm tại góc dưới bên trái, kích thước ~30–35% chiều rộng và ~35–40% chiều cao màn hình khi mở.
/// - Khi thu gọn, hiển thị nút Chat nổi nhỏ 52 dp với huy hiệu tin nhắn chưa đọc.
/// - Hỗ trợ kéo cuộn tin nhắn, ô nhập văn bản và nút gửi tối ưu cho thao tác chạm trên điện thoại.
/// - Tự động nâng khung chat khi mở bàn phím ảo trên Android/iOS để không bị che khuất.
/// </summary>
public class NetworkChatUI : MonoBehaviour
{
    public static NetworkChatUI Instance { get; private set; }

    [Header("Panel Root & Toggle Button")]
    [SerializeField] private GameObject chatPanelRoot;
    [SerializeField] private Button floatingChatButton;
    [SerializeField] private Button minimizeButton;
    [SerializeField] private TextMeshProUGUI unreadBadgeText;
    [SerializeField] private GameObject unreadBadgeObj;

    [Header("Messages & Scroll")]
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

    private int unreadMessagesCount = 0;
    private bool isOpen = false;
    public bool IsOpen => isOpen;

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

        // Mặc định khởi đầu ở trạng thái thu gọn (chỉ hiện nút nổi góc dưới bên trái)
        SetChatOpen(false);

        // Hiển thị lại các tin nhắn đã có từ trước (nếu có)
        ReloadHistory();
    }

    private void OnEnable()
    {
        NetworkChatManager.OnChatMessageReceived += HandleMessageReceived;
        NetworkChatManager.OnChatHistoryCleared += HandleHistoryCleared;
    }

    private void OnDisable()
    {
        NetworkChatManager.OnChatMessageReceived -= HandleMessageReceived;
        NetworkChatManager.OnChatHistoryCleared -= HandleHistoryCleared;
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
    // 1. QUẢN LÝ TRẠNG THÁI MỞ / THU GỌN
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
            // Khi mở khung chat, ẩn nút nổi để tiết kiệm diện tích; khi đóng, hiện nút nổi
            floatingChatButton.gameObject.SetActive(!isOpen);
        }

        if (isOpen)
        {
            // Reset số lượng tin nhắn chưa đọc khi mở
            unreadMessagesCount = 0;
            UpdateUnreadBadge();

            // Cuộn xuống cuối
            StartCoroutine(ScrollToBottomCoroutine());

            // Tự động kích hoạt bàn phím trên mobile nếu cần hoặc focus ô nhập
            if (chatInputField != null)
            {
                chatInputField.ActivateInputField();
            }
        }
    }

    // =========================================================
    // 2. GỬI & NHẬN TIN NHẮN
    // =========================================================

    public void OnSendClicked()
    {
        if (chatInputField == null) return;

        string text = chatInputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (NetworkChatManager.Instance != null)
        {
            NetworkChatManager.Instance.SendChatMessage(text);
        }
        else
        {
            Debug.LogWarning("[CHAT UI] Chưa có NetworkChatManager trong scene!");
        }

        chatInputField.text = string.Empty;
        chatInputField.ActivateInputField();
    }

    private void HandleMessageReceived(NetworkChatManager.ChatMessageEntry entry)
    {
        AddMessageItem(entry);

        if (!isOpen)
        {
            unreadMessagesCount++;
            UpdateUnreadBadge();
        }

        StartCoroutine(ScrollToBottomCoroutine());
    }

    private void HandleHistoryCleared()
    {
        if (messageContent != null)
        {
            for (int i = messageContent.childCount - 1; i >= 0; i--)
            {
                Destroy(messageContent.GetChild(i).gameObject);
            }
        }
        unreadMessagesCount = 0;
        UpdateUnreadBadge();
    }

    private void ReloadHistory()
    {
        if (NetworkChatManager.Instance == null || messageContent == null) return;

        foreach (var msg in NetworkChatManager.Instance.MessageHistory)
        {
            AddMessageItem(msg);
        }
        StartCoroutine(ScrollToBottomCoroutine());
    }

    private void AddMessageItem(NetworkChatManager.ChatMessageEntry entry)
    {
        if (messageContent == null) return;

        GameObject itemObj = null;
        if (messageItemPrefab != null)
        {
            itemObj = Instantiate(messageItemPrefab, messageContent);
        }
        else
        {
            // Fallback tạo nhanh GameObject Text nếu không có prefab
            itemObj = new GameObject("ChatMessageItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            itemObj.transform.SetParent(messageContent, false);
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

            if (entry.IsSystem)
            {
                textComp.text = $"<color=#FFD700><i>[Hệ thống] {entry.MessageText}</i></color>";
            }
            else
            {
                ulong localClientId = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
                bool isMe = entry.SenderClientId == localClientId;

                string nameColor = isMe ? "#55DDFF" : "#FFCC44";
                string prefix = isMe ? "Bạn" : entry.SenderName.ToString();

                textComp.text = $"<color={nameColor}><b>{prefix}:</b></color> <color=#FFFFFF>{entry.MessageText}</color>";
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
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

                // Nâng khung chat lên trên bàn phím ảo
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

        // Tìm FloatingChatButton nếu chưa gán
        if (floatingChatButton == null)
        {
            Transform fbTrans = canvasTrans.Find("FloatingChatButton");
            if (fbTrans != null)
            {
                floatingChatButton = fbTrans.GetComponent<Button>();
            }
        }

        // Tìm ChatPanelRoot nếu chưa gán
        if (chatPanelRoot == null)
        {
            Transform pTrans = canvasTrans.Find("CompactChatPanel");
            if (pTrans != null)
            {
                chatPanelRoot = pTrans.gameObject;
            }
        }

        // Nếu cả hai đều chưa có, tự động tạo cấu trúc UI hoàn chỉnh tại runtime
        if (floatingChatButton == null || chatPanelRoot == null)
        {
            BuildRuntimeChatUI(canvasTrans);
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
    /// Xây dựng tự động hệ thống Chat UI chuẩn nếu scene chưa được setup trước qua Editor.
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
            fbRt.sizeDelta = new Vector2(52, 52); // Chuẩn 52 dp

            Image fbImg = fbObj.GetComponent<Image>();
            fbImg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
            floatingChatButton = fbObj.GetComponent<Button>();
            floatingChatButton.targetGraphic = fbImg;

            // Icon văn bản '💬' hoặc 'CHAT'
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
            pImg.color = new Color(0.06f, 0.09f, 0.14f, 0.88f); // Nền tối bán trong suốt, không làm mờ cả màn hình
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

            // Minimize / Close Button
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

            // --- Message Scroll View ---
            GameObject scrollObj = new GameObject("MessageScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollObj.transform.SetParent(pObj.transform, false);
            RectTransform svRt = scrollObj.GetComponent<RectTransform>();
            svRt.anchorMin = new Vector2(0, 0);
            svRt.anchorMax = new Vector2(1, 1);
            svRt.offsetMin = new Vector2(8, 46); // Chừa chỗ cho hàng nhập ở dưới
            svRt.offsetMax = new Vector2(-8, -36); // Chừa chỗ cho header

            Image svImg = scrollObj.GetComponent<Image>();
            svImg.color = new Color(0, 0, 0, 0.25f);
            scrollRect = scrollObj.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
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

            // Content
            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewportObj.transform, false);
            messageContent = contentObj.GetComponent<RectTransform>();
            messageContent.anchorMin = new Vector2(0, 1);
            messageContent.anchorMax = new Vector2(1, 1);
            messageContent.pivot = new Vector2(0.5f, 1);
            messageContent.offsetMin = Vector2.zero;
            messageContent.offsetMax = Vector2.zero;

            var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var csf = contentObj.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = vpRt;
            scrollRect.content = messageContent;

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

            // Text Area
            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(8, 2);
            taRt.offsetMax = new Vector2(-8, -2);

            // Placeholder Text
            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            phObj.transform.SetParent(textArea.transform, false);
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI phTmp = phObj.GetComponent<TextMeshProUGUI>();
            if (cachedFont != null) phTmp.font = cachedFont;
            phTmp.text = "Nhập tin nhắn...";
            phTmp.fontSize = 14;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.75f);
            phTmp.alignment = TextAlignmentOptions.Left;

            // Text Component
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
        }

        SetupEvents();
        Debug.Log("[CHAT UI] Đã khởi tạo hoàn chỉnh hệ thống Chat UI đa tỷ lệ màn hình tại góc dưới bên trái.");
    }
}
