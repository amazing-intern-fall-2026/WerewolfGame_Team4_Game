using Unity.Netcode;
using UnityEngine;

public class NetworkGameTimer : NetworkBehaviour
{
    [Header("Thời gian từng Phase")]

    [SerializeField]
    private float roleRevealTime = 5f;

    [SerializeField]
    private float dayStartTime = 10f;

    [SerializeField]
    private float eventTime = 10f;

    [SerializeField]
    private float taskTime = 20f;

    // Khớp GameRoleManager của Dev2
    [SerializeField]
    private float nightTime = 15f;

    [SerializeField]
    private float resolveNightTime = 5f;

    // Khớp GameRoleManager của Dev2
    [SerializeField]
    private float discussionTime = 30f;

    // Khớp GameRoleManager của Dev2
    [SerializeField]
    private float votingTime = 20f;

    [SerializeField]
    private float resolveVoteTime = 5f;

    [SerializeField]
    private float gameOverTime = 5f;


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
        if (NetworkPhaseSync.Instance != null)
        {
            lastPhase =
                NetworkPhaseSync.Instance.CurrentPhase.Value;

            initialized = true;

            if (IsServer)
            {
                SetTimerForPhase(lastPhase);
            }
        }
    }


    private void Update()
    {
        if (!IsServer)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;


        // =========================
        // PHASE MỚI
        // =========================

        if (!initialized)
        {
            initialized = true;

            lastPhase =
                currentPhase;

            SetTimerForPhase(
                currentPhase
            );
        }
        else if (currentPhase != lastPhase)
        {
            Debug.Log(
                "NETWORK TIMER | Phase đổi: "
                + lastPhase
                + " → "
                + currentPhase
            );

            lastPhase =
                currentPhase;

            SetTimerForPhase(
                currentPhase
            );
        }


        // =========================
        // ĐẾM NGƯỢC
        // =========================

        if (TimeRemaining.Value <= 0f)
            return;

        TimeRemaining.Value -=
            Time.deltaTime;

        if (TimeRemaining.Value < 0f)
        {
            TimeRemaining.Value = 0f;
        }
    }


    public void SetTimerForPhase(
        GamePhase phase
    )
    {
        if (!IsServer)
            return;

        switch (phase)
        {
            case GamePhase.RoleReveal:

                TimeRemaining.Value =
                    roleRevealTime;

                break;


            case GamePhase.DayStart:

                TimeRemaining.Value =
                    dayStartTime;

                break;


            case GamePhase.Event:

                TimeRemaining.Value =
                    eventTime;

                break;


            case GamePhase.Task:

                TimeRemaining.Value =
                    taskTime;

                break;


            case GamePhase.Night:

                TimeRemaining.Value =
                    nightTime;

                break;


            case GamePhase.ResolveNight:

                TimeRemaining.Value =
                    resolveNightTime;

                break;


            case GamePhase.Discussion:

                TimeRemaining.Value =
                    discussionTime;

                break;


            case GamePhase.Voting:

                TimeRemaining.Value =
                    votingTime;

                break;


            case GamePhase.ResolveVote:

                TimeRemaining.Value =
                    resolveVoteTime;

                break;


            case GamePhase.GameOver:

                TimeRemaining.Value =
                    gameOverTime;

                break;


            default:

                TimeRemaining.Value =
                    0f;

                break;
        }


        Debug.Log(
            "NETWORK TIMER | "
            + phase
            + " | "
            + TimeRemaining.Value
            + " giây"
        );
    }


    // Giữ hàm cũ để không làm
    // hỏng các script Networking khác.
    public void SetTimerForState(
        NetworkGameState state
    )
    {
        if (!IsServer)
            return;

        switch (state)
        {
            case NetworkGameState.Night:

                TimeRemaining.Value =
                    nightTime;

                break;


            case NetworkGameState.Morning:

                TimeRemaining.Value =
                    dayStartTime;

                break;


            case NetworkGameState.Discussion:

                TimeRemaining.Value =
                    discussionTime;

                break;


            case NetworkGameState.Voting:

                TimeRemaining.Value =
                    votingTime;

                break;


            case NetworkGameState.Resolve:

                TimeRemaining.Value =
                    resolveVoteTime;

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