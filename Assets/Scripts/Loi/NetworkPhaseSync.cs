using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseSync : NetworkBehaviour
{
    public static NetworkPhaseSync Instance;

    public NetworkVariable<GamePhase> CurrentPhase =
        new NetworkVariable<GamePhase>(
            GamePhase.RoleReveal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // NIGHT CYCLE
    // =========================================================

    public NetworkVariable<int> CurrentNightCycle =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private int lastNetworkNightDay = -1;

    // =========================================================
    // WINNER
    // =========================================================

    public NetworkVariable<Unity.Collections.FixedString64Bytes> Winner =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        _ = NetworkGameOverManager.Instance;

        CurrentPhase.OnValueChanged += OnPhaseChanged;
        CurrentNightCycle.OnValueChanged += OnNightCycleChanged;

        Debug.Log(
            "NetworkPhaseSync Spawned | Phase = "
            + CurrentPhase.Value
            + " | NightCycle = "
            + CurrentNightCycle.Value
        );

        if (IsServer)
        {
            SyncInitialPhase();
        }
    }

    // =========================================================
    // NETWORK DESPAWN
    // =========================================================

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
        CurrentNightCycle.OnValueChanged -= OnNightCycleChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsServer)
            return;

        if (GameRoleManager.Instance == null)
            return;

        GameRoleManager roleManager =
            GameRoleManager.Instance;

        GamePhase dev2Phase =
            roleManager.currentPhase;

        // =====================================================
        // SYNC PHASE
        // =====================================================

        if (CurrentPhase.Value != dev2Phase)
        {
            CurrentPhase.Value = dev2Phase;

            if (dev2Phase == GamePhase.GameOver)
            {
                string winnerName = roleManager.Winner;
                if (string.IsNullOrEmpty(winnerName))
                    winnerName = "Draw";

                Winner.Value = new Unity.Collections.FixedString64Bytes(winnerName);

                Debug.Log(
                    "NETWORK PHASE AUTO SYNC: GameOver | Winner = "
                    + winnerName
                );

                NetworkGameOverManager.Instance.ShowGameOverUI(winnerName);
            }
            else
            {
                Debug.Log(
                    "NETWORK PHASE AUTO SYNC: "
                    + CurrentPhase.Value
                );
            }
        }

        // Luôn đảm bảo GameOver UI được hiển thị nếu phase hiện tại là GameOver
        if (CurrentPhase.Value == GamePhase.GameOver || (roleManager != null && roleManager.currentState == GameState.GameOver))
        {
            string winner = Winner.Value.ToString();
            if (string.IsNullOrEmpty(winner) && roleManager != null)
                winner = roleManager.Winner;
            if (string.IsNullOrEmpty(winner))
                winner = "Draw";

            NetworkGameOverManager.Instance.ShowGameOverUI(winner);
        }

        // =====================================================
        // SYNC NIGHT CYCLE
        // =====================================================

        if (dev2Phase == GamePhase.Night)
        {
            int currentDay =
                roleManager.currentDay;

            if (currentDay != lastNetworkNightDay)
            {
                lastNetworkNightDay =
                    currentDay;

                CurrentNightCycle.Value =
                    currentDay;

                Debug.Log(
                    "NETWORK NIGHT CYCLE → "
                    + CurrentNightCycle.Value
                    + " | Day="
                    + currentDay
                );
            }
        }
    }

    // =========================================================
    // INITIAL PHASE
    // =========================================================

    private void SyncInitialPhase()
    {
        if (GameRoleManager.Instance == null)
            return;

        GameRoleManager roleManager =
            GameRoleManager.Instance;

        GamePhase initialPhase =
            roleManager.currentPhase;

        CurrentPhase.Value =
            initialPhase;

        if (initialPhase == GamePhase.GameOver)
        {
            string winnerName = roleManager.Winner;
            if (string.IsNullOrEmpty(winnerName))
                winnerName = "Draw";

            Winner.Value = new Unity.Collections.FixedString64Bytes(winnerName);
        }

        Debug.Log(
            "SERVER: Initial Phase → "
            + initialPhase
        );

        // Nếu game bắt đầu trực tiếp ở Night
        if (initialPhase == GamePhase.Night)
        {
            lastNetworkNightDay =
                roleManager.currentDay;

            CurrentNightCycle.Value =
                roleManager.currentDay;

            Debug.Log(
                "SERVER: Initial Night Cycle → "
                + CurrentNightCycle.Value
            );
        }
    }

    // =========================================================
    // MANUAL SET PHASE
    // =========================================================

    public void SetNetworkPhase(GamePhase phase)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "NETWORK PHASE: Chỉ Server mới được set Phase."
            );

            return;
        }

        CurrentPhase.Value =
            phase;

        Debug.Log(
            "NETWORK PHASE: SERVER SET → "
            + phase
        );
    }

    // =========================================================
    // PHASE CHANGED
    // =========================================================

    private void OnPhaseChanged(
        GamePhase oldPhase,
        GamePhase newPhase)
    {
        Debug.Log(
            "NETWORK PHASE: "
            + oldPhase
            + " → "
            + newPhase
        );

        if (newPhase == GamePhase.GameOver)
        {
            string winnerName = Winner.Value.ToString();
            if (string.IsNullOrEmpty(winnerName) && GameRoleManager.Instance != null)
                winnerName = GameRoleManager.Instance.Winner;
            if (string.IsNullOrEmpty(winnerName))
                winnerName = "Draw";

            NetworkGameOverManager.Instance.ShowGameOverUI(winnerName);
        }
        else if (oldPhase == GamePhase.GameOver && newPhase != GamePhase.GameOver)
        {
            NetworkGameOverManager.Instance?.ResetUIState();
        }
    }

    // =========================================================
    // CLIENT RPC: RESET CLIENT MATCH STATE
    // =========================================================

    [ClientRpc]
    public void ResetClientMatchStateClientRpc()
    {
        Debug.Log("[NetworkPhaseSync] ResetClientMatchStateClientRpc: Đặt lại UI và Skill trên Client.");
        NetworkGameOverManager.Instance?.ResetUIState();
        NetworkSkillUI.Instance?.ResetForNewMatch();
        NetworkHunterDeathUI.Instance?.ResetForNewMatch();
        NetworkDeathNotification.Instance?.HideDeathNotification();
    }

    // =========================================================
    // NIGHT CYCLE CHANGED
    // =========================================================

    private void OnNightCycleChanged(
        int oldNight,
        int newNight)
    {
        Debug.Log(
            "NETWORK NIGHT CYCLE: "
            + oldNight
            + " → "
            + newNight
        );
    }
}