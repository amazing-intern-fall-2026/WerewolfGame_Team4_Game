using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    private bool isLeaving = false;

    private void Awake()
    {
        // Chỉ giữ lại một SceneLoader
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // SceneLoader không bị hủy khi chuyển Scene
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        StartCoroutine(RegisterDisconnectCallback());
    }

    private IEnumerator RegisterDisconnectCallback()
    {
        // Chờ NetworkManager tồn tại
        yield return null;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log("SceneLoader đã đăng ký Disconnect Callback.");
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // =========================================================
    // CLIENT / HOST CHỦ ĐỘNG LEAVE
    // =========================================================

    public void LeaveLobby()
    {
        if (isLeaving)
            return;

        isLeaving = true;

        Debug.Log("Đang Leave Lobby...");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Chuyển Host về MainMenu ngay
        SceneManager.LoadScene("MainMenu");
    }

    // =========================================================
    // CLIENT BỊ HOST DISCONNECT
    // =========================================================

    private void OnClientDisconnected(ulong clientId)
    {
        if (isLeaving)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Client bị disconnect khỏi Host.");

            isLeaving = true;

            SceneManager.LoadScene("MainMenu");
        }
    }

    // =========================================================
    // LOAD MAIN MENU
    // =========================================================

    private void LoadMainMenu()
    {
        Debug.Log("→ LOAD MAIN MENU");

        SceneManager.LoadScene("MainMenu");
    }
}