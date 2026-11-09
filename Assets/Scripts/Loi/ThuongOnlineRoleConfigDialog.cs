using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Modal tùy chỉnh vai trò và số lượng vai trò cho Thương Online (Lobby).
/// Tối ưu hóa đặc biệt cho thiết bị di động (Android & iOS) và màn hình cảm ứng:
///   - 4 khu vực phân tách rõ ràng (Tiêu đề, Danh sách cuộn, Thông tin cấu hình, Nút chức năng).
///   - Sử dụng VerticalLayoutGroup với childControlHeight = true để các hàng vai trò KHÔNG BAO GIỜ bị chồng lấn.
///   - Chiều cao mỗi hàng vai trò 64px, touch target nút + / - to rõ (font 26, kích thước ngón tay dễ chạm).
///   - ScrollView chiếm 68% diện tích bảng, cuộn dọc mượt mà có scrollbar hiển thị vị trí.
///   - Nền backdrop và cửa sổ đặc 100% không cho đồ họa game phía sau xuyên qua làm rối mắt.
///   - Phân quyền: Host được chỉnh sửa, Client chỉ xem cấu hình.
/// </summary>
public class ThuongOnlineRoleConfigDialog : MonoBehaviour
{
    private static ThuongOnlineRoleConfigDialog instance;
    public static ThuongOnlineRoleConfigDialog Instance => instance;
    public static bool IsShowing => instance != null && instance.gameObject.activeSelf;

    private Canvas dialogCanvas;
    private GraphicRaycaster dialogRaycaster;
    private GameObject shadeRoot;
    private GameObject windowRoot;
    private RectTransform contentContainer;
    private ScrollRect roleScrollRect;

    private TMP_Text titleText;
    private TMP_Text statusSummaryText;
    private GameObject validationBanner;
    private Image validationBannerImage;
    private TMP_Text validationText;

    private Button defaultButton;
    private Button saveButton;
    private Button closeButton;

    private bool isHostMode = true;
    private int currentPlayerCount = 2;
    private Action<Dictionary<RoleType, int>> onSaveCallback;

    private readonly Dictionary<RoleType, int> draftRoleAmounts = new Dictionary<RoleType, int>();
    private readonly Dictionary<RoleType, TMP_Text> roleAmountLabels = new Dictionary<RoleType, TMP_Text>();
    private readonly Dictionary<RoleType, TMP_Text> roleNameLabels = new Dictionary<RoleType, TMP_Text>();
    private readonly Dictionary<RoleType, Button> roleMinusButtons = new Dictionary<RoleType, Button>();
    private readonly Dictionary<RoleType, Button> rolePlusButtons = new Dictionary<RoleType, Button>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void ShowDialog(
        bool isHost,
        Dictionary<RoleType, int> initialConfig,
        int playerCount,
        Action<Dictionary<RoleType, int>> onSave)
    {
        EnsureInstance();
        instance.SetupAndOpen(isHost, initialConfig, playerCount, onSave);
    }

    public static void HideDialog()
    {
        if (instance != null)
        {
            instance.gameObject.SetActive(false);
            if (instance.shadeRoot != null)
                instance.shadeRoot.SetActive(false);
        }
    }

    public static void EnsureInstance()
    {
        if (instance != null) return;

        var existing = FindAnyObjectByType<ThuongOnlineRoleConfigDialog>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[ThuongOnlineRoleConfigDialog]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ThuongOnlineRoleConfigDialog>();
        instance.BuildUI();
        go.SetActive(false);
    }

    private void SetupAndOpen(
        bool isHost,
        Dictionary<RoleType, int> initialConfig,
        int playerCount,
        Action<Dictionary<RoleType, int>> onSave)
    {
        isHostMode = isHost;
        currentPlayerCount = Mathf.Max(1, playerCount);
        onSaveCallback = onSave;

        draftRoleAmounts.Clear();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            draftRoleAmounts[role] = 0;
        }

        if (initialConfig != null)
        {
            foreach (var kvp in initialConfig)
            {
                draftRoleAmounts[kvp.Key] = Mathf.Max(0, kvp.Value);
            }
        }

