using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManagert : MonoBehaviour
{
    public static EventManagert Instance { get; private set; }

    public GameEventType SelectedEvent { get; private set; }
        = GameEventType.None;

    public int ScheduledDay { get; private set; }
    public bool HasSchedule { get; private set; }

    // Một ván chỉ có một ngày event, nên chỉ cần một cờ chống log trùng.
    private bool hasLoggedScheduledDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Chỉ hủy component trùng, không hủy object có manager khác.
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool InitializeForMatch(int totalDays)
    {
        // Gọi lại trong cùng ván vẫn giữ nguyên lịch đã tạo.
        if (HasSchedule)
            return true;

        // Tránh phạm vi rỗng và tránh tràn khi tính totalDays + 1.
        if (totalDays < 1 || totalDays == int.MaxValue)
        {
            Debug.LogError(
                "[Event Debug] Số ngày phải từ 1 đến int.MaxValue - 1.",
                this);
            return false;
        }

        List<GameEventType> pool = BuildEventPool();

        if (pool.Count == 0)
        {
            Debug.LogError("[Event Debug] Không có event để chọn.", this);
            return false;
        }

        int index = UnityEngine.Random.Range(0, pool.Count);

        SelectedEvent = pool[index];
        ScheduledDay = UnityEngine.Random.Range(1, totalDays + 1);
        HasSchedule = true;
        hasLoggedScheduledDay = false;

        Debug.Log(
            $"[Event Debug] Đã chọn {GetDisplayName(SelectedEvent)}" +
            $" | Ngày xuất hiện: {ScheduledDay}",
            this);

        return true;
    }

    private static List<GameEventType> BuildEventPool()
    {
        var pool = new List<GameEventType>();

        foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
        {
            // Contains tránh tăng xác suất nếu enum có alias trùng giá trị.
            if (type != GameEventType.None && !pool.Contains(type))
                pool.Add(type);
        }

        return pool;
    }

    public GameEventType GetEventForDay(int day)
    {
        if (!HasSchedule || day != ScheduledDay)
            return GameEventType.None;

        return SelectedEvent;
    }

    public static string GetDisplayName(GameEventType eventType)
    {
        switch (eventType)
        {
            case GameEventType.BloodMoon:
                return "blood moon event";
            case GameEventType.Fog:
                return "fog event";
            case GameEventType.HarvestFestival:
                return "festival event";
            case GameEventType.ClearSky:
                return "clear sky event";
            case GameEventType.GhostMonth:
                return "ghost month event";
            case GameEventType.TuongNight:
                return "tuong night event";
            case GameEventType.None:
                return string.Empty;
            default:
                // Khi thêm enum mới, bổ sung tên chính thức ở switch này.
                return eventType.ToString().ToLowerInvariant() + " event";
        }
    }

    public void NotifyDayStarted(int day)
    {
        GameEventType todayEvent = GetEventForDay(day);

        if (todayEvent == GameEventType.None || hasLoggedScheduledDay)
            return;

        hasLoggedScheduledDay = true;

        Debug.Log(
            $"[Event Debug] Ngày {day}-{GetDisplayName(todayEvent)}",
            this);
    }

    // Giữ chữ ký hàm cũ để không làm mất liên kết UnityEvent nếu có.
    // Hàm chỉ thông báo ngày event; không random hay áp dụng hiệu ứng.
    public void TryStartEvent()
    {
        var game = GameRoleManager.Instance;

        if (game == null || game.currentState == GameState.GameOver)
            return;

        NotifyDayStarted(game.currentDay);
    }

    public void ResetForNewMatch()
    {
        SelectedEvent = GameEventType.None;
        ScheduledDay = 0;
        HasSchedule = false;
        hasLoggedScheduledDay = false;
    }
}
