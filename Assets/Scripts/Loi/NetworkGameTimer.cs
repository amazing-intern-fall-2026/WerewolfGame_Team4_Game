using Unity.Netcode;
using UnityEngine;

public class NetworkGameTimer : NetworkBehaviour
{
    [Header("DayStart Networking Timer")]
    [SerializeField]
    private float dayStartTime = 10f;

    public NetworkVariable<float> TimeRemaining =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private GamePhase lastPhase;
    private bool initialized;

    public override void OnNetworkSpawn()
    {
        if (NetworkPhaseSync.Instance == null)
            return;

        lastPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        initialized = true;

        if (IsServer)
        {
            SetupTimerForPhase(lastPhase);
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        if (GameRoleManager.Instance == null)
            return;

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        // =====================================
        // PHASE MỚI
        // =====================================

        if (!initialized)
        {
            initialized = true;
            lastPhase = currentPhase;

            SetupTimerForPhase(currentPhase);

            return;
        }

        if (currentPhase != lastPhase)
        {
            Debug.Log(
                "NETWORK TIMER | Phase đổi: "
                + lastPhase
                + " → "
                + currentPhase
            );

            lastPhase = currentPhase;

            SetupTimerForPhase(currentPhase);

            return;
        }

        // =====================================
        // DAY START
        // =====================================
        // DayStart không được GameRoleManager
        // tự đếm trong Update vì currentState = Day.
        //
        // Vì vậy Networking tự đếm DayStart.
        // =====================================

        if (currentPhase == GamePhase.DayStart)
        {
            if (TimeRemaining.Value > 0f)
            {
                TimeRemaining.Value -=
                    Time.deltaTime;

                if (TimeRemaining.Value < 0f)
                {
                    TimeRemaining.Value = 0f;
                }
            }

            return;
        }

        // =====================================
        // CÁC PHASE KHÁC
        // =====================================
        // RoleReveal / Night / Discussion / Voting
        // lấy thời gian trực tiếp từ Dev2.
        // =====================================

        SyncTimer();
    }

    private void SetupTimerForPhase(GamePhase phase)
    {
        if (!IsServer)
            return;

        // =====================================
        // DAY START
        // =====================================

        if (phase == GamePhase.DayStart)
        {
            TimeRemaining.Value =
                dayStartTime;

            Debug.Log(
                "NETWORK TIMER | "
                + "DayStart bắt đầu | "
                + dayStartTime
                + " giây"
            );

            return;
        }

        // =====================================
        // CÁC PHASE KHÁC
        // =====================================

        SyncTimer();
    }

    private void SyncTimer()
    {
        if (!IsServer)
            return;

        if (GameRoleManager.Instance == null)
            return;

        GamePhase currentPhase =
            GameRoleManager.Instance.currentPhase;

        // DayStart có timer riêng của Networking.
        if (currentPhase == GamePhase.DayStart)
            return;

        float dev2Time =
            GameRoleManager.Instance.PhaseTimeRemaining;

        if (dev2Time < 0f)
            dev2Time = 0f;

        TimeRemaining.Value =
            dev2Time;
    }

    // =====================================
    // GIỮ HÀM CŨ
    // =====================================

    public void SetTimerForPhase(GamePhase phase)
    {
        if (!IsServer)
            return;

        SetupTimerForPhase(phase);

        Debug.Log(
            "NETWORK TIMER | "
            + phase
            + " | "
            + TimeRemaining.Value
            + " giây"
        );
    }

    // =====================================
    // GIỮ HÀM CŨ
    // =====================================

    public void SetTimerForState(NetworkGameState state)
    {
        if (!IsServer)
            return;

        switch (state)
        {
            case NetworkGameState.Night:

                SyncTimer();

                break;

            case NetworkGameState.Morning:

                TimeRemaining.Value =
                    dayStartTime;

                break;

            case NetworkGameState.Discussion:

                SyncTimer();

                break;

            case NetworkGameState.Voting:

                SyncTimer();

                break;

            case NetworkGameState.Resolve:

                SyncTimer();

                break;

            default:

                TimeRemaining.Value =
                    0f;

                break;
        }

        Debug.Log(
            "SERVER TIMER: "
            + state
            + " | "
            + TimeRemaining.Value
            + " giây"
        );
    }
}