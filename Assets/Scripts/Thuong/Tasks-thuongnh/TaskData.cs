using UnityEngine;

[CreateAssetMenu(
    fileName = "New Task",
    menuName = "Werewolf/Task"
)]
public class TaskData : ScriptableObject
{
    public string taskName;

    public string description;

    [Min(0)] public float progressValue = 10f;

    [HideInInspector] public bool isCompleted; // Legacy serialized field; runtime state belongs to TaskManager.
}
