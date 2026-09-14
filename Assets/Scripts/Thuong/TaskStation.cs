using UnityEngine;
using UnityEngine.InputSystem;

// Offline prototype: task is completed by pressing E within range.
public class TaskStation : MonoBehaviour
{
    public TaskData task;
    public Transform player;
    [Min(0.1f)] public float interactionRadius = 1.5f;
    public GameObject marker;
    private void Update()
    {
        var tasks = TaskManager.Instance;
        bool available = tasks != null && task != null && GameManager.Instance != null &&
            GameManager.Instance.currentState == GameState.Day &&
            tasks.currentTasks.Contains(task) && !tasks.IsCompleted(task);
        if (marker != null && marker != gameObject) marker.SetActive(available);
        if (!available || player == null || Keyboard.current == null) return;
        var movement = player.GetComponent<Assets.Scripts.Thuong.PlayerMovement>();
        if (movement == null || !movement.IsAlive) return;
        if (Vector2.Distance(transform.position, player.position) <= interactionRadius &&
            Keyboard.current.eKey.wasPressedThisFrame)
            tasks.CompleteTask(task);
    }
}