        if (titleText != null)
        {
            titleText.text = isHost ? "THIẾT LẬP VAI TRÒ VÀ SỐ LƯỢNG" : "DANH SÁCH VAI TRÒ (CHỈ XEM)";
        }

        // Cập nhật hiển thị các nút chức năng theo quyền
        if (defaultButton != null) defaultButton.gameObject.SetActive(isHost);
        if (saveButton != null) saveButton.gameObject.SetActive(isHost);

        if (closeButton != null)
        {
            var closeRect = closeButton.GetComponent<RectTransform>();
            if (isHost)
            {
                closeRect.anchorMin = new Vector2(0.70f, 0f);
                closeRect.anchorMax = new Vector2(0.96f, 1f);
            }
            else
            {
                // Nếu là Client chỉ xem, căn nút Đóng ra giữa
                closeRect.anchorMin = new Vector2(0.35f, 0f);
                closeRect.anchorMax = new Vector2(0.65f, 1f);
            }
        }

        RefreshAllRows();
        RefreshSummaryAndValidation();

        // Đưa thanh cuộn về đầu trang
        if (roleScrollRect != null)
        {
            roleScrollRect.verticalNormalizedPosition = 1f;
        }

        gameObject.SetActive(true);
        if (shadeRoot != null) shadeRoot.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void UpdateSynchronizedConfig(Dictionary<RoleType, int> newConfig, int playerCount)
    {
        currentPlayerCount = Mathf.Max(1, playerCount);
        if (!isHostMode && newConfig != null)
        {
            foreach (var kvp in newConfig)
            {
                draftRoleAmounts[kvp.Key] = Mathf.Max(0, kvp.Value);
            }
            RefreshAllRows();
            RefreshSummaryAndValidation();
        }
    }

    // ==========================================
    // XÂY DỰNG TOÀN BỘ GIAO DIỆN (BUILD UI)
    // ==========================================

    private void BuildUI()
    {
        dialogCanvas = gameObject.AddComponent<Canvas>();
        dialogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dialogCanvas.sortingOrder = 30; // Nằm trên cả Lobby Canvas

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;

        dialogRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        // 1. BACKDROP ĐẬM - KHÔNG NHÌN XUYÊN THỦNG GÂY RỐI MẮT
        shadeRoot = CreatePanel("Shade", transform, Vector2.zero, Vector2.one, new Color(0.02f, 0.02f, 0.02f, 0.94f));
        var shadeImg = shadeRoot.GetComponent<Image>();
        shadeImg.raycastTarget = true;

        // 2. CỬA SỔ CHÍNH (WINDOW) - CĂN GIỮA, BO GÓC, TẬN DỤNG TỐI ĐA DIỆN TÍCH MÀN HÌNH
        windowRoot = CreatePanel("Window", shadeRoot.transform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.98f), new Color(0.12f, 0.08f, 0.05f, 1.0f));
        var windowImg = windowRoot.GetComponent<Image>();
        windowImg.raycastTarget = true;
        ThuongMedievalTheme.Active?.StylePanel(windowImg, "Window");

        // === KHU VỰC A: THANH TIÊU ĐỀ (TOP BAR) ===
        var topBar = CreatePanel("TopBar", windowRoot.transform, new Vector2(0.015f, 0.885f), new Vector2(0.985f, 0.985f), new Color(0.18f, 0.12f, 0.08f, 1f));
        ThuongMedievalTheme.Active?.StylePanel(topBar.GetComponent<Image>(), "TopBar");

        titleText = CreateText("TitleText", topBar.transform, "THIẾT LẬP VAI TRÒ VÀ SỐ LƯỢNG",
            new Vector2(0.02f, 0.48f), new Vector2(0.98f, 0.96f), 26, TextAlignmentOptions.Center, FontStyle.Bold);
        titleText.color = new Color32(255, 240, 205, 255);

