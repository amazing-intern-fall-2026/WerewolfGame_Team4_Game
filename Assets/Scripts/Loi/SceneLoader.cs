using System.Collections;
using System.Threading.Tasks;
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
        Debug.Log("SceneLoader: Người chơi chủ động rời Lobby...");

        StartCoroutine(ShutdownThenLoadMainMenu());
    }

    private IEnumerator ShutdownThenLoadMainMenu()
    {
        Debug.Log("SceneLoader: Bắt đầu quy trình Leave và Shutdown...");

        // 1. Rời kênh thoại Vivox và đồng bộ chờ hoàn tất trước
        if (NetworkVoiceManager.Instance != null)
        {
            Debug.Log("SceneLoader: [1/3] Đang chờ rời kênh thoại Vivox...");
            Task leaveVoiceTask = NetworkVoiceManager.Instance.LeaveVoiceChannelAsync();
            while (leaveVoiceTask != null && !leaveVoiceTask.IsCompleted)
            {
                yield return null;
            }

            if (leaveVoiceTask != null && leaveVoiceTask.IsFaulted)
            {
                Debug.LogWarning($"SceneLoader: [1/3] Kênh thoại kết thúc có cảnh báo: {leaveVoiceTask.Exception?.GetBaseException().Message}");
            }
            else
            {
                Debug.Log("SceneLoader: [1/3] Đã rời kênh thoại Vivox hoàn tất.");
            }
        }

        // 2. Đóng session phòng nếu là Host
        if (RoomManager.IsHostOfCurrentRoom || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
        {
            Debug.Log("SceneLoader: [2/3] Đang đóng session phòng Host...");
            Task closeRoomTask = RoomManager.CloseCurrentRoomAsync();
            while (closeRoomTask != null && !closeRoomTask.IsCompleted)
            {
                yield return null;
            }
            Debug.Log("SceneLoader: [2/3] Đã đóng session phòng Host.");
        }
        else
        {
            RoomManager.CurrentRoomCode = null;
            RoomManager.CurrentVoiceChannelId = null;
            RoomManager.IsHostOfCurrentRoom = false;
        }

        // 3. Shutdown NetworkManager nếu đang hoạt động
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("SceneLoader: [3/3] Shutdown NetworkManager...");
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
                Debug.Log("SceneLoader: [3/3] Shutdown hoàn tất.");
            }
        }

        // 4. Chuyển về MainMenu
        Debug.Log("SceneLoader: Hoàn tất dọn dẹp, đang tải Scene MainMenu...");
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
            "SceneLoader: Client bị disconnect khỏi Host."
        );

        isLeaving = true;

        StartCoroutine(ShutdownThenLoadMainMenu());
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