using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseTimerController : MonoBehaviour
{
    private NetworkGameTimer gameTimer;
    private NetworkPhaseSync phaseSync;

    private GamePhase lastPhase;
    private bool initialized;

    private void Start()
    {
        gameTimer =
            FindFirstObjectByType<NetworkGameTimer>();

        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();
    }

    private void Update()
    {
        // Chỉ Server xử lý chuyển phase
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        if (gameTimer == null)
        {
            gameTimer =
                FindFirstObjectByType<NetworkGameTimer>();

            if (gameTimer == null)
                return;
        }

        if (phaseSync == null)
        {
            phaseSync =
                FindFirstObjectByType<NetworkPhaseSync>();

            if (phaseSync == null)
                return;
        }

        GamePhase currentPhase =
            phaseSync.CurrentPhase.Value;

        // =========================
        // KHỞI TẠO
        // =========================

        if (!initialized)
        {
            initialized = true;
            lastPhase = currentPhase;
            return;
        }

        // =========================
        // PHASE ĐÃ ĐỔI
        // =========================

        if (currentPhase != lastPhase)
        {
            Debug.Log(
                "PHASE TIMER CONTROLLER | "
                + lastPhase
                + " → "
                + currentPhase
            );

            lastPhase = currentPhase;
            return;
        }

        // =========================
        // TIMER CHƯA HẾT
        // =========================

        if (gameTimer.TimeRemaining.Value > 0f)
            return;

        // =========================
        // TIMER HẾT
        // =========================

        HandlePhaseFinished(currentPhase);
    }

    private void HandlePhaseFinished(
        GamePhase phase
    )
    {
        if (GameRoleManager.Instance == null)
            return;

        switch (phase)
        {
            case GamePhase.DayStart:

                HandleDayStartFinished();

                break;
        }
    }

    private void HandleDayStartFinished()
    {
        Debug.Log(
            "PHASE TIMER CONTROLLER | "
            + "DayStart hết giờ → EndDay()"
        );

        GameRoleManager gameRoleManager =
            GameRoleManager.Instance;

        if (
            gameRoleManager.currentState
            != GameState.Day
        )
        {
            return;
        }

        gameRoleManager.EndDay();
    }
}