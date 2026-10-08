using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Giao diện kỹ năng local của prototype Thương. Toàn bộ Canvas được tạo khi chạy.
public class RoleAbilityUI : MonoBehaviour
{
    public int localPlayerID;

    private Canvas abilityCanvas;
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
    private TMP_Text skillLabel;
    private TMP_Text descriptionLabel;
    private TMP_Text statusLabel;
    private TMP_Text targetFeedbackLabel;
    private TMP_Text targetTitleLabel;
    private TMP_Text resultFeedbackLabel;
    private TMP_Text revealRoleLabel;
    private TMP_Text revealFactionLabel;
    private TMP_Text revealDescriptionLabel;
    private TMP_Text revealTimerLabel;
    private Image revealPortrait;
    private Sprite defaultPortrait;
    private RectTransform targetContent;
    private readonly Dictionary<int, Button> targetButtons = new Dictionary<int, Button>();
    private bool initialized;
    private bool hasShownRole;
    private RoleType shownRole;
    private ThuongRoleDefinition shownDefinition;
    private int shownPlayerID = -1;
    private bool shownAwakened;
    private AbilitySystem observedAbilitySystem;
    private GameState previousState;
    private string actionFeedback;
    private bool selectingHunterTrap;

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
        if (abilityCanvas == null) return;
        if (Application.isPlaying) Destroy(abilityCanvas.gameObject);
        else DestroyImmediate(abilityCanvas.gameObject);
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
        return label;
    }

    private Button ActionButton(string name, Transform parent, string value,
        Vector2 min, Vector2 max, string color, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(name, parent, min, max, color, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Hex("#B8F3EA");
        colors.pressedColor = Hex("#79DED1");
        colors.selectedColor = Color.white;
        colors.disabledColor = Hex("#657B82");
        button.colors = colors;
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
        Sprite roleIcon = Resources.Load<Sprite>("RoleAbilityIcon");
        defaultPortrait = roleIcon;
        if (roleIcon == null)
            Debug.LogWarning("[Role Ability] Không tìm thấy sprite Resources/RoleAbilityIcon.");

        var root = new GameObject("Role Ability Canvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        abilityCanvas = root.GetComponent<Canvas>();
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
        var card = Panel("Card", window.transform, new Vector2(.18f, .16f),
            new Vector2(.82f, .84f), "#142D38", true);
        Label("Title", card, "ROLE ABILITY", new Vector2(.04f, .88f),
            new Vector2(.7f, .98f), 32, "#EBCB83");
        ActionButton("Close", card, "ĐÓNG", new Vector2(.81f, .88f),
            new Vector2(.96f, .97f), "#425A63", () => ShowWindow(false));

        var left = Panel("Character", card, new Vector2(.04f, .11f),
            new Vector2(.35f, .84f), "#1B3B45");
        Label("Character heading", left, "NHÂN VẬT", new Vector2(.08f, .84f),
            new Vector2(.92f, .96f), 22, "#79DED1", TextAlignmentOptions.Center);
        var iconFrame = Panel("Icon frame", left, new Vector2(.17f, .28f),
            new Vector2(.83f, .79f), "#254B54");
        AddRoleIcon(iconFrame, "Role icon", new Vector2(.05f, .05f),
            new Vector2(.95f, .95f), roleIcon);
        playerLabel = Label("Player ID", left, "Đang chờ người chơi", new Vector2(.08f, .08f),
            new Vector2(.92f, .26f), 23, "#F4FAF8", TextAlignmentOptions.Center);

        Label("Arrow", card, ">", new Vector2(.365f, .39f),
            new Vector2(.435f, .61f), 64, "#EBCB83", TextAlignmentOptions.Center);
        var right = Panel("Ability", card, new Vector2(.45f, .11f),
            new Vector2(.96f, .84f), "#1B3B45");
        abilityView = Panel("Ability details", right, Vector2.zero, Vector2.one,
            "#00000000").gameObject;
        roleLabel = Label("Role", abilityView.transform, "ROLE", new Vector2(.06f, .80f),
            new Vector2(.95f, .96f), 31, "#79DED1");
        skillLabel = Label("Skill", abilityView.transform, "KỸ NĂNG", new Vector2(.06f, .65f),
            new Vector2(.95f, .79f), 23, "#EBCB83");
        descriptionLabel = Label("Description", abilityView.transform, "", new Vector2(.06f, .40f),
            new Vector2(.95f, .65f), 20, "#D7E7E7");
        statusLabel = Label("Status", abilityView.transform, "", new Vector2(.06f, .19f),
            new Vector2(.95f, .39f), 19, "#B8D0D1");
        useButton = ActionButton("Use skill", abilityView.transform, "CHỌN MỤC TIÊU",
            new Vector2(.06f, .04f), new Vector2(.95f, .17f), "#28716F", OpenTargets);
        shamanGoodCharmButton = ActionButton("Shaman good charm", abilityView.transform,
            "BÙA LỢI", new Vector2(.06f, .04f), new Vector2(.49f, .17f),
            "#28716F", () => OpenShamanTargets(true));
        shamanBadCharmButton = ActionButton("Shaman bad charm", abilityView.transform,
            "BÙA HẠI", new Vector2(.52f, .04f), new Vector2(.95f, .17f),
            "#7D4548", () => OpenShamanTargets(false));
        shamanGoodCharmButton.gameObject.SetActive(false);
        shamanBadCharmButton.gameObject.SetActive(false);
        hunterTrapButton = ActionButton("Hunter trap", abilityView.transform,
            "ĐẶT BẪY", new Vector2(.06f, .04f), new Vector2(.95f, .17f),
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
        revealTimerLabel = Label("Countdown", card, "Bắt đầu sau 25 giây",
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
        ActionButton("OK", right, "OK  /  BẮT ĐẦU",
            new Vector2(.70f, .05f), new Vector2(.95f, .19f), "#28716F",
            () => GameRoleManager.Instance?.SkipRoleReveal());
        revealWindow.SetActive(false);
    }

    private static bool HasActiveAbility(RoleType role)
    {
        return role == RoleType.DogSpirit || role == RoleType.Seer ||
               role == RoleType.VillageGuardian || role == RoleType.SerpentSpirit ||
             role == RoleType.Ogre || role == RoleType.WhiteHound ||
             role == RoleType.Shaman || role == RoleType.Cursed ||
               role == RoleType.FoxSpirit || role == RoleType.Killer ||
               role == RoleType.WeaverOfFate;
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
                return "Bạn thuộc phe Dân Làng. Mỗi đêm chọn một người chơi để dùng bùa lợi bảo vệ hoặc bùa hại khiến họ im lặng. Mỗi loại bùa dùng một lần.";
            case RoleType.WhiteHound:
                return "Bạn bắt đầu ở phe Dân Làng. Nếu bị Dog Spirit tấn công, bạn thức tỉnh thành Bạch Khuyển và có thể chọn mục tiêu tấn công vào các đêm sau.";
            case RoleType.Idiot:
                return "Bạn thuộc phe Dân Làng. Nếu bị loại bởi bỏ phiếu, bạn sẽ sống sót nhờ khả năng đặc biệt. Bạn vẫn có thể hoàn thành nhiệm vụ và tham gia thảo luận.";
            case RoleType.Hunter:
                return "Bạn thuộc phe Dân Làng. Khi chết, chọn một người trong lúc thảo luận để đặt bẫy; mục tiêu chết sau lượt bỏ phiếu kế tiếp.";
            case RoleType.WeaverOfFate:
                return "Bạn thuộc phe Dân Làng. Mỗi đêm, bạn có thể ghép đôi mình với một người chơi khác để tạo cặp đôi và cùng chiến thắng khi còn sống.";
            case RoleType.Cursed:
                return "Bạn thuộc phe Dân Làng. Mỗi đêm chọn một người chơi; họ bị lời nguyền và không thể nói trong buổi thảo luận kế tiếp.";
            case RoleType.Brat:
                return "Bạn thuộc phe Dân Làng. Vai Đứa Trẻ có thao tác nhìn trộm; giao diện của thao tác này chưa được nối trong prototype.";
            case RoleType.Madman:
                return "Bạn thuộc phe Trung Lập. Vai này có điều kiện thắng riêng trong logic hiện có của dự án.";
            case RoleType.FoxSpirit:
                return "Bạn thuộc phe Trung Lập. Mỗi đêm mê hoặc tối đa hai người. Nếu cả hai còn sống khi ván kết thúc, Cáo tinh chiến thắng.";
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
        revealDescriptionLabel.text = definition != null && !string.IsNullOrWhiteSpace(definition.description)
            ? definition.description : RoleDescription(player.roleType);
        // Unity's missing/destroyed asset references use Unity-null, not CLR-null.
        // Do not use ?? for an optional serialized Sprite or it can suppress the fallback.
        Sprite portrait = definition != null ? definition.portrait : null;
        if (portrait == null) portrait = Resources.Load<Sprite>("RolePortraits/" + player.roleType);
        if (portrait == null) portrait = defaultPortrait;
        if (portrait == null) portrait = Resources.Load<Sprite>("RoleAbilityIcon");
        revealPortrait.sprite = portrait;
        revealPortrait.color = revealPortrait.sprite != null ? Color.white : Hex("#79DED1");
    }

    private static string PlayerName(PlayerData player)
    {
        string prototypeName = "Player " + player.playerID;
        return string.IsNullOrWhiteSpace(player.playerName) || player.playerName == prototypeName
            ? "Player " + (player.playerID + 1) : player.playerName;
    }

    private void RefreshRole(PlayerData player)
    {
        hasShownRole = true;
        shownRole = player.roleType;
        shownDefinition = player.roleDefinition;
        shownPlayerID = player.playerID;
        shownAwakened = player.isWhiteHoundAwakened;
        roleLabel.text = DisplayRoleName(player);
        openLabel.text = "ROLE / " + DisplayRoleName(player);
        RefreshReveal(player);

        switch (shownRole)
        {
            case RoleType.DogSpirit:
                skillLabel.text = "TẤN CÔNG";
                descriptionLabel.text = "Chọn một người chơi còn sống để tấn công trong đêm.";
                break;
            case RoleType.SerpentSpirit:
                skillLabel.text = "TẤN CÔNG";
                descriptionLabel.text = "Có thể tấn công từ đêm thứ ba.";
                break;
            case RoleType.Ogre:
                skillLabel.text = "TẤN CÔNG";
                descriptionLabel.text = "Chọn một người chơi còn sống để tấn công trong đêm.";
                break;
            case RoleType.Seer:
                skillLabel.text = "SOI DOGSPIRIT";
                descriptionLabel.text = "Chọn một người chơi để biết họ có phải Dogspirit hay không. Không hiển thị Role cụ thể.";
                break;
            case RoleType.VillageGuardian:
                skillLabel.text = "BẢO VỆ";
                descriptionLabel.text = "Chọn một người chơi để bảo vệ trong đêm. Không chọn cùng mục tiêu hai đêm liên tiếp.";
                break;
            case RoleType.Shaman:
                skillLabel.text = "BÙA LỢI / BÙA HẠI";
                descriptionLabel.text = "Mỗi loại bùa dùng một lần. Chọn bùa rồi chọn mục tiêu trong đêm.";
                break;
            case RoleType.WhiteHound:
                skillLabel.text = "TẤN CÔNG";
                descriptionLabel.text = player.isWhiteHoundAwakened
                    ? "Bạch Khuyển đã thức tỉnh. Chọn một người chơi để tấn công trong đêm."
                    : "Chỉ có thể tấn công sau khi bị Dog Spirit tấn công và thức tỉnh.";
                break;
            case RoleType.Cursed:
                skillLabel.text = "NGUYỀN";
                descriptionLabel.text = "Đánh dấu một người chơi còn sống bằng lời nguyền.";
                break;
            case RoleType.FoxSpirit:
                skillLabel.text = "MÊ HOẶC";
                descriptionLabel.text = "Mỗi đêm chọn tối đa hai người còn sống để mê hoặc.";
                break;
            case RoleType.Killer:
                skillLabel.text = "TẤN CÔNG";
                descriptionLabel.text = "Chọn một mục tiêu vào các đêm chẵn.";
                break;
            default:
                skillLabel.text = "KỸ NĂNG NỘI TẠI";
                descriptionLabel.text = "Role này không có kỹ năng chủ động trên giao diện. Hãy tham gia thảo luận và bỏ phiếu.";
                break;
        }
        bool configured = RoleManager.UsesConfiguredAbility(player);
        if (configured)
        {
            skillLabel.text = !string.IsNullOrWhiteSpace(shownDefinition.abilityName)
                ? shownDefinition.abilityName : shownDefinition.abilityType.ToString();
            if (!string.IsNullOrWhiteSpace(shownDefinition.description)) descriptionLabel.text = shownDefinition.description;
        }
        if (shownDefinition != null && shownDefinition.abilityIcon != null)
        {
            foreach (var image in abilityCanvas.GetComponentsInChildren<Image>(true))
                if (image.gameObject.name == "Role icon" || image.gameObject.name == "Role icon on button")
                    image.sprite = shownDefinition.abilityIcon;
        }
        else
        {
            foreach (var image in abilityCanvas.GetComponentsInChildren<Image>(true))
                if (image.gameObject.name == "Role icon" || image.gameObject.name == "Role icon on button")
                    image.sprite = Resources.Load<Sprite>("RoleAbilityIcon");
        }
        bool isShaman = !configured && shownRole == RoleType.Shaman;
        useButton.gameObject.SetActive(HasActiveAbility(player) && !isShaman);
        shamanGoodCharmButton.gameObject.SetActive(isShaman);
        shamanBadCharmButton.gameObject.SetActive(isShaman);
        hunterTrapButton.gameObject.SetActive(shownRole == RoleType.Hunter);
    }

    private bool CanUse(PlayerData player, GameRoleManager game, out string reason)
    {
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
        else if (game == null || game.currentState != GameState.Night)
            reason = "Kỹ năng chỉ dùng được vào ban đêm.";
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
        if (observedAbilitySystem != AbilitySystem.Instance)
        {
            if (observedAbilitySystem != null) observedAbilitySystem.AbilityResolved -= HandleAbilityResolved;
            observedAbilitySystem = AbilitySystem.Instance;
            if (observedAbilitySystem != null) observedAbilitySystem.AbilityResolved += HandleAbilityResolved;
        }
        GameRoleManager game = GameRoleManager.Instance;
        PlayerData player = PlayerManager.Instance?.GetplayerByID(localPlayerID);
        if (player != null)
        {
            playerLabel.text = $"PLAYER {player.playerID + 1}  /  ID {player.playerID}" +
                (player.isAlive ? " / ALIVE" : " / DEAD");
            if (!hasShownRole || shownRole != player.roleType || shownDefinition != player.roleDefinition ||
                shownPlayerID != player.playerID || shownAwakened != player.isWhiteHoundAwakened) RefreshRole(player);
        }

        bool revealing = game != null && game.currentState == GameState.RoleReveal;
        if (revealWindow.activeSelf != revealing) revealWindow.SetActive(revealing);
        if (revealing)
        {
            if (window.activeSelf) ShowWindow(false);
            openButton.gameObject.SetActive(false);
            revealTimerLabel.text = $"Bắt đầu sau {Mathf.CeilToInt(game.PhaseTimeRemaining)} giây";
            previousState = game.currentState;
            return;
        }

        if (game != null && game.currentState != previousState)
        {
            if (game.currentState == GameState.Night)
            {
                actionFeedback = null;
                if (player != null && player.isAlive && HasActiveAbility(player))
                {
                    CancelTargets();
                    ShowWindow(true);
                }
            }
            else if (game.currentState == GameState.Discussion && player != null &&
                     player.roleType == RoleType.Hunter && !player.isAlive && player.hasHunterTrap)
            {
                CancelTargets();
                ShowWindow(true);
            }
            else if (previousState == GameState.Night)
                ShowWindow(false);
            previousState = game.currentState;
        }

        bool ended = game != null && game.currentState == GameState.GameOver;
        openButton.gameObject.SetActive(!ended);
        if (ended)
        {
            ShowWindow(false);
            return;
        }

        bool canUse = CanUse(player, game, out string reason);
        bool canSetTrap = CanSetHunterTrap(player, game);
        if (targetView.activeSelf && (player == null || (!player.isAlive && !canSetTrap)))
        {
            CancelTargets();
            actionFeedback = null;
        }
        foreach (var target in targetButtons)
        {
            bool alive = PlayerManager.Instance != null && PlayerManager.Instance.IsAlive(target.Key);
            bool validTarget = RoleManager.UsesConfiguredAbility(player) && AbilitySystem.Instance != null
                ? AbilitySystem.Instance.Validate(player, PlayerManager.Instance.GetplayerByID(target.Key)).IsValid : alive;
            target.Value.interactable = selectingHunterTrap ? alive && canSetTrap : validTarget && canUse;
        }
        // Luôn cho mở danh sách lobby; chỉ thực thi action khi CanUse hợp lệ.
        useButton.interactable = player != null && player.isAlive && HasActiveAbility(player);
        ShamanRole shaman = GetLocalShamanRole();
        bool canUseShamanCharm = player != null && player.isAlive && !player.HasEffect(StatusEffectType.Silenced) && !player.hasUseNightAction &&
                                 game != null && game.currentState == GameState.Night;
        shamanGoodCharmButton.interactable = canUseShamanCharm && shaman != null && shaman.HasGoodCharm;
        shamanBadCharmButton.interactable = canUseShamanCharm && shaman != null && shaman.HasBadCharm;
        hunterTrapButton.gameObject.SetActive(player != null && player.roleType == RoleType.Hunter &&
                             player.hasHunterTrap);
        hunterTrapButton.interactable = CanSetHunterTrap(player, game);
        if (window.activeSelf && abilityView.activeSelf)
            statusLabel.text = !string.IsNullOrEmpty(actionFeedback) ? actionFeedback : reason;
    }

    private void ShowWindow(bool visible)
    {
        if (window == null) return;
        window.SetActive(visible);
        if (!visible) CancelTargets();
    }

    private void OpenTargets()
    {
        PlayerData player = PlayerManager.Instance?.GetplayerByID(localPlayerID);
        bool canSetHunterTrap = CanSetHunterTrap(player, GameRoleManager.Instance);
        if (player != null && !player.isAlive && !canSetHunterTrap)
        {
            actionFeedback = "Bạn đã bị loại và không thể dùng kỹ năng.";
            CancelTargets();
            return;
        }
        if (player == null || (!HasActiveAbility(player) && !canSetHunterTrap))
        {
            actionFeedback = "Chưa có kỹ năng chủ động để chọn mục tiêu.";
            return;
        }

        foreach (Button button in targetButtons.Values)
        {
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject);
            else DestroyImmediate(button.gameObject);
        }
        targetButtons.Clear();
        List<PlayerData> lobby = PlayerManager.Instance?.players;
        if (lobby == null)
        {
            actionFeedback = "Lobby chưa có danh sách người chơi.";
            return;
        }

        int lobbyCount = 0;
        int count = 0;
        foreach (PlayerData target in lobby)
        {
            if (target != null) lobbyCount++;
            if (target == null) continue;
            bool allowed = RoleManager.UsesConfiguredAbility(player) && !selectingHunterTrap
                ? TargetValidator.Validate(player, target, player.roleDefinition).IsValid
                : target.isAlive && target.playerID != localPlayerID;
            if (!allowed)
                continue;
            int targetID = target.playerID;
            string name = $"{PlayerName(target)}  /  ID {targetID}";
            Button button = ActionButton("Target " + targetID, targetContent,
                name, Vector2.zero, Vector2.one, "#2F5962", () => SelectTarget(targetID));
            button.GetComponentInChildren<TMP_Text>().fontSize = 17;
            targetButtons[targetID] = button;
            count++;
        }

        targetTitleLabel.text = $"MỤC TIÊU / LOBBY {lobbyCount} NGƯỜI";
        bool canUse = CanUse(player, GameRoleManager.Instance, out string reason);
        targetFeedbackLabel.text = count == 0 ? "Không có mục tiêu còn sống hợp lệ." :
            canUse ? $"{count} người chơi có thể chọn." : reason;
        abilityView.SetActive(false);
        resultView.SetActive(false);
        targetView.SetActive(true);
        LayoutRebuilder.MarkLayoutForRebuild(targetContent);
    }

    private static bool CanSetHunterTrap(PlayerData player, GameRoleManager game)
    {
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

    private void OpenShamanTargets(bool goodCharm)
    {
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
        if (selectingHunterTrap)
        {
            if (!CanSetHunterTrap(player, GameRoleManager.Instance))
            {
                targetFeedbackLabel.text = "Hiện chưa thể đặt bẫy.";
                return;
            }

            HunterRole hunter = GetLocalHunterRole();
            if (hunter == null)
            {
                targetFeedbackLabel.text = "Không tìm thấy vai trò Thợ Săn.";
                return;
            }

            if (!hunter.TrySetTrap(targetID, out string trapFeedback))
            {
                targetFeedbackLabel.text = trapFeedback;
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

        if (!CanUse(player, GameRoleManager.Instance, out string reason))
        {
            targetFeedbackLabel.text = reason;
            return;
        }

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
            targetFeedbackLabel.text = feedback;
            return;
        }

        actionFeedback = feedback;
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        resultFeedbackLabel.text = target == null ? feedback :
            $"{feedback}\nMục tiêu: {PlayerName(target)} (ID {targetID}).";
        targetView.SetActive(false);
        abilityView.SetActive(false);
        resultView.SetActive(true);
        Debug.Log("[Role Ability] " + feedback);
    }

    private static string DisplayRoleName(PlayerData player) =>
        player.roleDefinition != null && !string.IsNullOrWhiteSpace(player.roleDefinition.displayName)
            ? player.roleDefinition.displayName : RoleName(player.roleType);
    private static bool HasActiveAbility(PlayerData player) => RoleManager.UsesConfiguredAbility(player)
        ? player.roleDefinition.abilityType != AbilityType.None : HasActiveAbility(player.roleType);
    private void HandleAbilityResolved(AbilityResolution result)
    {
        if (result.Actor == null || result.Actor.playerID != localPlayerID) return;
        actionFeedback = result.Message;
        if (resultFeedbackLabel != null) resultFeedbackLabel.text = result.Message;
    }

    private void CancelTargets()
    {
        selectingHunterTrap = false;
        if (targetView != null) targetView.SetActive(false);
        if (resultView != null) resultView.SetActive(false);
        if (abilityView != null) abilityView.SetActive(true);
    }
}
