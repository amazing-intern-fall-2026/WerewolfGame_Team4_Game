using UnityEngine;
using Unity.Netcode;

public class NetworkSkillUI : MonoBehaviour
{
    [Header("DogSpirit Skill")]
    [SerializeField]
    private GameObject attackButton;

    [Header("Seer Skill")]
    [SerializeField]
    private GameObject inspectButton;

    [Header("VillageGuardian Skill")]
    [SerializeField]
    private GameObject protectButton;

    [Header("Shaman Skill")]
    [SerializeField]
    private GameObject saveButton;

    [SerializeField]
    private GameObject killButton;

    [Header("Hunter Skill")]
    [SerializeField]
    private GameObject trapButton;

    [Header("Skill UI")]
    [SerializeField]
    private GameObject skillPanel;

    [Header("Target UI")]
    [SerializeField]
    private GameObject targetPanel;

    [SerializeField]
    private NetworkTargetListUI targetListUI;

    [Header("Shaman Victim Prompt UI")]
    [SerializeField]
    private GameObject shamanPromptPanel;

    [SerializeField]
    private TMPro.TextMeshProUGUI shamanPromptText;

    [SerializeField]
    private UnityEngine.UI.Button shamanPromptSaveButton;

    [SerializeField]
    private UnityEngine.UI.Button shamanPromptDismissButton;

    private int currentPromptVictimId = -1;

    private NetworkPhaseSync phaseSync;

    private bool isDogSpirit;
    private bool isSeer;
    private bool isVillageGuardian;
    private bool isShaman;
    private bool isHunter;

    private bool isNight;
    private bool isDiscussion;
    private bool hasUsedSkill;

    private int pendingSubActionId;
    private bool shamanHasUsedSave;
    private bool shamanHasUsedKill;

    public static NetworkSkillUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        CleanupUnusedText();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetForNewMatch()
    {
        CleanupUnusedText();
        hasUsedSkill = false;
        shamanHasUsedSave = false;
        shamanHasUsedKill = false;
        currentPromptVictimId = -1;
        HideShamanPrompt();

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }

