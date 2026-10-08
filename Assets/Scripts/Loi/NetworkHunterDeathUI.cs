using UnityEngine;

public class NetworkHunterDeathUI : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField]
    private NetworkTargetListUI targetListUI;

    [SerializeField]
    private GameObject targetPanel;

    private NetworkPlayerStateSync localStateSync;
    private NetworkPhaseSync phaseSync;

    private bool isHunter;
    private bool hunterHasDied;
    private bool hasOpenedTargetUI;

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived += OnRoleReceived;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;

        if (localStateSync != null)
        {
            localStateSync.State.OnValueChanged -= OnStateChanged;
        }

        if (phaseSync != null)
        {
            phaseSync.CurrentPhase.OnValueChanged -= OnPhaseChanged;
        }
    }

    private void Start()
    {
        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();

        if (phaseSync != null)
        {
            phaseSync.CurrentPhase.OnValueChanged -=
                OnPhaseChanged;

            phaseSync.CurrentPhase.OnValueChanged +=
                OnPhaseChanged;
        }

        FindLocalState();

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }

        CheckHunterDiscussion();
    }

    private void Update()
    {
        if (localStateSync == null)
        {
            FindLocalState();
        }

        if (phaseSync == null)
        {
            phaseSync =
                FindFirstObjectByType<NetworkPhaseSync>();

            if (phaseSync != null)
            {
                phaseSync.CurrentPhase.OnValueChanged -=
                    OnPhaseChanged;

                phaseSync.CurrentPhase.OnValueChanged +=
                    OnPhaseChanged;
            }
        }

        /*
         * Kiểm tra liên tục để tránh trường hợp
         * Hunter chết trước khi UI được tạo/subcribe event.
         */
        CheckHunterDiscussion();
    }

    private void OnRoleReceived(RoleType role)
    {
        isHunter =
            role == RoleType.Hunter;

        Debug.Log(
            "HUNTER UI | Role = "
            + role
            + " | IsHunter = "
            + isHunter
        );

        if (!isHunter)
        {
            hunterHasDied = false;
            hasOpenedTargetUI = false;

            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
        }
    }

    private void FindLocalState()
    {
        if (localStateSync != null)
            return;

        NetworkPlayerStateSync[] states =
            FindObjectsByType<NetworkPlayerStateSync>(
                FindObjectsSortMode.None
            );

        foreach (NetworkPlayerStateSync state in states)
        {
            if (state == null)
                continue;

            if (!state.IsOwner)
                continue;

            localStateSync = state;

            localStateSync.State.OnValueChanged -=
                OnStateChanged;

            localStateSync.State.OnValueChanged +=
                OnStateChanged;

            Debug.Log(
                "HUNTER UI | Đã tìm thấy State của local Player."
            );

            if (
                state.State.Value ==
                NetworkPlayerStateType.Dead
            )
            {
                hunterHasDied = true;

                Debug.Log(
                    "HUNTER UI | Hunter đã chết trước khi UI khởi tạo."
                );
            }

            break;
        }
    }

    private void OnStateChanged(
        NetworkPlayerStateType oldState,
        NetworkPlayerStateType newState)
    {
        Debug.Log(
            "HUNTER UI | State: "
            + oldState
            + " → "
            + newState
        );

        if (!isHunter)
            return;

        if (
            newState ==
            NetworkPlayerStateType.Dead
        )
        {
            hunterHasDied = true;

            Debug.Log(
                "HUNTER UI | Hunter đã chết."
            );
        }
    }

    private void OnPhaseChanged(
        GamePhase oldPhase,
        GamePhase newPhase)
    {
        Debug.Log(
            "HUNTER UI | Phase: "
            + oldPhase
            + " → "
            + newPhase
        );

        CheckHunterDiscussion();
    }

    private void CheckHunterDiscussion()
    {
        if (!isHunter)
            return;

        if (!hunterHasDied)
            return;

        if (hasOpenedTargetUI)
            return;

        if (phaseSync == null)
            return;

        GamePhase currentPhase =
            phaseSync.CurrentPhase.Value;

        /*
         * Hunter chỉ được bắn khi Discussion bắt đầu.
         */
        if (currentPhase != GamePhase.Discussion)
            return;

        OpenHunterTargetUI();
    }

    private void OpenHunterTargetUI()
    {
        if (!isHunter)
            return;

        if (!hunterHasDied)
            return;

        if (hasOpenedTargetUI)
            return;

        hasOpenedTargetUI = true;

        Debug.Log(
            "HUNTER UI | Discussion → mở Target Panel."
        );

        if (targetListUI != null)
        {
            targetListUI.ShowTargetList();
        }
        else if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "HUNTER UI | Chưa gán TargetListUI hoặc TargetPanel."
            );
        }
    }
}