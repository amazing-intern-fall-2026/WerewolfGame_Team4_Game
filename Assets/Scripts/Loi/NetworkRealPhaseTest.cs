using UnityEngine;

public class NetworkRealPhaseTest : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (GameRoleManager.Instance == null)
            {
                Debug.LogWarning("REAL PHASE TEST: Không tìm thấy GameRoleManager!");
                return;
            }

            Debug.Log("REAL PHASE TEST: Gọi EndDay()");

            GameRoleManager.Instance.EndDay();
        }
    }
}