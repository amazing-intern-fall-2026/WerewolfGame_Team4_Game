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

    [Header("WeaverOfFate Skill")]
    [SerializeField]
    private GameObject weaverButton;

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
    private bool isHunter;
    private bool isWeaverOfFate;

    private bool isNight;
    private bool isDiscussion;
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

        isWeaverOfFate =
            role == RoleType.WeaverOfFate;

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
            + " | WeaverOfFate = "
            + isWeaverOfFate
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

        bool normalSkillAvailable =
            (
                isDogSpirit ||
                isSeer ||
                isVillageGuardian ||
                isShaman ||
                isWeaverOfFate
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

        if (trapButton != null)
        {
            trapButton.SetActive(
                hunterCanUseTrap
            );
        }

        if (weaverButton != null)
        {
            weaverButton.SetActive(
                isWeaverOfFate &&
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

        OpenTargetPanel();
    }

    public void OnWeaverClicked()
    {
        if (!isWeaverOfFate)
            return;

        if (!isNight)
        {
            Debug.LogWarning(
                "SKILL UI | WeaverOfFate chỉ dùng ở Night."
            );
            return;
        }

        if (hasUsedSkill)
            return;

        Debug.Log(
            "SKILL UI | WeaverOfFate mở Target Panel."
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

        if (trapButton != null)
            trapButton.SetActive(false);

        if (weaverButton != null)
            weaverButton.SetActive(false);

        if (targetPanel != null)
            targetPanel.SetActive(false);
    }
}