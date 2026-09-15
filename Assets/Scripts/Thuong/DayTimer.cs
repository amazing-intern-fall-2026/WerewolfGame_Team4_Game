using UnityEngine;
public class DayTimer : MonoBehaviour
{
    public static DayTimer Instance;
    [Min(0.1f)] public float dayDuration = 120f;
    public float TimeRemaining { get; private set; }
    private bool running;
    private void Awake() { Instance = this; }
    public void StartTimer() { TimeRemaining = Mathf.Max(0.1f, dayDuration); running = true; }
    public void StopTimer() { running = false; }
    private void Update()
    {
        if (!running) return;
        if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Day)
        { running = false; return; }
        TimeRemaining = Mathf.Max(0, TimeRemaining - Time.deltaTime);
        if (TimeRemaining <= 0) { running = false; GameRoleManager.Instance.EndDay(); }
    }
}
