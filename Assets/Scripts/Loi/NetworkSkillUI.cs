using UnityEngine;

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

    [Header("Skill UI")]
    [SerializeField]
    private GameObject skillPanel;

    [Header("Target UI")]
    [SerializeField]
    private GameObject targetPanel;

    [SerializeField]
    private NetworkTargetListUI targetListUI;

    private NetworkPhaseSync phaseSync;

    private bool isDogSpirit;
    private bool isSeer;
    private bool isVillageGuardian;

    private bool isNight;

    private bool hasUsedSkill;

    // =========================================
    // Enable / Disable
    // =========================================

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived +=
            OnRoleReceived;

        NetworkPlayerAction.OnNetworkActionResult +=
            OnNetworkActionResult;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -=
            OnRoleReceived;

        NetworkPlayerAction.OnNetworkActionResult -=
            OnNetworkActionResult;
    }

    // =========================================
    // Start
    // =========================================

    private void Start()
    {
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

    // =========================================
    // Update
    // =========================================

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

        // =====================================
        // Phase thay đổi
        // =====================================

        if (newIsNight != isNight)
        {
            isNight = newIsNight;

            if (isNight)
            {
                // Night mới → cho phép dùng skill lại
                hasUsedSkill = false;
            }
            else
            {
                // Không còn Night → đóng Target Panel
                if (targetPanel != null)
                {
                    targetPanel.SetActive(false);
                }
            }

            UpdateSkillUI();
        }
    }

    // =========================================
    // Role Received
    // =========================================

    private void OnRoleReceived(RoleType role)
    {
        isDogSpirit =
            role == RoleType.DogSpirit;

        isSeer =
            role == RoleType.Seer;

        isVillageGuardian =
            role == RoleType.VillageGuardian;

        Debug.Log(
            "SKILL UI | Role = "
            + role
            + " | DogSpirit = "
            + isDogSpirit
            + " | Seer = "
            + isSeer
            + " | VillageGuardian = "
            + isVillageGuardian
        );

        UpdateSkillUI();
    }

    // =========================================
    // Update Skill UI
    // =========================================

    private void UpdateSkillUI()
    {
        bool canUseSkill =
            (
                isDogSpirit ||
                isSeer ||
                isVillageGuardian
            )
            &&
            isNight
            &&
            !hasUsedSkill;

        // =====================================
        // Skill Panel
        // =====================================

        if (skillPanel != null)
        {
            skillPanel.SetActive(canUseSkill);
        }

        // =====================================
        // DogSpirit → ATTACK
        // =====================================

        if (attackButton != null)
        {
            attackButton.SetActive(
                isDogSpirit &&
                isNight &&
                !hasUsedSkill
            );
        }

        // =====================================
        // Seer → INSPECT
        // =====================================

        if (inspectButton != null)
        {
            inspectButton.SetActive(
                isSeer &&
                isNight &&
                !hasUsedSkill
            );
        }

        // =====================================
        // VillageGuardian → PROTECT
        // =====================================

        if (protectButton != null)
        {
            protectButton.SetActive(
                isVillageGuardian &&
                isNight &&
                !hasUsedSkill
            );
        }

        // =====================================
        // Không có skill
        // =====================================

        if (!canUseSkill)
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
        }
    }

    // =========================================
    // DOGSPIRIT
    // =========================================

    public void OnAttackClicked()
    {
        if (!isDogSpirit)
        {
            return;
        }

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );

            return;
        }

        if (hasUsedSkill)
        {
            return;
        }

        Debug.Log(
            "SKILL UI | DogSpirit mở Target Panel."
        );

        OpenTargetPanel();
    }

    // =========================================
    // SEER
    // =========================================

    public void OnInspectClicked()
    {
        if (!isSeer)
        {
            return;
        }

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );

            return;
        }

        if (hasUsedSkill)
        {
            return;
        }

        Debug.Log(
            "SKILL UI | Seer mở Target Panel."
        );

        OpenTargetPanel();
    }

    // =========================================
    // VILLAGE GUARDIAN
    // =========================================

    public void OnProtectClicked()
    {
        if (!isVillageGuardian)
        {
            return;
        }

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | Chưa phải Night."
            );

            return;
        }

        if (hasUsedSkill)
        {
            return;
        }

        Debug.Log(
            "SKILL UI | VillageGuardian mở Target Panel."
        );

        OpenTargetPanel();
    }

    // =========================================
    // Open Target Panel
    // =========================================

    private void OpenTargetPanel()
    {
        if (targetListUI != null)
        {
            targetListUI.ShowTargetList();
        }
        else if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
    }

    // =========================================
    // Network Action Result
    // =========================================

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
            return;
        }

        hasUsedSkill = true;

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }

        UpdateSkillUI();
    }

    // =========================================
    // Hide All
    // =========================================

    private void HideAll()
    {
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        if (attackButton != null)
        {
            attackButton.SetActive(false);
        }

        if (inspectButton != null)
        {
            inspectButton.SetActive(false);
        }

        if (protectButton != null)
        {
            protectButton.SetActive(false);
        }

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
    }
}