using Unity.Netcode;
using UnityEngine;

public class NetworkTargetListUI : MonoBehaviour
{
    [Header("Target Panel")]
    [SerializeField]
    private GameObject targetPanel;

    [Header("Player Card")]
    [SerializeField]
    private GameObject playerCardPrefab;

    [SerializeField]
    private Transform cardContainer;

    [SerializeField]
    private UnityEngine.UI.Button closeButton;

    [SerializeField]
    private UnityEngine.UI.Button hudPlayerListButton;

    [Header("Confirmation UI (SerpentSpirit)")]
    [SerializeField]
    private GameObject confirmPanel;

    [SerializeField]
    private TMPro.TextMeshProUGUI selectedTargetNameText;

    [SerializeField]
    private UnityEngine.UI.Image selectedAvatarImage;

    [SerializeField]
    private UnityEngine.UI.Button confirmAttackButton;

    [SerializeField]
    private UnityEngine.UI.Button cancelButton;

    private int selectedTargetId = -1;
    private string selectedTargetName = string.Empty;
    private NetworkTargetCardUI selectedTargetCard = null;
    private RoleType? simulatedRoleForTest = null;
    private bool simulatedActionDispatchedForTest = false;

    private bool isPairSelectionMode = false;
    private int firstSelectedTargetId = -1;
    private string firstSelectedTargetName = string.Empty;
    private NetworkTargetCardUI firstSelectedTargetCard = null;
    private int secondSelectedTargetId = -1;
    private string secondSelectedTargetName = string.Empty;
    private NetworkTargetCardUI secondSelectedTargetCard = null;

    public bool IsPairSelectionMode => isPairSelectionMode;
    public int FirstSelectedTargetId => firstSelectedTargetId;
    public int SecondSelectedTargetId => secondSelectedTargetId;
    public string FirstSelectedTargetName => firstSelectedTargetName;
    public string SecondSelectedTargetName => secondSelectedTargetName;
    public NetworkTargetCardUI FirstSelectedTargetCard => firstSelectedTargetCard;
    public NetworkTargetCardUI SecondSelectedTargetCard => secondSelectedTargetCard;

    public int SelectedTargetId => selectedTargetId;
    public string SelectedTargetName => selectedTargetName;
    public NetworkTargetCardUI SelectedTargetCard => selectedTargetCard;
    public GameObject ConfirmPanel => confirmPanel;
    public UnityEngine.UI.Button ConfirmAttackButton => confirmAttackButton;
    public UnityEngine.UI.Button CancelButton => cancelButton;
    public TMPro.TextMeshProUGUI SelectedTargetNameText => selectedTargetNameText;
    public UnityEngine.UI.Image SelectedAvatarImage => selectedAvatarImage;
    public bool SimulatedActionDispatchedForTest => simulatedActionDispatchedForTest;

