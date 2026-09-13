using Unity.Netcode;
using UnityEngine;

public class NetworkGameStateTest : MonoBehaviour
{
    [Header("Network Game Timer")]
    [SerializeField] private NetworkGameTimer timer;

    private void Update()
    {
        if (timer == null)
            return;

        if (NetworkManager.Singleton == null)
            return;

        // Hiển thị thông tin mỗi khoảng 1 giây
        if (Time.frameCount % 60 == 0)
        {
            NetworkGameManager gameManager =
                FindAnyObjectByType<NetworkGameManager>();

            if (gameManager == null)
                return;

            Debug.Log(
                "Player " +
                NetworkManager.Singleton.LocalClientId +
                " | State: " +
                gameManager.CurrentState.Value +
                " | Timer: " +
                timer.TimeRemaining.Value.ToString("F1") +
                " giây"
            );
        }
    }
}