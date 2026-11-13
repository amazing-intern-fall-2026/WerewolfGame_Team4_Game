using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static string DisconnectNoticeMessage = null;
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
    // CLIENT / HOST CHỦ ĐỘNG RỜI PHÒNG VỀ MAINMENU
    // =========================================================

    /// <summary>
    /// Dọn dẹp toàn diện mọi dữ liệu session, static cache, danh sách người chơi, vote, action
    /// khi rời phòng, ngắt kết nối, hoặc trước khi bắt đầu tạo/vào phòng mới.
    /// Đảm bảo tính lũy đẳng (idempotent), có thể gọi nhiều lần liên tiếp an toàn.
    /// </summary>
    public static void CleanupNetworkSession()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            Thuong.Online.ThuongMainFlow.Clear();
        Debug.Log("[SceneLoader] Bắt đầu CleanupNetworkSession...");

        // 1. Cho phép Leave lần tiếp theo
        if (Instance != null)
        {
            Instance.isLeaving = false;
        }

        // 2. Dọn dẹp dữ liệu disconnect và left players
        NetworkGameManager.ResetSessionData();

        // 3. Xóa danh sách player và tên của match cũ
        NetworkGamePlayerSetup.Clear();
        NetworkGamePlayerNameSetup.Clear();

        // 4. Xóa cấu hình role và cache sprite
        NetworkRoleSync.ResetStaticSessionState();

        // 5. Reset Role Action Bridge (Shaman charms, pending kills, bitten whitehounds)
        NetworkRoleActionBridge.ResetAllRoleActionBridgeData();

        // 6. Reset votes
        NetworkPlayerVote.ResetNetworkVotes();

        // 7. Reset player actions
        NetworkPlayerAction.ResetServerActionData();

        // 8. Reset GameOver manager UI & timers
        if (NetworkGameOverManager.Instance != null)
        {
            NetworkGameOverManager.Instance.ResetUIState();
        }

        // 9. Dọn dẹp heartbeat và cache room nếu không còn trong trận
        RoomManager.StopHeartbeat();

        Debug.Log("[SceneLoader] CleanupNetworkSession hoàn tất thành công.");
    }

    public void LeaveRoomToMainMenu()
    {
        if (isLeaving)
        {
            Debug.LogWarning(
                "SceneLoader: Đang trong quá trình Leave."
            );

            return;
        }

        isLeaving = true;
        Debug.Log("SceneLoader: Người chơi chủ động rời phòng về MainMenu...");

        StartCoroutine(ShutdownThenLoadMainMenu());
    }

    public void LeaveLobby()
    {
        LeaveRoomToMainMenu();
    }

    private IEnumerator ShutdownThenLoadMainMenu()
    {
        Debug.Log("SceneLoader: Bắt đầu quy trình Leave và Shutdown...");

        // 0. Nếu là Client (không phải Server/Host), gửi RPC thông báo cho Host/Server trước khi ngắt kết nối
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsConnectedClient)
        {
            ulong localId = NetworkManager.Singleton.LocalClientId;
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsSpawned)
            {
                Debug.Log($"SceneLoader: [0/3] Gửi RequestLeaveRoomServerRpc cho Client {localId}...");
                NetworkGameManager.Instance.RequestLeaveRoomServerRpc(localId);
            }
            else if (LobbyManager.Instance != null && LobbyManager.Instance.IsSpawned)
            {
                Debug.Log($"SceneLoader: [0/3] Gửi RequestLeaveLobbyServerRpc cho Client {localId}...");
                LobbyManager.Instance.RequestLeaveLobbyServerRpc(localId);
            }

            // Đợi 0.15s để transport kịp gửi gói tin RPC đi trước khi đóng kết nối
            yield return new WaitForSecondsRealtime(0.15f);
        }

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

        // 2. Đóng session phòng nếu là Host hoặc còn LobbyId
        if (RoomManager.IsHostOfCurrentRoom || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) || !string.IsNullOrEmpty(RoomManager.CurrentLobbyId))
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
        if (Thuong.Online.ThuongOnlineSession.Active?.OwnsNetwork(NetworkManager.Singleton) == true) return;
        if (isLeaving)
            return;

        if (NetworkManager.Singleton == null)
            return;

        // Neu la Client (khong phai Server) va nhan disconnect cho chinh minh HOAC cho Server (Host shutdown)
        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                Debug.Log($"SceneLoader: Client bi disconnect khoi Host (clientId={clientId}, localId={NetworkManager.Singleton.LocalClientId}).");
                isLeaving = true;
                CleanupNetworkSession();
                if (clientId == NetworkManager.ServerClientId)
                {
                    DisconnectNoticeMessage = "Host \u0111\u00e3 r\u1eddi ph\u00f2ng. Tr\u1eadn \u0111\u1ea5u \u0111\u00e3 k\u1ebft th\u00fac.";
                    if (NetworkGameOverManager.Instance != null)
                    {
                        NetworkGameOverManager.Instance.ShowGameOverUI("Host Disconnected");
                    }
                }
                StartCoroutine(ShutdownThenLoadMainMenu());
            }
        }
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(DisconnectNoticeMessage))
        {
            GUI.depth = -2000;
            float w = 520;
            float h = 130;
            float x = (Screen.width - w) / 2f;
            float y = 50;

            GUI.Box(new Rect(x, y, w, h), "");
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.yellow;
            GUI.Box(new Rect(x, y, w, h), DisconnectNoticeMessage, style);

            if (GUI.Button(new Rect(x + w / 2 - 50, y + h - 35, 100, 28), "\u0110\u00d3NG"))
            {
                DisconnectNoticeMessage = null;
            }
        }
    }
    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (scene.name != "MainMenu")
            return;

        CleanupNetworkSession();

        Debug.Log(
            "SceneLoader: Đã về MainMenu và hoàn tất Cleanup."
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

    private void OnApplicationQuit()
    {
        Debug.Log("SceneLoader: OnApplicationQuit() -> Gửi tín hiệu rời phòng và đóng kết nối sạch sẽ.");
        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsSpawned)
                {
                    NetworkGameManager.Instance.RequestLeaveRoomServerRpc(localId);
                }
                else if (LobbyManager.Instance != null && LobbyManager.Instance.IsSpawned)
                {
                    LobbyManager.Instance.RequestLeaveLobbyServerRpc(localId);
                }
                NetworkManager.Singleton.Shutdown();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SceneLoader] Ngoại lệ khi OnApplicationQuit: {ex.Message}");
        }
    }
}