        UpdateSkillUI();
        Debug.Log("SKILL UI: Reset For New Match hoàn tất.");
    }

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived +=
            OnRoleReceived;

        NetworkPlayerAction.OnNetworkActionResult +=
            OnNetworkActionResult;

        NetworkPlayerAction.OnShamanVictimPromptReceived +=
            OnShamanVictimPromptReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -=
            OnRoleReceived;

        NetworkPlayerAction.OnNetworkActionResult -=
            OnNetworkActionResult;

        NetworkPlayerAction.OnShamanVictimPromptReceived -=
            OnShamanVictimPromptReceived;
    }

    private void Start()
    {
        CleanupUnusedText();
        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();

        hasUsedSkill = false;

        HideAll();

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }
    }

    private void Update()
    {
        if (phaseSync == null)
        {
            phaseSync =
                FindFirstObjectByType<NetworkPhaseSync>();

            if (phaseSync == null)
                return;
        }

        GamePhase currentPhase =
            phaseSync.CurrentPhase.Value;

        bool newIsNight =
            currentPhase == GamePhase.Night;

        bool newIsDiscussion =
            currentPhase == GamePhase.Discussion;

        // =====================================================
        // PHASE CHANGE
        // =====================================================

        bool phaseChanged =
            newIsNight != isNight ||
            newIsDiscussion != isDiscussion;

        if (phaseChanged)
        {
            // Phase mới → cho phép dùng Skill lại
            hasUsedSkill = false;

            isNight = newIsNight;
            isDiscussion = newIsDiscussion;

            // Đóng Target Panel khi đổi Phase
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }

            if (!isNight)
            {
                HideShamanPrompt();
            }
        }

        UpdateSkillUI();
    }

    private void OnRoleReceived(RoleType role)
    {
        isDogSpirit =
            role == RoleType.DogSpirit;

        isSeer =
            role == RoleType.Seer;

        isVillageGuardian =
            role == RoleType.VillageGuardian;

        isShaman =
            role == RoleType.Shaman;

        isHunter =
            role == RoleType.Hunter;

        shamanHasUsedSave = false;
        shamanHasUsedKill = false;
        currentPromptVictimId = -1;
        HideShamanPrompt();

        Debug.Log(
            "SKILL UI | Role = "
            + role
            + " | DogSpirit = "
            + isDogSpirit
            + " | Seer = "
            + isSeer
            + " | VillageGuardian = "
            + isVillageGuardian
            + " | Shaman = "
            + isShaman
            + " | Hunter = "
            + isHunter
        );

        UpdateSkillUI();
    }

    private bool IsLocalPlayerDead()
    {
        if (NetworkManager.Singleton == null)
            return false;

        if (!NetworkManager.Singleton.IsClient)
            return false;

        NetworkObject localPlayer =
            NetworkManager.Singleton.LocalClient?.PlayerObject;

        if (localPlayer == null)
            return false;

        NetworkPlayerStateSync stateSync =
            localPlayer.GetComponent<NetworkPlayerStateSync>();

        if (stateSync == null)
            return false;

        return stateSync.State.Value ==
               NetworkPlayerStateType.Dead;
    }

    private void UpdateSkillUI()
    {
        bool hunterCanUseTrap =
            isHunter &&
            isDiscussion &&
            IsLocalPlayerDead() &&
            !hasUsedSkill;

        bool shamanCanAct =
            isShaman &&
            (!shamanHasUsedSave || !shamanHasUsedKill);

        bool normalSkillAvailable =
            (
                isDogSpirit ||
                isSeer ||
                isVillageGuardian ||
                shamanCanAct
            )
            &&
            isNight &&
            !hasUsedSkill;

        bool canUseSkill =
            normalSkillAvailable ||
            hunterCanUseTrap;

        if (skillPanel != null)
        {
            skillPanel.SetActive(canUseSkill);
            CleanupUnusedText();
        }

        if (attackButton != null)
        {
            attackButton.SetActive(
                isDogSpirit &&
                isNight &&
                !hasUsedSkill
            );
        }

        if (inspectButton != null)
        {
            inspectButton.SetActive(
                isSeer &&
                isNight &&
                !hasUsedSkill
            );
        }

        if (protectButton != null)
        {
            protectButton.SetActive(
                isVillageGuardian &&
                isNight &&
                !hasUsedSkill
            );
        }

        if (saveButton != null)
        {
            saveButton.SetActive(
                isShaman &&
                isNight &&
                !hasUsedSkill &&
                !shamanHasUsedSave
            );
        }

        if (killButton != null)
        {
            killButton.SetActive(
                isShaman &&
                isNight &&
                !hasUsedSkill &&
                !shamanHasUsedKill
            );
        }

        if (trapButton != null)
        {
            trapButton.SetActive(
                hunterCanUseTrap
            );
        }

        if (!canUseSkill)
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
        }
    }

    public void OnAttackClicked()
    {
        if (!isDogSpirit)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | DogSpirit mở Target Panel."
        );

        OpenTargetPanel(0);
    }

    public void OnInspectClicked()
    {
        if (!isSeer)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | Seer mở Target Panel."
        );

        OpenTargetPanel(0);
    }

    public void OnProtectClicked()
    {
        if (!isVillageGuardian)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | VillageGuardian mở Target Panel."
        );

        OpenTargetPanel(0);
    }

    public void OnSaveClicked()
    {
        if (!isShaman)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | Shaman SAVE (Bùa Lợi) mở Target Panel."
        );

        // 1 = Good Charm (Cứu / Bảo vệ)
        OpenTargetPanel(1);
    }

    public void OnKillClicked()
    {
        if (!isShaman)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | Shaman KILL (Bùa Hại) mở Target Panel."
        );

        // 2 = Bad Charm (Hại / Câm lặng)
        OpenTargetPanel(2);
    }

    public void OnTrapClicked()
    {
        if (!isHunter)
            return;

        if (!isDiscussion)
        {
            Debug.LogWarning(
                "SKILL UI | Hunter chỉ đặt bẫy trong Discussion."
            );
            return;
        }

        if (!IsLocalPlayerDead())
        {
            Debug.LogWarning(
                "SKILL UI | Hunter chưa chết."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | Hunter mở Target Panel."
        );

        OpenTargetPanel(0);
    }

    private void OpenTargetPanel(int subActionId = 0)
    {
        pendingSubActionId = subActionId;

        if (targetListUI != null)
        {
            targetListUI.ShowTargetList(subActionId);
        }
        else if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
    }

    private void OnNetworkActionResult(
        bool success,
        string message)
    {
        Debug.Log(
            "SKILL UI | ACTION RESULT"
            + " | Success = "
            + success
            + " | Message = "
            + message
        );

        if (!success)
            return;

        hasUsedSkill = true;

        if (isShaman)
        {
            if (pendingSubActionId == 1)
            {
                shamanHasUsedSave = true;
                Debug.Log("SKILL UI | Shaman đã sử dụng Bùa Lợi (Cứu).");
            }
            else if (pendingSubActionId == 2)
            {
                shamanHasUsedKill = true;
                Debug.Log("SKILL UI | Shaman đã sử dụng Bùa Hại (Giết).");
            }
        }

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }

        HideShamanPrompt();

        UpdateSkillUI();
    }

    private void HideAll()
    {
        CleanupUnusedText();
        if (skillPanel != null)
            skillPanel.SetActive(false);

        if (attackButton != null)
            attackButton.SetActive(false);

        if (inspectButton != null)
            inspectButton.SetActive(false);

        if (protectButton != null)
            protectButton.SetActive(false);

        if (saveButton != null)
            saveButton.SetActive(false);

        if (killButton != null)
            killButton.SetActive(false);

        if (trapButton != null)
            trapButton.SetActive(false);

        if (targetPanel != null)
            targetPanel.SetActive(false);

        HideShamanPrompt();
    }

    // =========================================================
    // SHAMAN VICTIM PROMPT LOGIC
    // =========================================================

    private void OnShamanVictimPromptReceived(
        int victimId,
        string victimName)
    {
        if (!isShaman)
            return;

        if (shamanHasUsedSave)
            return;

        if (!isNight)
            return;

        currentPromptVictimId = victimId;
        EnsureShamanPromptUI();

        if (shamanPromptText != null)
        {
            int localId = -1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                localId = (int)NetworkManager.Singleton.LocalClientId;
            }

            string victimDisplay = (localId == victimId)
                ? $"{victimName} (Bản thân)"
                : victimName;

            shamanPromptText.text =
                $"<color=#E74C3C>DogSpirit</color> đã cắn <b>{victimDisplay}</b>!\nBạn có muốn dùng <b>Bùa Lợi</b> để cứu không?";
        }

        if (shamanPromptPanel != null)
        {
            shamanPromptPanel.SetActive(true);
        }

        Debug.Log(
            "SKILL UI | Hiện thông báo DogSpirit cắn mục tiêu cho Shaman: "
            + victimName
        );
    }

    private void OnPromptSaveClicked()
    {
        if (currentPromptVictimId < 0)
            return;

        HideShamanPrompt();

        pendingSubActionId = 1;

        NetworkPlayerAction[] actions =
            FindObjectsByType<NetworkPlayerAction>(
                FindObjectsSortMode.None
            );

        foreach (NetworkPlayerAction action in actions)
        {
            if (!action.IsOwner)
                continue;

            action.RequestActionServerRpc(
                (ulong)currentPromptVictimId,
                1
            );

            Debug.Log(
                "SKILL UI | Shaman bấm CỨU từ prompt → gửi RPC cứu Player "
                + (currentPromptVictimId + 1)
            );

            return;
        }

        Debug.LogWarning(
            "SKILL UI | Không tìm thấy NetworkPlayerAction của Shaman."
        );
    }

    private void OnPromptDismissClicked()
    {
        HideShamanPrompt();

        Debug.Log(
            "SKILL UI | Shaman chọn KHÔNG CỨU nạn nhân."
        );
    }

    private void HideShamanPrompt()
    {
        if (shamanPromptPanel != null)
        {
            shamanPromptPanel.SetActive(false);
        }
    }

    private void EnsureShamanPromptUI()
    {
        if (shamanPromptPanel != null)
            return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Transform parentTransform = parentCanvas != null ? parentCanvas.transform : transform;

        // Container Panel
        shamanPromptPanel = new GameObject(
            "ShamanVictimPromptPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image)
        );
        shamanPromptPanel.transform.SetParent(parentTransform, false);

        RectTransform rect = shamanPromptPanel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460, 220);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image bgImage = shamanPromptPanel.GetComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

        // Title
        GameObject titleObj = new GameObject(
            "TitleText",
            typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI)
        );
        titleObj.transform.SetParent(shamanPromptPanel.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -15);
        titleRect.sizeDelta = new Vector2(-20, 35);

        TMPro.TextMeshProUGUI titleText = titleObj.GetComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "THÔNG BÁO BÙA LỢI PHÁP SƯ";
        titleText.fontSize = 18;
        titleText.fontStyle = TMPro.FontStyles.Bold;
        titleText.color = new Color(0.95f, 0.77f, 0.25f);
        titleText.alignment = TMPro.TextAlignmentOptions.Center;

        // Message
        GameObject msgObj = new GameObject(
            "MessageText",
            typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI)
        );
        msgObj.transform.SetParent(shamanPromptPanel.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0, 0.35f);
        msgRect.anchorMax = new Vector2(1, 0.85f);
        msgRect.pivot = new Vector2(0.5f, 0.5f);
        msgRect.anchoredPosition = Vector2.zero;
        msgRect.sizeDelta = new Vector2(-40, 0);

        shamanPromptText = msgObj.GetComponent<TMPro.TextMeshProUGUI>();
        shamanPromptText.fontSize = 15;
        shamanPromptText.color = Color.white;
        shamanPromptText.alignment = TMPro.TextAlignmentOptions.Center;
        shamanPromptText.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // Button Container
        GameObject btnContainer = new GameObject(
            "ButtonContainer",
            typeof(RectTransform)
        );
        btnContainer.transform.SetParent(shamanPromptPanel.transform, false);
        RectTransform btnContainerRect = btnContainer.GetComponent<RectTransform>();
        btnContainerRect.anchorMin = new Vector2(0, 0);
        btnContainerRect.anchorMax = new Vector2(1, 0.35f);
        btnContainerRect.pivot = new Vector2(0.5f, 0);
        btnContainerRect.anchoredPosition = Vector2.zero;
        btnContainerRect.sizeDelta = new Vector2(-30, 0);

        // Save Button (Cứu)
        GameObject saveBtnObj = new GameObject(
            "PromptSaveButton",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button)
        );
        saveBtnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform saveBtnRect = saveBtnObj.GetComponent<RectTransform>();
        saveBtnRect.anchorMin = new Vector2(0.05f, 0.2f);
        saveBtnRect.anchorMax = new Vector2(0.48f, 0.85f);
        saveBtnRect.offsetMin = Vector2.zero;
        saveBtnRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image saveImg = saveBtnObj.GetComponent<UnityEngine.UI.Image>();
        saveImg.color = new Color(0.18f, 0.55f, 0.28f);

        shamanPromptSaveButton = saveBtnObj.GetComponent<UnityEngine.UI.Button>();
        shamanPromptSaveButton.onClick.AddListener(OnPromptSaveClicked);

        GameObject saveTextObj = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI)
        );
        saveTextObj.transform.SetParent(saveBtnObj.transform, false);
        RectTransform saveTextRect = saveTextObj.GetComponent<RectTransform>();
        saveTextRect.anchorMin = Vector2.zero;
        saveTextRect.anchorMax = Vector2.one;
        saveTextRect.offsetMin = Vector2.zero;
        saveTextRect.offsetMax = Vector2.zero;
        TMPro.TextMeshProUGUI saveText = saveTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        saveText.text = "CỨU MỤC TIÊU";
        saveText.fontSize = 15;
        saveText.fontStyle = TMPro.FontStyles.Bold;
        saveText.color = Color.white;
        saveText.alignment = TMPro.TextAlignmentOptions.Center;

        // Dismiss Button (Không cứu)
        GameObject dismissBtnObj = new GameObject(
            "PromptDismissButton",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button)
        );
        dismissBtnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform dismissBtnRect = dismissBtnObj.GetComponent<RectTransform>();
        dismissBtnRect.anchorMin = new Vector2(0.52f, 0.2f);
        dismissBtnRect.anchorMax = new Vector2(0.95f, 0.85f);
        dismissBtnRect.offsetMin = Vector2.zero;
        dismissBtnRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image dismissImg = dismissBtnObj.GetComponent<UnityEngine.UI.Image>();
        dismissImg.color = new Color(0.45f, 0.48f, 0.52f);

        shamanPromptDismissButton = dismissBtnObj.GetComponent<UnityEngine.UI.Button>();
        shamanPromptDismissButton.onClick.AddListener(OnPromptDismissClicked);

        GameObject dismissTextObj = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI)
        );
        dismissTextObj.transform.SetParent(dismissBtnObj.transform, false);
        RectTransform dismissTextRect = dismissTextObj.GetComponent<RectTransform>();
        dismissTextRect.anchorMin = Vector2.zero;
        dismissTextRect.anchorMax = Vector2.one;
        dismissTextRect.offsetMin = Vector2.zero;
        dismissTextRect.offsetMax = Vector2.zero;
        TMPro.TextMeshProUGUI dismissText = dismissTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        dismissText.text = "KHÔNG CỨU";
        dismissText.fontSize = 15;
        dismissText.fontStyle = TMPro.FontStyles.Bold;
        dismissText.color = Color.white;
        dismissText.alignment = TMPro.TextAlignmentOptions.Center;

        shamanPromptPanel.SetActive(false);
    }

    private void CleanupUnusedText()
    {
        if (skillPanel != null)
        {
            Transform unused = skillPanel.transform.Find("ActionMessageText");
            if (unused != null)
            {
                unused.gameObject.SetActive(false);
                var tmp = unused.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = string.Empty;
                }
                var txt = unused.GetComponent<UnityEngine.UI.Text>();
                if (txt != null)
                {
                    txt.text = string.Empty;
                }
            }
        }
    }
}