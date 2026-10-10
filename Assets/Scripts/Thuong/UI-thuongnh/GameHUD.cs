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
    private TMP_Text phaseCueText;
    private Image phaseCueAccent;

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }

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
        if (meetingPanel != null && meetingTitle == null)
        {
            meetingTitle = meetingPanel.transform
                .Find("Title")?.GetComponent<TMP_Text>();
        }

        if (meetingPanel != null && votingStatusText == null)
        {
            votingStatusText = meetingPanel.transform
                .Find("Hint")?.GetComponent<TMP_Text>();
        }

        ApplyPrototypeStyle();
        ConfigureSingleLineText(phaseText);
        ConfigureSingleLineText(timerText);
        ConfigureSingleLineText(dailyProgressText);
        ConfigureSingleLineText(votingStatusText);

        var abilityUI = GetComponent<RoleAbilityUI>();
        if (abilityUI == null) abilityUI = gameObject.AddComponent<RoleAbilityUI>();
        abilityUI.Initialize(phaseText != null ? phaseText.font : null, localVoterID);
    }

    private static void SetPanelColor(Transform target, string color)
    {
        if (target != null && target.TryGetComponent(out Image image))
            image.color = Hex(color);
    }

    private static void SetLabel(Transform target, string value, float size, string color)
    {
        if (target == null || !target.TryGetComponent(out TMP_Text label)) return;
        label.text = value;
        label.fontSize = size;
        label.color = Hex(color);
    }

    private static void StyleSlider(Slider slider, string trackColor, string fillColor)
    {
        if (slider == null) return;
        if (slider.TryGetComponent(out Image track)) track.color = Hex(trackColor);
        if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fill))
            fill.color = Hex(fillColor);
    }

    private static void AddAccent(Transform parent, string name, string color)
    {
        if (parent.Find(name) != null) return;
        var accent = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = accent.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, .965f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = accent.GetComponent<Image>();
        image.color = Hex(color);
        image.raycastTarget = false;
    }

    private void CreatePhaseCue()
    {
        // Added behind the existing panels so vote buttons remain clickable.
        Transform existing = transform.Find("PhaseCue");
        if (existing != null)
        {
            phaseCueText = existing.Find("Message")?.GetComponent<TMP_Text>();
            phaseCueAccent = existing.Find("Accent")?.GetComponent<Image>();
            return;
        }

        var panel = new GameObject("PhaseCue", typeof(RectTransform), typeof(Image));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = new Vector2(.255f, .09f);
        panelRect.anchorMax = new Vector2(.98f, .16f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = Hex("#142932E8");
        panelImage.raycastTarget = false;
        panelRect.SetAsFirstSibling();

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.SetParent(panelRect, false);
        accentRect.anchorMin = Vector2.zero;
        accentRect.anchorMax = new Vector2(.006f, 1);
        accentRect.offsetMin = accentRect.offsetMax = Vector2.zero;
        phaseCueAccent = accent.GetComponent<Image>();
        phaseCueAccent.raycastTarget = false;

        var message = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        var messageRect = message.GetComponent<RectTransform>();
        messageRect.SetParent(panelRect, false);
        messageRect.anchorMin = new Vector2(.025f, .1f);
        messageRect.anchorMax = new Vector2(.98f, .9f);
        messageRect.offsetMin = messageRect.offsetMax = Vector2.zero;
        phaseCueText = message.GetComponent<TMP_Text>();
        if (phaseText != null) phaseCueText.font = phaseText.font;
        phaseCueText.fontSize = 20;
        phaseCueText.color = Hex("#EAF7F5");
        phaseCueText.alignment = TextAlignmentOptions.MidlineLeft;
        phaseCueText.textWrappingMode = TextWrappingModes.NoWrap;
        phaseCueText.overflowMode = TextOverflowModes.Ellipsis;
        phaseCueText.raycastTarget = false;
    }

    private void ApplyPrototypeStyle()
    {
        // Runtime styling upgrades the existing scene without replacing its references.
        Transform top = transform.Find("TopBar");
        Transform sidebar = transform.Find("TaskPanel");
        if (top == null || sidebar == null) return;

        SetPanelColor(top, "#142932");
        SetPanelColor(sidebar, "#142932");
        SetPanelColor(meetingPanel != null ? meetingPanel.transform : null, "#172F38");
        SetPanelColor(resultPanel != null ? resultPanel.transform : null, "#10232BEF");
        SetLabel(top.Find("Title"), "MA SÓI  /  NHIỆM VỤ CỦA DÂN LÀNG", 22, "#EAF7F5");
        SetLabel(sidebar.Find("Section"), "NHIỆM VỤ HÔM NAY", 20, "#EBCB83");
        SetLabel(sidebar.Find("Instructions"),
            "WASD / MŨI TÊN  Di chuyển\nE  Tương tác tại điểm sáng\nR  Chơi lại\n\nHoàn thành nhiệm vụ để lấp đầy\nthanh tiến độ chung và chiến thắng.",
            18, "#AFC5C9");
        SetLabel(transform.Find("Footer"),
            "THƯƠNG  /  BẢN CHƠI THỬ     |     HOÀN THÀNH 100% TIẾN ĐỘ TRƯỚC NGÀY 7",
            17, "#8EA8AD");
        StyleSlider(progressSlider, "#314B53", "#79DED1");
        StyleSlider(dailyProgressSlider, "#314B53", "#EBCB83");
        if (progressText != null) progressText.color = Hex("#EAF7F5");
        if (dailyProgressText != null) dailyProgressText.color = Hex("#EBCB83");
        if (taskListText != null)
        {
            taskListText.fontSize = 22;
            taskListText.lineSpacing = 12;
            taskListText.color = Hex("#EAF7F5");
            taskListText.alignment = TextAlignmentOptions.TopLeft;
        }
        AddAccent(top, "TopAccent", "#79DED1");
        AddAccent(sidebar, "SidebarAccent", "#EBCB83");
        CreatePhaseCue();
        StyleMeeting();
    }

    private void StyleMeeting()
    {
        if (meetingPanel == null) return;
        if (meetingTitle != null) meetingTitle.color = Hex("#EBCB83");
        if (votingStatusText != null) votingStatusText.color = Hex("#EAF7F5");
        for (int i = 0; i < meetingPanel.transform.childCount; i++)
        {
            Transform row = meetingPanel.transform.GetChild(i);
            if (!row.TryGetComponent(out VoteButton voteButton) ||
                !row.TryGetComponent(out Button button)) continue;
            SetPanelColor(row, i % 2 == 0 ? "#294753" : "#30505B");
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Hex("#B8F3EA");
            colors.pressedColor = Hex("#79DED1");
            colors.selectedColor = Hex("#B8F3EA");
            colors.disabledColor = Hex("#778C91");
            button.colors = colors;
            SetLabel(row.Find("Name"),
                "PLAYER " + (voteButton.targetID + 1) +
                (voteButton.targetID == localVoterID ? "   /   BẠN" : ""),
                22, "#EAF7F5");
        }
    }

    private string BuildDayLabel(int day)
    {
        string dayLabel = $"NGÀY {day}";
        EventManager events = EventManager.Instance;

        if (events == null)
            return dayLabel;

        GameEventType todayEvent = events.GetEventForDay(day);

        if (todayEvent == GameEventType.None)
            return dayLabel;

        return dayLabel + " - " + EventManager.GetDisplayName(todayEvent);
    }

    private static string GetPhaseName(GameState state)
    {
        switch (state)
        {
            case GameState.RoleReveal: return "GIỚI THIỆU ROLE";
            case GameState.Day: return "BAN NGÀY";
            case GameState.Night: return "BAN ĐÊM";
            case GameState.Discussion: return "THẢO LUẬN";
            case GameState.Voting: return "BỎ PHIẾU";
            case GameState.VotingResult: return "KIỂM PHIẾU";
            case GameState.GameOver: return "KẾT THÚC";
            default: return "ĐANG CHỜ";
        }
    }

    private static string GetPhaseCue(GameState state)
    {
        switch (state)
        {
            case GameState.RoleReveal: return "ROLE ĐÃ ĐƯỢC PHÂN  /  Đọc thẻ vai trò hoặc nhấn OK để bắt đầu.";
            case GameState.Day: return "BAN NGÀY  /  Tìm điểm sáng, đến gần và nhấn E để hoàn thành nhiệm vụ.";
            case GameState.Night: return "BAN ĐÊM  /  Mở ROLE / KỸ NĂNG để chọn mục tiêu nếu bạn có kỹ năng.";
            case GameState.Discussion: return "THẢO LUẬN  /  Chuẩn bị chọn người để bỏ phiếu.";
            case GameState.Voting: return "BỎ PHIẾU  /  Chọn một Player còn sống trên bảng họp.";
            case GameState.VotingResult: return "KIỂM PHIẾU  /  Đang tổng hợp kết quả.";
            case GameState.GameOver: return "KẾT THÚC  /  Nhấn R để chơi ván mới.";
            default: return "ĐANG CHỜ  /  Ván chơi sắp bắt đầu.";
        }
    }

    private static string GetWinnerLabel(string winner)
    {
        switch (winner)
        {
            case "Villagers": return "DÂN LÀNG CHIẾN THẮNG";
            case "Werewolves": return "MA SÓI CHIẾN THẮNG";
            case "Lovers": return "CẶP ĐÔI CHIẾN THẮNG";
            case "White Wolf": return "SÓI TRẮNG CHIẾN THẮNG";
            case "Killer": return "KẺ SÁT NHÂN CHIẾN THẮNG";
            case "Madman": return "KẺ ĐIÊN CHIẾN THẮNG";
            case "Fox Spirit": return "HỒ LY CHIẾN THẮNG";
            default: return "VÁN CHƠI KẾT THÚC";
        }
    }

    private void RefreshDayAndTimer(GameRoleManager game)
    {
        if (game == null)
            return;

        if (phaseText != null)
            phaseText.text = BuildDayLabel(game.currentDay) + "  /  " + GetPhaseName(game.currentState);

        float seconds = game.currentState == GameState.Day &&
                        DayTimer.Instance != null
            ? DayTimer.Instance.TimeRemaining
            : game.PhaseTimeRemaining;

        if (timerText != null)
        {
            int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            timerText.text = $"{remainingSeconds / 60:00}:{remainingSeconds % 60:00}";
            timerText.color = remainingSeconds <= 10 && game.currentState != GameState.GameOver
                ? Hex("#F08E82") : Hex("#EBCB83");
        }
        if (phaseCueText != null) phaseCueText.text = GetPhaseCue(game.currentState);
        if (phaseCueAccent != null)
            phaseCueAccent.color = game.currentState == GameState.Night
                ? Hex("#A7A9EF") : game.currentState == GameState.Voting
                    ? Hex("#EBCB83") : Hex("#79DED1");
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
            dailyProgressText.text = $"HÔM NAY  {tasks.CompletedToday}/{tasks.currentTasks.Count}  |  {tasks.DailyProgress:0}%";
        RefreshDayAndTimer(game);
        if (taskListText != null)
        {
            var text = new StringBuilder();
            foreach (var task in tasks.currentTasks)
            {
                if (task == null) continue;
                bool completed = tasks.IsCompleted(task);
                text.Append(completed ? "<color=#79DED1>[x]</color>  " : "<color=#8EA8AD>[ ]</color>  ");
                text.AppendLine(task.taskName);
            }
            taskListText.text = text.Length == 0 ? "Chưa có nhiệm vụ hôm nay." : text.ToString();
        }
        if (meetingPanel != null) meetingPanel.SetActive(
            game.currentState == GameState.Discussion || game.currentState == GameState.Voting);
        if (meetingTitle != null) meetingTitle.text = game.currentState == GameState.Voting ? "BỎ PHIẾU" : "THẢO LUẬN";
        if (votingStatusText != null)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(localVoterID);
            int chosen = VoteManager.Instance != null ? VoteManager.Instance.GetVotedTarget(localVoterID) : -1;
            if (game.currentState == GameState.Discussion)
                votingStatusText.text = $"Bỏ phiếu bắt đầu sau {Mathf.CeilToInt(game.PhaseTimeRemaining)} giây.";
            else if (voter == null) votingStatusText.text = "Không tìm thấy dữ liệu người bỏ phiếu.";
            else if (!voter.isAlive) votingStatusText.text = "Bạn đã bị loại và không thể bỏ phiếu.";
            else if (voter.hasVoted)
                votingStatusText.text = chosen >= 0 ? $"Đã chọn Player {chosen + 1}. Đang chờ kết quả." : "Đã bỏ phiếu. Đang chờ kết quả.";
            else votingStatusText.text = $"Bạn là Player {localVoterID + 1}. Chọn một người còn sống.";
        }
        bool ended = game.currentState == GameState.GameOver;
        if (resultPanel != null) resultPanel.SetActive(ended);
        if (resultText != null && ended) resultText.text = GetWinnerLabel(game.Winner);
    }
}