        statusSummaryText = CreateText("StatusSummary", topBar.transform, "Tổng số vai trò đã chọn: 0  |  Số người chơi trong phòng: 0",
            new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.48f), 18, TextAlignmentOptions.Center, FontStyle.Normal);
        statusSummaryText.color = new Color32(225, 192, 128, 255);

        // === KHU VỰC B: DANH SÁCH VAI TRÒ (SCROLL VIEW) ===
        // Chiếm trọn vẹn 69% chiều cao trung tâm bảng từ y = 0.19f đến 0.88f
        var scrollRectGo = CreatePanel("RoleScrollView", windowRoot.transform, new Vector2(0.015f, 0.19f), new Vector2(0.985f, 0.88f), new Color(0.07f, 0.05f, 0.03f, 0.95f));
        roleScrollRect = scrollRectGo.AddComponent<ScrollRect>();
        roleScrollRect.horizontal = false;
        roleScrollRect.vertical = true;
        roleScrollRect.scrollSensitivity = 45f;
        roleScrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreatePanel("Viewport", scrollRectGo.transform, new Vector2(0f, 0f), new Vector2(0.985f, 1f), Color.clear);
        viewport.AddComponent<RectMask2D>();
        roleScrollRect.viewport = viewport.GetComponent<RectTransform>();

        var content = CreatePanel("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Color.clear);
        contentContainer = content.GetComponent<RectTransform>();
        contentContainer.pivot = new Vector2(0.5f, 1f);
        contentContainer.offsetMin = Vector2.zero;
        contentContainer.offsetMax = Vector2.zero;

        // BỐ CỤC VERTICAL LAYOUT GROUP: CHILDCONTROLHEIGHT = TRUE ĐỂ CHỐNG CHỒNG HÀNG
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true; // BẮT BUỘC TRUE: Unity sẽ tự động gán chiều cao 64px cho từng dòng!
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        roleScrollRect.content = contentContainer;

        // Thanh cuộn dọc (Scrollbar)
        var scrollbarGo = CreatePanel("Scrollbar", scrollRectGo.transform, new Vector2(0.987f, 0.01f), new Vector2(0.998f, 0.99f), new Color(0.12f, 0.08f, 0.05f, 1f));
        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var handle = CreatePanel("Handle", scrollbarGo.transform, Vector2.zero, Vector2.one, new Color32(180, 138, 78, 255));
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        roleScrollRect.verticalScrollbar = scrollbar;

        // Sinh danh sách các hàng vai trò
        BuildRoleRows();

        // === KHU VỰC C: THÔNG TIN CẤU HÌNH & VALIDATION BANNER ===
        // Nằm ngay dưới ScrollView từ y = 0.105f đến 0.18f
        validationBanner = CreatePanel("ValidationBanner", windowRoot.transform, new Vector2(0.015f, 0.105f), new Vector2(0.985f, 0.18f), new Color(0.14f, 0.22f, 0.14f, 1f));
        validationBannerImage = validationBanner.GetComponent<Image>();

        validationText = CreateText("ValidationText", validationBanner.transform, "Kiểm tra cấu hình vai trò...",
            new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f), 18, TextAlignmentOptions.Center, FontStyle.Bold);

        // === KHU VỰC D: THANH NÚT CHỨC NĂNG (BOTTOM BUTTON BAR) ===
        // Nằm ở đáy bảng từ y = 0.015f đến 0.095f
        var bottomBar = CreatePanel("BottomBar", windowRoot.transform, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.095f), Color.clear);

        defaultButton = CreateButton("DefaultButton", bottomBar.transform, "MẶC ĐỊNH",
            new Vector2(0.04f, 0.05f), new Vector2(0.30f, 0.95f),
            new Color(0.35f, 0.24f, 0.16f, 1f), OnDefaultClicked);

        saveButton = CreateButton("SaveButton", bottomBar.transform, "LƯU CẤU HÌNH",
            new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.95f),
            new Color(0.12f, 0.44f, 0.16f, 1f), OnSaveClicked, isProminent: true);

        closeButton = CreateButton("CloseButton", bottomBar.transform, "ĐÓNG",
            new Vector2(0.70f, 0.05f), new Vector2(0.96f, 0.95f),
            new Color(0.28f, 0.18f, 0.14f, 1f), OnCloseClicked);
    }

    private void BuildRoleRows()
    {
        roleAmountLabels.Clear();
        roleNameLabels.Clear();
        roleMinusButtons.Clear();
        rolePlusButtons.Clear();

        RoleType[] roles = RoleCatalog.AllConcreteRoles;
        for (int i = 0; i < roles.Length; i++)
        {
            RoleType role = roles[i];
            CreateRoleRow(role, i);
        }
    }

    private void CreateRoleRow(RoleType role, int index)
    {
        // 1. Row Container
        Color rowBg = index % 2 == 0 ? new Color(0.18f, 0.13f, 0.09f, 0.95f) : new Color(0.22f, 0.16f, 0.11f, 0.95f);
        var row = CreatePanel("RoleRow_" + role, contentContainer.transform, Vector2.zero, Vector2.one, rowBg);
        ThuongMedievalTheme.Active?.StylePanel(row.GetComponent<Image>(), "RoleRow");

        // LayoutElement với chiều cao chuẩn 64px
        var layoutElem = row.AddComponent<LayoutElement>();
        layoutElem.minHeight = 64f;
        layoutElem.preferredHeight = 64f;
        layoutElem.flexibleHeight = 0f;

        // 2. Role Icon (Trái, 48x48)
        var iconObj = CreatePanel("Icon", row.transform, new Vector2(0.012f, 0.12f), new Vector2(0.065f, 0.88f), Color.white);
        var iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        Sprite roleSprite = RoleInfoDatabase.GetRoleSprite(role);
        if (roleSprite != null)
        {
            iconImg.sprite = roleSprite;
            iconImg.enabled = true;
        }
        else
        {
            iconImg.enabled = false;
        }

        // 3. Role Name & Faction Text (Căn trái, font to 21)
        var nameText = CreateText("NameText", row.transform, string.Empty,
            new Vector2(0.075f, 0.10f), new Vector2(0.72f, 0.90f), 20, TextAlignmentOptions.MidlineLeft, FontStyle.Normal);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        roleNameLabels[role] = nameText;

        // 4. Minus Button (−) (Touch target lớn 50px, font 26 dày)
        var minusBtn = CreateButton("MinusBtn", row.transform, "−",
            new Vector2(0.74f, 0.12f), new Vector2(0.81f, 0.88f),
            new Color(0.38f, 0.18f, 0.15f, 1f), () => ChangeRoleAmount(role, -1), fontSize: 26);
        roleMinusButtons[role] = minusBtn;

        // 5. Amount Badge (Nền đen sẫm, số trắng font 24 bold căn giữa)
        var amountBadge = CreatePanel("AmountBadge", row.transform, new Vector2(0.82f, 0.12f), new Vector2(0.91f, 0.88f), new Color(0.09f, 0.06f, 0.04f, 1f));
        var amountText = CreateText("AmountText", amountBadge.transform, "0",
            Vector2.zero, Vector2.one, 24, TextAlignmentOptions.Center, FontStyle.Bold);
        amountText.color = Color.white;
        roleAmountLabels[role] = amountText;

        // 6. Plus Button (+) (Touch target lớn 50px, font 26 dày)
        var plusBtn = CreateButton("PlusBtn", row.transform, "+",
            new Vector2(0.92f, 0.12f), new Vector2(0.99f, 0.88f),
            new Color(0.16f, 0.38f, 0.18f, 1f), () => ChangeRoleAmount(role, 1), fontSize: 26);
        rolePlusButtons[role] = plusBtn;

        RefreshRoleRow(role);
    }

    private static string GetFactionColorHex(FactionType faction)
    {
        return faction switch
        {
            FactionType.Monster => "#FF7B7B",   // Đỏ nhạt cho Quái vật
            FactionType.Villager => "#A5D6A7",  // Xanh lá dịu cho Dân làng
            FactionType.Neutral => "#FFD54F",   // Vàng cho Trung lập
            _ => "#E0C080"
        };
    }

    // ==========================================
    // LOGIC ĐIỀU CHỈNH VÀ KIỂM TRA SỐ LƯỢNG
    // ==========================================

    private void ChangeRoleAmount(RoleType role, int delta)
    {
        if (!isHostMode) return;

        int current = draftRoleAmounts.TryGetValue(role, out int val) ? val : 0;
        int next = current + delta;

        // Không cho phép số lượng âm
        if (next < 0) return;

        // Không cho vượt quá sức chứa tối đa của game (25)
        int total = GetTotalDraftRoles();
        if (delta > 0 && total >= 25)
        {
            SetWarningBanner("Tổng số lượng vai trò không thể vượt quá 25 người chơi!", false);
            return;
        }

        draftRoleAmounts[role] = next;
        RefreshRoleRow(role);
        RefreshSummaryAndValidation();
    }

    private int GetTotalDraftRoles()
    {
        return draftRoleAmounts.Values.Sum();
    }

    private void RefreshRoleRow(RoleType role)
    {
        int amount = draftRoleAmounts.TryGetValue(role, out int val) ? val : 0;
        if (roleAmountLabels.TryGetValue(role, out TMP_Text label))
        {
            label.text = amount.ToString();
        }

        if (roleNameLabels.TryGetValue(role, out TMP_Text nameLabel))
        {
            RoleInfoData info = RoleInfoDatabase.GetRoleInfo(role);
            string displayName = info != null ? info.DisplayName : role.ToString();
            string factionColor = GetFactionColorHex(info != null ? info.Faction : FactionType.Villager);
            string factionName = info != null ? info.FactionName : "";

            if (amount > 0)
            {
                nameLabel.text = $"<b>{displayName}</b>   <size=15><color={factionColor}>({factionName})</color></size>   <size=13><color=#35C759>● ĐÃ CHỌN</color></size>";
                nameLabel.color = new Color32(255, 246, 226, 255);
            }
            else
            {
                nameLabel.text = $"<color=#8E8E93><b>{displayName}</b>   <size=15>({factionName})</size>   <size=13>○ KHÔNG DÙNG</size></color>";
                nameLabel.color = new Color32(142, 142, 147, 255);
            }
        }

        if (roleMinusButtons.TryGetValue(role, out Button minusBtn))
        {
            minusBtn.gameObject.SetActive(isHostMode);
            minusBtn.interactable = isHostMode && amount > 0;
        }

        if (rolePlusButtons.TryGetValue(role, out Button plusBtn))
        {
            plusBtn.gameObject.SetActive(isHostMode);
            plusBtn.interactable = isHostMode && GetTotalDraftRoles() < 25;
        }
    }

    private void RefreshAllRows()
    {
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            RefreshRoleRow(role);
        }
    }

    private void RefreshSummaryAndValidation()
    {
        int totalRoles = GetTotalDraftRoles();
        int players = currentPlayerCount;

        if (statusSummaryText != null)
        {
            statusSummaryText.text = $"Tổng số vai trò đã chọn: <b><color=#FFF080>{totalRoles}</color></b>   |   Số người chơi trong phòng: <b><color=#80D0FF>{players}</color></b>";
        }

        if (totalRoles == players)
        {
            int wolves = GetWolfCount();
            int villagers = totalRoles - wolves;

            if (wolves == 0)
            {
                SetWarningBanner("⚠ Phải có ít nhất 1 vai trò thuộc phe Ma sói (Khuyển tinh) để bắt đầu!", false);
                if (saveButton != null) saveButton.interactable = false;
            }
            else if (villagers == 0)
            {
                SetWarningBanner("⚠ Phải có ít nhất 1 vai trò thuộc phe Dân làng để bắt đầu!", false);
                if (saveButton != null) saveButton.interactable = false;
            }
            else
            {
                SetWarningBanner($"✓ Cấu hình vai trò hợp lệ ({totalRoles}/{players} người). Sẵn sàng bắt đầu ván đấu!", true);
                if (saveButton != null) saveButton.interactable = isHostMode;
            }
        }
        else if (totalRoles < players)
        {
            int missing = players - totalRoles;
            SetWarningBanner($"⚠ Còn thiếu {missing} vai trò nữa mới đủ sĩ số phòng (Hiện tại: {totalRoles}/{players} người).", false);
            if (saveButton != null) saveButton.interactable = false;
        }
        else
        {
            int excess = totalRoles - players;
            SetWarningBanner($"⚠ Đang dư {excess} vai trò! Vui lòng bấm (−) để giảm bớt (Hiện tại: {totalRoles}/{players} người).", false);
            if (saveButton != null) saveButton.interactable = false;
        }
    }

    private void SetWarningBanner(string message, bool isValid)
    {
        if (validationText != null)
        {
            validationText.text = message;
            validationText.color = isValid ? new Color32(165, 235, 175, 255) : new Color32(255, 195, 160, 255);
        }

        if (validationBannerImage != null)
        {
            validationBannerImage.color = isValid
                ? new Color(0.11f, 0.28f, 0.14f, 1f)  // Nền xanh đậm dịu mắt khi hợp lệ
                : new Color(0.35f, 0.14f, 0.10f, 1f); // Nền đỏ nâu đậm cảnh báo khi chưa hợp lệ
        }
    }

    private int GetWolfCount()
    {
        int wolves = 0;
        if (draftRoleAmounts.TryGetValue(RoleType.DogSpirit, out int d)) wolves += d;
        if (draftRoleAmounts.TryGetValue(RoleType.WhiteHound, out int w)) wolves += w;
        if (draftRoleAmounts.TryGetValue(RoleType.SerpentSpirit, out int s)) wolves += s;
        if (draftRoleAmounts.TryGetValue(RoleType.Ogre, out int o)) wolves += o;
        return wolves;
    }

    private void OnDefaultClicked()
    {
        if (!isHostMode) return;

        var defaultDict = ThuongOnlineRoleConfigAdapter.GetDefaultRolesForPlayerCount(currentPlayerCount);
        draftRoleAmounts.Clear();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            draftRoleAmounts[role] = defaultDict.TryGetValue(role, out int amount) ? amount : 0;
        }

        RefreshAllRows();
        RefreshSummaryAndValidation();
    }

    private void OnSaveClicked()
    {
        if (!isHostMode) return;

        int totalRoles = GetTotalDraftRoles();
        if (totalRoles != currentPlayerCount)
        {
            SetWarningBanner($"Không thể lưu! Tổng số vai trò ({totalRoles}) phải bằng số người chơi ({currentPlayerCount}).", false);
            return;
        }

        if (GetWolfCount() == 0)
        {
            SetWarningBanner("Không thể lưu! Phải có ít nhất 1 vai trò thuộc phe Ma sói.", false);
            return;
        }

        onSaveCallback?.Invoke(new Dictionary<RoleType, int>(draftRoleAmounts));
        HideDialog();
    }

    private void OnCloseClicked()
    {
        HideDialog();
    }

    private void Update()
    {
        if (shadeRoot != null && shadeRoot.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HideDialog();
            }
        }
    }

    // ==========================================
    // UI BUILDER HELPERS
    // ==========================================

    private static GameObject CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = color;
        return go;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        Vector2 min,
        Vector2 max,
        int fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        FontStyle fontStyle = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = go.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = fontSize;
        tmp.enableAutoSizing = true;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        tmp.fontStyle = fontStyle == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;

        ThuongMedievalTheme.Active?.StyleText(tmp);
        return tmp;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 min,
        Vector2 max,
        Color bgColor,
        UnityEngine.Events.UnityAction action,
        int fontSize = 20,
        bool isProminent = false)
    {
        var panel = CreatePanel(name, parent, min, max, bgColor);
        var btn = panel.AddComponent<Button>();
        var img = panel.GetComponent<Image>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor = bgColor * 0.85f;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = colors;

        ThuongMedievalTheme.Active?.StylePanel(img, name);

        var text = CreateText("Label", panel.transform, label,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), fontSize, TextAlignmentOptions.Center, FontStyle.Bold);

        text.color = isProminent ? new Color32(255, 245, 175, 255) : new Color32(235, 220, 195, 255);

        return btn;
    }
}
