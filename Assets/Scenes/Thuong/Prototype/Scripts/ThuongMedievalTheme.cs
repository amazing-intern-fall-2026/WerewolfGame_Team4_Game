using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Optional theme: only the Thuong gameplay scene installs this component.
[DefaultExecutionOrder(2000)]
public sealed class ThuongMedievalTheme : MonoBehaviour
{
    public static ThuongMedievalTheme Active { get; private set; }
    public Sprite paper;
    public Sprite wood;
    public Sprite defaultPortrait;
    public static readonly Color Ink = new Color32(55, 35, 23, 255);
    public static readonly Color Cream = new Color32(255, 235, 191, 255);
    private GameObject creditsRoot;
    private void Awake() => Active = this;
    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (creditsRoot != null) Destroy(creditsRoot);
    }
    private void Start() { ApplyHUD(); CreateCredits(); }

    public void StylePanel(Image image, string panelName)
    {
        if (image == null || image.color.a < .05f || panelName.Contains("Accent") || panelName == "Fill") return;
        if (panelName.Contains("Window") || panelName == "Role Reveal" || panelName == "ResultPanel")
        {
            image.sprite = null;
            image.color = new Color(.09f, .065f, .04f, .90f);
            return;
        }
        bool wooden = image.GetComponent<Button>() != null || panelName == "TopBar" || panelName == "PhaseCue" ||
                      panelName == "AliveBadge" || panelName.Contains("Slider") || panelName == "Icon frame" || panelName == "Portrait frame";
        image.sprite = wooden ? wood : paper;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = 1;
        if (image.TryGetComponent(out Button button))
        {
            var colors = button.colors;
            colors.highlightedColor = new Color32(255, 223, 158, 255);
            colors.pressedColor = new Color32(202, 158, 102, 255);
            colors.disabledColor = new Color32(133, 120, 98, 255);
            button.colors = colors;
        }
    }

    public void StyleText(TMP_Text text)
    {
        if (text == null) return;
        bool wooden = true;
        for (Transform p = text.transform.parent; p != null; p = p.parent)
        {
            var image = p.GetComponent<Image>();
            if (image == null || image.color.a < .05f) continue;
            wooden = image.sprite == wood || image.sprite == null;
            break;
        }
        text.color = wooden ? Cream : Ink;
        text.margin = new Vector4(8, 0, 8, 0);
    }

    public void ApplyHUD()
    {
        foreach (Image image in GetComponentsInChildren<Image>(true)) StylePanel(image, image.name);
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) StyleText(text);
        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            var panel = text.GetComponentInParent<Image>();
            text.color = panel != null && panel.sprite == paper ? Ink : Cream;
        }
        var hud = GetComponent<GameHUD>();
        if (hud != null && hud.taskListText != null) hud.taskListText.lineSpacing = 8;
        if (hud != null)
        {
            hud.transform.Find("TopBar/Title").GetComponent<TMP_Text>().text = "MA SÓI  /  LÀNG TRUNG CỔ";
            if (hud.progressSlider?.fillRect != null) hud.progressSlider.fillRect.GetComponent<Image>().color = new Color32(126, 158, 77, 255);
            if (hud.dailyProgressSlider?.fillRect != null) hud.dailyProgressSlider.fillRect.GetComponent<Image>().color = new Color32(203, 147, 62, 255);
        }
        var instructions = transform.Find("TaskPanel/Instructions")?.GetComponent<TMP_Text>();
        if (instructions != null)
            instructions.text = "WASD / MŨI TÊN  Di chuyển\nE  Thực hiện nhiệm vụ\nTab  Xem toàn làng\nF1–F12 / PgUp / PgDn  Đổi người\nR  Chơi lại";
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("Accent")) t.gameObject.SetActive(false);
    }

    private void CreateCredits()
    {
        creditsRoot = new GameObject("Medieval asset credits", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = creditsRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
        var scaler = creditsRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var window = CreditPanel("Credits Window", creditsRoot.transform, Vector2.zero, Vector2.one);
        var card = CreditPanel("Credits Card", window, new Vector2(.15f, .12f), new Vector2(.85f, .88f));
        CreditText("Title", card, "NGUỒN ASSET", new Vector2(.05f, .88f), new Vector2(.75f, .97f), 26);
        var close = CreditPanel("Close", card, new Vector2(.81f, .89f), new Vector2(.95f, .97f));
        var closeButton = close.gameObject.AddComponent<Button>(); closeButton.targetGraphic = close.GetComponent<Image>();
        StylePanel(close.GetComponent<Image>(), close.name);
        CreditText("Text", close, "ĐÓNG", Vector2.zero, Vector2.one, 18);
        closeButton.onClick.AddListener(() => window.gameObject.SetActive(false));
        var viewport = CreditPanel("Credits viewport", card, new Vector2(.05f, .06f), new Vector2(.95f, .85f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = CreditText("Credits", viewport, Resources.Load<TextAsset>("MedievalCredits")?.text ?? "", Vector2.up, Vector2.one, 17);
        text.richText = false; text.alignment = TextAlignmentOptions.TopLeft;
        text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(.5f, 1);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = text.rectTransform;
        scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;
        window.gameObject.SetActive(false);
        var open = CreditPanel("Asset credits button", transform, new Vector2(.865f, .014f), new Vector2(.985f, .071f));
        var openButton = open.gameObject.AddComponent<Button>(); openButton.targetGraphic = open.GetComponent<Image>();
        StylePanel(open.GetComponent<Image>(), open.name);
        CreditText("Text", open, "NGUỒN ASSET", Vector2.zero, Vector2.one, 16);
        openButton.onClick.AddListener(() => { window.gameObject.SetActive(true); scroll.verticalNormalizedPosition = 1; });
        var footer = transform.Find("Footer") as RectTransform;
        if (footer != null) footer.anchorMax = new Vector2(.85f, footer.anchorMax.y);
    }

    private RectTransform CreditPanel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        StylePanel(go.GetComponent<Image>(), name); return rect;
    }
    private TMP_Text CreditText(string name, Transform parent, string value, Vector2 min, Vector2 max, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); var text = go.GetComponent<TMP_Text>();
        var rect = text.rectTransform; rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero; text.font = GetComponent<GameHUD>().phaseText.font;
        text.fontSize = size; text.text = value; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; StyleText(text); return text;
    }
}
