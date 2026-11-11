using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hộp thoại modal xác nhận trở về Menu chính (Return to Main Menu Confirmation Dialog).
/// Hiển thị thông báo xác nhận:
/// - Tiêu đề: "XÁC NHẬN"
/// - Nội dung: "Bạn có chắc chắn muốn trở về Menu chính không?"
///   (Nếu là Host: cảnh báo phòng sẽ kết thúc đối với tất cả người chơi)
/// - Lựa chọn 1: "Tiếp tục chơi" (Đóng hộp thoại, giữ nguyên trận đấu)
/// - Lựa chọn 2: "Trở về Menu" (Tiến hành quy trình thoát trận)
/// </summary>
public class NetworkReturnToMenuConfirmationDialog : MonoBehaviour
{
    private static NetworkReturnToMenuConfirmationDialog instance;
    public static NetworkReturnToMenuConfirmationDialog Instance => instance;

    private GameObject dialogRoot;
    private TMP_Text messageText;
    private Action pendingConfirmAction;
    private Action pendingCancelAction;

    public static bool IsShowing => instance != null && instance.dialogRoot != null && instance.dialogRoot.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (instance != null) return;

        var existing = FindAnyObjectByType<NetworkReturnToMenuConfirmationDialog>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[NetworkReturnToMenuConfirmationDialog]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NetworkReturnToMenuConfirmationDialog>();
    }

    public static void ShowDialog(bool isHost, Action onConfirm, Action onCancel = null)
    {
        EnsureInstance();
        instance.InternalShow(isHost, onConfirm, onCancel);
    }

    public static void HideDialog()
    {
        if (instance != null)
        {
            instance.InternalHide();
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Phím ESC đóng hộp thoại nếu đang mở
        if (dialogRoot != null && dialogRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelClicked();
        }
    }

    public void InternalShow(bool isHost, Action onConfirm, Action onCancel = null)
    {
        pendingConfirmAction = onConfirm;
        pendingCancelAction = onCancel;

        if (dialogRoot == null)
        {
            BuildDialogUI();
        }

        if (messageText != null)
        {
            if (isHost)
            {
                messageText.text = "Bạn là <color=#FFEAA7><b>Chủ phòng</b></color>.\nNếu bạn rời trận, phiên chơi sẽ kết thúc đối với tất cả người chơi.\n\n<b>Bạn có chắc chắn muốn trở về Menu chính không?</b>";
            }
            else
            {
                messageText.text = "Bạn có chắc chắn muốn rời trận và\n<b>trở về Menu chính</b> không?";
            }
        }

        dialogRoot.SetActive(true);
        dialogRoot.transform.SetAsLastSibling();
    }

    public void InternalHide()
    {
        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }
    }

    private void OnConfirmClicked()
    {
        InternalHide();
        var callback = pendingConfirmAction;
        pendingConfirmAction = null;
        pendingCancelAction = null;
        callback?.Invoke();
    }

    private void OnCancelClicked()
    {
        InternalHide();
        var callback = pendingCancelAction;
        pendingConfirmAction = null;
        pendingCancelAction = null;
        callback?.Invoke();
    }

    private void BuildDialogUI()
    {
        // 1. Root Canvas
        dialogRoot = new GameObject("ReturnToMenuConfirmationDialog", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogRoot.transform.SetParent(transform, false);

        var canvas = dialogRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9999; // Luôn hiển thị trên cùng

        var scaler = dialogRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = GetDialogFont();

        // 2. Backdrop làm tối nền và chặn click xuyên màn hình
        var backdropObj = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdropObj.transform.SetParent(dialogRoot.transform, false);
        var bgRt = backdropObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = backdropObj.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.82f);
        bgImg.raycastTarget = true;

        // 3. Khung Dialog chính giữa màn hình
        var panelObj = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObj.transform.SetParent(dialogRoot.transform, false);
        var panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(640f, 380f);
        var panelImg = panelObj.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

        // 4. Thanh Header tiêu đề
        var headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        headerObj.transform.SetParent(panelObj.transform, false);
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 60f);
        var headerImg = headerObj.GetComponent<Image>();
        headerImg.color = new Color(0.18f, 0.23f, 0.32f, 1f);

        var titleTmp = CreateTMP(headerObj.transform, "TitleText", "XÁC NHẬN", 28, FontStyles.Bold, new Color(1f, 0.92f, 0.65f), TextAlignmentOptions.Center, font);
        var titleRt = titleTmp.rectTransform;
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // 5. Nội dung thông báo
        messageText = CreateTMP(panelObj.transform, "MessageText", "", 21, FontStyles.Normal, Color.white, TextAlignmentOptions.Center, font);
        var msgRt = messageText.rectTransform;
        msgRt.anchorMin = new Vector2(0.5f, 0.5f);
        msgRt.anchorMax = new Vector2(0.5f, 0.5f);
        msgRt.pivot = new Vector2(0.5f, 0.5f);
        msgRt.anchoredPosition = new Vector2(0f, 18f);
        msgRt.sizeDelta = new Vector2(560f, 160f);
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.richText = true;

        // 6. Nút 1: "Tiếp tục chơi" (Màu xanh đá / slate)
        CreateDialogButton(panelObj.transform, "CancelButton", "Tiếp tục chơi", new Vector2(-145f, -135f), new Vector2(240f, 60f), new Color(0.24f, 0.32f, 0.44f, 1f), OnCancelClicked, font);

        // 7. Nút 2: "Trở về Menu" (Màu đỏ tươi / crimson)
        CreateDialogButton(panelObj.transform, "ConfirmButton", "Trở về Menu", new Vector2(145f, -135f), new Vector2(240f, 60f), new Color(0.82f, 0.22f, 0.22f, 1f), OnConfirmClicked, font);
    }

    private static void CreateDialogButton(Transform parent, string name, string labelText, Vector2 pos, Vector2 size, Color btnColor, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
    {
        var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        var rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = btnObj.GetComponent<Image>();
        img.color = btnColor;

        var btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var txt = CreateTMP(btnObj.transform, "Label", labelText, 21, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
        var txtRt = txt.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;

        if (font != null)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
        }

        return tmp;
    }

    private static TMP_FontAsset GetDialogFont()
    {
        TMP_FontAsset font = null;
        try
        {
            font = AuthUI.GetVietnameseFont();
        }
        catch { }

        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (font == null)
        {
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (allFonts != null && allFonts.Length > 0)
            {
                font = allFonts[0];
            }
        }

        return font;
    }
}
