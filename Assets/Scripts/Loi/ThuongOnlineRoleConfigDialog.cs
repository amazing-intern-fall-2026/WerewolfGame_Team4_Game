using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện Modal tùy chỉnh vai trò và số lượng vai trò cho Thương Online (Lobby).
/// Hoạt động tương thích phong cách thiết kế Thương Online (Medieval Theme).
/// Phân quyền:
///   - Host: Tăng/giảm vai trò, Mặc định, Lưu, Đóng.
///   - Client: Chỉ xem cấu hình hiện tại, các nút chỉnh sửa bị vô hiệu hóa/ẩn.
/// </summary>
public class ThuongOnlineRoleConfigDialog : MonoBehaviour
{
    private static ThuongOnlineRoleConfigDialog instance;
    public static ThuongOnlineRoleConfigDialog Instance => instance;
    public static bool IsShowing => instance != null && instance.gameObject.activeSelf;

    private Canvas dialogCanvas;
    private GraphicRaycaster dialogRaycaster;
    private GameObject shadeRoot;
    private RectTransform contentContainer;
    private TMP_Text titleText;
    private TMP_Text statusSummaryText;
    private TMP_Text warningText;
    private Button defaultButton;
    private Button saveButton;
    private Button closeButton;

    private bool isHostMode = true;
    private int currentPlayerCount = 2;
    private Action<Dictionary<RoleType, int>> onSaveCallback;

    private readonly Dictionary<RoleType, int> draftRoleAmounts = new Dictionary<RoleType, int>();
    private readonly Dictionary<RoleType, TMP_Text> roleAmountLabels = new Dictionary<RoleType, TMP_Text>();
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

        if (defaultButton != null) defaultButton.gameObject.SetActive(isHost);
        if (saveButton != null) saveButton.gameObject.SetActive(isHost);

        RefreshAllRows();
        RefreshSummaryAndValidation();

        gameObject.SetActive(true);
        if (shadeRoot != null)
            shadeRoot.SetActive(true);
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

    private void BuildUI()
    {
        dialogCanvas = gameObject.AddComponent<Canvas>();
        dialogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dialogCanvas.sortingOrder = 25; // Trên Lobby Canvas (20)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;

        dialogRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        // 1. Shade / Backdrop
        shadeRoot = CreatePanel("Shade", transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.82f));
        var shadeImg = shadeRoot.GetComponent<Image>();
        shadeImg.raycastTarget = true;

