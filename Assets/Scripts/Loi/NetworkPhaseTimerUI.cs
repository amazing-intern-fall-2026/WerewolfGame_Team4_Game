using TMPro;
using UnityEngine;

public class NetworkPhaseTimerUI : MonoBehaviour
{
    [Header("Timer Text")]
    [SerializeField]
    private TextMeshProUGUI timerText;

    [Header("Phase Text")]
    [SerializeField]
    private TextMeshProUGUI phaseText;

    private NetworkGameTimer gameTimer;
    private NetworkPhaseSync phaseSync;

    private void Start()
    {
        gameTimer =
            FindFirstObjectByType<NetworkGameTimer>();

        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();

        if (gameTimer == null)
        {
            Debug.LogWarning(
                "TIMER UI: Không tìm thấy NetworkGameTimer!"
            );
        }

        if (phaseSync == null)
        {
            Debug.LogWarning(
                "TIMER UI: Không tìm thấy NetworkPhaseSync!"
            );
        }
    }

    private void Update()
    {
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

        float time =
            gameTimer.TimeRemaining.Value;

        GamePhase phase =
            phaseSync.CurrentPhase.Value;

        // Hiển thị Phase
        if (phaseText != null)
        {
            phaseText.text =
                GetPhaseName(phase);
        }

        // Hiển thị thời gian
        if (timerText != null)
        {
            int seconds =
                Mathf.CeilToInt(time);

            timerText.text =
                seconds.ToString();
        }
    }

    private string GetPhaseName(
        GamePhase phase
    )
    {
        switch (phase)
        {
            case GamePhase.RoleReveal:
                return "ROLE REVEAL";

            case GamePhase.DayStart:
                return "DAY START";

            case GamePhase.Event:
                return "EVENT";

            case GamePhase.Task:
                return "TASK";

            case GamePhase.Night:
                return "NIGHT";

            case GamePhase.ResolveNight:
                return "RESOLVE NIGHT";

            case GamePhase.Discussion:
                return "DISCUSSION";

            case GamePhase.Voting:
                return "VOTING";

            case GamePhase.ResolveVote:
                return "RESOLVE VOTE";

            case GamePhase.GameOver:
                return "GAME OVER";

            default:
                return phase.ToString();
        }
    }
}