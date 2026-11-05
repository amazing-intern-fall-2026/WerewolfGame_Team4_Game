using Unity.Netcode;
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
    private bool hasUsedHunterShot;

    public static NetworkHunterDeathUI Instance { get; private set; }

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

    public void ResetForNewMatch()
    {
        hunterHasDied = false;
        hasOpenedTargetUI = false;
        hasUsedHunterShot = false;
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
        Debug.Log("HUNTER DEATH UI: Reset For New Match hoàn tất.");
    }

    private void OnEnable()
    {
        NetworkRoleSync.OnLocalRoleReceived += OnRoleReceived;
        NetworkRoleSync.OnLocalHunterRevengeStateChanged += OnHunterRevengeStateChanged;
        NetworkPlayerAction.OnNetworkActionResult += OnNetworkActionResult;
    }

    private void OnDisable()
    {
        NetworkRoleSync.OnLocalRoleReceived -= OnRoleReceived;
        NetworkRoleSync.OnLocalHunterRevengeStateChanged -= OnHunterRevengeStateChanged;
        NetworkPlayerAction.OnNetworkActionResult -= OnNetworkActionResult;

        if (localStateSync != null)
        {
            localStateSync.State.OnValueChanged -= OnStateChanged;
        }

        if (phaseSync != null)
        {
            phaseSync.CurrentPhase.OnValueChanged -= OnPhaseChanged;
        }
    }

    private void OnHunterRevengeStateChanged(bool canShoot)
    {
        Debug.Log($"HUNTER UI | Nhận sync quyền bắn trả thù từ Server: {canShoot}");
        if (canShoot)
        {
            hunterHasDied = true;
            if (targetPanel != null && targetPanel.activeSelf && isHunter && !hasUsedHunterShot)
        {
            NetworkDeathNotification.Instance?.HideDeathNotification();
        }

        CheckHunterDiscussion();
        }
    }

    private void OnNetworkActionResult(bool success, string message)
    {
        if (!isHunter) return;
        if (success)
        {
            hasUsedHunterShot = true;
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
            }
            if (targetListUI != null)
            {
                targetListUI.CloseTargetList();
            }
            Debug.Log("HUNTER UI | Thợ Săn đã hoàn thành phát bắn trả thù.");
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

        ResetForNewMatch();

        FindLocalState();

        if (NetworkRoleSync.LocalInstance != null)
        {
            OnRoleReceived(
                NetworkRoleSync.LocalInstance.LocalRole
            );
        }

        if (targetPanel != null && targetPanel.activeSelf && isHunter && !hasUsedHunterShot)
        {
            NetworkDeathNotification.Instance?.HideDeathNotification();
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
        if (targetPanel != null && targetPanel.activeSelf && isHunter && !hasUsedHunterShot)
        {
            NetworkDeathNotification.Instance?.HideDeathNotification();
        }

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

            if (targetPanel != null && targetPanel.activeSelf && isHunter && !hasUsedHunterShot)
        {
            NetworkDeathNotification.Instance?.HideDeathNotification();
        }

        CheckHunterDiscussion();
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

        if (targetPanel != null && targetPanel.activeSelf && isHunter && !hasUsedHunterShot)
        {
            NetworkDeathNotification.Instance?.HideDeathNotification();
        }

        CheckHunterDiscussion();
    }

    public void TriggerHunterRevenge()
    {
        hunterHasDied = true;
        hasOpenedTargetUI = false;
        OpenHunterTargetUI();
    }

    private void CheckHunterDiscussion()
    {
        if (!isHunter)
            return;

        if (hasUsedHunterShot)
            return;

        int localId = NetworkManager.Singleton != null ? (int)NetworkManager.Singleton.LocalClientId : -1;
        PlayerData me = localId >= 0 && PlayerManager.Instance != null ? PlayerManager.Instance.GetplayerByID(localId) : null;

        if (me != null && me.hasUsedHunterShot)
            return;

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasUsedHunterShot)
            return;

        if (!hunterHasDied)
        {
            if (me != null && !me.isAlive)
            {
                hunterHasDied = true;
            }
            else if (localStateSync != null && localStateSync.State.Value == NetworkPlayerStateType.Dead)
            {
                hunterHasDied = true;
            }
        }

        if (!hunterHasDied)
            return;

        // Kiểm tra nguyên nhân tử vong: Chỉ Monster (DogSpirit) hoặc Vote
        if (me != null && me.hasDeathRecord)
        {
            bool validCause = me.lastDeathCause == DeathCause.Monster || me.lastDeathCause == DeathCause.Vote || me.wasVotedOut;
            bool serverPermitted = NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.CanHunterRevengeShot;
            if (!validCause && !serverPermitted)
            {
                return;
            }
        }

        if (hasOpenedTargetUI && targetPanel != null && targetPanel.activeSelf)
            return;

        if (phaseSync == null)
            phaseSync = FindFirstObjectByType<NetworkPhaseSync>();

        if (phaseSync == null)
            return;

        GamePhase currentPhase = phaseSync.CurrentPhase.Value;

        // Không mở trong lúc Setup, Night (chờ trời sáng), hoặc GameOver
        if (currentPhase == GamePhase.Setup || currentPhase == GamePhase.Night || currentPhase == GamePhase.GameOver)
            return;

        // Cho phép Hunter bắn khi bị vote chết hoặc bị DogSpirit cắn chết khi trời sáng (Discussion, Voting, ResolveVote, DayResolution, DayStart)
        OpenHunterTargetUI();
    }

    public void OpenHunterTargetUI()
    {
        if (!isHunter)
            return;

        if (hasUsedHunterShot)
            return;

        int myId = NetworkManager.Singleton != null ? (int)NetworkManager.Singleton.LocalClientId : -1;
        PlayerData myData = myId >= 0 && PlayerManager.Instance != null ? PlayerManager.Instance.GetplayerByID(myId) : null;
        if (myData != null && myData.hasUsedHunterShot)
            return;

        if (NetworkRoleSync.LocalInstance != null && NetworkRoleSync.LocalInstance.HasUsedHunterShot)
            return;

        hasOpenedTargetUI = true;

        Debug.Log(
            "HUNTER UI | Mở Target Panel để Thợ Săn bắn trả thù."
        );

        // Đóng thông báo Bạn đã chết để không che chắn bảng chọn mục tiêu
        NetworkDeathNotification.Instance?.HideDeathNotification();

        if (targetListUI != null)
        {
            targetListUI.ShowTargetList();
        }
        else if (targetPanel != null)
        {
            targetPanel.SetActive(true);
        }
        else if (NetworkTargetListUI.Instance != null)
        {
            NetworkTargetListUI.Instance.ShowTargetList();
        }
        else
        {
            Debug.LogWarning(
                "HUNTER UI | Chưa gán TargetListUI hoặc TargetPanel."
            );
        }
    }
}