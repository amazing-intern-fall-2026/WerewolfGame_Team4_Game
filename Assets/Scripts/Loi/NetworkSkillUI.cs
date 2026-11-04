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
    private bool isWhiteHound;
    private bool isWhiteHoundAwakened;
    private bool isSeer;
    private bool isVillageGuardian;
    private bool isShaman;
    private bool isHunter;
    private bool isKiller;

    private bool isNight;
    private bool isDiscussion;
    private bool hasUsedSkill;

    private int pendingSubActionId;
    private bool shamanHasUsedSave;
    private bool shamanHasUsedKill;

    public bool ShamanHasUsedSave => shamanHasUsedSave;
    public bool ShamanHasUsedKill => shamanHasUsedKill;
    public GameObject SaveButton => saveButton;
    public GameObject KillButton => killButton;
    public GameObject SkillPanel => skillPanel;

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

        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged +=
            OnWhiteHoundAwakenedChanged;

        NetworkRoleSync.OnLocalShamanCharmsChanged +=
            OnShamanCharmsChanged;

        NetworkRoleSync.OnLocalHunterRevengeStateChanged +=
            OnHunterRevengeStateChanged;

        NetworkPlayerAction.OnNetworkActionResult +=
            OnNetworkActionResult;

        NetworkPlayerAction.OnShamanVictimPromptReceived +=
            OnShamanVictimPromptReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -=
            OnRoleReceived;

        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -=
            OnWhiteHoundAwakenedChanged;

        NetworkRoleSync.OnLocalShamanCharmsChanged -=
            OnShamanCharmsChanged;

        NetworkRoleSync.OnLocalHunterRevengeStateChanged -=
            OnHunterRevengeStateChanged;

        NetworkPlayerAction.OnNetworkActionResult -=
            OnNetworkActionResult;

        NetworkPlayerAction.OnShamanVictimPromptReceived -=
            OnShamanVictimPromptReceived;
    }

    private void OnShamanCharmsChanged(bool usedSave, bool usedKill)
    {
        shamanHasUsedSave = usedSave;
        shamanHasUsedKill = usedKill;
        Debug.Log($"SKILL UI | Shaman charms updated from server: SaveUsed={usedSave}, KillUsed={usedKill}");
        UpdateSkillUI();
    }

    private void OnHunterRevengeStateChanged(bool canShoot)
    {
        Debug.Log($"SKILL UI | Hunter Revenge State Changed from server: CanShoot={canShoot}");
        UpdateSkillUI();
    }

    private void Start()
    {
        CleanupUnusedText();
        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();

        ResetForNewMatch();

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
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

            // Đóng Target Panel khi đổi Phase (trừ khi Thợ Săn đang chờ bắn trả thù)
            if (targetPanel != null && !CanHunterRevengeNow())
            {
                targetPanel.SetActive(false);
            }

            if (!isNight)
            {
                HideShamanPrompt();
            }
        }

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasAssignedRole)
        {
            RoleType r = NetworkRoleSync.LocalInstance.LocalRole;
            bool roleRecognized = isDogSpirit || isWhiteHound || isSeer || isVillageGuardian || isShaman || isHunter || isKiller;
            if (!roleRecognized && (r == RoleType.DogSpirit || r == RoleType.WhiteHound || r == RoleType.WhiteWolf || r == RoleType.Seer || r == RoleType.VillageGuardian || r == RoleType.Shaman || r == RoleType.Hunter || r == RoleType.Killer))
            {
                OnRoleReceived(r);
            }
        }

        UpdateSkillUI();
    }

    private void OnRoleReceived(RoleType role)
    {
        isDogSpirit =
            role == RoleType.DogSpirit;

        isWhiteHound =
            role == RoleType.WhiteHound || role == RoleType.WhiteWolf;

        isWhiteHoundAwakened =
            NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.IsWhiteHoundAwakened;

        isSeer =
            role == RoleType.Seer;

        isVillageGuardian =
            role == RoleType.VillageGuardian;

        isShaman =
            role == RoleType.Shaman;

        isHunter =
            role == RoleType.Hunter;

        isKiller =
            role == RoleType.Killer;

        hasUsedSkill = false;
        if (NetworkRoleSync.LocalInstance != null)
        {
            shamanHasUsedSave = NetworkRoleSync.LocalInstance.HasUsedSave;
            shamanHasUsedKill = NetworkRoleSync.LocalInstance.HasUsedKill;
        }
        else
        {
            shamanHasUsedSave = false;
            shamanHasUsedKill = false;
        }
        currentPromptVictimId = -1;
        HideShamanPrompt();

        Debug.Log(
            "SKILL UI | Role = "
            + role
            + " | DogSpirit = "
            + isDogSpirit
            + " | WhiteHound = "
            + isWhiteHound
            + " | Awakened = "
            + isWhiteHoundAwakened
            + " | Seer = "
            + isSeer
            + " | VillageGuardian = "
            + isVillageGuardian
            + " | Shaman = "
            + isShaman
            + " | Hunter = "
            + isHunter
            + " | Killer = "
            + isKiller
        );

        UpdateSkillUI();
    }

    private void OnWhiteHoundAwakenedChanged(bool awakened)
    {
        isWhiteHoundAwakened = awakened;
        Debug.Log("SKILL UI | WhiteHound Awakened = " + awakened + " -> Cập nhật chức năng tấn công!");
        UpdateSkillUI();
    }

    private bool IsLocalWhiteHoundAwakened()
    {
        if (isWhiteHoundAwakened)
            return true;

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.IsWhiteHoundAwakened)
        {
            isWhiteHoundAwakened = true;
            return true;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            int localId = (int)NetworkManager.Singleton.LocalClientId;
            PlayerData p = PlayerManager.Instance?.GetplayerByID(localId);
            if (p != null && p.isWhiteHoundAwakened)
            {
                isWhiteHoundAwakened = true;
                return true;
            }
        }

        return false;
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

        return stateSync.State.Value == NetworkPlayerStateType.Dead ||
               stateSync.State.Value == NetworkPlayerStateType.Spectating ||
               stateSync.State.Value == NetworkPlayerStateType.Disconnected;
    }

    public int GetCurrentNightNumber()
    {
        if (phaseSync != null && phaseSync.CurrentNightCycle.Value > 0)
        {
            return phaseSync.CurrentNightCycle.Value;
        }

        if (NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0)
        {
            return NightManager.Instance.CurrentNightNumber;
        }

        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentDay > 0)
        {
            return GameRoleManager.Instance.currentDay;
        }

        return 1;
    }

    private int GetLocalPlayerId()
    {
        if (NetworkManager.Singleton != null)
        {
            return (int)NetworkManager.Singleton.LocalClientId;
        }
        return -1;
    }

    public bool IsHunterPendingRevenge()
    {
        if (!isHunter || !IsLocalPlayerDead()) return false;

        int localId = GetLocalPlayerId();
        PlayerData pData = localId >= 0 && PlayerManager.Instance != null ? PlayerManager.Instance.GetplayerByID(localId) : null;

        bool hasUsedShot = (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasUsedHunterShot) ||
                           (pData != null && pData.hasUsedHunterShot);

        if (hasUsedShot) return false;

        // Nếu NetworkRoleSync đã sync trạng thái bắn trả thù
        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.CanHunterRevengeShot)
        {
            return true;
        }

        if (pData != null)
        {
            bool validCause = pData.lastDeathCause == DeathCause.Monster || pData.lastDeathCause == DeathCause.Vote || pData.wasVotedOut;
            if (pData.hasHunterTrap || validCause)
                return true;
        }

        return false;
    }

    public bool CanHunterRevengeNow()
    {
        if (!IsHunterPendingRevenge()) return false;
        if (isNight) return false;

        if (NetworkPhaseSync.Instance != null)
        {
            GamePhase p = NetworkPhaseSync.Instance.CurrentPhase.Value;
            if (p == GamePhase.Setup || p == GamePhase.Night || p == GamePhase.GameOver)
                return false;
        }
        return true;
    }

    private void UpdateSkillUI()
    {
        bool hunterCanUseTrap = CanHunterRevengeNow();

        bool shamanCanAct =
            isShaman &&
            (!shamanHasUsedSave || !shamanHasUsedKill);

        int currentNight = GetCurrentNightNumber();
        bool isEvenNight = currentNight > 0 && currentNight % 2 == 0;

        bool killerCanAct =
            isKiller &&
            isNight &&
            isEvenNight &&
            !hasUsedSkill &&
            !IsLocalPlayerDead();

        bool canAttackAsDogSpirit =
            isDogSpirit || (isWhiteHound && IsLocalWhiteHoundAwakened());

        bool normalSkillAvailable =
            (
                (canAttackAsDogSpirit && !hasUsedSkill) ||
                (isSeer && !hasUsedSkill) ||
                (isVillageGuardian && !hasUsedSkill) ||
                shamanCanAct ||
                killerCanAct
            )
            &&
            isNight &&
            !IsLocalPlayerDead();

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
                (canAttackAsDogSpirit || (isKiller && isEvenNight)) &&
                isNight &&
                !hasUsedSkill &&
                !IsLocalPlayerDead()
            );
        }

        if (inspectButton != null)
        {
            inspectButton.SetActive(
                isSeer &&
                isNight &&
                !hasUsedSkill &&
                !IsLocalPlayerDead()
            );
        }

        if (protectButton != null)
        {
            protectButton.SetActive(
                isVillageGuardian &&
                isNight &&
                !hasUsedSkill &&
                !IsLocalPlayerDead()
            );
        }

        if (saveButton != null)
        {
            saveButton.SetActive(
                isShaman &&
                isNight &&
                !shamanHasUsedSave &&
                !IsLocalPlayerDead()
            );
        }

        if (killButton != null)
        {
            killButton.SetActive(
                isShaman &&
                isNight &&
                !shamanHasUsedKill &&
                !IsLocalPlayerDead()
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
            if (targetPanel != null && !CanHunterRevengeNow())
            {
                targetPanel.SetActive(false);
            }
        }
    }

    public void OnAttackClicked()
    {
        bool canAttackAsDogSpirit = isDogSpirit || (isWhiteHound && IsLocalWhiteHoundAwakened());
        if (!canAttackAsDogSpirit && !isKiller)
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

        if (isKiller)
        {
            int currentNight = GetCurrentNightNumber();
            if (currentNight <= 0 || currentNight % 2 != 0)
            {
                Debug.LogWarning(
                    "SKILL UI | Sát Nhân chỉ có thể tấn công vào đêm chẵn (Đêm hiện tại: "
                    + currentNight
                    + ")."
                );
                return;
            }
        }

        Debug.Log(
            "SKILL UI | "
            + (isDogSpirit ? "DogSpirit" : (isWhiteHound ? "WhiteHound (Phe DogSpirit)" : "Sát Nhân (Killer)"))
            + " mở Target Panel."
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

        if (shamanHasUsedSave)
        {
            Debug.LogWarning(
                "SKILL UI | Bùa Lợi (Cứu) đã được sử dụng."
            );
            return;
        }

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

        if (shamanHasUsedKill)
        {
            Debug.LogWarning(
                "SKILL UI | Bùa Hại (Giết) đã được sử dụng."
            );
            return;
        }

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

        if (!IsLocalPlayerDead())
        {
            Debug.LogWarning(
                "SKILL UI | Hunter chưa chết."
            );
            return;
        }

        if (!CanHunterRevengeNow())
        {
            Debug.LogWarning(
                "SKILL UI | Hunter không thể bắn trả thù ở thời điểm này hoặc đã dùng quyền bắn."
            );
            return;
        }

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

    private bool isClosingTargetPanel;

    public void CloseTargetPanel()
    {
        if (isClosingTargetPanel) return;
        isClosingTargetPanel = true;
        try
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
            if (targetListUI != null)
            {
                targetListUI.CloseTargetList();
            }
            NetworkTargetListUI.Instance?.CloseTargetList();
        }
        finally
        {
            isClosingTargetPanel = false;
        }
    }

    public void NotifyActionDispatched()
    {
        CloseTargetPanel();
        if (isDogSpirit || (isWhiteHound && IsLocalWhiteHoundAwakened()) || isSeer || isVillageGuardian || isKiller)
        {
            hasUsedSkill = true;
            UpdateSkillUI();
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
        {
            hasUsedSkill = false;
            UpdateSkillUI();
            return;
        }

        if (isHunter)
        {
            hasUsedSkill = true;
            int localId = GetLocalPlayerId();
            if (localId >= 0 && PlayerManager.Instance != null)
            {
                PlayerData me = PlayerManager.Instance.GetplayerByID(localId);
                if (me != null)
                {
                    me.hasHunterTrap = false;
                    me.hasUsedHunterShot = true;
                }
            }
            if (NetworkRoleSync.LocalInstance != null)
            {
                NetworkRoleSync.LocalInstance.HasUsedHunterShot = true;
            }
            Debug.Log("SKILL UI | Hunter đã thực hiện phát đạn trả thù thành công.");
        }
        else if (isShaman)
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
            hasUsedSkill = shamanHasUsedSave && shamanHasUsedKill;
        }
        else
        {
            hasUsedSkill = true;
        }

        CloseTargetPanel();
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
        rect.sizeDelta = new Vector2(660, 340);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image bgImage = shamanPromptPanel.GetComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.10f, 0.12f, 0.18f, 0.98f);

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
        titleRect.anchoredPosition = new Vector2(0, -20);
        titleRect.sizeDelta = new Vector2(-40, 50);

        TMPro.TextMeshProUGUI titleText = titleObj.GetComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "THÔNG BÁO BÙA LỢI PHÁP SƯ";
        titleText.fontSize = 32;
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
        msgRect.anchorMin = new Vector2(0, 0.38f);
        msgRect.anchorMax = new Vector2(1, 0.82f);
        msgRect.pivot = new Vector2(0.5f, 0.5f);
        msgRect.anchoredPosition = Vector2.zero;
        msgRect.sizeDelta = new Vector2(-60, 0);

        shamanPromptText = msgObj.GetComponent<TMPro.TextMeshProUGUI>();
        shamanPromptText.fontSize = 24;
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
        btnContainerRect.anchorMax = new Vector2(1, 0.38f);
        btnContainerRect.pivot = new Vector2(0.5f, 0);
        btnContainerRect.anchoredPosition = Vector2.zero;
        btnContainerRect.sizeDelta = new Vector2(-40, 0);

        // Save Button (Cứu)
        GameObject saveBtnObj = new GameObject(
            "PromptSaveButton",
            typeof(RectTransform),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button)
        );
        saveBtnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform saveBtnRect = saveBtnObj.GetComponent<RectTransform>();
        saveBtnRect.anchorMin = new Vector2(0.04f, 0.15f);
        saveBtnRect.anchorMax = new Vector2(0.48f, 0.88f);
        saveBtnRect.offsetMin = Vector2.zero;
        saveBtnRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image saveImg = saveBtnObj.GetComponent<UnityEngine.UI.Image>();
        saveImg.color = new Color(0.18f, 0.58f, 0.28f);

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
        saveText.fontSize = 26;
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
        dismissBtnRect.anchorMin = new Vector2(0.52f, 0.15f);
        dismissBtnRect.anchorMax = new Vector2(0.96f, 0.88f);
        dismissBtnRect.offsetMin = Vector2.zero;
        dismissBtnRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image dismissImg = dismissBtnObj.GetComponent<UnityEngine.UI.Image>();
        dismissImg.color = new Color(0.45f, 0.48f, 0.54f);

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
        dismissText.fontSize = 26;
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

    // =========================================================
    // TESTING AND SIMULATION HELPERS
    // =========================================================

    public void SetupForTest(
        RoleType role,
        bool isNightPhase,
        GameObject testSkillPanel = null,
        GameObject testSaveBtn = null,
        GameObject testKillBtn = null)
    {
        if (testSkillPanel != null) skillPanel = testSkillPanel;
        if (testSaveBtn != null) saveButton = testSaveBtn;
        if (testKillBtn != null) killButton = testKillBtn;

        isNight = isNightPhase;
        OnRoleReceived(role);
        UpdateSkillUI();
    }

    public void SetPhaseForTest(bool isNightPhase, bool isDiscussionPhase)
    {
        bool phaseChanged = isNightPhase != isNight || isDiscussionPhase != isDiscussion;
        if (phaseChanged)
        {
            hasUsedSkill = false;
            isNight = isNightPhase;
            isDiscussion = isDiscussionPhase;
            if (targetPanel != null) targetPanel.SetActive(false);
            if (!isNight) HideShamanPrompt();
        }
        UpdateSkillUI();
    }

    public void SimulateActionResultForTest(bool success, int subActionId)
    {
        pendingSubActionId = subActionId;
        OnNetworkActionResult(success, "Test Action");
    }

    public void SetShamanCharmState(bool usedSave, bool usedKill)
    {
        shamanHasUsedSave = usedSave;
        shamanHasUsedKill = usedKill;
        UpdateSkillUI();
    }
}