        // 2. Main Window Panel
        var window = CreatePanel("Window", shadeRoot.transform, new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.95f), new Color(0.15f, 0.11f, 0.07f, 0.98f));
        ThuongMedievalTheme.Active?.StylePanel(window.GetComponent<Image>(), "Window");

        // 3. Header Title
        titleText = CreateText("Title", window.transform, "THIẾT LẬP VAI TRÒ VÀ SỐ LƯỢNG",
            new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.98f), 26, TextAlignmentOptions.Center, FontStyle.Bold);
        titleText.color = ThuongMedievalTheme.Cream;

        // 4. Status Summary Text
        statusSummaryText = CreateText("Summary", window.transform, "",
            new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.905f), 19, TextAlignmentOptions.Center);
        statusSummaryText.color = ThuongMedievalTheme.Cream;

        // 5. Warning / Validation Text
        warningText = CreateText("Warning", window.transform, "",
            new Vector2(0.04f, 0.805f), new Vector2(0.96f, 0.85f), 16, TextAlignmentOptions.Center);

        // 6. Scroll View Container
        var scrollRectGo = CreatePanel("RoleScrollView", window.transform, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.80f), new Color(0.08f, 0.06f, 0.04f, 0.5f));
        var scrollRect = scrollRectGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35f;

        var viewport = CreatePanel("Viewport", scrollRectGo.transform, Vector2.zero, Vector2.one, Color.clear);
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        var content = CreatePanel("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Color.clear);
        contentContainer = content.GetComponent<RectTransform>();
        contentContainer.pivot = new Vector2(0.5f, 1f);
        contentContainer.offsetMin = Vector2.zero;
        contentContainer.offsetMax = Vector2.zero;

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentContainer;

        // 7. Tạo danh sách các dòng Role
        BuildRoleRows();

        // 8. Bottom Buttons (MẶC ĐỊNH, LƯU, ĐÓNG)
        defaultButton = CreateButton("DefaultButton", window.transform, "MẶC ĐỊNH",
            new Vector2(0.06f, 0.035f), new Vector2(0.32f, 0.105f), OnDefaultClicked);

        saveButton = CreateButton("SaveButton", window.transform, "LƯU CẤU HÌNH",
            new Vector2(0.36f, 0.035f), new Vector2(0.64f, 0.105f), OnSaveClicked);

        closeButton = CreateButton("CloseButton", window.transform, "ĐÓNG",
            new Vector2(0.68f, 0.035f), new Vector2(0.94f, 0.105f), OnCloseClicked);
    }

    private void BuildRoleRows()
    {
        roleAmountLabels.Clear();
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
        var row = CreatePanel("RoleRow_" + role, contentContainer.transform, Vector2.zero, Vector2.one,
            index % 2 == 0 ? new Color(0.20f, 0.15f, 0.10f, 0.90f) : new Color(0.24f, 0.18f, 0.12f, 0.90f));
        ThuongMedievalTheme.Active?.StylePanel(row.GetComponent<Image>(), "RoleRow");

        var layoutElem = row.AddComponent<LayoutElement>();
        layoutElem.minHeight = 56f;
        layoutElem.preferredHeight = 56f;

        // Role Icon
        var iconObj = CreatePanel("Icon", row.transform, new Vector2(0.015f, 0.10f), new Vector2(0.075f, 0.90f), Color.white);
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

        // Role Name & Faction Text
        RoleInfoData info = RoleInfoDatabase.GetRoleInfo(role);
        string displayName = info != null ? info.DisplayName : role.ToString();
        string factionName = info != null ? info.FactionName : "";
        string roleFullLabel = $"{displayName}  <size=14><color=#E0C080>({factionName})</color></size>";

        var nameText = CreateText("Name", row.transform, roleFullLabel,
            new Vector2(0.085f, 0.10f), new Vector2(0.70f, 0.90f), 19, TextAlignmentOptions.MidlineLeft);
        nameText.color = ThuongMedievalTheme.Cream;

        // Minus Button
        var minusBtn = CreateButton("MinusBtn", row.transform, "−",
            new Vector2(0.72f, 0.14f), new Vector2(0.80f, 0.86f), () => ChangeRoleAmount(role, -1));
        roleMinusButtons[role] = minusBtn;

        // Amount Label
        var amountText = CreateText("Amount", row.transform, "0",
            new Vector2(0.81f, 0.10f), new Vector2(0.89f, 0.90f), 22, TextAlignmentOptions.Center, FontStyle.Bold);
        amountText.color = Color.white;
        roleAmountLabels[role] = amountText;

        // Plus Button
        var plusBtn = CreateButton("PlusBtn", row.transform, "+",
            new Vector2(0.90f, 0.14f), new Vector2(0.98f, 0.86f), () => ChangeRoleAmount(role, 1));
        rolePlusButtons[role] = plusBtn;
    }

    private void ChangeRoleAmount(RoleType role, int delta)
    {
        if (!isHostMode) return;

        int current = draftRoleAmounts.TryGetValue(role, out int val) ? val : 0;
        int next = current + delta;

        // Không cho phép số lượng âm
        if (next < 0) return;

        // Không cho tổng số lượng vượt quá sức chứa tối đa của game (25)
        int total = GetTotalDraftRoles();
        if (delta > 0 && total >= 25)
        {
            SetWarning("Tổng số lượng vai trò không thể vượt quá 25!");
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
            statusSummaryText.text = $"Tổng số vai trò đã chọn: <b><color=#FFE880>{totalRoles}</color></b>  |  Số người chơi trong phòng: <b><color=#A0E8FF>{players}</color></b>";
        }

        if (warningText != null)
        {
            if (totalRoles == players)
            {
                // Kiểm tra điều kiện có sói và dân
                int wolves = GetWolfCount();
                int villagers = totalRoles - wolves;

                if (wolves == 0)
                {
                    warningText.text = "<color=#FF6B6B>⚠ Phải có ít nhất 1 Ma sói (Khuyển tinh) để bắt đầu!</color>";
                    if (saveButton != null) saveButton.interactable = false;
                }
                else if (villagers == 0)
                {
                    warningText.text = "<color=#FF6B6B>⚠ Phải có ít nhất 1 Dân làng / Phe thiện để bắt đầu!</color>";
                    if (saveButton != null) saveButton.interactable = false;
                }
                else
                {
                    warningText.text = "<color=#35C759>✓ Cấu hình vai trò đã cân bằng và sẵn sàng lưu.</color>";
                    if (saveButton != null) saveButton.interactable = isHostMode;
                }
            }
            else if (totalRoles < players)
            {
                warningText.text = $"<color=#FFA726>⚠ Còn thiếu {players - totalRoles} vai trò nữa mới đủ số người chơi ({totalRoles}/{players}).</color>";
                if (saveButton != null) saveButton.interactable = false;
            }
            else
            {
                warningText.text = $"<color=#FF5252>⚠ Đang dư {totalRoles - players} vai trò! Vui lòng giảm bớt vai trò ({totalRoles}/{players}).</color>";
                if (saveButton != null) saveButton.interactable = false;
            }
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

    private void SetWarning(string message)
    {
        if (warningText != null)
        {
            warningText.text = $"<color=#FF5252>{message}</color>";
        }
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
            SetWarning($"Không thể lưu! Tổng số vai trò ({totalRoles}) phải bằng số người chơi ({currentPlayerCount}).");
            return;
        }

        if (GetWolfCount() == 0)
        {
            SetWarning("Không thể lưu! Phải có ít nhất 1 vai trò thuộc phe Ma sói.");
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
        tmp.fontSizeMin = 10;
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
        UnityEngine.Events.UnityAction action)
    {
        var panel = CreatePanel(name, parent, min, max, new Color(0.42f, 0.28f, 0.16f, 1f));
        var btn = panel.AddComponent<Button>();
        btn.targetGraphic = panel.GetComponent<Image>();
        btn.onClick.AddListener(action);

        ThuongMedievalTheme.Active?.StylePanel(panel.GetComponent<Image>(), name);

        var text = CreateText("Label", panel.transform, label,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), 19, TextAlignmentOptions.Center, FontStyle.Bold);
        text.color = ThuongMedievalTheme.Cream;

        return btn;
    }
}
