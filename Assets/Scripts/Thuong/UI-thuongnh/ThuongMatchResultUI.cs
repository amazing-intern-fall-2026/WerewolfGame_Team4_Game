using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Both offline flows show a frozen result captured after the complete resolution.
public sealed class ThuongMatchResultUI : MonoBehaviour
{
    private GameObject panel;
    private TMP_Text title, roster;
    private GameMatchSummary displayed;
    private Action replay;
    public void Initialize(TMP_FontAsset font, Action restart)
    {
        replay = restart;
        if (panel != null) return;
        panel = new GameObject("Full match result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(panel, gameObject.scene);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = 3000;
        var scaler = panel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var background = Box("Result panel", panel.transform, Vector2.zero, Vector2.one, new Color(.04f, .08f, .1f, .98f));
        title = Label("Winner", background, font, new Vector2(.06f, .84f), new Vector2(.94f, .96f), 38);
        var hint = Label("Heading", background, font, new Vector2(.06f, .77f), new Vector2(.94f, .84f), 23);
        hint.text = "KẾT QUẢ TỪNG NGƯỜI CHƠI / VAI TRÒ VÀ NGUYÊN NHÂN BỊ LOẠI";
        var viewport = Box("Scrollable results", background, new Vector2(.06f, .16f), new Vector2(.94f, .76f), new Color(.09f, .16f, .19f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        roster = Label("Players", viewport, font, new Vector2(0, 1), Vector2.one, 24);
        roster.rectTransform.pivot = new Vector2(.5f, 1);
        roster.margin = new Vector4(20, 12, 20, 12);
        roster.alignment = TextAlignmentOptions.TopLeft;
        roster.textWrappingMode = TextWrappingModes.Normal;
        roster.enableAutoSizing = false;
        roster.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = roster.rectTransform; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        var buttonRect = Box("Play again", background, new Vector2(.36f, .04f), new Vector2(.64f, .12f), new Color(.16f, .43f, .4f));
        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonRect.GetComponent<Image>();
        button.onClick.AddListener(Replay);
        Label("Label", buttonRect, font, Vector2.zero, Vector2.one, 25).text = "CHƠI LẠI";
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panel == null) return;
        if (Application.isPlaying) Destroy(panel);
        else DestroyImmediate(panel);
    }

    private void Update()
    {
        if (panel == null) return;
        var game = GameRoleManager.Instance;
        bool visible = ThuongLogicSceneScope.IsActive && Thuong.Online.ThuongOnlineSession.Active == null &&
            game != null && game.currentState == GameState.GameOver && game.Result != null;
        panel.SetActive(visible);
        if (!visible || displayed == game.Result) return;
        displayed = game.Result;
        title.text = WinnerLabel(displayed.Winner) + $" / NGÀY {displayed.Day}";
        var text = new StringBuilder();
        foreach (var player in displayed.Players)
        {
            text.AppendLine($"P{player.PlayerID + 1:00}  {player.Name}  |  {player.RoleName}  |  {FactionLabel(player.Faction)}");
            text.AppendLine(player.Alive ? "CÒN SỐNG\n" :
                $"ĐÃ BỊ LOẠI / {(player.HasDeathRecord ? CauseLabel(player.DeathCause) : "Không có ghi nhận")}\n");
        }
        roster.text = text.ToString();
    }

    private void Replay()
    {
        if (replay != null) { replay(); return; }
        var scene = gameObject.scene;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.path);
#endif
    }
    public static string WinnerLabel(string winner) => winner switch
    {
        "Villagers" => "DÂN LÀNG CHIẾN THẮNG", "Werewolves" => "MA SÓI CHIẾN THẮNG",
        "Lovers" => "CẶP ĐÔI CHIẾN THẮNG", "Killer" => "SÁT NHÂN CHIẾN THẮNG",
        "White Wolf" => "BẠCH KHUYỂN CHIẾN THẮNG", "Madman" => "KẺ ĐIÊN CHIẾN THẮNG",
        "Fox Spirit" => "HỒ LY CHIẾN THẮNG", "Draw" => "VÁN ĐẤU HÒA", _ => winner + " CHIẾN THẮNG"
    };
    private static string FactionLabel(FactionType faction) => faction switch
    { FactionType.Villager => "Dân làng", FactionType.Monster => "Ma Sói", _ => "Phe thứ ba" };
    private static string CauseLabel(DeathCause cause) => cause switch
    {
        DeathCause.Vote => "Bỏ phiếu", DeathCause.Monster => "Sói tấn công", DeathCause.Curse => "Lời nguyền",
        DeathCause.Ability => "Kỹ năng", DeathCause.Killer => "Sát nhân", DeathCause.Lover => "Người yêu chết",
        DeathCause.Trap => "Bẫy Thợ Săn", DeathCause.Poison => "Trúng độc", _ => cause.ToString()
    };
    private static RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = color; return rect;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, int size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = root.GetComponent<TMP_Text>(); if (font != null) label.font = font;
        label.fontSize = size; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.richText = false;
        label.enableAutoSizing = true; label.fontSizeMin = 15; label.fontSizeMax = size; return label;
    }
}
