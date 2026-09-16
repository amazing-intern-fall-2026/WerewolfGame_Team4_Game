using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    public void LeaveLobby()
    {
        Debug.Log("Leave Button được nhấn.");

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("Không tìm thấy SceneLoader!");
            return;
        }

        SceneLoader.Instance.LeaveLobby();
    }
}