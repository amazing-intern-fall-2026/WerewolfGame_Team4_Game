using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Built on the existing HUD so the scene needs no additional Inspector wiring.
public sealed class PlayerRosterUI : MonoBehaviour
{
    private sealed class Row
    {
        public int playerID;
        public Text name;
        public Text state;
        public Image background;
    }

    private GameHUD hud;
    private PlayerManager players;
    private GameObject window;
    private Button openButton;
    private Text summary;
    private RectTransform content;
    private readonly List<Row> rows = new List<Row>();

    public void Initialize(GameHUD gameHUD)
    {
        if (window != null) return;
        hud = gameHUD;
        RectTransform root = gameHUD.GetComponent<RectTransform>();
        if (root == null) return;

        openButton = CreateButton("Player Roster Button", root, "PLAYERS",
            new Vector2(.015f, .015f), new Vector2(.235f, .075f));
        summary = openButton.GetComponentInChildren<Text>();
        openButton.onClick.AddListener(() =>
        {
            window.SetActive(!window.activeSelf);
            if (window.activeSelf) window.transform.SetAsLastSibling();
            Refresh();
        });

        window = Panel("Player Roster Window", root, Vector2.zero, Vector2.one,
            new Color(0f, 0f, 0f, .65f)).gameObject;
        var overlay = window.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 40;
        window.AddComponent<GraphicRaycaster>();
        var panel = Panel("Player Roster Panel", window.transform,
            new Vector2(.27f, .3f), new Vector2(.73f, .84f), new Color(.09f, .12f, .13f));
        Label("Title", panel, "PLAYERS", new Vector2(.05f, .88f),
            new Vector2(.7f, .98f), TextAnchor.MiddleLeft, 24);
        var close = CreateButton("Close", panel, "X", new Vector2(.88f, .9f), new Vector2(.97f, .98f));
        close.onClick.AddListener(() => window.SetActive(false));

        var viewport = Panel("Viewport", panel, new Vector2(.04f, .04f),
            new Vector2(.96f, .86f), new Color(.12f, .16f, .17f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var contentObject = new GameObject("Content", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        window.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (window == null) return;
        bool ended = GameRoleManager.Instance != null &&
            GameRoleManager.Instance.currentState == GameState.GameOver;
        openButton.gameObject.SetActive(!ended);
        if (ended) window.SetActive(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (players != null) players.AliveStateChanged -= OnAliveStateChanged;
        if (window != null) Release(window);
        if (openButton != null) Release(openButton.gameObject);
    }

    private void OnAliveStateChanged(PlayerData player) => Refresh();

    public void Refresh()
    {
        if (content == null) return;
        if (players != PlayerManager.Instance)
        {
            if (players != null) players.AliveStateChanged -= OnAliveStateChanged;
            players = PlayerManager.Instance;
            if (players != null) players.AliveStateChanged += OnAliveStateChanged;
        }

        var lobby = players != null ? players.players : null;
        bool changed = false;
        int count = 0;
        if (lobby != null)
            foreach (PlayerData player in lobby)
            {
                if (player == null) continue;
                if (count >= rows.Count || rows[count].playerID != player.playerID) changed = true;
                count++;
            }
        if (changed || count != rows.Count) Rebuild(lobby);

        int alive = 0;
        foreach (Row row in rows)
        {
            PlayerData player = players.GetplayerByID(row.playerID);
            if (player == null) continue;
            if (player.isAlive) alive++;
            string name = string.IsNullOrWhiteSpace(player.playerName) ||
                player.playerName == "Player " + player.playerID
                ? "PLAYER " + (player.playerID + 1) : player.playerName;
            if (hud != null && player.playerID == hud.localVoterID) name += " / YOU";
            if (row.name.text != name) row.name.text = name;
            string state = player.isAlive ? "ALIVE" : "DEAD";
            if (row.state.text != state) row.state.text = state;
            row.state.color = player.isAlive ? new Color(.5f, .9f, .67f) : new Color(1f, .55f, .55f);
            row.background.color = player.isAlive ? new Color(.16f, .21f, .22f) : new Color(.2f, .16f, .17f);
        }
        string value = $"PLAYERS  {alive} ALIVE / {rows.Count - alive} DEAD";
        if (summary.text != value) summary.text = value;
    }

    private void Rebuild(List<PlayerData> lobby)
    {
        foreach (Row row in rows)
        {
            row.background.gameObject.SetActive(false);
            Release(row.background.gameObject);
        }
        rows.Clear();
        if (lobby == null) return;
        foreach (PlayerData player in lobby)
        {
            if (player == null) continue;
            var rect = Panel("Roster Player " + player.playerID, content, Vector2.zero, Vector2.one, Color.clear);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 44f;
            rows.Add(new Row
            {
                playerID = player.playerID,
                background = rect.GetComponent<Image>(),
                name = Label("Name", rect, "", new Vector2(.03f, 0f), new Vector2(.73f, 1f), TextAnchor.MiddleLeft, 20),
                state = Label("State", rect, "", new Vector2(.76f, 0f), new Vector2(.97f, 1f), TextAnchor.MiddleRight, 20)
            });
        }
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static void Release(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private static Text Label(string name, Transform parent, string value, Vector2 min,
        Vector2 max, TextAnchor alignment, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string value, Vector2 min, Vector2 max)
    {
        var rect = Panel(name, parent, min, max, new Color(.18f, .35f, .31f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        Label("Label", rect, value, new Vector2(.04f, .02f), new Vector2(.96f, .98f), TextAnchor.MiddleCenter, 18);
        return button;
    }
}
