using UnityEngine;

public class EventManagert : MonoBehaviour
{
    public static EventManagert Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void TryStartEvent()
    {
        
    }
}
