using System.Collections.Generic;
using UnityEngine;

public class NightResolver : MonoBehaviour
{
    public static NightResolver Instance { get; private set; }

    private int protectedPlayerId = -1;
    private List<int> attackTargetIds = new List<int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterProtection(int targetId) => protectedPlayerId = targetId;
    public void RegisterAttack(int targetId) => attackTargetIds.Add(targetId);

    public void ResolveNightActions()
    {
        foreach (int targetId in attackTargetIds)
        {
            if (targetId == protectedPlayerId)
            {
                Debug.Log($"[NightResolver] Player {targetId} được cứu!");
            }
            else
            {
                Debug.Log($"[NightResolver] Player {targetId} đã chết!");
                // Đồng bộ gọi PlayerManger của Thương nếu có hàm Kill/SetAlive
            }
        }
        protectedPlayerId = -1;
        attackTargetIds.Clear();
    }
}