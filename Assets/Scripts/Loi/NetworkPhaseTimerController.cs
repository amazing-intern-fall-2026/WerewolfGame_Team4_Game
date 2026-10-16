using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseTimerController : MonoBehaviour
{
    private NetworkGameTimer gameTimer;
    private NetworkPhaseSync phaseSync;

    private GamePhase lastPhase;
    private bool initialized;
    private bool phaseFinishedHandled;

    private void Start()
    {
        _ = NetworkGameOverManager.Instance;

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

        // Dừng xử lý timer khi Game Over và kích hoạt UI nếu có winner hợp lệ
        if (NetworkGameOverManager.GameEnded)
        {
            string winner = GameRoleManager.Instance != null ? GameRoleManager.Instance.Winner : null;
            if (string.IsNullOrEmpty(winner) && NetworkGameOverManager.Instance != null)
                winner = NetworkGameOverManager.Instance.currentWinner;
            if (!string.IsNullOrEmpty(winner))
            {
                NetworkGameOverManager.Instance.ShowGameOverUI(winner);
            }
            return;
        }

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
            phaseFinishedHandled = false;
            return;
        }

        // Bỏ qua nếu phase hiện tại là GameOver
        if (currentPhase == GamePhase.GameOver)
            return;

        // =========================
        // TIMER CHƯA HẾT
        // =========================

        if (gameTimer.TimeRemaining.Value > 0f)
        {
            phaseFinishedHandled = false;
            return;
        }

        // =========================
        // TIMER HẾT
        // =========================

        if (phaseFinishedHandled)
            return;

        phaseFinishedHandled = true;
        HandlePhaseFinished(currentPhase);
    }

    private void HandlePhaseFinished(
        GamePhase phase
    )
    {
        if (GameRoleManager.Instance == null)
            return;

        // Không chuyển phase sau khi Game Over
        if (NetworkGameOverManager.GameEnded || phase == GamePhase.GameOver)
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

    public void ResetForNewMatch()
    {
        lastPhase = GamePhase.Setup;
        phaseFinishedHandled = false;
        initialized = false;
        Debug.Log("PHASE TIMER CONTROLLER: Reset For New Match hoàn tất.");
    }
}