using TMPro;
using UnityEngine;

public sealed class PlayerNameTag : MonoBehaviour
{
    private static readonly Vector2 TextRectSize = new Vector2(3f, 0.5f);
    private static readonly Vector3 TextLocalPosition = new Vector3(0f, 1.1f, 0f);
    // Compensate for the Player root scale (0.5, 0.65) to match the effective
    // text scale used by the scene's existing task labels.
    private static readonly Vector3 TextLocalScale = new Vector3(2f, 1.54f, 1f);

    [SerializeField] private TMP_Text nameText;

    private void Awake()
    {
        FindNameText();
    }

    /// <summary>
    /// Reuses a 3D TMP label on the player, or creates one if needed.
    /// The prototype uses 3D TextMeshPro for its other world-space labels too.
    /// </summary>
    public static PlayerNameTag AttachOrCreate(Transform playerRoot, PlayerData player)
    {
        if (playerRoot == null)
            return null;

        PlayerNameTag tag = FindWorldTextTag(playerRoot);
        if (tag == null)
            tag = CreateFor(playerRoot);

        tag.ConfigureVisuals(playerRoot);
        tag.SetPlayer(player);
        return tag;
    }

    private static PlayerNameTag FindWorldTextTag(Transform playerRoot)
    {
        PlayerNameTag[] tags = playerRoot.GetComponentsInChildren<PlayerNameTag>(true);
        foreach (PlayerNameTag candidate in tags)
        {
            candidate.FindNameText();
            if (candidate.nameText is TextMeshPro)
                return candidate;
        }

        // Remove an older UGUI/Canvas version if one is still in the open scene.
        foreach (PlayerNameTag oldTag in tags)
        {
            Canvas oldCanvas = oldTag.GetComponent<Canvas>();
            if (oldCanvas != null && oldCanvas.transform != playerRoot)
            {
                oldCanvas.gameObject.SetActive(false);
                Destroy(oldCanvas.gameObject);
            }
            else if (oldTag.nameText != null)
            {
                oldTag.nameText.gameObject.SetActive(false);
            }

            if (oldTag.transform == playerRoot)
                Destroy(oldTag);
            else
                Destroy(oldTag.gameObject);
        }

        return null;
    }

    private static PlayerNameTag CreateFor(Transform playerRoot)
    {
        GameObject labelObject = new GameObject(
            "PlayerNameLabel",
            typeof(RectTransform),
            typeof(TextMeshPro),
            typeof(PlayerNameTag));
        labelObject.transform.SetParent(playerRoot, false);

        PlayerNameTag tag = labelObject.GetComponent<PlayerNameTag>();
        tag.nameText = labelObject.GetComponent<TextMeshPro>();
        return tag;
    }

    private void FindNameText()
    {
        if (nameText == null)
            nameText = GetComponent<TMP_Text>();

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ConfigureVisuals(Transform playerRoot)
    {
        FindNameText();
        if (nameText == null)
        {
            Debug.LogError("[PlayerNameTag] Không tìm thấy TextMeshPro 3D.", this);
            return;
        }

        // Keep a single world-space mesh label attached to this player.
        RectTransform textRect = nameText.rectTransform;
        textRect.sizeDelta = TextRectSize;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.localPosition = TextLocalPosition;
        textRect.localScale = TextLocalScale;

        nameText.fontSize = 2.3f;
        nameText.enableAutoSizing = false;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color32(240, 247, 245, 255);
        nameText.raycastTarget = false;

        // Match the known-good 3D TMP labels already visible in this scene.
        GameHUD hud = FindAnyObjectByType<GameHUD>();
        if (hud != null && hud.phaseText != null && hud.phaseText.font != null)
            nameText.font = hud.phaseText.font;

        if (nameText.font == null)
            Debug.LogError("[PlayerNameTag] Chưa có TMP Font Asset để render tên.", this);

        Renderer textRenderer = nameText.GetComponent<Renderer>();
        SpriteRenderer playerRenderer = playerRoot.GetComponent<SpriteRenderer>();
        if (textRenderer != null)
        {
            if (playerRenderer != null)
            {
                textRenderer.sortingLayerID = playerRenderer.sortingLayerID;
                textRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
            }
            else
            {
                textRenderer.sortingOrder = 20;
            }
        }
    }

    public void SetPlayer(PlayerData player)
    {
        FindNameText();
        if (nameText == null)
        {
            Debug.LogError("[PlayerNameTag] Chưa tạo được tên player.", this);
            return;
        }

        if (player == null)
        {
            nameText.text = "Unknown";
            return;
        }

        // PlayerManager stores generated names as Player 0, Player 1, etc.
        // Display those as Player 1, Player 2 while preserving custom names.
        string generatedName = "Player " + player.playerID;
        nameText.text = string.IsNullOrWhiteSpace(player.playerName) ||
                        player.playerName == generatedName
            ? "Player " + (player.playerID + 1)
            : player.playerName;
    }
}
