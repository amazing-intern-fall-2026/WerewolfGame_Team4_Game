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

            Debug.Log(
                "NETWORK PHASE AUTO SYNC: "
                + CurrentPhase.Value
            );
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