    public static NetworkTargetListUI Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseTargetList);
        }

        if (hudPlayerListButton != null)
        {
            hudPlayerListButton.onClick.RemoveAllListeners();
            hudPlayerListButton.onClick.AddListener(() => ShowTargetList(-1));
        }

        EnsureConfirmationUI();
    }

    public void CloseTargetList()
    {
        ResetSelection();
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
            NetworkSkillUI.Instance?.CloseTargetPanel();
        }
    }

    public void Refresh()
    {
        if (targetPanel != null && targetPanel.activeSelf)
        {
            ShowTargetList(currentSubActionId);
        }
    }

    public bool RequiresConfirmation()
    {
        if (isPairSelectionMode)
        {
            return true;
        }

        if (simulatedRoleForTest.HasValue)
        {
            return simulatedRoleForTest.Value == RoleType.SerpentSpirit;
        }

        RoleType localRole = RoleType.Villager;
        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
        {
            localRole = NetworkRoleSync.LocalInstance.LocalRole;
        }
        else if (NetworkSkillUI.Instance != null && NetworkSkillUI.Instance.IsSerpentSpirit)
        {
            localRole = RoleType.SerpentSpirit;
        }
        else
        {
            var session = Thuong.Online.ThuongOnlineSession.Active;
            if (session != null && session.HasLocalRole)
            {
                localRole = session.GetPrivateRole();
            }
        }

        return localRole == RoleType.SerpentSpirit;
    }

    public void SetSimulatedRoleForTest(RoleType? role)
    {
        simulatedRoleForTest = role;
    }

    public void ResetSelection()
    {
        if (selectedTargetCard != null)
        {
            selectedTargetCard.SetSelected(false);
            selectedTargetCard = null;
        }

        if (firstSelectedTargetCard != null)
        {
            firstSelectedTargetCard.SetSelected(false);
            firstSelectedTargetCard = null;
        }

        if (secondSelectedTargetCard != null)
        {
            secondSelectedTargetCard.SetSelected(false);
            secondSelectedTargetCard = null;
        }

        selectedTargetId = -1;
        selectedTargetName = string.Empty;
        firstSelectedTargetId = -1;
        firstSelectedTargetName = string.Empty;
        secondSelectedTargetId = -1;
        secondSelectedTargetName = string.Empty;

        if (selectedTargetNameText != null)
        {
            selectedTargetNameText.text = isPairSelectionMode ? "Hãy chọn 2 người chơi để ghép đôi." : "Chưa chọn mục tiêu";
        }

        if (selectedAvatarImage != null)
        {
            selectedAvatarImage.gameObject.SetActive(false);
        }

        if (confirmAttackButton != null)
        {
            confirmAttackButton.interactable = false;
        }
    }

    public void ShowPairTargetList()
    {
        isPairSelectionMode = true;
        firstSelectedTargetId = -1;
        secondSelectedTargetId = -1;
        firstSelectedTargetName = string.Empty;
        secondSelectedTargetName = string.Empty;
        firstSelectedTargetCard = null;
        secondSelectedTargetCard = null;

        ShowTargetList(subActionId: 0);

        EnsureConfirmationUI();
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }

        if (confirmAttackButton != null)
        {
            confirmAttackButton.interactable = false;
            var txt = confirmAttackButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = "XÁC NHẬN GHÉP ĐÔI";
            }
        }

        if (selectedTargetNameText != null)
        {
            selectedTargetNameText.text = "Chọn người 1 & người 2";
        }

        Debug.Log("TARGET LIST | Đã mở chế độ ghép đôi cho Ông Tơ Bà Nguyệt.");
    }

    public void OnCardSelected(NetworkTargetCardUI card, int id, string name)
    {
        if (isPairSelectionMode)
        {
            HandlePairCardSelected(card, id, name);
            return;
        }

        if (selectedTargetCard != null && selectedTargetCard != card)
        {
            selectedTargetCard.SetSelected(false);
        }

        selectedTargetCard = card;
        selectedTargetId = id;
        selectedTargetName = name;

        if (selectedTargetCard != null)
        {
            selectedTargetCard.SetSelected(true);
        }

        if (selectedTargetNameText != null)
        {
            selectedTargetNameText.text = $"Mục tiêu: <color=#FFD700>{name}</color>";
        }

        if (selectedAvatarImage != null)
        {
            Sprite spr = card != null ? card.GetAvatarSprite() : null;
            if (spr != null)
            {
                selectedAvatarImage.sprite = spr;
                selectedAvatarImage.gameObject.SetActive(true);
            }
            else
            {
                selectedAvatarImage.gameObject.SetActive(false);
            }
        }

        if (confirmAttackButton != null)
        {
            confirmAttackButton.interactable = true;
        }

        Debug.Log($"TARGET LIST | Đã chọn mục tiêu: Player {id + 1} ({name})");
    }

    private void HandlePairCardSelected(NetworkTargetCardUI card, int id, string name)
    {
        if (firstSelectedTargetId == id)
        {
            if (firstSelectedTargetCard != null) firstSelectedTargetCard.SetSelected(false);
            firstSelectedTargetId = -1;
            firstSelectedTargetName = string.Empty;
            firstSelectedTargetCard = null;
        }
        else if (secondSelectedTargetId == id)
        {
            if (secondSelectedTargetCard != null) secondSelectedTargetCard.SetSelected(false);
            secondSelectedTargetId = -1;
            secondSelectedTargetName = string.Empty;
            secondSelectedTargetCard = null;
        }
        else if (firstSelectedTargetId < 0)
        {
            firstSelectedTargetId = id;
            firstSelectedTargetName = name;
            firstSelectedTargetCard = card;
            if (card != null) card.SetSelected(true);
        }
        else if (secondSelectedTargetId < 0)
        {
            secondSelectedTargetId = id;
            secondSelectedTargetName = name;
            secondSelectedTargetCard = card;
            if (card != null) card.SetSelected(true);
        }
        else
        {
            if (secondSelectedTargetCard != null) secondSelectedTargetCard.SetSelected(false);
            secondSelectedTargetId = id;
            secondSelectedTargetName = name;
            secondSelectedTargetCard = card;
            if (card != null) card.SetSelected(true);
        }

        bool bothSelected = firstSelectedTargetId >= 0 && secondSelectedTargetId >= 0 && firstSelectedTargetId != secondSelectedTargetId;
        if (confirmAttackButton != null)
        {
            confirmAttackButton.interactable = bothSelected;
        }

        if (selectedTargetNameText != null)
        {
            if (bothSelected)
            {
                selectedTargetNameText.text = $"Cặp đôi: <color=#00FFFF>{firstSelectedTargetName}</color> & <color=#FF69B4>{secondSelectedTargetName}</color>";
            }
            else if (firstSelectedTargetId >= 0)
            {
                selectedTargetNameText.text = $"Người 1: <color=#00FFFF>{firstSelectedTargetName}</color>. Hãy chọn người 2.";
            }
            else if (secondSelectedTargetId >= 0)
            {
                selectedTargetNameText.text = $"Người 2: <color=#FF69B4>{secondSelectedTargetName}</color>. Hãy chọn người 1.";
            }
            else
            {
                selectedTargetNameText.text = "Hãy chọn 2 người chơi để ghép đôi.";
            }
        }
    }

    public void OnConfirmAttackClicked()
    {
        if (isPairSelectionMode)
        {
            if (firstSelectedTargetId < 0 || secondSelectedTargetId < 0 || firstSelectedTargetId == secondSelectedTargetId)
            {
                Debug.LogWarning("TARGET LIST | Chưa chọn đủ 2 người chơi hợp lệ để ghép đôi!");
                return;
            }

            simulatedActionDispatchedForTest = true;

            NetworkPlayerAction[] actions =
                FindObjectsByType<NetworkPlayerAction>(
                    FindObjectsSortMode.None
                );

            bool dispatched = false;
            foreach (NetworkPlayerAction action in actions)
            {
                if (!action.IsOwner)
                    continue;

                action.RequestPairActionServerRpc(
                    (ulong)firstSelectedTargetId,
                    (ulong)secondSelectedTargetId
                );

                dispatched = true;
                break;
            }

            var session = Thuong.Online.ThuongOnlineSession.Active;
            if (session != null)
            {
                session.RequestAbility(firstSelectedTargetId, secondSelectedTargetId);
            }

            Debug.Log($"TARGET LIST | WeaverOfFate đã xác nhận ghép đôi Player {firstSelectedTargetId + 1} ({firstSelectedTargetName}) & Player {secondSelectedTargetId + 1} ({secondSelectedTargetName}) | Dispatched={dispatched}");

            CloseTargetList();
            NetworkSkillUI.Instance?.SetWeaverAbilityUsed(true);
            return;
        }

        if (selectedTargetId < 0)
        {
            Debug.LogWarning("TARGET LIST | Chưa chọn mục tiêu để tấn công!");
            return;
        }

        simulatedActionDispatchedForTest = true;

        NetworkPlayerAction[] actionsNormal =
            FindObjectsByType<NetworkPlayerAction>(
                FindObjectsSortMode.None
            );

        bool dispatchedNormal = false;
        foreach (NetworkPlayerAction action in actionsNormal)
        {
            if (!action.IsOwner)
                continue;

            action.RequestActionServerRpc(
                (ulong)selectedTargetId,
                currentSubActionId
            );

            dispatchedNormal = true;
            break;
        }

        Debug.Log($"TARGET LIST | Xà Tinh đã xác nhận tấn công mục tiêu Player {selectedTargetId + 1} ({selectedTargetName}) | Dispatched={dispatchedNormal}");

        CloseTargetList();
        NetworkSkillUI.Instance?.NotifyActionDispatched();
    }

    public void OnCancelClicked()
    {
        Debug.Log("TARGET LIST | Nhấn HỦY chọn mục tiêu.");
        ResetSelection();
        isPairSelectionMode = false;
        CloseTargetList();
    }

    public void EnsureConfirmationUI()
    {
        if (targetPanel == null)
            return;

        if (confirmPanel != null)
            return;

        Transform existing = targetPanel.transform.Find("SerpentTargetConfirmBar");
        if (existing != null)
        {
            confirmPanel = existing.gameObject;
        }
        else
        {
            var go = new GameObject("SerpentTargetConfirmBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(targetPanel.transform, false);
            confirmPanel = go;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 25f);
            rt.sizeDelta = new Vector2(900f, 75f);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.10f, 0.13f, 0.20f, 0.95f);
        }

        var font = GetUIFont();

        if (selectedAvatarImage == null)
        {
            Transform avTrans = confirmPanel.transform.Find("SelectedAvatar");
            if (avTrans != null)
            {
                selectedAvatarImage = avTrans.GetComponent<UnityEngine.UI.Image>();
            }
            else
            {
                var avGo = new GameObject("SelectedAvatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                avGo.transform.SetParent(confirmPanel.transform, false);
                selectedAvatarImage = avGo.GetComponent<UnityEngine.UI.Image>();
                var avRt = avGo.GetComponent<RectTransform>();
                avRt.anchorMin = new Vector2(0f, 0.5f);
                avRt.anchorMax = new Vector2(0f, 0.5f);
                avRt.pivot = new Vector2(0f, 0.5f);
                avRt.anchoredPosition = new Vector2(25f, 0f);
                avRt.sizeDelta = new Vector2(55f, 55f);
                avGo.SetActive(false);
            }
        }

        if (selectedTargetNameText == null)
        {
            Transform nameTrans = confirmPanel.transform.Find("SelectedTargetName");
            if (nameTrans != null)
            {
                selectedTargetNameText = nameTrans.GetComponent<TMPro.TextMeshProUGUI>();
            }
            else
            {
                var nameGo = new GameObject("SelectedTargetName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                nameGo.transform.SetParent(confirmPanel.transform, false);
                selectedTargetNameText = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
                var nameRt = nameGo.GetComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0f, 0.5f);
                nameRt.anchorMax = new Vector2(0f, 0.5f);
                nameRt.pivot = new Vector2(0f, 0.5f);
                nameRt.anchoredPosition = new Vector2(95f, 0f);
                nameRt.sizeDelta = new Vector2(360f, 50f);

                selectedTargetNameText.fontSize = 22;
                selectedTargetNameText.fontStyle = TMPro.FontStyles.Bold;
                selectedTargetNameText.color = new Color(1f, 0.85f, 0.1f, 1f);
                selectedTargetNameText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                selectedTargetNameText.text = "Chưa chọn mục tiêu";
                if (font != null)
                {
                    selectedTargetNameText.font = font;
                    selectedTargetNameText.fontSharedMaterial = font.material;
                }
            }
        }

        if (cancelButton == null)
        {
            Transform canTrans = confirmPanel.transform.Find("CancelButton");
            if (canTrans != null)
            {
                cancelButton = canTrans.GetComponent<UnityEngine.UI.Button>();
            }
            else
            {
                var canGo = new GameObject("CancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                canGo.transform.SetParent(confirmPanel.transform, false);
                cancelButton = canGo.GetComponent<UnityEngine.UI.Button>();
                var canRt = canGo.GetComponent<RectTransform>();
                canRt.anchorMin = new Vector2(1f, 0.5f);
                canRt.anchorMax = new Vector2(1f, 0.5f);
                canRt.pivot = new Vector2(1f, 0.5f);
                canRt.anchoredPosition = new Vector2(-280f, 0f);
                canRt.sizeDelta = new Vector2(120f, 52f);

                var canImg = canGo.GetComponent<UnityEngine.UI.Image>();
                canImg.color = new Color(0.28f, 0.35f, 0.44f, 1f);
                cancelButton.targetGraphic = canImg;

                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                txtGo.transform.SetParent(canGo.transform, false);
                var txt = txtGo.GetComponent<TMPro.TextMeshProUGUI>();
                txt.text = "HỦY";
                txt.fontSize = 20;
                txt.fontStyle = TMPro.FontStyles.Bold;
                txt.color = Color.white;
                txt.alignment = TMPro.TextAlignmentOptions.Center;
                if (font != null) { txt.font = font; txt.fontSharedMaterial = font.material; }

                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        if (confirmAttackButton == null)
        {
            Transform confTrans = confirmPanel.transform.Find("ConfirmAttackButton");
            if (confTrans != null)
            {
                confirmAttackButton = confTrans.GetComponent<UnityEngine.UI.Button>();
            }
            else
            {
                var confGo = new GameObject("ConfirmAttackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                confGo.transform.SetParent(confirmPanel.transform, false);
                confirmAttackButton = confGo.GetComponent<UnityEngine.UI.Button>();
                var confRt = confGo.GetComponent<RectTransform>();
                confRt.anchorMin = new Vector2(1f, 0.5f);
                confRt.anchorMax = new Vector2(1f, 0.5f);
                confRt.pivot = new Vector2(1f, 0.5f);
                confRt.anchoredPosition = new Vector2(-25f, 0f);
                confRt.sizeDelta = new Vector2(240f, 52f);

                var confImg = confGo.GetComponent<UnityEngine.UI.Image>();
                confImg.color = new Color(0.78f, 0.14f, 0.14f, 1f);
                confirmAttackButton.targetGraphic = confImg;

                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                txtGo.transform.SetParent(confGo.transform, false);
                var txt = txtGo.GetComponent<TMPro.TextMeshProUGUI>();
                txt.text = "XÁC NHẬN TẤN CÔNG";
                txt.fontSize = 19;
                txt.fontStyle = TMPro.FontStyles.Bold;
                txt.color = Color.white;
                txt.alignment = TMPro.TextAlignmentOptions.Center;
                if (font != null) { txt.font = font; txt.fontSharedMaterial = font.material; }

                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                confirmAttackButton.onClick.RemoveAllListeners();
                confirmAttackButton.onClick.AddListener(OnConfirmAttackClicked);
                confirmAttackButton.interactable = false;
            }
        }

        confirmPanel.SetActive(false);
    }

    private static TMPro.TMP_FontAsset GetUIFont()
    {
        TMPro.TMP_FontAsset font = null;
        try
        {
            font = AuthUI.GetVietnameseFont();
        }
        catch { }

        if (font == null)
        {
            font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (font == null)
        {
            var allFonts = Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>();
            if (allFonts != null && allFonts.Length > 0)
            {
                font = allFonts[0];
            }
        }

        return font;
    }

    public void SetupForTest(
        GameObject testTargetPanel,
        GameObject testConfirmPanel = null,
        TMPro.TextMeshProUGUI testTargetNameText = null,
        UnityEngine.UI.Image testAvatarImage = null,
        UnityEngine.UI.Button testConfirmBtn = null,
        UnityEngine.UI.Button testCancelBtn = null)
    {
        targetPanel = testTargetPanel;
        confirmPanel = testConfirmPanel;
        selectedTargetNameText = testTargetNameText;
        selectedAvatarImage = testAvatarImage;
        confirmAttackButton = testConfirmBtn;
        cancelButton = testCancelBtn;
    }

    private void OnEnable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged +=
            OnPlayerNameChanged;
    }

    private void OnDisable()
    {
        NetworkPlayerNameSync.OnAnyPlayerNameChanged -=
            OnPlayerNameChanged;
    }

    private int currentSubActionId;

    private void OnPlayerNameChanged()
    {
        if (targetPanel == null)
            return;

        if (!targetPanel.activeSelf)
            return;

        Debug.Log(
            "TARGET LIST: Player Name đã sync → cập nhật Card."
        );

        ShowTargetList(currentSubActionId);
    }

    public void ShowTargetList(int subActionId = 0)
    {
        currentSubActionId = subActionId;
        ResetSelection();

        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }

        bool isConfirmationRequired = RequiresConfirmation();
        if (isConfirmationRequired)
        {
            EnsureConfirmationUI();
            if (confirmPanel != null)
            {
                confirmPanel.SetActive(true);
            }
        }
        else
        {
            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);
            }
        }

        ClearCards();

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogWarning(
                "TARGET LIST: Không tìm thấy NetworkManager!"
            );

            return;
        }

        int localPlayerID = -1;

        PlayerController[] players =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );

        foreach (PlayerController player in players)
        {
            if (!player.IsOwner)
                continue;

            localPlayerID =
                (int)player.OwnerClientId;

            break;
        }

        Debug.Log(
            "TARGET LIST: Local Player ID = "
            + localPlayerID
        );

        if (players == null ||
            players.Length == 0)
        {
            Debug.LogWarning(
                "TARGET LIST: Chưa tìm thấy Player Network nào!"
            );

            return;
        }

        Debug.Log(
            "TARGET LIST: Tìm thấy "
            + players.Length
            + " Player Network."
        );

        // ==========================================
        // LẤY ROLE CỦA PLAYER LOCAL
        // ==========================================

        RoleType localRole =
            RoleType.Villager;

        if (NetworkRoleSync.LocalInstance != null)
        {
            localRole =
                NetworkRoleSync.LocalInstance.LocalRole;
        }

        bool isGuardian =
            localRole == RoleType.VillageGuardian;

        // Pháp Sư (Shaman) được phép dùng Bùa Lợi (subActionId == 1) tự cứu bản thân
        // WeaverOfFate (Ghép đôi) cho phép chọn bản thân là một trong hai người
        bool allowSelfTarget =
            isGuardian || (localRole == RoleType.Shaman && currentSubActionId == 1) || currentSubActionId < 0 || isPairSelectionMode;

        Debug.Log(
            "TARGET LIST: Local Role = "
            + localRole
            + " | SubAction = "
            + currentSubActionId
            + " | AllowSelfTarget = "
            + allowSelfTarget
        );

        // ==========================================
        // TẠO TARGET CARD
        // ==========================================

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            int playerID =
                (int)player.OwnerClientId;

            // ==========================================
            // KHÔNG CHO TỰ TARGET
            // NGOẠI TRỪ GUARDIAN HOẶC SHAMAN DÙNG BÙA LỢI
            // ==========================================

            if (!allowSelfTarget &&
                playerID == localPlayerID)
            {
                continue;
            }

            if (playerCardPrefab == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "Player Card Prefab đang NULL!"
                );

                return;
            }

            if (cardContainer == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "Card Container đang NULL!"
                );

                return;
            }

            NetworkPlayerStateSync stateSync =
                player.GetComponent<NetworkPlayerStateSync>();

            bool isDisconnected = (stateSync != null && stateSync.State.Value == NetworkPlayerStateType.Disconnected) ||
                                  (PlayerManager.Instance != null && PlayerManager.Instance.IsDisconnected(playerID));

            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            bool notInServerClients = isServer && (NetworkManager.Singleton.ConnectedClients == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)playerID));

            // Bỏ qua hoàn toàn nếu player đã thoát, ngắt kết nối, hoặc không còn trong danh sách active players
            if (isDisconnected ||
                NetworkGameManager.IsPlayerLeft((ulong)playerID) ||
                notInServerClients ||
                (PlayerManager.Instance != null && !PlayerManager.Instance.IsActivePlayer(playerID)))
            {
                continue;
            }

            GameObject card =
                Instantiate(
                    playerCardPrefab,
                    cardContainer
                );

            NetworkTargetCardUI cardUI =
                card.GetComponent<NetworkTargetCardUI>();

            if (cardUI == null)
            {
                Debug.LogWarning(
                    "TARGET LIST: "
                    + "PlayerCard chưa có "
                    + "NetworkTargetCardUI!"
                );

                Destroy(card);

                continue;
            }

            // ==========================================
            // KIỂM TRA ALIVE
            // ==========================================

            bool isAlive = true;

            if (stateSync != null)
            {
                // Khi vào ban đêm (Night Phase), các player còn sống được chuyển sang trạng thái Sleeping.
                // Do đó, cả Alive và Sleeping đều được xem là còn sống hợp lệ.
                isAlive =
                    stateSync.State.Value == NetworkPlayerStateType.Alive ||
                    stateSync.State.Value == NetworkPlayerStateType.Sleeping;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerData pd = PlayerManager.Instance.GetplayerByID(playerID);
                if (pd != null && !pd.isAlive)
                {
                    isAlive = false;
                }
            }

            // ==========================================
            // LẤY PLAYER NAME
            // ==========================================

            NetworkPlayerNameSync nameSync =
                player.GetComponent<NetworkPlayerNameSync>();

            Debug.Log(
                "TARGET NAME DEBUG | Player = "
                + (playerID + 1)
                + " | nameSync = "
                + (nameSync != null ? "FOUND" : "NULL")
                + " | Name = "
                + (nameSync != null
                    ? nameSync.GetPlayerName()
                    : "N/A")
            );

            string playerName =
                "Player " + (playerID + 1);

            if (nameSync != null)
            {
                playerName =
                    nameSync.GetPlayerName();
            }

            Debug.Log(
                "TARGET NAME TEST"
                + " | Player = "
                + (playerID + 1)
                + " | Name = "
                + playerName
            );

            // ==========================================
            // SETUP CARD
            // ==========================================

            cardUI.Setup(
                playerID,
                playerName,
                isAlive,
                currentSubActionId
            );

            Debug.Log(
                "TARGET LIST: Tạo Card"
                + " | PlayerID = "
                + playerID
                + " | Name = "
                + playerName
                + " | Alive = "
                + isAlive
            );
        }
    }

    private void ClearCards()
    {
        if (cardContainer == null)
            return;

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }
    }
}