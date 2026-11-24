using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Giao diện kỹ năng local của prototype Thương. Toàn bộ Canvas được tạo khi chạy.
public class RoleAbilityUI : MonoBehaviour
{
    public int localPlayerID;

    private Canvas abilityCanvas;
    private GraphicRaycaster abilityRaycaster;
    private TMP_FontAsset font;
    private GameObject window;
    private GameObject abilityView;
    private GameObject targetView;
    private GameObject resultView;
    private GameObject revealWindow;
    private Button openButton;
    private Button useButton;
    private Button shamanGoodCharmButton;
    private Button shamanBadCharmButton;
    private Button hunterTrapButton;
    private TMP_Text openLabel;
    private TMP_Text playerLabel;
    private TMP_Text roleLabel;
    private TMP_Text factionLabel;
    private TMP_Text skillLabel;
    private TMP_Text descriptionLabel;
    private TMP_Text skillDescriptionLabel;
    private TMP_Text restrictionsLabel;
    private bool previousHunterPending;
    private bool previousDogSpiritDecision;
    private int shownOnlineInformation;
    private TMP_Text statusLabel;
    private TMP_Text targetFeedbackLabel;
    private TMP_Text targetTitleLabel;
    private TMP_Text resultFeedbackLabel;
    private TMP_Text revealRoleLabel;
    private TMP_Text revealFactionLabel;
    private TMP_Text revealDescriptionLabel;
    private TMP_Text revealTimerLabel;
    private Image revealPortrait;
    private Image characterPortraitImage;
    private Sprite defaultPortrait;
    private RectTransform targetContent;
    private readonly Dictionary<int, Button> targetButtons = new();
    private readonly Dictionary<int, TMP_Text> monsterVoteBadgeLabels = new();
    private readonly Dictionary<int, Image> monsterVoteAvatarImages = new();
    private Thuong.Online.ThuongOnlineSession observedMonsterSession;
    private NightManager observedMonsterNight;
    private bool monsterVoteUiDirty;
    private bool initialized;
    private bool hasShownRole;
    private RoleType shownRole;
    private ThuongRoleDefinition shownDefinition;
    private int shownPlayerID = -1;
    private bool shownAwakened;
    private FactionType shownFaction;
    private AbilitySystem observedAbilitySystem;
    private GameState previousState;
    private string actionFeedback;
    private string targetFailureFeedback;
    private bool selectingHunterTrap;
    private int firstLoverTargetID = -1;
    private bool onlineGoodCharm;

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }

    public void Initialize(TMP_FontAsset hudFont, int playerID)
    {
        if (initialized) return;
        localPlayerID = playerID;
        font = hudFont;
        BuildUI();
        initialized = true;
    }

    private void Start()
    {
        if (!initialized) Initialize(null, localPlayerID);
    }

    private void OnDestroy()
    {
        if (observedAbilitySystem != null) observedAbilitySystem.AbilityResolved -= HandleAbilityResolved;
        if (observedMonsterSession != null) observedMonsterSession.MonsterVoteUpdated -= HandleMonsterVoteUpdated;
        if (observedMonsterNight != null) observedMonsterNight.MonsterVoteStateChanged -= HandleMonsterVoteUpdated;
        if (abilityCanvas != null) Destroy(abilityCanvas.gameObject);
    }

    private void HandleMonsterVoteUpdated()
    {
        monsterVoteUiDirty = true;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 min,
        Vector2 max, string color, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = Hex(color);
        image.raycastTarget = raycast;
        ThuongMedievalTheme.Active?.StylePanel(image, name);
        return rect;
    }

    private TMP_Text Label(string name, Transform parent, string value, Vector2 min,
        Vector2 max, int size, string color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<TMP_Text>();
        if (font != null) label.font = font;
        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = 13;
        label.color = Hex(color);
        label.alignment = alignment;
        label.raycastTarget = false;
        ThuongMedievalTheme.Active?.StyleText(label);
        return label;
    }

    private Button ActionButton(string name, Transform parent, string value,
        Vector2 min, Vector2 max, string color, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(name, parent, min, max, color, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        ThuongMedievalTheme.Active?.StylePanel(rect.GetComponent<Image>(), name);
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Hex("#B8F3EA");
        colors.pressedColor = Hex("#79DED1");
        colors.selectedColor = Color.white;
        colors.disabledColor = Hex("#657B82");
        button.colors = colors;
        if (ThuongMedievalTheme.Active != null)
        {
            colors.highlightedColor = new Color32(255, 223, 158, 255);
            colors.pressedColor = new Color32(202, 158, 102, 255);
            colors.disabledColor = new Color32(133, 120, 98, 255);
            button.colors = colors;
        }
        button.onClick.AddListener(action);
        Label("Text", rect, value, new Vector2(.03f, .05f),
            new Vector2(.97f, .95f), 20, "#F4FAF8", TextAlignmentOptions.Center);
        return button;
    }

    private static void AddRoleIcon(Transform parent, string name, Vector2 min,
        Vector2 max, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : Hex("#79DED1");
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void BuildUI()
    {
        Sprite roleIcon = ThuongMedievalTheme.Active != null
            ? ThuongMedievalTheme.Active.defaultPortrait : Resources.Load<Sprite>("RoleAbilityIcon");
        defaultPortrait = roleIcon;
        if (roleIcon == null)
            Debug.LogWarning("[Role Ability] Không tìm thấy sprite Resources/RoleAbilityIcon.");

        var root = new GameObject("Role Ability Canvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        abilityCanvas = root.GetComponent<Canvas>();
        abilityRaycaster = root.GetComponent<GraphicRaycaster>();
        abilityCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        abilityCanvas.sortingOrder = 30;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;

        openButton = ActionButton("Open Role Ability", root.transform, "ROLE / KỸ NĂNG",
            new Vector2(.805f, .805f), new Vector2(.985f, .865f), "#28716F", () => ShowWindow(true));
        openLabel = openButton.GetComponentInChildren<TMP_Text>();
        openLabel.rectTransform.anchorMin = new Vector2(.24f, .05f);
        openLabel.rectTransform.anchorMax = new Vector2(.97f, .95f);
        openLabel.alignment = TextAlignmentOptions.MidlineLeft;
        AddRoleIcon(openButton.transform, "Role icon on button",
            new Vector2(.035f, .12f), new Vector2(.205f, .88f), roleIcon);

        window = Panel("Role Ability Window", root.transform, Vector2.zero,
            Vector2.one, "#07151BD9", true).gameObject;
        var card = Panel("Card", window.transform, new Vector2(.10f, .07f),
            new Vector2(.90f, .93f), "#142D38", true);
        Label("Title", card, "ROLE ABILITY", new Vector2(.04f, .91f),
            new Vector2(.7f, .98f), 30, "#EBCB83");
        ActionButton("Close", card, "ĐÓNG", new Vector2(.83f, .90f),
            new Vector2(.96f, .98f), "#425A63", () => ShowWindow(false));

        var left = Panel("Character", card, new Vector2(.04f, .03f),
            new Vector2(.35f, .89f), "#1B3B45");
        Label("Character heading", left, "NHÂN VẬT", new Vector2(.05f, .92f),
            new Vector2(.95f, .98f), 22, "#79DED1", TextAlignmentOptions.Center);
        var iconFrame = Panel("Icon frame", left, new Vector2(.06f, .14f),
            new Vector2(.94f, .91f), "#254B54");

        var iconObj = new GameObject("Role icon", typeof(RectTransform), typeof(Image));
        var iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.SetParent(iconFrame, false);
        iconRt.anchorMin = new Vector2(0.04f, 0.04f);
        iconRt.anchorMax = new Vector2(0.96f, 0.96f);
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        characterPortraitImage = iconObj.GetComponent<Image>();
        characterPortraitImage.sprite = defaultPortrait;
        characterPortraitImage.preserveAspect = true;
        characterPortraitImage.raycastTarget = false;

        playerLabel = Label("Player ID", left, "Đang chờ người chơi", new Vector2(.04f, .02f),
            new Vector2(.96f, .13f), 20, "#F4FAF8", TextAlignmentOptions.Center);

        Label("Arrow", card, ">", new Vector2(.352f, .42f),
            new Vector2(.372f, .58f), 36, "#EBCB83", TextAlignmentOptions.Center);
        var right = Panel("Ability", card, new Vector2(.375f, .03f),
            new Vector2(.96f, .89f), "#1B3B45");
        abilityView = Panel("Ability details", right, Vector2.zero, Vector2.one,
            "#00000000").gameObject;

        roleLabel = Label("Role", abilityView.transform, "ROLE", new Vector2(.04f, .90f),
            new Vector2(.96f, .98f), 28, "#79DED1");
        factionLabel = Label("Faction", abilityView.transform, "PHE", new Vector2(.04f, .83f),
            new Vector2(.96f, .90f), 20, "#EBCB83");

        var infoBox = Panel("Info box", abilityView.transform, new Vector2(.04f, .21f),
            new Vector2(.96f, .82f), "#14303A", true);
        descriptionLabel = Label("Description", infoBox, "", new Vector2(.03f, .70f),
            new Vector2(.97f, .98f), 18, "#D7E7E7");
        skillLabel = Label("Skill", infoBox, "KỸ NĂNG", new Vector2(.03f, .50f),
            new Vector2(.97f, .69f), 19, "#EBCB83");
        skillDescriptionLabel = Label("Skill description", infoBox, "", new Vector2(.03f, .26f),
            new Vector2(.97f, .49f), 17, "#F4FAF8");
        restrictionsLabel = Label("Restrictions", infoBox, "", new Vector2(.03f, .02f),
            new Vector2(.97f, .25f), 16, "#FFAA44");

        statusLabel = Label("Status", abilityView.transform, "", new Vector2(.04f, .14f),
            new Vector2(.96f, .20f), 16, "#B8D0D1");

        useButton = ActionButton("Use skill", abilityView.transform, "CHỌN MỤC TIÊU",
            new Vector2(.04f, .03f), new Vector2(.96f, .13f), "#28716F", OpenTargets);
        shamanGoodCharmButton = ActionButton("Shaman good charm", abilityView.transform,
            "BÙA LỢI", new Vector2(.04f, .03f), new Vector2(.48f, .13f),
            "#28716F", () => OpenShamanTargets(true));
        shamanBadCharmButton = ActionButton("Shaman bad charm", abilityView.transform,
            "BÙA HẠI", new Vector2(.52f, .03f), new Vector2(.96f, .13f),
            "#7D4548", () => OpenShamanTargets(false));
        shamanGoodCharmButton.gameObject.SetActive(false);
        shamanBadCharmButton.gameObject.SetActive(false);
        hunterTrapButton = ActionButton("Hunter trap", abilityView.transform,
            "ĐẶT BẪY", new Vector2(.04f, .03f), new Vector2(.96f, .13f),
            "#7D4548", OpenHunterTrapTargets);
        hunterTrapButton.gameObject.SetActive(false);

        targetView = Panel("Target selection", right, Vector2.zero, Vector2.one,
            "#00000000").gameObject;
        targetTitleLabel = Label("Target title", targetView.transform, "CHỌN MỤC TIÊU", new Vector2(.06f, .86f),
            new Vector2(.95f, .98f), 25, "#EBCB83");
        targetFeedbackLabel = Label("Target feedback", targetView.transform, "", new Vector2(.06f, .17f),
            new Vector2(.95f, .25f), 16, "#B8D0D1");

        var viewport = Panel("Target viewport", targetView.transform,
            new Vector2(.06f, .26f), new Vector2(.95f, .84f), "#14303A", true);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Targets", typeof(RectTransform),
            typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        targetContent = content.GetComponent<RectTransform>();
        targetContent.SetParent(viewport, false);
        targetContent.anchorMin = new Vector2(0, 1);
        targetContent.anchorMax = Vector2.one;
        targetContent.pivot = new Vector2(.5f, 1);
        targetContent.offsetMin = targetContent.offsetMax = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(214, 44);
        grid.spacing = new Vector2(8, 8);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = targetContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        ActionButton("Cancel", targetView.transform, "HỦY / QUAY LẠI",
            new Vector2(.06f, .04f), new Vector2(.95f, .16f), "#425A63", CancelTargets);

        resultView = Panel("Action result", right, Vector2.zero, Vector2.one,
            "#00000000").gameObject;
        Label("Result title", resultView.transform, "ĐÃ THỰC HIỆN KỸ NĂNG",
            new Vector2(.06f, .78f), new Vector2(.95f, .94f), 27, "#79DED1");
        resultFeedbackLabel = Label("Result message", resultView.transform, "",
            new Vector2(.06f, .30f), new Vector2(.95f, .75f), 24, "#F4FAF8");
        ActionButton("Result back", resultView.transform, "QUAY LẠI",
            new Vector2(.06f, .04f), new Vector2(.95f, .17f), "#28716F", CancelTargets);
        targetView.SetActive(false);
        resultView.SetActive(false);
        window.SetActive(false);
        BuildRoleRevealUI();
    }

    private void BuildRoleRevealUI()
    {
        // Keep this as the last Canvas child so it covers every gameplay control.
        revealWindow = Panel("Role Reveal", abilityCanvas.transform, Vector2.zero,
            Vector2.one, "#07151BEF", true).gameObject;
        var card = Panel("Role Card", revealWindow.transform,
            new Vector2(.15f, .18f), new Vector2(.85f, .82f), "#142D38", true);
        Label("Card title", card, "THẺ VAI TRÒ CỦA BẠN",
            new Vector2(.04f, .86f), new Vector2(.70f, .97f), 32, "#EBCB83");
        revealTimerLabel = Label("Countdown", card, Thuong.Online.ThuongOnlineSession.Active != null ? "Không giới hạn thời gian" : "Bắt đầu sau 25 giây",
            new Vector2(.72f, .87f), new Vector2(.96f, .96f), 19,
            "#B8D0D1", TextAlignmentOptions.Right);

        var left = Panel("Role portrait and name", card,
            new Vector2(.04f, .10f), new Vector2(.34f, .82f), "#1B3B45");
        var portraitFrame = Panel("Portrait frame", left,
            new Vector2(.13f, .27f), new Vector2(.87f, .91f), "#254B54");
        var portraitObject = new GameObject("Role portrait PNG", typeof(RectTransform), typeof(Image));
        var portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.SetParent(portraitFrame, false);
        portraitRect.anchorMin = new Vector2(.05f, .05f);
        portraitRect.anchorMax = new Vector2(.95f, .95f);
        portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
        revealPortrait = portraitObject.GetComponent<Image>();
        revealPortrait.sprite = defaultPortrait;
        revealPortrait.preserveAspect = true;
        revealPortrait.raycastTarget = false;
        revealRoleLabel = Label("Role name", left, "ĐANG PHÂN VAI",
            new Vector2(.05f, .13f), new Vector2(.95f, .27f), 25,
            "#F4FAF8", TextAlignmentOptions.Center);
        revealFactionLabel = Label("Faction", left, "",
            new Vector2(.05f, .03f), new Vector2(.95f, .13f), 18,
            "#79DED1", TextAlignmentOptions.Center);

        var right = Panel("Role description", card,
            new Vector2(.37f, .10f), new Vector2(.96f, .82f), "#1B3B45");
        Label("Description heading", right, "MÔ TẢ ROLE",
            new Vector2(.05f, .85f), new Vector2(.95f, .96f), 25, "#79DED1");
        revealDescriptionLabel = Label("Description", right, "",
            new Vector2(.05f, .25f), new Vector2(.95f, .82f), 22,
            "#EAF7F5", TextAlignmentOptions.TopLeft);
        ActionButton("OK", right, Thuong.Online.ThuongOnlineSession.Active != null ? "OK / ĐÃ ĐỌC" : "OK  /  BẮT ĐẦU",
            new Vector2(.70f, .05f), new Vector2(.95f, .19f), "#28716F",
            () =>
            {
                var online = Thuong.Online.ThuongOnlineSession.Active;
                if (online != null)
                {
                    online.ReadLocalRole();
                }
                else GameRoleManager.Instance?.SkipRoleReveal();
            });
        revealWindow.SetActive(false);
    }

    private static bool HasActiveAbility(RoleType role)
    {
        return role == RoleType.DogSpirit || role == RoleType.Seer ||
               role == RoleType.VillageGuardian || role == RoleType.SerpentSpirit ||
             role == RoleType.Ogre || role == RoleType.WhiteHound ||
             role == RoleType.Shaman || role == RoleType.Cursed ||
                             role == RoleType.FoxSpirit || role == RoleType.Killer ||
                             role == RoleType.WeaverOfFate || role == RoleType.Brat;
    }

    private static string RoleName(RoleType role)
    {
        switch (role)
        {
            case RoleType.DogSpirit: return "DOG SPIRIT";
            case RoleType.SerpentSpirit: return "XÀ TINH";
            case RoleType.Ogre: return "QUỶ KHỔNG LỒ";
            case RoleType.Seer: return "TIÊN TRI";
            case RoleType.VillageGuardian: return "BẢO VỆ LÀNG";
            case RoleType.Villager: return "DÂN LÀNG";
            case RoleType.Mayor: return "TRƯỞNG LÀNG";
            case RoleType.Shaman: return "PHÁP SƯ";
            case RoleType.WhiteHound: return "SÓI TRẮNG";
            case RoleType.Idiot: return "KẺ NGỐC";
            case RoleType.Hunter: return "THỢ SĂN";
            case RoleType.WeaverOfFate: return "DỆT DUYÊN";
            case RoleType.Cursed: return "KẺ NGUYỀN";
            case RoleType.Brat: return "ĐỨA TRẺ";
            case RoleType.Madman: return "KẺ ĐIÊN";
            case RoleType.FoxSpirit: return "HỒ LY";
            case RoleType.Killer: return "SÁT NHÂN";
            default: return role.ToString();
        }
    }

    private static string RoleDescription(RoleType role)
    {
        switch (role)
        {
            case RoleType.DogSpirit:
                return "Bạn thuộc phe Ma Sói. Mỗi đêm, chọn một người chơi còn sống khác mình để tấn công. Ban ngày, tham gia thảo luận và bỏ phiếu để bảo vệ phe của bạn.";
            case RoleType.Villager:
                return "Bạn thuộc phe Dân Làng và không có kỹ năng chủ động ban đêm. Hãy hoàn thành nhiệm vụ trong ngày, thảo luận để tìm Ma Sói và bỏ phiếu. Đạt 100% tiến độ chung để dân làng chiến thắng.";
            case RoleType.Seer:
                return "Bạn thuộc phe Dân Làng. Mỗi đêm, chọn một người chơi còn sống để kiểm tra họ có phải Dog Spirit hay không. Kết quả không tiết lộ vai trò cụ thể; hãy dùng thông tin đó khi thảo luận và bỏ phiếu.";
            case RoleType.VillageGuardian:
                return "Bạn thuộc phe Dân Làng. Mỗi đêm, chọn một người chơi còn sống để bảo vệ khỏi đòn tấn công của Ma Sói. Bạn không thể bảo vệ cùng một người trong hai đêm liên tiếp.";
            case RoleType.Mayor:
                return "Bạn thuộc phe Dân Làng. Lá phiếu của bạn có sức nặng gấp đôi khi bỏ phiếu. Hãy hoàn thành nhiệm vụ ban ngày và dùng quyền bỏ phiếu để giúp dân làng tìm Ma Sói.";
            case RoleType.SerpentSpirit:
                return "Bạn thuộc phe Ma Sói. Từ đêm thứ ba, có thể chọn một mục tiêu để tấn công.";
            case RoleType.Ogre:
                return "Bạn thuộc phe Ma Sói và có thể chọn mục tiêu tấn công vào ban đêm. Ban ngày, bạn vẫn tham gia thảo luận và bỏ phiếu.";
            case RoleType.Shaman:
                return StatusEffectSystem.Instance != null
                    ? "Bạn thuộc phe Dân Làng. Bùa lợi bảo vệ trong đêm; bùa hại khóa kỹ năng của mục tiêu đến lúc thảo luận. Mỗi loại bùa dùng một lần mỗi ván."
                    : "Bạn thuộc phe Dân Làng. Mỗi đêm chọn một người chơi để dùng bùa lợi bảo vệ hoặc bùa hại khiến họ im lặng. Mỗi loại bùa dùng một lần.";
            case RoleType.WhiteHound:
                return "Bạn bắt đầu ở phe Dân Làng. Nếu bị Dog Spirit tấn công, bạn thức tỉnh thành Bạch Khuyển và có thể chọn mục tiêu tấn công vào các đêm sau.";
            case RoleType.Idiot:
                return "Bạn thuộc phe Dân Làng. Nếu bị loại bởi bỏ phiếu, bạn sẽ sống sót nhờ khả năng đặc biệt. Bạn vẫn có thể hoàn thành nhiệm vụ và tham gia thảo luận.";
            case RoleType.Hunter:
                return "Bạn thuộc phe Dân Làng. Khi chết, chọn một người trong lúc thảo luận để đặt bẫy; mục tiêu chết sau lượt bỏ phiếu kế tiếp.";
            case RoleType.WeaverOfFate:
                return "Bạn thuộc phe Dân Làng. Trong đêm, chọn hai người chơi còn sống để ghép đôi. Kỹ năng dùng một lần mỗi ván.";
            case RoleType.Cursed:
                return StatusEffectSystem.Instance != null
                    ? "Bạn thuộc phe Dân Làng. Nguyền một người còn sống; lời nguyền kích hoạt gây chết khi bắt đầu buổi thảo luận thứ hai tính từ đêm đặt nguyền. Bảo vệ thông thường không chặn lời nguyền."
                    : "Bạn thuộc phe Dân Làng. Mỗi đêm chọn một người chơi; họ bị lời nguyền và không thể nói trong buổi thảo luận kế tiếp.";
            case RoleType.Brat:
                return $"Bạn thuộc phe Dân Làng. Mỗi đêm, chọn NHÌN TRỘM để xem các hành động đã đăng ký. Có {BratRole.CaughtChance:P0} khả năng bị Sói phát hiện và giết.";
            case RoleType.Madman:
                return "Bạn thuộc phe Trung Lập. Thắng riêng nếu bị bỏ phiếu loại trong ngày đầu tiên.";
            case RoleType.FoxSpirit:
                return "Bạn thuộc phe Trung Lập. Mỗi đêm mê hoặc tối đa hai người. Bạn thắng khi còn sống, đã mê hoặc ít nhất hai người và toàn bộ người đã mê hoặc còn sống sau khi giải quyết đêm.";
            case RoleType.Killer:
                return "Bạn thuộc phe Trung Lập. Có thể chọn mục tiêu tấn công vào các đêm chẵn.";
            default:
                return "Vai trò này chưa có mô tả riêng trong bản thử. Mở ROLE / KỸ NĂNG khi vào ván để xem các thao tác hiện có.";
        }
    }

    private void RefreshReveal(PlayerData player)
    {
        var definition = player.roleDefinition;
        revealRoleLabel.text = DisplayRoleName(player);
        revealFactionLabel.text = player.faction == FactionType.Monster
                ? "PHE MA SÓI" : player.faction == FactionType.Villager
                    ? "PHE DÂN LÀNG" : "PHE TRUNG LẬP";
        string guide = RoleDescription(player.roleType);
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null && player.roleType == RoleType.FoxSpirit)
            guide = "Bạn thuộc phe Trung Lập. Mỗi đêm chọn tối đa hai người còn sống để mê hoặc. Xét thắng khi xử lý xong đêm, sau toàn bộ đòn đánh và hiệu ứng.";
        revealDescriptionLabel.text = definition != null && !string.IsNullOrWhiteSpace(definition.description) &&
            !guide.Contains(definition.description) ? guide + "\n\n" + definition.description : guide;
        if (online != null)
        {
            string skillGuide = player.roleType == RoleType.FoxSpirit ? guide : revealDescriptionLabel.text;
            revealDescriptionLabel.text = "ĐIỀU KIỆN THẮNG ONLINE\n" + online.LocalWinObjective + "\n\nHƯỚNG DẪN KỸ NĂNG\n" + skillGuide;
        }
        // Unity's missing/destroyed asset references use Unity-null, not CLR-null.
        // Do not use ?? for an optional serialized Sprite or it can suppress the fallback.
        Sprite portrait = RoleInfoDatabase.GetRoleSprite(player.roleType);
        if (portrait == null && definition != null) portrait = definition.portrait;
        if (portrait == null) portrait = Resources.Load<Sprite>("RolePortraits/" + player.roleType);
        if (portrait == null) portrait = defaultPortrait;
        if (portrait == null) portrait = Resources.Load<Sprite>("RoleAbilityIcon");
        revealPortrait.sprite = portrait;
        revealPortrait.color = revealPortrait.sprite != null ? Color.white : Hex("#79DED1");
        revealPortrait.preserveAspect = true;
    }

    private static string PlayerName(PlayerData player)
    {
        string prototypeName = "Player " + player.playerID;
        return string.IsNullOrWhiteSpace(player.playerName) || player.playerName == prototypeName
            ? "Player " + (player.playerID + 1) : player.playerName;
    }

    private string TargetDisplayLabel(PlayerData target, bool includeSelfTag = true)
    {
        if (target == null) return string.Empty;
        string prefix = $"P{target.playerID + 1:00}";
        string baseName = PlayerName(target);
        string formatted = baseName.StartsWith(prefix + " ", System.StringComparison.OrdinalIgnoreCase)
            ? baseName
            : $"{prefix}  {baseName}";
        if (includeSelfTag && target.playerID == localPlayerID)
            formatted += " (Bản thân)";
        return formatted;
    }

    private void RefreshRole(PlayerData player)
    {
        hasShownRole = true;
        shownRole = player.roleType;
        shownDefinition = player.roleDefinition;
        shownPlayerID = player.playerID;
        shownAwakened = player.isWhiteHoundAwakened;
        shownFaction = player.faction;

        RefreshReveal(player);

        var onlineSession = Thuong.Online.ThuongOnlineSession.Active;
        shownOnlineInformation = onlineSession != null ? onlineSession.RoleInformationVersion : 0;
        RoleInfoData info = onlineSession != null && onlineSession.HasLocalRole
            ? onlineSession.LocalRoleInformation() : RoleInfoDatabase.GetRoleInfo(shownRole);
        if (onlineSession == null && shownRole == RoleType.WhiteHound && shownAwakened)
        {
            info.DisplayName = "Bạch khuyển (Phe DogSpirit)";
            info.FactionName = "Phe Quái vật (DogSpirit)";
            info.Faction = FactionType.Monster;
            info.SkillName = "Tấn công ban đêm";
            info.SkillDescription = "Đã bị DogSpirit cắn và thức tỉnh! Mỗi đêm có thể cùng bầy sói tấn công 1 người chơi.";
            info.Restrictions = "Sử dụng chức năng tấn công vào ban đêm cùng phe DogSpirit.";
        }

        if (roleLabel != null) roleLabel.text = info.DisplayName.ToUpper();
        if (factionLabel != null)
        {
            factionLabel.text = "PHE: " + info.FactionName.ToUpper();
            factionLabel.color = info.Faction == FactionType.Monster ? Hex("#FF5555")
                                : info.Faction == FactionType.Neutral ? Hex("#FFDD44")
                                : Hex("#79DED1");
        }
        if (descriptionLabel != null)
        {
            descriptionLabel.text = "<b>Mô tả vai trò:</b> " + info.Description;
        }
        if (skillLabel != null)
        {
            skillLabel.text = "<b>Kỹ năng đặc biệt:</b> " + info.SkillName;
        }
        if (skillDescriptionLabel != null)
        {
            skillDescriptionLabel.text = "<b>Chi tiết & Thời điểm:</b> " + info.SkillDescription;
        }
        if (restrictionsLabel != null)
        {
            restrictionsLabel.text = "<b>Điều kiện & Giới hạn:</b> " + info.Restrictions;
        }
        if (playerLabel != null)
        {
            playerLabel.text = $"<b>{TargetDisplayLabel(player, includeSelfTag: false)}</b>\n<size=85%>{info.DisplayName}</size>";
        }
        if (openLabel != null) openLabel.text = "ROLE / " + info.DisplayName.ToUpper();

        // Cập nhật Sprite hình ảnh vai trò thực tế
        Sprite roleSprite = RoleInfoDatabase.GetRoleSprite(shownRole);
        if (roleSprite == null && shownDefinition != null) roleSprite = shownDefinition.portrait;
        if (roleSprite == null) roleSprite = defaultPortrait;

        if (characterPortraitImage != null)
        {
            characterPortraitImage.sprite = roleSprite;
            characterPortraitImage.color = Color.white;
            characterPortraitImage.preserveAspect = true;
        }

        foreach (var image in abilityCanvas.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "Role icon")
            {
                image.sprite = roleSprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else if (image.gameObject.name == "Role icon on button")
            {
                image.sprite = shownDefinition != null && shownDefinition.abilityIcon != null
                    ? shownDefinition.abilityIcon : roleSprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
        }

        bool configured = RoleManager.UsesConfiguredAbility(player);
        TMP_Text useLabel = useButton.GetComponentInChildren<TMP_Text>();
        if (useLabel != null)
            useLabel.text = shownRole == RoleType.Brat ? "NHÌN TRỘM" : "CHỌN MỤC TIÊU";
        if (configured && onlineSession == null)
        {
            if (skillLabel != null)
                skillLabel.text = !string.IsNullOrWhiteSpace(shownDefinition.abilityName)
                    ? "<b>Kỹ năng:</b> " + shownDefinition.abilityName : "<b>Kỹ năng:</b> " + shownDefinition.abilityType.ToString();
            if (skillDescriptionLabel != null && !string.IsNullOrWhiteSpace(shownDefinition.description))
                skillDescriptionLabel.text = "<b>Chi tiết:</b> " + shownDefinition.description;
        }

        bool isShaman = !configured && shownRole == RoleType.Shaman;
        useButton.gameObject.SetActive(HasActiveAbility(player) && !isShaman);
        shamanGoodCharmButton.gameObject.SetActive(isShaman);
        shamanBadCharmButton.gameObject.SetActive(isShaman);
        hunterTrapButton.gameObject.SetActive(shownRole == RoleType.Hunter);
    }

    private bool CanUse(PlayerData player, GameRoleManager game, out string reason)
    {
        if (Thuong.Online.ThuongOnlineSession.Active != null)
            return Thuong.Online.ThuongOnlineSession.Active.CanUseLocalAbility(out reason);
        if (player == null || !hasShownRole)
            reason = "Đang chờ phân Role cho người chơi.";
        else if (CanSetHunterTrap(player, game))
        {
            reason = "Chọn mục tiêu cho bẫy của bạn.";
            return true;
        }
        else if (RoleManager.UsesConfiguredAbility(player))
        {
            if (AbilitySystem.Instance == null) { reason = "Chưa có hệ thống kỹ năng."; return false; }
            var validation = AbilitySystem.Instance.ValidateActor(player);
            reason = validation.IsValid ? "Chọn kỹ năng để mở danh sách mục tiêu." : validation.Message;
            return validation.IsValid;
        }
        else if (player.roleType == RoleType.WhiteHound && !player.isWhiteHoundAwakened)
            reason = "Chỉ có thể tấn công sau khi thức tỉnh.";
        else if (!player.isAlive)
            reason = "Bạn đã bị loại và không thể dùng kỹ năng.";
        else if (!HasActiveAbility(player))
            reason = "Role này chỉ có kỹ năng nội tại hoặc quyền bỏ phiếu.";
        else if (player.HasEffect(StatusEffectType.Silenced))
            reason = "Người chơi đang bị Silenced.";
        else if (game == null || game.currentPhase != GamePhase.Night)
            reason = "Kỹ năng chỉ dùng được vào ban đêm.";
        else if (NightManager.IsMonsterPackMember(player) && NightManager.Instance != null)
        {
            var night = NightManager.Instance;
            if (night.IsDogSpiritDecisionActive)
            {
                if (NightManager.CanParticipateInDogSpiritDecision(player, out string decisionReason))
                {
                    reason = "Hòa phiếu! Chó Tinh chọn 1 mục tiêu trong nhóm cao phiếu nhất để chốt ngay.";
                    return true;
                }
                reason = player.roleType == RoleType.DogSpirit
                    ? decisionReason
                    : "Hòa phiếu! Đang chờ Chó Tinh chọn mục tiêu cuối cùng...";
                return false;
            }
            if (night.IsMonsterVoteClosed || night.IsMonsterAttackLockedForCurrentNight())
            {
                reason = "Lượt bỏ phiếu của phe Quái vật trong đêm này đã kết thúc.";
                return false;
            }
            if (!NightManager.CanParticipateInMonsterPackAttack(player, night.CurrentEffectiveNightNumber(), out reason))
                return false;
            reason = night.GetMonsterVotedTarget(player.playerID) >= 0
                ? "Đã bỏ phiếu. Chọn người khác để đổi phiếu trước khi hết giờ."
                : "Chọn một mục tiêu để bỏ phiếu tấn công đêm nay.";
            return true;
        }
        else if (player.roleType == RoleType.WeaverOfFate &&
                 GetLocalWeaverRole()?.HasUsedAbility == true)
            reason = "Dệt Duyên chỉ dùng được một lần mỗi ván.";
        else if (player.roleType == RoleType.Killer &&
                 ((NightManager.Instance != null ? NightManager.Instance.CurrentNightNumber : 0) <= 0
                  || (NightManager.Instance != null ? NightManager.Instance.CurrentNightNumber : 0) % 2 != 0))
            reason = "Sát Nhân chỉ có thể tấn công vào đêm chẵn.";
        else if (player.hasUseNightAction)
            reason = "Bạn đã dùng kỹ năng trong đêm này.";
        else if (RoleManager.Instance == null && RoleManger.Instance == null)
            reason = "Chưa có hệ thống quản lý Role.";
        else
        {
            reason = "Chọn kỹ năng để mở danh sách mục tiêu.";
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (!initialized) return;
        var online = Thuong.Online.ThuongOnlineSession.Active;
        bool canvasVisible = online == null || online.HasLocalRole && (online.MatchStarted || online.ShowRoleCard);
        abilityCanvas.enabled = canvasVisible;
        abilityRaycaster.enabled = canvasVisible;
        if (online != null)
        {
            if (!online.HasLocalRole)
            {
                hasShownRole = false;
                previousState = GameState.RoleReveal;
                ShowWindow(false);
                return;
            }
            localPlayerID = online.LocalPlayerId;
        }
        if (online == null && observedAbilitySystem != AbilitySystem.Instance)
        {
            if (observedAbilitySystem != null) observedAbilitySystem.AbilityResolved -= HandleAbilityResolved;
            observedAbilitySystem = AbilitySystem.Instance;
            if (observedAbilitySystem != null) observedAbilitySystem.AbilityResolved += HandleAbilityResolved;
        }
        GameRoleManager game = GameRoleManager.Instance;
        GameState state = online != null ? online.VisibleState : game != null ? game.currentState : GameState.RoleReveal;
        PlayerData player = PlayerManager.Instance?.GetplayerByID(localPlayerID);
        if (player != null)
        {
            if (!hasShownRole || shownRole != player.roleType || shownPlayerID != player.playerID ||
                shownDefinition != player.roleDefinition || shownAwakened != player.isWhiteHoundAwakened ||
                online != null && (shownFaction != player.faction || shownOnlineInformation != online.RoleInformationVersion)) RefreshRole(player);
        }

        bool revealing = online != null ? online.ShowRoleCard : state == GameState.RoleReveal;
        if (revealWindow.activeSelf != revealing) revealWindow.SetActive(revealing);
        if (revealing)
        {
            if (window.activeSelf) ShowWindow(false);
            openButton.gameObject.SetActive(false);
            if (online != null) revealTimerLabel.text = "Không giới hạn thời gian";
            else revealTimerLabel.text = $"Bắt đầu sau {Mathf.CeilToInt(game != null ? game.PhaseTimeRemaining : 0)} giây";
            previousState = state;
            return;
        }

        if (online != null && !online.MatchStarted)
        {
            openButton.gameObject.SetActive(false);
            ShowWindow(false);
            return;
        }

        if ((online != null || game != null) && state != previousState)
        {
            if (state == GameState.Night)
            {
                actionFeedback = null;
                if (player != null && player.isAlive && HasActiveAbility(player))
                {
                    CancelTargets();
                    ShowWindow(true);
                }
            }
            else if (state == GameState.Discussion && player != null &&
                     player.roleType == RoleType.Hunter && !player.isAlive && player.hasHunterTrap)
            {
                CancelTargets();
                ShowWindow(true);
            }
            else if (previousState == GameState.Night)
                ShowWindow(false);
            previousState = state;
        }

        bool hunterPending = online != null && online.CanSetHunterTrap;
        if (hunterPending && !previousHunterPending)
        {
            CancelTargets();
            ShowWindow(true);
        }
        previousHunterPending = hunterPending;

        bool ended = state == GameState.GameOver;
        openButton.gameObject.SetActive(!ended);
        if (ended)
        {
            ShowWindow(false);
            return;
        }

        bool canUse = CanUse(player, game, out string reason);
        useButton.interactable = canUse;
        bool isLocalMonsterPack = IsLocalMonsterVoteActive(online, player);
        bool isDecisionActive = IsLocalDogSpiritDecisionActive(online, player) && !selectingHunterTrap;
        bool canLocalDecide = isDecisionActive && CanLocalPlayerDecideDogSpirit(online, player);
        if (isDecisionActive && !previousDogSpiritDecision && canLocalDecide && window != null && !ended)
        {
            ShowWindow(true);
            OpenTargets();
        }
        previousDogSpiritDecision = isDecisionActive;
        if (online != observedMonsterSession)
        {
            if (observedMonsterSession != null) observedMonsterSession.MonsterVoteUpdated -= HandleMonsterVoteUpdated;
            observedMonsterSession = online;
            if (observedMonsterSession != null) observedMonsterSession.MonsterVoteUpdated += HandleMonsterVoteUpdated;
        }
        if (online == null && NightManager.Instance != observedMonsterNight)
        {
            if (observedMonsterNight != null) observedMonsterNight.MonsterVoteStateChanged -= HandleMonsterVoteUpdated;
            observedMonsterNight = NightManager.Instance;
            if (observedMonsterNight != null) observedMonsterNight.MonsterVoteStateChanged += HandleMonsterVoteUpdated;
        }
        int myMonsterVoteId = GetLocalMonsterVoteChoice(online, player);
        TMP_Text useBtnLabel = useButton != null ? useButton.GetComponentInChildren<TMP_Text>() : null;
        if (useBtnLabel != null && shownRole != RoleType.Brat)
        {
            useBtnLabel.text = isDecisionActive
                ? (canLocalDecide ? "CHỐT MỤC TIÊU (CHÓ TINH)" : "CHỜ CHÓ TINH CHỐT...")
                : isLocalMonsterPack
                    ? (myMonsterVoteId >= 0 ? "XEM / ĐỔI PHIẾU" : "BỎ PHIẾU TẤN CÔNG")
                    : "CHỌN MỤC TIÊU";
        }
        if (targetView.activeSelf)
        {
            if (isLocalMonsterPack) UpdateMonsterVoteHeader(online, game, player);
            if (monsterVoteUiDirty || isLocalMonsterPack)
            {
                monsterVoteUiDirty = false;
                RefreshTargets(player, canUse, reason);
            }
            else
            {
                RefreshTargets(player, canUse, reason);
            }
        }
        ShamanRole shaman = GetLocalShamanRole();
        bool canUseShamanCharm = player != null && player.isAlive && !player.HasEffect(StatusEffectType.Silenced) && !player.hasUseNightAction &&
                                 state == GameState.Night && game != null && game.currentPhase == GamePhase.Night;
        shamanGoodCharmButton.interactable = online != null ? online.CanUseLocalAbility(out _) && online.HasGoodCharm
            : canUseShamanCharm && shaman != null && shaman.HasGoodCharm;
        shamanBadCharmButton.interactable = online != null ? online.CanUseLocalAbility(out _) && online.HasBadCharm
            : canUseShamanCharm && shaman != null && shaman.HasBadCharm;
        hunterTrapButton.gameObject.SetActive(player != null && player.roleType == RoleType.Hunter &&
                             player.hasHunterTrap);
        hunterTrapButton.interactable = CanSetHunterTrap(player, game);
        if (window.activeSelf && abilityView.activeSelf)
        {
            string packSummary = GetMonsterPackStatusLine(online, player);
            string baseText = online != null ? online.Feedback + "\n" + reason
                : !string.IsNullOrEmpty(actionFeedback) ? actionFeedback : reason;
            statusLabel.text = !string.IsNullOrEmpty(packSummary) ? packSummary + "\n" + baseText : baseText;
        }
        if (online != null && resultView.activeSelf)
        {
            string packSummary = GetMonsterPackStatusLine(online, player);
            resultFeedbackLabel.text = !string.IsNullOrEmpty(packSummary)
                ? online.Feedback + "\n" + packSummary
                : online.Feedback;
        }
    }

    private void ShowWindow(bool visible)
    {
        if (window == null) return;
        window.SetActive(visible);
        if (!visible) CancelTargets();
    }

    private void OpenTargets()
    {
        targetFailureFeedback = null;
        PlayerData player = PlayerManager.Instance?.GetplayerByID(localPlayerID);
        bool canSetHunterTrap = CanSetHunterTrap(player, GameRoleManager.Instance);
        if (player == null || (!HasActiveAbility(player) && !canSetHunterTrap))
        {
            actionFeedback = "Chưa có kỹ năng chủ động để chọn mục tiêu.";
            return;
        }
        if (player.roleType == RoleType.Brat)
        {
            SubmitBratPeek(player);
            return;
        }

        foreach (Button button in targetButtons.Values)
        {
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
        targetButtons.Clear();
        monsterVoteBadgeLabels.Clear();
        monsterVoteAvatarImages.Clear();
        List<PlayerData> lobby = PlayerManager.Instance?.players;
        if (lobby == null)
        {
            actionFeedback = "Lobby chưa có danh sách người chơi.";
            return;
        }

        var online = Thuong.Online.ThuongOnlineSession.Active;
        bool monsterVoteMode = IsLocalMonsterVoteActive(online, player) && !selectingHunterTrap;
        var grid = targetContent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = monsterVoteMode ? new Vector2(224, 54) : new Vector2(214, 44);
        }

        int lobbyCount = 0;
        bool selectingSecondLover = player.roleType == RoleType.WeaverOfFate &&
                                    firstLoverTargetID != -1;
        foreach (PlayerData target in lobby)
        {
            if (target != null) lobbyCount++;
            if (target == null)
                continue;
            if (selectingSecondLover && target.playerID == firstLoverTargetID)
                continue;
            int targetID = target.playerID;
            string name = TargetDisplayLabel(target);
            Button button = ActionButton("Target " + targetID, targetContent,
                name, Vector2.zero, Vector2.one, "#2F5962", () => SelectTarget(targetID));
            TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
            {
                buttonLabel.fontSizeMax = 16;
                buttonLabel.fontSizeMin = 11;
                buttonLabel.fontSize = 16;
            }
            if (monsterVoteMode)
            {
                ConfigureMonsterVoteCard(button, buttonLabel, online, targetID);
            }
            targetButtons.Add(targetID, button);
        }

        if (monsterVoteMode)
        {
            UpdateMonsterVoteHeader(online, GameRoleManager.Instance, player);
        }
        else
        {
            targetTitleLabel.text = selectingSecondLover
                ? "CHỌN NGƯỜI THỨ HAI"
                : $"MỤC TIÊU / LOBBY {lobbyCount} NGƯỜI";
        }
        bool canUse = CanUse(player, GameRoleManager.Instance, out string reason);
        int selectableCount = RefreshTargets(player, canUse, reason);
        if (!canUse)
            targetFeedbackLabel.text = reason;
        else if (selectingSecondLover)
        {
            PlayerData firstTarget = PlayerManager.Instance.GetplayerByID(firstLoverTargetID);
            targetFeedbackLabel.text = firstTarget == null
                ? "Chọn người chơi thứ hai."
                : $"Đã chọn {TargetDisplayLabel(firstTarget)}. Chọn người thứ hai.";
        }
        else if (!monsterVoteMode)
            targetFeedbackLabel.text = selectableCount == 0
                ? "Không có mục tiêu còn sống hợp lệ."
                : $"{selectableCount} người chơi có thể chọn.";
        abilityView.SetActive(false);
        resultView.SetActive(false);
        targetView.SetActive(true);
        LayoutRebuilder.MarkLayoutForRebuild(targetContent);
    }

    private void SubmitBratPeek(PlayerData player)
    {
        if (!CanUse(player, GameRoleManager.Instance, out string reason))
        {
            actionFeedback = reason;
            if (statusLabel != null) statusLabel.text = reason;
            return;
        }

        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            online.RequestAbility(-1);
            targetView.SetActive(false);
            abilityView.SetActive(false);
            resultView.SetActive(true);
            resultFeedbackLabel.text = "Đang nhìn trộm...";
            return;
        }

        BratRole brat = GetLocalBratRole();
        if (brat == null)
        {
            actionFeedback = "Không tìm thấy vai trò Đứa Trẻ.";
            if (statusLabel != null) statusLabel.text = actionFeedback;
            return;
        }

        if (!brat.TryPeek(out string feedback))
        {
            actionFeedback = feedback;
            if (statusLabel != null) statusLabel.text = feedback;
            return;
        }

        actionFeedback = feedback;
        resultFeedbackLabel.text = feedback;
        targetView.SetActive(false);
        abilityView.SetActive(false);
        resultView.SetActive(true);
    }

    private void ConfigureMonsterVoteCard(Button button, TMP_Text nameLabel, Thuong.Online.ThuongOnlineSession online, int targetId)
    {
        if (button == null) return;
        var avatarRect = Panel("VoteAvatarFrame", button.transform, new Vector2(0.025f, 0.12f), new Vector2(0.185f, 0.88f), "#1B3B45");
        var avatarImgGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        var avatarImgRt = avatarImgGo.GetComponent<RectTransform>();
        avatarImgRt.SetParent(avatarRect, false);
        avatarImgRt.anchorMin = new Vector2(0.05f, 0.05f);
        avatarImgRt.anchorMax = new Vector2(0.95f, 0.95f);
        avatarImgRt.offsetMin = avatarImgRt.offsetMax = Vector2.zero;
        var avatarImg = avatarImgGo.GetComponent<Image>();
        Sprite avatarSprite = online != null ? online.GetOnlinePlayerAvatarSprite(targetId) : defaultPortrait;
        avatarImg.sprite = avatarSprite != null ? avatarSprite : defaultPortrait;
        avatarImg.preserveAspect = true;
        avatarImg.raycastTarget = false;
        avatarImg.color = avatarImg.sprite != null ? Color.white : Hex("#79DED1");
        monsterVoteAvatarImages[targetId] = avatarImg;

        if (nameLabel != null)
        {
            nameLabel.rectTransform.anchorMin = new Vector2(0.20f, 0.06f);
            nameLabel.rectTransform.anchorMax = new Vector2(0.71f, 0.94f);
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        var badgeRect = Panel("VoteCountBadge", button.transform, new Vector2(0.72f, 0.14f), new Vector2(0.975f, 0.86f), "#10252E");
        var voteCountLabel = Label("VoteCount", badgeRect, "0 phiếu", Vector2.zero, Vector2.one, 14, "#EBCB83", TextAlignmentOptions.Center);
        voteCountLabel.fontSizeMin = 10;
        voteCountLabel.fontSizeMax = 14;
        monsterVoteBadgeLabels[targetId] = voteCountLabel;
    }

    private void UpdateMonsterVoteHeader(Thuong.Online.ThuongOnlineSession online, GameRoleManager game, PlayerData player)
    {
        if (targetTitleLabel == null) return;
        float remaining = online != null ? online.VisiblePhaseRemaining : (game != null ? game.PhaseTimeRemaining : 0f);
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (IsLocalDogSpiritDecisionActive(online, player) && !selectingHunterTrap)
        {
            bool canDecide = CanLocalPlayerDecideDogSpirit(online, player);
            targetTitleLabel.text = canDecide
                ? $"HÒA PHIẾU • CHÓ TINH CHỐT ({seconds / 60:00}:{seconds % 60:00})"
                : $"HÒA PHIẾU • CHỜ CHÓ TINH ({seconds / 60:00}:{seconds % 60:00})";
            return;
        }
        bool locked = online != null ? online.MonsterPackTargetLocked : (NightManager.Instance != null && NightManager.Instance.IsMonsterAttackLockedForCurrentNight());
        if (locked)
        {
            targetTitleLabel.text = "KẾT QUẢ MONSTER VOTE (ĐÃ KHÓA)";
            return;
        }
        targetTitleLabel.text = $"MONSTER VOTE • CÒN {seconds / 60:00}:{seconds % 60:00}";
    }

    private static bool IsLocalMonsterVoteActive(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null)
        {
            if (!online.IsLocalMonsterPackMember) return false;
            if (online.GetPrivateRole() == RoleType.SerpentSpirit && online.GetVisibleDay() < 3) return false;
            return online.VisibleState == GameState.Night || online.MonsterPackTargetLocked || online.IsDogSpiritDecisionActive;
        }
        if (!NightManager.IsMonsterPackMember(player)) return false;
        if (player.roleType == RoleType.SerpentSpirit &&
            NightManager.Instance != null && NightManager.Instance.CurrentEffectiveNightNumber() < 3)
        {
            return false;
        }
        return true;
    }

    private static bool IsLocalDogSpiritDecisionActive(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null) return online.IsDogSpiritDecisionActive;
        return NightManager.IsMonsterPackMember(player) &&
               NightManager.Instance != null &&
               NightManager.Instance.IsDogSpiritDecisionActive;
    }

    private static bool CanLocalPlayerDecideDogSpirit(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null) return online.CanLocalPlayerDecideDogSpiritTarget;
        return NightManager.CanParticipateInDogSpiritDecision(player, out _);
    }

    private static bool IsTargetInDogSpiritTiedList(Thuong.Online.ThuongOnlineSession online, int targetId)
    {
        if (targetId < 0) return false;
        if (online != null) return online.IsDogSpiritTiedCandidate(targetId);
        return NightManager.Instance != null && NightManager.Instance.IsDogSpiritTiedCandidate(targetId);
    }

    private static int GetLocalMonsterVoteChoice(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null && online.IsLocalMonsterPackMember)
            return online.LocalMonsterVotedTargetId;
        if (player != null && NightManager.IsMonsterPackMember(player) && NightManager.Instance != null)
            return NightManager.Instance.GetMonsterVotedTarget(player.playerID);
        return -1;
    }

    private int RefreshTargets(PlayerData player, bool canUse, string reason)
    {
        var online = Thuong.Online.ThuongOnlineSession.Active;
        int packTargetId = GetCurrentMonsterPackTargetId(online, player);
        int myVoteId = GetLocalMonsterVoteChoice(online, player);
        bool isLocalMonsterPack = IsLocalMonsterVoteActive(online, player) && !selectingHunterTrap;
        bool isDecisionActive = isLocalMonsterPack && IsLocalDogSpiritDecisionActive(online, player);
        bool canLocalDecide = isDecisionActive && CanLocalPlayerDecideDogSpirit(online, player);
        bool isLocked = online != null ? online.MonsterPackTargetLocked : (NightManager.Instance != null && NightManager.Instance.IsMonsterAttackLockedForCurrentNight());
        int activeCount = 0;
        foreach (var pair in targetButtons)
        {
            var target = PlayerManager.Instance?.GetplayerByID(pair.Key);
            bool allowed = ValidateTarget(player, target).IsValid &&
                !(player?.roleType == RoleType.WeaverOfFate && firstLoverTargetID == pair.Key);
            if (isDecisionActive)
            {
                allowed = allowed && IsTargetInDogSpiritTiedList(online, pair.Key);
            }
            bool isMyChoice = isLocalMonsterPack && !isDecisionActive && myVoteId >= 0 && pair.Key == myVoteId;
            bool isLeadingTarget = isLocalMonsterPack && !isDecisionActive && packTargetId >= 0 && pair.Key == packTargetId;
            bool isConfirmedTarget = isLocalMonsterPack && isLocked && packTargetId >= 0 && pair.Key == packTargetId;
            pair.Value.gameObject.SetActive(allowed);
            pair.Value.interactable = allowed && canUse && !isLocked && (isDecisionActive ? canLocalDecide : !isMyChoice);
            if (target != null)
            {
                TMP_Text btnLabel = pair.Value.GetComponentInChildren<TMP_Text>();
                if (btnLabel != null)
                {
                    if (isDecisionActive)
                        btnLabel.text = TargetDisplayLabel(target);
                    else
                        btnLabel.text = isMyChoice
                            ? TargetDisplayLabel(target) + " [ĐÃ CHỌN]"
                            : TargetDisplayLabel(target);
                }
                if (isLocalMonsterPack && monsterVoteBadgeLabels.TryGetValue(pair.Key, out TMP_Text countLabel) && countLabel != null)
                {
                    int votes = online != null
                        ? online.GetMonsterVoteCountForTarget(pair.Key)
                        : (NightManager.Instance != null ? NightManager.Instance.GetMonsterVoteCountForTarget(pair.Key) : 0);
                    countLabel.text = $"{votes} phiếu";
                }
                Image btnImg = pair.Value.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = isConfirmedTarget
                        ? Hex("#9C3D3D")
                        : isDecisionActive
                            ? (canLocalDecide ? Hex("#B06A2E") : Hex("#4E5D64"))
                            : isMyChoice
                                ? Hex("#2E7D5B")
                                : (isLeadingTarget
                                    ? Hex("#9C3D3D")
                                    : (ThuongMedievalTheme.Active != null ? Color.white : Hex("#2F5962")));
                }
            }
            if (allowed) activeCount++;
        }
        if (!canUse || isLocked || isDecisionActive)
        {
            string packSummary = isLocalMonsterPack ? GetMonsterPackStatusLine(online, player) : string.Empty;
            targetFeedbackLabel.text = !string.IsNullOrEmpty(packSummary) ? packSummary : reason;
        }
        else if (isLocalMonsterPack)
        {
            string packSummary = GetMonsterPackStatusLine(online, player);
            if (!string.IsNullOrEmpty(packSummary)) targetFeedbackLabel.text = packSummary;
        }
        else if (!(player?.roleType == RoleType.WeaverOfFate && firstLoverTargetID != -1))
        {
            targetFeedbackLabel.text = activeCount == 0
                ? "Không có mục tiêu còn sống hợp lệ."
                : $"{activeCount} người chơi có thể chọn.";
        }
        return activeCount;
    }

    private static int GetCurrentMonsterPackTargetId(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null && online.IsLocalMonsterPackMember)
            return online.MonsterPackTargetId;
        if (NightManager.IsMonsterPackMember(player) && NightManager.Instance != null)
            return NightManager.Instance.ActiveMonsterTargetId;
        return -1;
    }

    private string GetMonsterPackStatusLine(Thuong.Online.ThuongOnlineSession online, PlayerData player)
    {
        if (online != null && online.IsLocalMonsterPackMember)
        {
            if (online.VisibleState == GameState.Night || online.MonsterPackTargetLocked)
                return online.FormatMonsterPackStatusSummary();
            return string.Empty;
        }

        if (NightManager.IsMonsterPackMember(player) && NightManager.Instance != null)
        {
            var night = NightManager.Instance;
            int targetId = night.ActiveMonsterTargetId;
            int myVoteId = player != null ? night.GetMonsterVotedTarget(player.playerID) : -1;
            int highest = night.MonsterVoteHighestCount;
            bool isTie = night.IsMonsterVoteTied;
            bool locked = night.IsMonsterAttackLockedForCurrentNight();
            if (night.IsDogSpiritDecisionActive)
            {
                return player != null && NightManager.CanParticipateInDogSpiritDecision(player, out _)
                    ? $"HÒA PHIẾU ({highest} phiếu)! Chó Tinh chọn 1 người trong nhóm cao phiếu nhất để chốt ngay!"
                    : $"HÒA PHIẾU ({highest} phiếu) — Đang chờ Chó Tinh chọn mục tiêu cuối cùng...";
            }
            if (locked)
            {
                if (targetId >= 0) return $"Đã chốt mục tiêu tấn công: P{targetId + 1:00} ({highest} phiếu)";
                if (isTie) return $"Kết quả Monster Vote: Hòa phiếu ({highest} phiếu) — Không tấn công.";
                return "Kết quả Monster Vote: Không có phiếu hợp lệ.";
            }
            string myPart = myVoteId >= 0 ? $"Phiếu của bạn: P{myVoteId + 1:00}" : "Bạn chưa bỏ phiếu";
            string leadPart = isTie ? $"Hòa phiếu ({highest} phiếu) — Bằng phiếu Chó Tinh sẽ chốt"
                : (targetId >= 0 ? $"Dẫn đầu: P{targetId + 1:00} ({highest} phiếu)" : "Chưa có phiếu nào");
            return $"{myPart}  •  {leadPart}";
        }

        return string.Empty;
    }

    private ActionValidationResult ValidateTarget(PlayerData actor, PlayerData target)
    {
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            if (actor != null && actor.roleType == RoleType.Shaman && !selectingHunterTrap &&
                target != null && target.playerID == actor.playerID)
            {
                return onlineGoodCharm && online.HasGoodCharm && target.isAlive
                    ? ActionValidationResult.Success()
                    : ActionValidationResult.Fail(ActionFailureReason.SelfTargetForbidden, "Không được dùng bùa hại lên chính mình.");
            }
            if (online.IsLocalMonsterPackMember && !selectingHunterTrap && online.IsDogSpiritDecisionActive)
            {
                if (target == null || !target.isAlive)
                    return ActionValidationResult.Fail(ActionFailureReason.TargetDead, "Mục tiêu đã bị loại.");
                return online.IsDogSpiritTiedCandidate(target.playerID)
                    ? ActionValidationResult.Success()
                    : ActionValidationResult.Fail(ActionFailureReason.FactionForbidden, "Chỉ được chọn một trong các mục tiêu có số phiếu cao nhất.");
            }
            return target != null && online.CanTargetLocalAbility(target.playerID)
                ? ActionValidationResult.Success()
                : ActionValidationResult.Fail(ActionFailureReason.FactionForbidden, "Mục tiêu không hợp lệ theo luật kỹ năng.");
        }
        if (NightManager.IsMonsterPackMember(actor) && !selectingHunterTrap &&
            NightManager.Instance != null && NightManager.Instance.IsDogSpiritDecisionActive)
        {
            if (target == null)
                return ActionValidationResult.Fail(ActionFailureReason.TargetMissing, "Mục tiêu không tồn tại.");
            return NightManager.Instance.IsValidDogSpiritDecisionTarget(target.playerID, out string decisionError)
                ? ActionValidationResult.Success()
                : ActionValidationResult.Fail(ActionFailureReason.FactionForbidden, decisionError);
        }
        bool allowSelfTarget = actor != null && actor.roleType == RoleType.Shaman && !selectingHunterTrap &&
                               GetLocalShamanRole()?.IsGoodCharmSelected == true;
        return RoleManager.UsesConfiguredAbility(actor) && !selectingHunterTrap
            ? TargetValidator.Validate(actor, target, actor.roleDefinition)
            : TargetValidator.ValidateLegacy(actor, target, allowSelfTarget);
    }

    private static bool CanSetHunterTrap(PlayerData player, GameRoleManager game)
    {
        if (Thuong.Online.ThuongOnlineSession.Active != null)
            return Thuong.Online.ThuongOnlineSession.Active.CanSetHunterTrap;
        return player != null && player.roleType == RoleType.Hunter && !player.isAlive &&
               player.hasHunterTrap && game != null && game.currentState == GameState.Discussion;
    }

    private HunterRole GetLocalHunterRole()
    {
        if (RoleManager.Instance != null &&
            RoleManager.Instance.playerRoles.TryGetValue(localPlayerID, out BaseRole role))
            return role as HunterRole;

        if (RoleManger.Instance != null &&
            RoleManger.Instance.playerRoles.TryGetValue(localPlayerID, out role))
            return role as HunterRole;

        return null;
    }

    private void OpenHunterTrapTargets()
    {
        selectingHunterTrap = true;
        OpenTargets();
    }

    private ShamanRole GetLocalShamanRole()
    {
        if (RoleManager.Instance != null &&
            RoleManager.Instance.playerRoles.TryGetValue(localPlayerID, out BaseRole role))
            return role as ShamanRole;

        if (RoleManger.Instance != null &&
            RoleManger.Instance.playerRoles.TryGetValue(localPlayerID, out role))
            return role as ShamanRole;

        return null;
    }

    private WeaverOfFateRole GetLocalWeaverRole()
    {
        if (RoleManager.Instance != null &&
            RoleManager.Instance.playerRoles.TryGetValue(localPlayerID, out BaseRole role))
            return role as WeaverOfFateRole;

        if (RoleManger.Instance != null &&
            RoleManger.Instance.playerRoles.TryGetValue(localPlayerID, out role))
            return role as WeaverOfFateRole;

        return null;
    }

    private BratRole GetLocalBratRole()
    {
        if (RoleManager.Instance != null &&
            RoleManager.Instance.playerRoles.TryGetValue(localPlayerID, out BaseRole role))
            return role as BratRole;

        if (RoleManger.Instance != null &&
            RoleManger.Instance.playerRoles.TryGetValue(localPlayerID, out role))
            return role as BratRole;

        return null;
    }

    private void OpenShamanTargets(bool goodCharm)
    {
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            if (goodCharm ? !online.HasGoodCharm : !online.HasBadCharm) return;
            onlineGoodCharm = goodCharm;
            OpenTargets();
            return;
        }
        ShamanRole shaman = GetLocalShamanRole();
        if (shaman == null || !shaman.SelectCharm(goodCharm))
        {
            actionFeedback = goodCharm ? "Bùa lợi đã được sử dụng." : "Bùa hại đã được sử dụng.";
            if (statusLabel != null) statusLabel.text = actionFeedback;
            return;
        }

        OpenTargets();
    }

    private void SelectTarget(int targetID)
    {
        PlayerData player = PlayerManager.Instance?.GetplayerByID(localPlayerID);
        var online = Thuong.Online.ThuongOnlineSession.Active;
        if (online != null)
        {
            bool isShamanSelfSave = player != null && player.roleType == RoleType.Shaman &&
                                    !selectingHunterTrap && targetID == player.playerID;
            bool isDogSpiritDecision = online.IsLocalMonsterPackMember && !selectingHunterTrap && online.IsDogSpiritDecisionActive;
            if (isDogSpiritDecision)
            {
                if (!online.CanLocalPlayerDecideDogSpiritTarget)
                { targetFeedbackLabel.text = "Chỉ Chó Tinh mới được chọn mục tiêu hòa phiếu cuối cùng."; return; }
                if (!online.IsDogSpiritTiedCandidate(targetID))
                { targetFeedbackLabel.text = "Chỉ được chọn một trong các mục tiêu có số phiếu cao nhất."; return; }
            }
            else if (isShamanSelfSave)
            {
                if (!onlineGoodCharm)
                { targetFeedbackLabel.text = "Không được dùng bùa hại lên chính mình."; return; }
                if (!online.HasGoodCharm || !player.isAlive)
                { targetFeedbackLabel.text = "Bùa lợi không khả dụng."; return; }
            }
            else if (!online.CanTargetLocalAbility(targetID))
            { targetFeedbackLabel.text = "Mục tiêu không hợp lệ theo luật kỹ năng."; return; }
            bool canUseOnline = online.CanUseLocalAbility(out string networkReason);
            if (player == null || !canUseOnline)
            {
                targetFeedbackLabel.text = player == null ? "Đang chờ dữ liệu vai trò." : networkReason;
                return;
            }
            if (player.roleType == RoleType.WeaverOfFate && firstLoverTargetID < 0)
            {
                firstLoverTargetID = targetID;
                OpenTargets();
                return;
            }
            online.RequestAbility(player.roleType == RoleType.WeaverOfFate ? firstLoverTargetID : targetID,
                player.roleType == RoleType.WeaverOfFate ? targetID : -1,
                player.roleType == RoleType.Shaman, onlineGoodCharm, selectingHunterTrap);
            firstLoverTargetID = -1;
            if (online.IsLocalMonsterPackMember && !selectingHunterTrap)
            {
                targetFeedbackLabel.text = isDogSpiritDecision
                    ? "Đang chốt mục tiêu cuối cùng..."
                    : "Đang gửi phiếu bầu...";
                return;
            }
            selectingHunterTrap = false;
            targetView.SetActive(false);
            abilityView.SetActive(false);
            resultView.SetActive(true);
            resultFeedbackLabel.text = "Đang gửi yêu cầu...";
            return;
        }
        if (selectingHunterTrap)
        {
            if (!CanSetHunterTrap(player, GameRoleManager.Instance))
            {
                targetFeedbackLabel.text = targetFailureFeedback = "Hiện chưa thể đặt bẫy.";
                return;
            }

            HunterRole hunter = GetLocalHunterRole();
            if (hunter == null)
            {
                targetFeedbackLabel.text = targetFailureFeedback = "Không tìm thấy vai trò Thợ Săn.";
                return;
            }

            if (!hunter.TrySetTrap(targetID, out string trapFeedback))
            {
                targetFeedbackLabel.text = targetFailureFeedback = trapFeedback;
                return;
            }

            selectingHunterTrap = false;
            actionFeedback = trapFeedback;
            resultFeedbackLabel.text = trapFeedback;
            targetView.SetActive(false);
            abilityView.SetActive(false);
            resultView.SetActive(true);
            return;
        }

        var validation = ValidateTarget(player, PlayerManager.Instance?.GetplayerByID(targetID));
        if (!validation.IsValid)
        {
            targetFeedbackLabel.text = targetFailureFeedback = validation.Message;
            return;
        }
        if (!CanUse(player, GameRoleManager.Instance, out string reason))
        {
            targetFeedbackLabel.text = targetFailureFeedback = reason;
            return;
        }

        if (player.roleType == RoleType.WeaverOfFate)
        {
            if (firstLoverTargetID == -1)
            {
                firstLoverTargetID = targetID;
                OpenTargets();
                return;
            }

                WeaverOfFateRole weaver = GetLocalWeaverRole();
                string loverFeedback = string.Empty;
            if (weaver == null || !weaver.TryMakeLovers(firstLoverTargetID, targetID,
                    out loverFeedback))
            {
                targetFeedbackLabel.text = weaver == null
                    ? "Không tìm thấy vai trò Dệt Duyên."
                    : loverFeedback;
                return;
            }

            firstLoverTargetID = -1;
            actionFeedback = loverFeedback;
            resultFeedbackLabel.text = loverFeedback;
            targetView.SetActive(false);
            abilityView.SetActive(false);
            resultView.SetActive(true);
            Debug.Log("[Role Ability] " + loverFeedback);
            return;
        }

        bool wasDecisionStep = NightManager.IsMonsterPackMember(player) && !selectingHunterTrap &&
                               NightManager.Instance != null && NightManager.Instance.IsDogSpiritDecisionActive;
        bool success;
        string feedback;
        if (RoleManager.Instance != null)
            success = RoleManager.Instance.UseNightAbility(localPlayerID, targetID, out feedback);
        else if (RoleManger.Instance != null)
            success = RoleManger.Instance.UseNightAbility(localPlayerID, targetID, out feedback);
        else
        {
            success = false;
            feedback = "Chưa có hệ thống quản lý Role.";
        }

        if (!success)
        {
            targetFeedbackLabel.text = targetFailureFeedback = feedback;
            return;
        }

        actionFeedback = feedback;
        if (NightManager.IsMonsterPackMember(player) && !selectingHunterTrap)
        {
            if (wasDecisionStep)
            {
                GameRoleManager.Instance?.CompleteDogSpiritDecisionIfReady();
                PlayerData confirmedTarget = PlayerManager.Instance?.GetplayerByID(targetID);
                resultFeedbackLabel.text = confirmedTarget == null ? feedback :
                    $"{feedback}\nMục tiêu: {TargetDisplayLabel(confirmedTarget)}.";
                targetView.SetActive(false);
                abilityView.SetActive(false);
                resultView.SetActive(true);
                Debug.Log("[Role Ability] " + feedback);
                return;
            }
            RefreshTargets(player, CanUse(player, GameRoleManager.Instance, out string nextReason), nextReason);
            targetFeedbackLabel.text = feedback;
            Debug.Log("[Role Ability] " + feedback);
            return;
        }
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        resultFeedbackLabel.text = target == null ? feedback :
            $"{feedback}\nMục tiêu: {TargetDisplayLabel(target)}.";
        targetView.SetActive(false);
        abilityView.SetActive(false);
        resultView.SetActive(true);
        Debug.Log("[Role Ability] " + feedback);
    }

    private static string DisplayRoleName(PlayerData player) =>
        player.roleDefinition != null && !string.IsNullOrWhiteSpace(player.roleDefinition.displayName)
            ? player.roleDefinition.displayName : RoleName(player.roleType);
    private static bool HasActiveAbility(PlayerData player) => Thuong.Online.ThuongOnlineSession.Active != null
        ? Thuong.Online.ThuongOnlineSession.Active.HasActiveLocalAbility : RoleManager.UsesConfiguredAbility(player)
            ? player.roleDefinition.abilityType != AbilityType.None : HasActiveAbility(player.roleType);
    private void HandleAbilityResolved(AbilityResolution result)
    {
        if (result.Actor == null || result.Actor.playerID != localPlayerID) return;
        actionFeedback = result.Message;
        if (resultFeedbackLabel != null) resultFeedbackLabel.text = result.Message;
    }

    private void CancelTargets()
    {
        targetFailureFeedback = null;
        selectingHunterTrap = false;
        firstLoverTargetID = -1;
        if (targetView != null) targetView.SetActive(false);
        if (resultView != null) resultView.SetActive(false);
        if (abilityView != null) abilityView.SetActive(true);
    }
}
