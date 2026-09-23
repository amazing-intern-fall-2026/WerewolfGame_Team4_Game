using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public Slider progressSlider;
    public TMP_Text progressText;
    public Slider dailyProgressSlider;
    public TMP_Text dailyProgressText;
    public TMP_Text phaseText;
    public TMP_Text timerText;
    public TMP_Text taskListText;
    public GameObject meetingPanel;
    public GameObject resultPanel;
    public TMP_Text resultText;
    [Header("Voting")]
    public TMP_Text meetingTitle;
    public TMP_Text votingStatusText;
    public int localVoterID;

    private void ConfigureSingleLineText(TMP_Text label)
    {
        if (label == null)
            return;

        float originalSize = Mathf.Max(1f, label.fontSize);

        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMax = originalSize;
        label.fontSizeMin = Mathf.Min(originalSize, 18f);
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void Start()
    {
        // Phải chạy trước return của meetingPanel.
        ConfigureSingleLineText(phaseText);
        ConfigureSingleLineText(timerText);

        // Giữ phần tìm reference của prototype hiện có.
        if (meetingPanel == null)
            return;

        if (meetingTitle == null)
        {
            meetingTitle = meetingPanel.transform
                .Find("Title")?.GetComponent<TMP_Text>();
        }

        if (votingStatusText == null)
        {
            votingStatusText = meetingPanel.transform
                .Find("Hint")?.GetComponent<TMP_Text>();
        }
    }

    private string BuildDayLabel(int day)
    {
        string dayLabel = $"Ngày {day}";
        EventManagert events = EventManagert.Instance;

        if (events == null)
            return dayLabel;

        GameEventType todayEvent = events.GetEventForDay(day);

        if (todayEvent == GameEventType.None)
            return dayLabel;

        return dayLabel + "-" + EventManagert.GetDisplayName(todayEvent);
    }

    private void RefreshDayAndTimer(GameRoleManager game)
    {
        if (game == null)
            return;

        if (phaseText != null)
            phaseText.text = BuildDayLabel(game.currentDay);

        float seconds = game.currentState == GameState.Day &&
                        DayTimer.Instance != null
            ? DayTimer.Instance.TimeRemaining
            : game.PhaseTimeRemaining;

        if (timerText != null)
        {
            int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            timerText.text = $"{GetCompactPhaseName(game.currentPhase)} | {remainingSeconds}s";
        }
    }

    private static string GetCompactPhaseName(GamePhase phase)
    {
        // The existing timer occupies a narrow section of the prototype HUD.
        switch (phase)
        {
            case GamePhase.RoleReveal: return "Role";
            case GamePhase.DayStart: return "Day";
            case GamePhase.Task: return "Task";
            case GamePhase.Event: return "Event";
            case GamePhase.Discussion: return "Talk";
            case GamePhase.Voting: return "Vote";
            case GamePhase.ResolveVote: return "Count";
            case GamePhase.Night: return "Night";
            case GamePhase.ResolveNight: return "Dawn";
            case GamePhase.GameOver: return "End";
            default: return "Phase";
        }
    }

    private void Update()
    {
        var game = GameRoleManager.Instance;
        var tasks = TaskManager.Instance;
        if (game == null || tasks == null) return;
        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = 100;
            progressSlider.value = tasks.progress;
        }
        if (progressText != null) progressText.text = $"{tasks.progress:0}%";
        if (dailyProgressSlider != null)
        {
            dailyProgressSlider.minValue = 0;
            dailyProgressSlider.maxValue = 100;
            dailyProgressSlider.value = tasks.DailyProgress;
        }
        if (dailyProgressText != null)
            dailyProgressText.text = $"Trong ngày: {tasks.CompletedToday}/{tasks.currentTasks.Count} — {tasks.DailyProgress:0}%";
        RefreshDayAndTimer(game);
        if (taskListText != null)
        {
            var text = new StringBuilder("NHIỆM VỤ\n");
            foreach (var task in tasks.currentTasks)
                if (task != null) text.AppendLine($"{(tasks.IsCompleted(task) ? "[x]" : "[ ]")} {task.taskName}");
            taskListText.text = text.ToString();
        }
        if (meetingPanel != null) meetingPanel.SetActive(
            game.currentState == GameState.Discussion || game.currentState == GameState.Voting);
        if (meetingTitle != null) meetingTitle.text = game.currentState == GameState.Voting ? "BỎ PHIẾU" : "THẢO LUẬN";
        if (votingStatusText != null)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(localVoterID);
            int chosen = VoteManager.Instance != null ? VoteManager.Instance.GetVotedTarget(localVoterID) : -1;
            if (game.currentState == GameState.Discussion)
                votingStatusText.text = $"Chờ {Mathf.CeilToInt(game.PhaseTimeRemaining)}s để bắt đầu bỏ phiếu.";
            else if (voter == null) votingStatusText.text = "Không tìm thấy dữ liệu người bỏ phiếu.";
            else if (!voter.isAlive) votingStatusText.text = "Bạn đã bị loại nên không thể bỏ phiếu. Nhấn R để thử lại.";
            else if (voter.hasVoted)
                votingStatusText.text = chosen >= 0 ? $"Đã bỏ phiếu cho Player {chosen + 1}. Chờ kết quả." : "Bạn đã bỏ phiếu. Chờ kết quả.";
            else votingStatusText.text = $"Bạn là Player {localVoterID + 1}. Bấm một người còn sống để bỏ phiếu.";
        }
        bool ended = game.currentState == GameState.GameOver;
        if (resultPanel != null) resultPanel.SetActive(ended);
        if (resultText != null && ended) resultText.text =
            game.Winner == "Villagers" ? "DÂN LÀNG THẮNG" : "MA SÓI THẮNG";
    }
}

