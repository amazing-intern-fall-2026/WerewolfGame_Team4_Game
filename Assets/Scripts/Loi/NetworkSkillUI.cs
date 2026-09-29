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

    [Header("Shaman Skill")]
    [SerializeField]
    private GameObject saveButton;

    [SerializeField]
    private GameObject killButton;

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
    private bool isShaman;

    private bool isNight;
    private bool hasUsedSkill;

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

        if (newIsNight != isNight)
        {
            isNight = newIsNight;

            if (isNight)
            {
                hasUsedSkill = false;
            }
            else
            {
                if (targetPanel != null)
                {
                    targetPanel.SetActive(false);
                }
            }

            UpdateSkillUI();
        }
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
        );

        UpdateSkillUI();
    }

    private void UpdateSkillUI()
    {
        bool canUseSkill =
            (
                isDogSpirit ||
                isSeer ||
                isVillageGuardian ||
                isShaman
            )
            &&
            isNight
            &&
            !hasUsedSkill;

        if (skillPanel != null)
        {
            skillPanel.SetActive(canUseSkill);
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
                !hasUsedSkill
            );
        }

        if (killButton != null)
        {
            killButton.SetActive(
                isShaman &&
                isNight &&
                !hasUsedSkill
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

        OpenTargetPanel();
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

        OpenTargetPanel();
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

        OpenTargetPanel();
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
            "SKILL UI | Shaman SAVE mở Target Panel."
        );

        OpenTargetPanel();
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
            "SKILL UI | Shaman KILL mở Target Panel."
        );

        OpenTargetPanel();
    }

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

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }

        UpdateSkillUI();
    }

    private void HideAll()
    {
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

        if (targetPanel != null)
            targetPanel.SetActive(false);
    }
}