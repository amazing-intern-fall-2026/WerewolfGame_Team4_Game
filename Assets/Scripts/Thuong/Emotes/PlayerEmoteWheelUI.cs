using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Thuong.Online;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Thuong.Emotes
{
[DefaultExecutionOrder(-200)]
public sealed class PlayerEmoteWheelUI : MonoBehaviour
{
    private sealed class Bubble
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public PlayerEmoteGraphic Face;
        public TMP_Text Label;
        public SpriteRenderer Sprite;
    }

    private static PlayerEmoteWheelUI instance;
    public static bool IsOpen => instance != null && instance.wheelRoot.activeSelf;

    private readonly Dictionary<int, Bubble> bubbles = new Dictionary<int, Bubble>();
    private readonly List<int> expiredPlayers = new List<int>();
    private ThuongOnlineSession session;
    private RectTransform canvasRect, wheel;
    private GameObject wheelRoot;
    private PlayerEmoteGraphic ring, preview;
    private readonly PlayerEmoteGraphic[] faces = new PlayerEmoteGraphic[PlayerEmoteCatalog.Count];
    private TMP_Text selectedLabel, hint, controls;
    private TMP_FontAsset font;
    private int selection;
    private GamePhase openedPhase;
    private uint openedEpoch;
    private Vector2 lastMousePosition;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public static void Ensure(ThuongOnlineSession player)
    {
        if (instance == null)
        {
            var root = new GameObject("Player emotes", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, player.EmoteScene);
            instance = root.AddComponent<PlayerEmoteWheelUI>();
        }
        if (instance.session != player)
        {
            instance.CloseWheel(false);
            instance.session = player;
        }
    }

    public static bool IsOpenFor(ThuongOnlineSession player) => IsOpen && instance.session == player;

    public static void DisposeFor(ThuongOnlineSession player)
    {
        if (instance == null || instance.session != player) return;
        instance.CloseWheel(false);
        Destroy(instance.gameObject);
    }

    public static void CancelFor(ThuongOnlineSession player)
    {
        if (instance == null || instance.session != player) return;
        instance.CloseWheel(false);
        foreach (var bubble in instance.bubbles.Values) Destroy(bubble.Root.gameObject);
        instance.bubbles.Clear();
    }

    private void Awake()
    {
        instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        canvasRect = (RectTransform)transform;
        font = ThuongOnlineSession.Active != null ? ThuongOnlineSession.Active.EmoteFont : TMP_Settings.defaultFontAsset;
        BuildWheel();
        hint = Label("Keyboard hint", canvasRect, "", new Vector2(0, 30), new Vector2(380, 30), 17);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 0);
        hint.color = new Color32(248, 225, 172, 255);
    }

    private void BuildWheel()
    {
        RectTransform shade = Rect("Wheel modal", canvasRect, Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        var background = shade.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, .045f, .065f, .65f);
        wheelRoot = shade.gameObject;
        wheel = Rect("Emote wheel", shade, new Vector2(0, 12), new Vector2(440, 440));
        ring = wheel.gameObject.AddComponent<PlayerEmoteGraphic>();
        ring.Kind = PlayerEmoteGraphic.Shape.Wheel;
        ring.raycastTarget = false;
        for (int id = 1; id <= PlayerEmoteCatalog.Count; id++)
        {
            Vector2 direction = PlayerEmoteCatalog.Direction(id);
            Vector2 center = direction * 138;
            faces[id - 1] = Face("Emote " + id, wheel, center + new Vector2(0, 12), 57, id);
            var label = Label("Label " + id, wheel, PlayerEmoteCatalog.Label(id), center + new Vector2(0, -33), new Vector2(110, 26), 15);
            label.color = new Color32(236, 234, 219, 255);
        }
        preview = Face("Selection preview", wheel, new Vector2(0, 13), 45, 1);
        selectedLabel = Label("Selection", wheel, "Chọn cảm xúc", new Vector2(0, -27), new Vector2(132, 30), 15);
        Label("Title", shade, "BIỂU CẢM", new Vector2(0, 286), new Vector2(400, 38), 27).characterSpacing = 4;
        controls = Label("Controls", shade, "", new Vector2(0, -255), new Vector2(720, 30), 18);
        Label("Cancel hint", shade, "Rê chuột / phím hướng để chọn  ·  Esc hoặc trở về tâm để hủy",
            new Vector2(0, -286), new Vector2(820, 28), 15).color = new Color32(180, 190, 190, 255);
        wheelRoot.SetActive(false);
    }

    private void Update()
    {
        if (session == null || !session.IsEmoteSessionActive)
        {
            CloseWheel(false);
            hint.gameObject.SetActive(false);
            return;
        }

        bool canUse = session.CanExpressEmote;
        hint.gameObject.SetActive(canUse && !IsOpen && !IsTyping());
        hint.text = session.EmoteCooldownRemaining > 0
            ? $"Biểu cảm · {session.EmoteCooldownRemaining:0.0}s"
            : $"Giữ [{session.EmoteKey}] · Biểu cảm";

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) { CloseWheel(false); return; }
        var key = keyboard[session.EmoteKey];
        if (IsOpen)
        {
            if (!canUse || !Application.isFocused || IsTyping() || HasMinigame() ||
                session.EmotePhase != openedPhase || session.EmoteEpoch != openedEpoch || keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseWheel(false);
                return;
            }
            UpdateSelection(keyboard);
            if (key.wasReleasedThisFrame) CloseWheel(true);
            else if (!key.isPressed) CloseWheel(false);
        }
        else if (canUse && Application.isFocused && key.wasPressedThisFrame &&
                 session.EmoteCooldownRemaining <= 0 && !IsTyping() && !HasMinigame())
        {
            OpenWheel();
        }
    }

    private static bool IsTyping()
    {
        if (TouchScreenKeyboard.visible) return true;
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        TMP_InputField tmp = selected.GetComponentInParent<TMP_InputField>();
        InputField legacy = selected.GetComponentInParent<InputField>();
        return tmp != null && tmp.isFocused || legacy != null && legacy.isFocused;
    }

    private static bool HasMinigame() => FindFirstObjectByType<Thuong.Minigames.ThuongMinigameWindow>() != null;

    private void OpenWheel()
    {
        openedPhase = session != null ? session.EmotePhase : GamePhase.Task;
        openedEpoch = session != null ? session.EmoteEpoch : 0;
        session?.PauseForEmoteWheel();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        lastMousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        wheelRoot.SetActive(true);
        if (hint != null) hint.gameObject.SetActive(false);
        wheelRoot.transform.SetAsLastSibling();
        controls.text = $"Giữ [{(session != null ? session.EmoteKey : Key.T)}] để chọn · Thả phím để gửi";
        SetSelection(0);
    }

    private void UpdateSelection(Keyboard keyboard)
    {
        Vector2 direction = new Vector2(
            (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0) -
            (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0),
            (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1 : 0) -
            (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1 : 0));

        if (Mouse.current != null)
        {
            Vector2 position = Mouse.current.position.ReadValue();
            if ((position - lastMousePosition).sqrMagnitude > .25f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel, position, null, out Vector2 offset);
                SetSelection(PlayerEmoteCatalog.Select(offset));
                lastMousePosition = position;
            }
        }
        if (direction != Vector2.zero) SetSelection(PlayerEmoteCatalog.Select(direction, 0));
    }

    private void SetSelection(int id)
    {
        selection = id;
        ring.Selected = id;
        preview.gameObject.SetActive(id > 0);
        preview.EmoteId = id;
        selectedLabel.text = id > 0 ? PlayerEmoteCatalog.Label(id) : "Chọn cảm xúc";
        for (int i = 0; i < faces.Length; i++)
        {
            faces[i].rectTransform.localScale = Vector3.one * (id == i + 1 ? 1.12f : 1f);
            faces[i].color = id == 0 || id == i + 1 ? Color.white : new Color(.78f, .78f, .78f, 1);
        }
    }

    private void CloseWheel(bool send)
    {
        if (wheelRoot == null || !wheelRoot.activeSelf) return;
        int chosen = selection;
        wheelRoot.SetActive(false);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        selection = 0;
        if (send && session != null && chosen > 0) session.TrySendEmote(chosen);
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        expiredPlayers.Clear();
        foreach (var pair in bubbles)
        {
            if (session == null || !session.HasEmoteAvatar(pair.Key)) expiredPlayers.Add(pair.Key);
            else pair.Value.Root.gameObject.SetActive(false);
        }
        foreach (var player in expiredPlayers)
        {
            Destroy(bubbles[player].Root.gameObject);
            bubbles.Remove(player);
        }
        if (camera == null || session == null) return;

        foreach (ThuongEmoteView player in session.VisibleEmotes)
        {
            if (!bubbles.TryGetValue(player.PlayerId, out Bubble bubble))
            {
                bubble = CreateBubble(player);
                bubbles.Add(player.PlayerId, bubble);
            }
            Vector3 world = player.Anchor.position + Vector3.up * 1.3f;
            if (bubble.Sprite != null) world.y = bubble.Sprite.bounds.max.y + .9f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 position);
            bubble.Root.anchoredPosition = position;
            bubble.Root.gameObject.SetActive(true);
            int id = player.Id;
            bubble.Face.EmoteId = id;
            bubble.Label.text = PlayerEmoteCatalog.Label(id);
            float remaining = (float)(player.ExpiresAt - session.EmoteTime);
            float age = PlayerEmoteCatalog.DisplaySeconds - remaining;
            bubble.Group.alpha = Mathf.Clamp01(remaining / .35f);
            bubble.Root.localScale = Vector3.one * Mathf.Lerp(.65f, 1f, Mathf.Clamp01(age / .12f));
        }
    }

    private Bubble CreateBubble(ThuongEmoteView player)
    {
        RectTransform root = Rect("Emote bubble " + player.PlayerId, canvasRect, Vector2.zero, new Vector2(100, 100));
        root.SetAsFirstSibling();
        RectTransform plate = Rect("Speech bubble", root, Vector2.zero, new Vector2(66, 66));
        var graphic = plate.gameObject.AddComponent<PlayerEmoteGraphic>();
        graphic.Kind = PlayerEmoteGraphic.Shape.Bubble;
        graphic.raycastTarget = false;
        var label = Label("Emotion label", root, "", new Vector2(0, 48), new Vector2(130, 25), 15);
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = .22f;
        label.outlineColor = new Color32(18, 26, 31, 255);
        return new Bubble
        {
            Root = root,
            Group = root.gameObject.AddComponent<CanvasGroup>(),
            Face = Face("Face", root, new Vector2(0, 1), 49, 1),
            Label = label,
            Sprite = player.Anchor.GetComponentInChildren<SpriteRenderer>()
        };
    }

    private void OnApplicationFocus(bool focused) { if (!focused) CloseWheel(false); }
    private void OnDisable() => CloseWheel(false);
    private void OnDestroy()
    {
        CloseWheel(false);
        if (instance == this) instance = null;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private PlayerEmoteGraphic Face(string name, Transform parent, Vector2 position, float size, int id)
    {
        var graphic = Rect(name, parent, position, new Vector2(size, size)).gameObject.AddComponent<PlayerEmoteGraphic>();
        graphic.raycastTarget = false;
        graphic.EmoteId = id;
        return graphic;
    }

    private TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(250, 238, 208, 255);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}

}
