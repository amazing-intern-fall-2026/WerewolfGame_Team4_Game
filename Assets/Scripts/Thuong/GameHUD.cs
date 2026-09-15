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

    private void Start()
    {
        // Backwards compatible with the existing prototype scene.
        if (meetingPanel == null) return;
        if (meetingTitle == null) meetingTitle = meetingPanel.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (votingStatusText == null) votingStatusText = meetingPanel.transform.Find("Hint")?.GetComponent<TMP_Text>();
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
        if (phaseText != null) phaseText.text = $"Ngày {game.currentDay} — {game.currentPhase}";
        float seconds = game.currentState == GameState.Day && DayTimer.Instance != null
            ? DayTimer.Instance.TimeRemaining : game.PhaseTimeRemaining;
        if (timerText != null) timerText.text = $"{Mathf.CeilToInt(seconds)}s";
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
            var voter = PlayerManger.Instance?.GetplayerByID(localVoterID);
            int chosen = VoteManger.Instance != null ? VoteManger.Instance.GetVotedTarget(localVoterID) : -1;
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
