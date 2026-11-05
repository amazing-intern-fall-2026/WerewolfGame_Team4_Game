using System.Collections.Generic;
using UnityEngine;
public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;
    public List<TaskData> allTasks = new List<TaskData>();
    public List<TaskData> currentTasks = new List<TaskData>();
    [Min(1)] public int tasksPerDay = 4;
    public float progress;
    private readonly HashSet<TaskData> completed = new HashSet<TaskData>();
    private readonly Dictionary<TaskData, int> targets = new(), contributions = new();
    private readonly Dictionary<int, HashSet<TaskData>> personal = new();
    private int cooperativePlayers, dailyTarget, dailyContributions;
    private float dayStartingProgress;
    public bool Cooperative => cooperativePlayers > 0;
    public int DailyTarget => Cooperative ? dailyTarget : currentTasks.Count;
    public int CompletedToday => Cooperative ? dailyContributions : completed.Count;
    public float DailyProgress => DailyTarget == 0 ? 0f : 100f * CompletedToday / DailyTarget;
    private void Awake() { Instance = this; progress = 0; }
    public bool IsCompleted(TaskData task) => task != null && (Cooperative
        ? TargetFor(task) > 0 && ContributionsFor(task) >= TargetFor(task) : completed.Contains(task));
    public int TargetFor(TaskData task) => task != null && targets.TryGetValue(task, out int value) ? value : 0;
    public int ContributionsFor(TaskData task) => task != null && contributions.TryGetValue(task, out int value) ? value : 0;
    public bool HasContributed(int playerID, TaskData task) => personal.TryGetValue(playerID, out var done) && done.Contains(task);
    public int CapacityFor(TaskData task) => !Cooperative ? 1 : Mathf.Max(1,
        Mathf.CeilToInt((float)cooperativePlayers / Mathf.Max(1, currentTasks.Count)));
    public void ConfigureCooperative(int players) => cooperativePlayers = Mathf.Clamp(players, 0, 25);
    public void ResetForNewMatch()
    {
        progress = 0;
        currentTasks.Clear();
        completed.Clear();
        targets.Clear(); contributions.Clear(); personal.Clear();
        dailyTarget = dailyContributions = 0; dayStartingProgress = 0;
    }
    public void StartNewDay()
    {
        currentTasks.Clear();
        completed.Clear();
        targets.Clear(); contributions.Clear(); personal.Clear(); dailyContributions = 0;
        dayStartingProgress = progress;
        var pool = new List<TaskData>();
        if (allTasks != null)
            foreach (var task in allTasks)
                if (task != null && !pool.Contains(task)) pool.Add(task);
        int alive = PlayerManager.Instance?.players?.FindAll(p => p != null && p.isAlive && !p.isDisconnected).Count ?? cooperativePlayers;
        if (Cooperative) cooperativePlayers = Mathf.Max(1, alive);
        int count = Cooperative ? Mathf.Clamp(cooperativePlayers, 4, 6) : tasksPerDay;
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            currentTasks.Add(pool[index]);
            pool.RemoveAt(index);
        }
        if (Cooperative && currentTasks.Count > 0)
        {
            // The daily team reward stays at 40%, independent of room size.
            dailyTarget = Mathf.Min(Mathf.Max(4, cooperativePlayers * 2), cooperativePlayers * currentTasks.Count);
            for (int i = 0; i < currentTasks.Count; i++)
                targets[currentTasks[i]] = dailyTarget / currentTasks.Count + (i < dailyTarget % currentTasks.Count ? 1 : 0);
        }
        if (currentTasks.Count == 0) Debug.LogWarning("TaskManager: assign task assets to All Tasks.");
    }
    public void CompleteTask(TaskData task)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        TryCompleteTask(hud != null ? hud.localVoterID : 0, task);
    }
    public bool TryCompleteTask(int playerID, TaskData task)
    {
        if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Day ||
            PlayerManager.Instance == null || !PlayerManager.Instance.IsAlive(playerID) ||
            task == null || !currentTasks.Contains(task) || IsCompleted(task)) return false;
        if (Cooperative)
        {
            if (!personal.TryGetValue(playerID, out var done)) personal[playerID] = done = new HashSet<TaskData>();
            if (!done.Add(task)) return false;
            contributions[task] = ContributionsFor(task) + 1;
            dailyContributions++;
            progress = Mathf.Min(100, dayStartingProgress + 40f * dailyContributions / Mathf.Max(1, dailyTarget));
        }
        else
        {
            completed.Add(task);
            progress = Mathf.Clamp(progress + Mathf.Max(0, task.progressValue), 0, 100);
        }
        WinConditionManager.Instance?.NotifyMatchStateChanged();
        if (progress >= 100) GameRoleManager.Instance.VillagerWin();
        return true;
    }
}
