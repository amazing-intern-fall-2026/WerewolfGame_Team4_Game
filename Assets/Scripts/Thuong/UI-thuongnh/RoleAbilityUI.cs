using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoleAbilityUI : MonoBehaviour
{
    [Header("Local player")]
    [SerializeField] private int localPlayerID;

    private RoleManger roleManager;
    private RectTransform abilityPanel;
    private RectTransform abilityButtonContainer;
    private RectTransform targetPanel;
    private RectTransform targetButtonContainer;
    private Text characterText;
    private Text abilityTitleText;
    private Text targetTitleText;
    private Text roleText;
    private Text statusText;
    private readonly List<Button> abilityButtons = new List<Button>();
    private bool isSelectingTarget;
    private bool uiReady;
    private int renderedPlayerID = -1;
    private RoleType renderedRole;

    public int LocalPlayerID
    {
        get => localPlayerID;
        set => localPlayerID = value;
    }

    private void Start()
    {
        EnsureUI();
        BindRoleManager();
    }

    private void OnDestroy()
    {
        if (roleManager != null)
            roleManager.RolesAssigned -= RefreshRole;
    }

    private void Update()
    {
        BindRoleManager();

        if (!uiReady)
            return;

        if (roleManager == null || !roleManager.RolesAssignedToPlayers)
            return;

        PlayerData localPlayer = PlayerManger.Instance?.GetplayerByID(localPlayerID);
        if (localPlayer == null)
            return;

        if (renderedPlayerID != localPlayer.playerID || renderedRole != localPlayer.roleType)
            RefreshRole();

        UpdateAbilityButtonState(localPlayer);
    }

    private void BindRoleManager()
    {
        if (roleManager != null || RoleManger.Instance == null)
            return;

        roleManager = RoleManger.Instance;
        roleManager.RolesAssigned += RefreshRole;
        if (roleManager.RolesAssignedToPlayers)
            RefreshRole();
    }

    private void RefreshRole()
    {
        if (!uiReady || roleManager == null || PlayerManger.Instance == null)
            return;

        PlayerData player = PlayerManger.Instance.GetplayerByID(localPlayerID);
        if (player == null)
            return;

        renderedPlayerID = player.playerID;
        renderedRole = player.roleType;
        isSelectingTarget = false;
        targetPanel.gameObject.SetActive(false);
        abilityPanel.gameObject.SetActive(true);

        ClearChildren(abilityButtonContainer);
        abilityButtons.Clear();

        characterText.text = BuildCharacterTemplate(player);
        roleText.text = "Role: " + RoleManger.GetRoleDisplayName(player.roleType);
        LocalRoleAbilityDefinition definition = LocalRoleAbilityCatalog.Get(player.roleType);
        abilityTitleText.text = definition.requiresTarget
            ? "KỸ NĂNG: " + definition.displayName
            : "KỸ NĂNG ROLE";

        if (!definition.requiresTarget)
        {
            statusText.text = definition.description;
            return;
        }

        statusText.text = definition.description;
        Button abilityButton = CreateButton(abilityButtonContainer, definition.displayName);
        abilityButton.onClick.AddListener(() => ShowTargets(definition));
        abilityButtons.Add(abilityButton);
    }

    private void ShowTargets(LocalRoleAbilityDefinition definition)
    {
        PlayerData source = PlayerManger.Instance?.GetplayerByID(localPlayerID);
        if (source == null || !roleManager.CanUseAbility(localPlayerID, definition.type))
        {
            statusText.text = "Chức năng chỉ dùng được vào ban đêm hoặc bạn đã dùng rồi.";
            return;
        }

        isSelectingTarget = true;
        abilityPanel.gameObject.SetActive(false);
        targetPanel.gameObject.SetActive(true);
        ClearChildren(targetButtonContainer);

        targetTitleText.text = "CHỌN MỤC TIÊU";
        statusText.text = "Chọn người chơi mục tiêu:";
        for (int i = 0; i < PlayerManger.Instance.players.Count; i++)
        {
            PlayerData target = PlayerManger.Instance.players[i];
            if (target == null || !target.isAlive || target.playerID == source.playerID)
                continue;

            Button targetButton = CreateButton(targetButtonContainer, target.playerName);
            int targetID = target.playerID;
            targetButton.onClick.AddListener(() => UseAbility(definition, targetID));
        }

        Button cancelButton = CreateButton(targetButtonContainer, "Hủy");
        cancelButton.onClick.AddListener(CancelTargetSelection);
    }

    private void UseAbility(LocalRoleAbilityDefinition definition, int targetID)
    {
        string result;
        bool succeeded = roleManager.TryUseAbility(localPlayerID, definition.type, targetID, out result);
        statusText.text = result;
        if (!succeeded)
            return;

        isSelectingTarget = false;
        targetPanel.gameObject.SetActive(false);
        abilityPanel.gameObject.SetActive(true);
        UpdateAbilityButtonState(PlayerManger.Instance.GetplayerByID(localPlayerID));
    }

    private void CancelTargetSelection()
    {
        isSelectingTarget = false;
        targetPanel.gameObject.SetActive(false);
        abilityPanel.gameObject.SetActive(true);
        statusText.text = "Đã hủy chọn mục tiêu.";
    }

    private static string BuildCharacterTemplate(PlayerData player)
    {
        return "PLAYER " + (player.playerID + 1) + "\n\n  O\n /|" + "\\" + "\n / " + "\\";
    }

    private void UpdateAbilityButtonState(PlayerData player)
    {
        bool canUse = player != null && !isSelectingTarget && roleManager.CanUseAbility(localPlayerID, LocalAbilityAbility(player.roleType));
        for (int i = 0; i < abilityButtons.Count; i++)
            abilityButtons[i].interactable = canUse;
    }

    private static LocalAbilityType LocalAbilityAbility(RoleType roleType)
    {
        return LocalRoleAbilityCatalog.Get(roleType).type;
    }

    private void EnsureUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("RoleAbilityCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        abilityPanel = CreatePanel(canvas.transform, "RoleAbilityPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(900f, 220f));
        characterText = CreateFullLabel(CreatePanel(abilityPanel, "CharacterPanel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(210f, 190f)), "CharacterText", "PLAYER\n\n  O\n /|\\\n / \\", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        CreateFullLabel(abilityPanel, "Connector", "-------------->", 22, TextAnchor.MiddleCenter, new Vector2(0.28f, 0f), new Vector2(0.38f, 1f));
        RectTransform abilityCard = CreatePanel(abilityPanel, "AbilityCard", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(620f, 190f));
        VerticalLayoutGroup abilityLayout = abilityCard.gameObject.AddComponent<VerticalLayoutGroup>();
        abilityLayout.padding = new RectOffset(16, 16, 12, 12);
        abilityLayout.spacing = 4f;
        abilityLayout.childControlWidth = true;
        abilityLayout.childControlHeight = true;
        abilityLayout.childForceExpandHeight = false;
        abilityTitleText = CreateLabel(abilityCard, "AbilityTitle", "KỸ NĂNG ROLE", 22);
        roleText = CreateLabel(abilityCard, "RoleText", "Role: Chưa nhận Role", 18);
        statusText = CreateLabel(abilityCard, "AbilityStatus", "Đang chờ nhận Role...", 15);
        targetPanel = CreatePanel(canvas.transform, "RoleTargetPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(460f, 330f));
        targetPanel.gameObject.SetActive(false);

        VerticalLayoutGroup targetLayout = targetPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        targetLayout.padding = new RectOffset(16, 16, 12, 12);
        targetLayout.spacing = 6f;
        targetLayout.childControlWidth = true;
        targetLayout.childControlHeight = true;
        targetLayout.childForceExpandHeight = false;
        targetTitleText = CreateLabel(targetPanel, "TargetTitle", "CHỌN MỤC TIÊU", 22);
        abilityButtonContainer = CreateContentPanel(abilityCard, "AbilityButtons");
        targetButtonContainer = CreateContentPanel(targetPanel, "TargetButtons");
        uiReady = true;
    }

    private static RectTransform CreatePanel(Transform parent, string objectName, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = panelObject.AddComponent<Image>();
        image.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        return rect;
    }

    private static RectTransform CreateContentPanel(Transform parent, string objectName)
    {
        GameObject container = new GameObject(objectName);
        container.transform.SetParent(parent, false);
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);
        VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        LayoutElement size = container.AddComponent<LayoutElement>();
        size.flexibleHeight = 1f;
        size.minHeight = 40f;
        return rect;
    }

    private static Text CreateLabel(Transform parent, string objectName, string value, int fontSize)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(parent, false);
        Text label = labelObject.AddComponent<Text>();
        label.text = value;
        label.font = GetBuiltinFont();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.minHeight = fontSize + 10f;
        return label;
    }

    private static Text CreateFullLabel(Transform parent, string objectName, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text label = labelObject.AddComponent<Text>();
        label.text = value;
        label.font = GetBuiltinFont();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        return label;
    }

    private static Button CreateButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.3f, 0.52f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.font = GetBuiltinFont();
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return button;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private static Font GetBuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
