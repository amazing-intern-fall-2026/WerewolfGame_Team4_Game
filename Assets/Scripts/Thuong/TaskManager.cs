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
    public int CompletedToday => completed.Count;
    public float DailyProgress => currentTasks.Count == 0 ? 0f : 100f * CompletedToday / currentTasks.Count;
    private void Awake() { Instance = this; progress = 0; }
    public bool IsCompleted(TaskData task) => task != null && completed.Contains(task);
    public void StartNewDay()
    {
        currentTasks.Clear();
        completed.Clear();
        var pool = new List<TaskData>();
        if (allTasks != null)
            foreach (var task in allTasks)
                if (task != null && !pool.Contains(task)) pool.Add(task);
        for (int i = 0; i < tasksPerDay && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            currentTasks.Add(pool[index]);
            pool.RemoveAt(index);
        }
        if (currentTasks.Count == 0) Debug.LogWarning("TaskManager: assign task assets to All Tasks.");
    }
    public void CompleteTask(TaskData task)
    {
        if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Day ||
            task == null || !currentTasks.Contains(task) || !completed.Add(task)) return;
        progress = Mathf.Clamp(progress + Mathf.Max(0, task.progressValue), 0, 100);
        if (progress >= 100) GameRoleManager.Instance.VillagerWin();
    }
}
