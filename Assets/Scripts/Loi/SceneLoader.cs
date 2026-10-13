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
        SceneManager.sceneLoaded += OnSceneLoaded;

        StartCoroutine(RegisterDisconnectCallback());
    }

    private IEnumerator RegisterDisconnectCallback()
    {
        // Chờ NetworkManager tồn tại
        yield return null;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -=
                OnClientDisconnected;

            NetworkManager.Singleton.OnClientDisconnectCallback +=
                OnClientDisconnected;

            Debug.Log(
                "SceneLoader đã đăng ký Disconnect Callback."
            );
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -=
                OnClientDisconnected;
        }
    }

    // =========================================================
    // CLIENT / HOST CHỦ ĐỘNG LEAVE
    // =========================================================

    public void LeaveLobby()
    {
        if (isLeaving)
        {
            Debug.LogWarning(
                "SceneLoader: Đang trong quá trình Leave."
            );

            return;
        }

        isLeaving = true;

        Debug.Log("Đang Leave Lobby...");

        // Rời Vivox voice channel
        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.LeaveVoiceChannel();
        }

        // Đóng session phòng nếu là Host
        if (RoomManager.IsHostOfCurrentRoom || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
        {
            _ = RoomManager.CloseCurrentRoomAsync();
        }
        else
        {
            RoomManager.CurrentRoomCode = null;
            RoomManager.CurrentVoiceChannelId = null;
            RoomManager.IsHostOfCurrentRoom = false;
        }

        StartCoroutine(ShutdownThenLoadMainMenu());
    }

    private IEnumerator ShutdownThenLoadMainMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Shutdown NetworkManager...");
            NetworkManager.Singleton.Shutdown();

            // Chờ Shutdown hoàn tất (IsListening về false) — tối đa 5 giây
            float timeout = 5f;
            float elapsed = 0f;
            while (NetworkManager.Singleton != null
                   && NetworkManager.Singleton.IsListening
                   && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("SceneLoader: NetworkManager vẫn IsListening sau 5s — tiếp tục load.");
            }
            else
            {
                Debug.Log("SceneLoader: Shutdown hoàn tất.");
            }
        }

        // Chuyển về MainMenu
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

        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        Debug.Log(
            "Client bị disconnect khỏi Host."
        );

        isLeaving = true;

        if (NetworkVoiceManager.Instance != null)
        {
            NetworkVoiceManager.Instance.LeaveVoiceChannel();
        }

        RoomManager.CurrentRoomCode = null;
        RoomManager.CurrentVoiceChannelId = null;
        RoomManager.IsHostOfCurrentRoom = false;

        SceneManager.LoadScene("MainMenu");
    }

    // =========================================================
    // SCENE ĐÃ LOAD
    // =========================================================

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (scene.name != "MainMenu")
            return;

        Debug.Log(
            "SceneLoader: Đã về MainMenu."
        );

        // Cho phép Leave lần tiếp theo
        isLeaving = false;

        Debug.Log(
            "SceneLoader: isLeaving = false."
        );
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