using System.IO;
using Assets.Scripts.Thuong;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ThuongPrototypeBuilder
{
    public const string Root = "Assets/Scenes/Thuong/Prototype";
    public const string ScenePath = Root + "/ThuongGameplayPrototype.unity";
    private static TMP_FontAsset font;
    private static Sprite sprite;
    private static Material worldMaterial;

    public static void UpgradeDailyProgress()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Art/PrototypeFont.asset");
        var hud = Object.FindAnyObjectByType<GameHUD>();
        var sidebar = hud.transform.Find("TaskPanel");
        if (hud.dailyProgressSlider == null) AddDailyProgress(hud, sidebar);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("THUONG_DAILY_PROGRESS_ADDED");
    }
    private static void AddDailyProgress(GameHUD hud, Transform sidebar)
    {
        var rect = Panel("DailyProgressSlider", sidebar, new Vector2(.06f,.77f), new Vector2(.94f,.8f), Hex("#344D56"));
        var fill = Panel("Fill", rect, Vector2.zero, Vector2.one, Hex("#F5CB77"));
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill; slider.targetGraphic = fill.GetComponent<Image>();
        slider.maxValue = 100; slider.interactable = false;
        hud.dailyProgressSlider = slider;
        hud.dailyProgressText = Text("DailyProgressText", sidebar, "Trong ngày: 0/4 — 0%",
            new Vector2(.06f,.81f), new Vector2(.95f,.89f), 22, Hex("#F5CB77"));
        hud.taskListText.rectTransform.anchorMin = new Vector2(.06f,.37f);
        hud.taskListText.rectTransform.anchorMax = new Vector2(.96f,.74f);
        hud.taskListText.alignment = TextAlignmentOptions.TopLeft;
        hud.transform.Find("TopBar/Title").GetComponent<TMP_Text>().text = "TỔNG TIẾN ĐỘ NHIỆM VỤ / TỐI ĐA 7 NGÀY";
    }

    [MenuItem("Tools/Thuong/Create Prototype Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath))
            throw new System.InvalidOperationException("Prototype already exists. Open it instead of overwriting.");
        Directory.CreateDirectory(Root + "/Data");
        Directory.CreateDirectory(Root + "/Art");
        AssetDatabase.Refresh();
        var texture = new Texture2D(16, 16);
        var pixels = new Color[256];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(Root + "/Art/Block.png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(Root + "/Art/Block.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Art/Block.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Block.png");
        worldMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        AssetDatabase.CreateAsset(worldMaterial, Root + "/Art/World.mat");
        font = TMP_FontAsset.CreateFontAsset(
            AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
        AssetDatabase.CreateAsset(font, Root + "/Art/PrototypeFont.asset");
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        font.TryAddCharacters("NHIỆM VỤ NGÀY DÂN LÀNG THẮNG MA SÓI THẢO LUẬN BỎ PHIẾU ĐÊM Lấy nước Thu hoạch Sửa rào Gom củi Dọn kho Thắp đèn Bạn tiến độ còn lại", out _);
        EditorUtility.SetDirty(font);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6.7f;
        camera.transform.position = new Vector3(-1.8f, 0, -10);
        camera.backgroundColor = Hex("#101E26"); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<AudioListener>();
        var map = new GameObject("Map").transform;
        Block("Ground", map, new Vector2(1, 0), new Vector2(15, 9), Hex("#203E42"), 0);
        Block("Path Horizontal", map, new Vector2(1, 0), new Vector2(14.5f, 1.2f), Hex("#37534E"), 1);
        Block("Path Vertical", map, new Vector2(1, 0), new Vector2(1.2f, 8.5f), Hex("#37534E"), 1);
        Wall(map, new Vector2(1, 4.6f), new Vector2(15.4f, .3f));
        Wall(map, new Vector2(1, -4.6f), new Vector2(15.4f, .3f));
        Wall(map, new Vector2(-6.6f, 0), new Vector2(.3f, 9.2f));
        Wall(map, new Vector2(8.6f, 0), new Vector2(.3f, 9.2f));
        var player = Block("Player", null, Vector2.zero, new Vector2(.5f, .65f), Hex("#8EE9E2"), 10);
        player.AddComponent<BoxCollider2D>();
        var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        player.AddComponent<PlayerMovement>().playerID = 0;
        player.AddComponent<PrototypeControls>();

        var systems = new GameObject("Systems").transform;
        var game = Manager<GameRoleManager>(systems);
        game.nightDuration = 5; game.discussionDuration = 8; game.votingDuration = 10;
        Manager<PlayerManager>(systems);
        Manager<RoleManager>(systems);
        var tasks = Manager<TaskManager>(systems);
        Manager<DayTimer>(systems).dayDuration = 45;
        Manager<NightManager>(systems); Manager<VoteManager>(systems);
        Manager<DeathResolver>(systems); Manager<WinConditionManager>(systems);

        string[] names = { "Lấy nước", "Thu hoạch", "Sửa rào", "Gom củi", "Dọn kho", "Thắp đèn" };
        string[] labels = { "01 / WELL", "02 / FARM", "03 / FENCE", "04 / WOOD", "05 / STORE", "06 / LAMP" };
        Vector2[] positions = { new Vector2(-4, 2.4f), new Vector2(1, 2.4f), new Vector2(6, 2.4f),
            new Vector2(-4,-2.4f), new Vector2(1,-2.4f), new Vector2(6,-2.4f) };
        var stations = new GameObject("TaskStations").transform;
        for (int i = 0; i < names.Length; i++)
        {
            var task = ScriptableObject.CreateInstance<TaskData>();
            task.taskName = names[i]; task.description = "Đến gần điểm sáng và nhấn E."; task.progressValue = 10;
            AssetDatabase.CreateAsset(task, Root + "/Data/Task" + (i + 1) + ".asset");
            tasks.allTasks.Add(task);
            var station = Block(labels[i], stations, positions[i], new Vector2(1.3f, .8f), Hex("#627C6B"), 3);
            // Keep the station root unscaled so range and child marker offsets use world units.
            station.transform.localScale = Vector3.one;
            station.GetComponent<SpriteRenderer>().size = new Vector2(1.3f, .8f);
            station.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Sliced;
            var marker = Block("Active Marker", station.transform, positions[i] + new Vector2(0, .9f),
                new Vector2(.22f,.22f), Hex("#F5CB77"), 4);
            var point = station.AddComponent<TaskStation>();
            point.task = task; point.player = player.transform; point.marker = marker; point.interactionRadius = 1.3f;
            var label = new GameObject("Label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(station.transform);
            label.transform.localPosition = new Vector3(0, -.8f, 0);
            label.font = font; label.text = labels[i]; label.fontSize = 2.3f;
            label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(3, .5f);
            label.GetComponent<MeshRenderer>().sortingOrder = 5;
        }

        var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var hud = canvas.AddComponent<GameHUD>();
        var top = Panel("TopBar", canvas.transform, new Vector2(.015f,.87f), new Vector2(.985f,.985f), Hex("#132A34"));
        Text("Title", top, "WEREWOLF / THUONG PROTOTYPE", new Vector2(.02f,.58f), new Vector2(.56f,.95f), 22, Hex("#8EE9E2"));
        hud.phaseText = Text("PhaseText", top, "Ngày 1", new Vector2(.66f,.48f), new Vector2(.9f,.95f), 20, Color.white);
        hud.timerText = Text("TimerText", top, "45s", new Vector2(.91f,.48f), new Vector2(.99f,.95f), 26, Hex("#F5CB77"));
        var sliderRect = Panel("ProgressSlider", top, new Vector2(.02f,.19f), new Vector2(.84f,.38f), Hex("#344D56"));
        var fill = Panel("Fill", sliderRect, Vector2.zero, Vector2.one, Hex("#8EE9E2"));
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill; slider.targetGraphic = fill.GetComponent<Image>(); slider.maxValue = 100; slider.interactable = false;
        hud.progressSlider = slider;
        hud.progressText = Text("ProgressText", top, "0%", new Vector2(.87f,.06f), new Vector2(.99f,.45f), 23, Color.white);
        var sidebar = Panel("TaskPanel", canvas.transform, new Vector2(.015f,.1f), new Vector2(.235f,.845f), Hex("#132A34"));
        Text("Section", sidebar, "01 / DAILY OBJECTIVES", new Vector2(.06f,.89f), new Vector2(.95f,.98f), 19, Hex("#F5CB77"));
        hud.taskListText = Text("TaskListText", sidebar, "NHIỆM VỤ", new Vector2(.06f,.36f), new Vector2(.96f,.86f), 25, Color.white);
        AddDailyProgress(hud, sidebar);
        Text("Instructions", sidebar, "WASD / Mũi tên: di chuyển\nE: làm nhiệm vụ ở gần\nR: chơi lại\n\nĐiểm sáng = nhiệm vụ hôm nay\nMỗi nhiệm vụ +10%\nĐạt 100% để dân làng thắng",
            new Vector2(.06f,.04f), new Vector2(.96f,.35f), 20, Hex("#ACBFC7"));
        Text("Footer", canvas.transform, "OFFLINE TEST  /  DAY 45s  →  NIGHT 5s  →  DISCUSS 8s  →  VOTE 10s",
            new Vector2(.26f,.015f), new Vector2(.985f,.075f), 20, Hex("#ACBFC7"));
        var meeting = Panel("MeetingPanel", canvas.transform, new Vector2(.3f,.19f), new Vector2(.86f,.79f), Hex("#18313E"));
        Text("Title", meeting, "HỌP LÀNG / BỎ PHIẾU", new Vector2(.06f,.82f), new Vector2(.95f,.96f), 32, Hex("#F5CB77"));
        Text("Hint", meeting, "Bạn là Player 1 (ID 0). Chọn một người khi đến Voting.",
            new Vector2(.06f,.69f), new Vector2(.95f,.81f), 20, Color.white);
        for (int i = 0; i < 5; i++)
        {
            float y = .56f - i * .105f;
            var row = Panel("Vote Player " + (i + 1), meeting, new Vector2(.08f,y), new Vector2(.92f,y+.085f), Hex("#345360"));
            row.gameObject.AddComponent<Button>().targetGraphic = row.GetComponent<Image>();
            var vote = row.gameObject.AddComponent<VoteButton>(); vote.voterID = 0; vote.targetID = i;
            Text("Name", row, "Player " + (i + 1) + (i == 0 ? " / BẠN" : ""), new Vector2(.04f,0), new Vector2(.96f,1), 22, Color.white);
        }
        hud.meetingPanel = meeting.gameObject; meeting.gameObject.SetActive(false);
        var result = Panel("ResultPanel", canvas.transform, Vector2.zero, Vector2.one, new Color(.035f,.075f,.1f,.97f));
        hud.resultText = Text("ResultText", result, "", new Vector2(.2f,.45f), new Vector2(.8f,.62f), 48, Hex("#F5CB77"));
        hud.resultText.alignment = TextAlignmentOptions.Center;
        var hint = Text("RestartHint", result, "Nhấn R để chơi lại", new Vector2(.2f,.3f), new Vector2(.8f,.43f), 27, Color.white);
        hint.alignment = TextAlignmentOptions.Center;
        hud.resultPanel = result.gameObject; result.gameObject.SetActive(false);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("THUONG_PROTOTYPE_CREATED: " + ScenePath);
    }
    private static T Manager<T>(Transform parent) where T : Component
    { var go = new GameObject(typeof(T).Name); go.transform.SetParent(parent); return go.AddComponent<T>(); }
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
    private static GameObject Block(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = pos; go.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
        renderer.sharedMaterial = worldMaterial; renderer.sortingOrder = order; return go;
    }
    private static void Wall(Transform parent, Vector2 pos, Vector2 size)
    { Block("Boundary", parent, pos, size, Hex("#56746C"), 2).AddComponent<BoxCollider2D>(); }
    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color; return rect;
    }
    private static TMP_Text Text(string name, Transform parent, string value, Vector2 min, Vector2 max, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
        text.fontSize = size; text.color = color; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft; return text;
    }
}
