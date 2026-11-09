using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Thuong.Online;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controller điều phối quy trình Trở về Main Menu cho Gameplay Online của Thương và Lợi.
/// 
/// Đảm bảo:
/// 1. Hiển thị hộp thoại xác nhận khi nhấn Rời phòng / Trở về Menu.
/// 2. Khi Host thoát:
///    - Phát thông báo kết thúc phiên tới tất cả Client.
///    - Dừng timer, khóa voting và hủy nhận hành động mới.
///    - Đưa Host và tất cả Client về MainMenu an toàn, không bị treo.
/// 3. Khi Client thoát:
///    - Gửi thông báo rời trận tới Host.
///    - Host cập nhật trạng thái disconnect và tiếp tục trận đấu cho người còn lại.
///    - Client về MainMenu an toàn.
/// 4. Sau GameOver:
///    - Cho phép xem kết quả trước khi nhấn thoát.
///    - Khi thoát, dọn dẹp sạch sẽ và trở về MainMenu.
/// 5. Dọn dẹp và reset toàn diện dữ liệu Networking của Lợi (Section 7).
/// 
/// TUYỆT ĐỐI KHÔNG sửa code Thương. Tích hợp 100% từ phía Lợi.
/// </summary>
public class NetworkReturnToMenuController : MonoBehaviour
{
    private static NetworkReturnToMenuController instance;
    public static NetworkReturnToMenuController Instance => instance;

    public const string HostSessionTerminatedMessage = "loi.online.host_session_terminated.v1";
    public const string ClientVoluntaryLeaveMessage = "loi.online.client_leave.v1";

    private readonly HashSet<Button> hookedButtons = new HashSet<Button>();
    private bool isNetworkRegistered = false;
    private bool isReturning = false;
    private float nextScanTime = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (instance != null) return;

        var existing = FindAnyObjectByType<NetworkReturnToMenuController>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[NetworkReturnToMenuController]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NetworkReturnToMenuController>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        if (instance != null)
        {
            instance.hookedButtons.Clear();
            instance.isReturning = false;

            if (scene.name == "MainMenu")
            {
                NetworkReturnToMenuConfirmationDialog.HideDialog();
                ResetAllNetworkingData();
            }
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsureNetworkHandlers();
    }

    private void Update()
    {
        EnsureNetworkHandlers();

        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + 0.35f;
            ScanAndHookLeaveButtons();
        }
    }

    private void OnDestroy()
    {
        UnregisterNetworkHandlers();
    }

    // =========================================================
    // 1. NETWORKING REGISTRATION & MESSAGES
    // =========================================================

    private void EnsureNetworkHandlers()
    {
        if (isNetworkRegistered) return;

        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && net.CustomMessagingManager != null)
        {
            try
            {
                net.CustomMessagingManager.RegisterNamedMessageHandler(HostSessionTerminatedMessage, ReceiveHostSessionTerminated);
                net.CustomMessagingManager.RegisterNamedMessageHandler(ClientVoluntaryLeaveMessage, ReceiveClientVoluntaryLeave);

                net.OnClientDisconnectCallback -= HandleClientDisconnected;
                net.OnClientDisconnectCallback += HandleClientDisconnected;

                isNetworkRegistered = true;
                Debug.Log("[NetworkReturnToMenuController] Đã đăng ký Network Message Handlers thành công.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkReturnToMenuController] Đăng ký message handler ngoại lệ: {ex.Message}");
            }
        }
    }

    private void UnregisterNetworkHandlers()
    {
        if (!isNetworkRegistered) return;

        var net = NetworkManager.Singleton;
        if (net != null)
        {
            try
            {
                if (net.CustomMessagingManager != null)
                {
                    net.CustomMessagingManager.UnregisterNamedMessageHandler(HostSessionTerminatedMessage);
                    net.CustomMessagingManager.UnregisterNamedMessageHandler(ClientVoluntaryLeaveMessage);
                }
                net.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            catch { }
        }

        isNetworkRegistered = false;
    }

    // =========================================================
    // 2. TỰ ĐỘNG PHÁT HIỆN VÀ GẮN SỰ KIỆN NÚT RỜI PHÒNG
    // =========================================================

    private void ScanAndHookLeaveButtons()
    {
        // 1. Nút "Rời phòng" của Thương Online trong trận (GameHUD)
        var roiPhongGO = GameObject.Find("Rời phòng");
        if (roiPhongGO != null)
        {
            var btn = roiPhongGO.GetComponent<Button>();
            if (btn != null && !hookedButtons.Contains(btn))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RequestReturnToMenu);
                hookedButtons.Add(btn);
                Debug.Log("[NetworkReturnToMenuController] Đã hook nút 'Rời phòng' trên GameHUD.");
            }
        }

        // 2. Nút "Leave finished match" trên bảng kết quả GameOver của Thương Online
        var leaveFinishedGO = GameObject.Find("Leave finished match");
        if (leaveFinishedGO != null)
        {
            var btn = leaveFinishedGO.GetComponent<Button>();
            if (btn != null && !hookedButtons.Contains(btn))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RequestReturnToMenu);
                hookedButtons.Add(btn);
                Debug.Log("[NetworkReturnToMenuController] Đã hook nút 'Leave finished match' trên bảng kết quả GameOver.");
            }
        }

        // 3. Nút "Leave lobby" trong phòng chờ của Thương Online
        var leaveLobbyGO = GameObject.Find("Leave lobby");
        if (leaveLobbyGO != null)
        {
            var btn = leaveLobbyGO.GetComponent<Button>();
            if (btn != null && !hookedButtons.Contains(btn))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RequestReturnToMenu);
                hookedButtons.Add(btn);
                Debug.Log("[NetworkReturnToMenuController] Đã hook nút 'Leave lobby' của Thương Online.");
            }
        }

        // 4. Nút "InGameLeaveButton" của Lợi trong scene Game
        var inGameLeaveGO = GameObject.Find("InGameLeaveButton");
        if (inGameLeaveGO != null)
        {
            var btn = inGameLeaveGO.GetComponent<Button>();
            if (btn != null && !hookedButtons.Contains(btn))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RequestReturnToMenu);
                hookedButtons.Add(btn);
                Debug.Log("[NetworkReturnToMenuController] Đã hook nút 'InGameLeaveButton' của Lợi.");
            }
        }
    }

    // =========================================================
    // 3. YÊU CẦU TRỞ VỀ MENU VÀ HIỂN THỊ HỘP THOẠI XÁC NHẬN
    // =========================================================

    public void RequestReturnToMenu()
    {
        if (isReturning) return;

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Debug.Log($"[NetworkReturnToMenuController] RequestReturnToMenu | isHost={isHost}. Mở hộp thoại xác nhận...");

        NetworkReturnToMenuConfirmationDialog.ShowDialog(
            isHost: isHost,
            onConfirm: OnConfirmedReturnToMenu,
            onCancel: OnCancelledReturnToMenu
        );
    }

    private void OnConfirmedReturnToMenu()
    {
        Debug.Log("[NetworkReturnToMenuController] Người chơi xác nhận Trở về Menu chính. Bắt đầu quy trình thoát...");
        ExecuteReturnToMenu();
    }

    private void OnCancelledReturnToMenu()
    {
        Debug.Log("[NetworkReturnToMenuController] Người chơi chọn Tiếp tục chơi. Giữ nguyên trận đấu.");
    }

    // =========================================================
    // 4. THỰC THI QUY TRÌNH THOÁT TRẬN (HOST VÀ CLIENT)
    // =========================================================

    public void ExecuteReturnToMenu()
    {
        if (isReturning) return;
        isReturning = true;

        StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        Debug.Log("[NetworkReturnToMenuController] Bắt đầu ReturnToMenuRoutine...");

        var net = NetworkManager.Singleton;
        bool isServer = net != null && net.IsServer;
        bool isConnectedClient = net != null && net.IsConnectedClient;

        // BƯỚC 1: XỬ LÝ THEO VAI TRÒ
        if (isServer)
        {
            // === HOST THOÁT TRẬN ===
            Debug.Log("[NetworkReturnToMenuController] Host đang thoát trận. Phát thông báo HostSessionTerminated...");

            // 1.1 Gửi message thông báo tới tất cả Client
            if (net != null && net.CustomMessagingManager != null)
            {
                try
                {
                    using var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);
                    writer.WriteValueSafe((byte)1);
                    net.CustomMessagingManager.SendNamedMessageToAll(HostSessionTerminatedMessage, writer);
                    Debug.Log("[NetworkReturnToMenuController] Đã gửi HostSessionTerminatedMessage tới tất cả clients.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkReturnToMenuController] Gửi HostSessionTerminatedMessage lỗi: {ex.Message}");
                }
            }

            SceneLoader.DisconnectNoticeMessage = "Host đã kết thúc phiên chơi.";

            // 1.2 Khóa voting, dừng timer, hủy action
            StopGameplayProcesses();

            // 1.3 Đợi 0.2s để card mạng kịp flush gói tin broadcast tới tất cả Client
            yield return new WaitForSecondsRealtime(0.2f);
        }
        else if (isConnectedClient)
        {
            // === CLIENT CHỦ ĐỘNG THOÁT TRẬN ===
            Debug.Log("[NetworkReturnToMenuController] Client đang chủ động thoát trận. Gửi thông báo tới Host...");

            // 2.1 Gửi ClientVoluntaryLeave tới Server
            if (net != null && net.CustomMessagingManager != null)
            {
                try
                {
                    using var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
                    writer.WriteValueSafe(net.LocalClientId);
                    net.CustomMessagingManager.SendNamedMessage(ClientVoluntaryLeaveMessage, NetworkManager.ServerClientId, writer);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkReturnToMenuController] Gửi ClientVoluntaryLeaveMessage lỗi: {ex.Message}");
                }

                // Gửi thêm message rời mặc định của Thương Online nếu có
                try
                {
                    using var writer = new FastBufferWriter(sizeof(uint), Allocator.Temp);
                    writer.WriteValueSafe(0u);
                    net.CustomMessagingManager.SendNamedMessage("thuong.online.leave.v1", NetworkManager.ServerClientId, writer);
                }
                catch { }
            }

            // Nếu trong Loi Scene, gửi RPC
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsSpawned)
            {
                try
                {
                    NetworkGameManager.Instance.RequestLeaveRoomServerRpc(net.LocalClientId);
                }
                catch { }
            }

            // 2.2 Khóa voting & dừng timer phía Client
            StopGameplayProcesses();

            // Đợi 0.15s cho gói tin gửi đi
            yield return new WaitForSecondsRealtime(0.15f);
        }

        // BƯỚC 2: RỜI KÊNH THOẠI VIVOX (NẾU CÓ)
        if (NetworkVoiceManager.Instance != null)
        {
            Debug.Log("[NetworkReturnToMenuController] Rời kênh thoại Vivox...");
            Task voiceTask = NetworkVoiceManager.Instance.LeaveVoiceChannelAsync();
            while (voiceTask != null && !voiceTask.IsCompleted)
            {
                yield return null;
            }
        }

        // BƯỚC 3: GIẢI PHÓNG PHÒNG TRÊN LOBBY / RELAY (NẾU LÀ HOST)
        if (isServer || RoomManager.IsHostOfCurrentRoom || !string.IsNullOrEmpty(RoomManager.CurrentLobbyId))
        {
            Debug.Log("[NetworkReturnToMenuController] Đóng session phòng Host trên Relay / Lobby...");
            Task closeTask = RoomManager.CloseCurrentRoomAsync();
            while (closeTask != null && !closeTask.IsCompleted)
            {
                yield return null;
            }
        }
        else
        {
            RoomManager.CurrentRoomCode = null;
            RoomManager.CurrentVoiceChannelId = null;
            RoomManager.IsHostOfCurrentRoom = false;
        }

        // BƯỚC 4: RESET TOÀN DIỆN DỮ LIỆU NETWORKING CỦA LỢI (Section 7)
        ResetAllNetworkingData();

        // BƯỚC 5: SHUTDOWN VÀ CHUYỂN VỀ MAINMENU
        PerformShutdownAndLoadMainMenu();
    }

    // =========================================================
    // 5. CLIENT NHẬN THÔNG BÁO HOST KẾT THÚC HOẶC DISCONNECT
    // =========================================================

    private void ReceiveHostSessionTerminated(ulong senderId, FastBufferReader reader)
    {
        Debug.Log("[NetworkReturnToMenuController] CLIENT: Nhận HostSessionTerminatedMessage từ Server! Đang về MainMenu...");

        SceneLoader.DisconnectNoticeMessage = "Chủ phòng đã thoát trận. Trận đấu đã kết thúc.";
        StopGameplayProcesses();

        if (!isReturning)
        {
            isReturning = true;
            StartCoroutine(ClientHandleHostTerminatedRoutine());
        }
    }

    private void ReceiveClientVoluntaryLeave(ulong senderId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsServer) return;

        Debug.Log($"[NetworkReturnToMenuController] SERVER: Nhận ClientVoluntaryLeaveMessage từ Client {senderId}.");

        // Server ngắt kết nối client này một cách an toàn mà không làm gián đoạn ván đấu của người khác
        if (senderId != NetworkManager.ServerClientId && net.ConnectedClients.ContainsKey(senderId))
        {
            net.DisconnectClient(senderId);
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.HandleServerPlayerDisconnect(senderId);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (isReturning) return;

        var net = NetworkManager.Singleton;
        if (net == null || net.IsServer) return;

        // Nếu Host (Server) ngắt kết nối hoặc chính client mình bị ngắt kết nối
        if (clientId == NetworkManager.ServerClientId || clientId == net.LocalClientId)
        {
            Debug.Log($"[NetworkReturnToMenuController] CLIENT: Phát hiện mất kết nối tới Server (clientId={clientId}). Đang dọn dẹp và về MainMenu...");
            SceneLoader.DisconnectNoticeMessage = "Mất kết nối với Chủ phòng. Trận đấu đã kết thúc.";
            StopGameplayProcesses();
            isReturning = true;
            StartCoroutine(ClientHandleHostTerminatedRoutine());
        }
    }

    private IEnumerator ClientHandleHostTerminatedRoutine()
    {
        // 1. Rời voice
        if (NetworkVoiceManager.Instance != null)
        {
            Task voiceTask = NetworkVoiceManager.Instance.LeaveVoiceChannelAsync();
            while (voiceTask != null && !voiceTask.IsCompleted)
            {
                yield return null;
            }
        }

        // 2. Reset dữ liệu
        ResetAllNetworkingData();

        // 3. Shutdown và Load MainMenu
        PerformShutdownAndLoadMainMenu();
    }

    // =========================================================
    // 6. SHUTDOWN NETWORKMANAGER VÀ TẢI MAINMENU AN TOÀN
    // =========================================================

    private void PerformShutdownAndLoadMainMenu()
    {
        UnregisterNetworkHandlers();

        // 1. Nếu đang chạy trong ThuongOnlineSession
        if (ThuongOnlineSession.Active != null)
        {
            Debug.Log("[NetworkReturnToMenuController] Thoát từ ThuongOnlineSession về MainMenu...");
            var session = ThuongOnlineSession.Active;

            if (session.GetComponent<NetworkManager>() is { IsListening: true, ShutdownInProgress: false } nm)
            {
                nm.Shutdown();
            }

            ThuongOnlineMenu.ReturnToMenu(session);
            isReturning = false;
            return;
        }

        // 2. Nếu đang chạy trong hệ thống Loi SceneLoader
        if (SceneLoader.Instance != null)
        {
            Debug.Log("[NetworkReturnToMenuController] Thoát qua SceneLoader về MainMenu...");
            SceneLoader.Instance.LeaveRoomToMainMenu();
            isReturning = false;
            return;
        }

        // 3. Fallback trực tiếp
        Debug.Log("[NetworkReturnToMenuController] Fallback Shutdown NetworkManager và tải MainMenu...");
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && !net.ShutdownInProgress)
        {
            net.Shutdown();
        }

        SceneManager.LoadScene("MainMenu");
        isReturning = false;
    }

    // =========================================================
    // 7. KHÓA TIẾN TRÌNH GAMEPLAY & RESET TOÀN BỘ NETWORKING (Section 7)
    // =========================================================

    public static void StopGameplayProcesses()
    {
        try
        {
            // Dừng timer
            if (NetworkGameTimer.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkGameTimer.Instance.TimeRemaining.Value = 0f;
            }
            var timerController = FindAnyObjectByType<NetworkPhaseTimerController>();
            if (timerController != null)
            {
                timerController.ResetForNewMatch();
            }

            // Khóa voting và hoàn trả màu highlight xanh
            if (ThuongOnlineVoteNetworkAdapter.Instance != null)
            {
                ThuongOnlineVoteNetworkAdapter.Instance.LockVotingAndClearHighlight();
            }

            NetworkPlayerVote.ResetNetworkVotes();

            var cards = FindObjectsByType<NetworkVoteCardUI>(FindObjectsSortMode.None);
            if (cards != null)
            {
                foreach (var c in cards)
                {
                    if (c != null) c.SetSelected(false);
                }
            }

            // Hủy các pending role action
            NetworkRoleActionBridge.ResetAllRoleActionBridgeData();
            NetworkPlayerAction.ResetServerActionData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkReturnToMenuController] StopGameplayProcesses ngoại lệ: {ex.Message}");
        }
    }

    public static void ResetAllNetworkingData()
    {
        Debug.Log("[NetworkReturnToMenuController] Dọn dẹp toàn diện mọi trạng thái Networking của Lợi (Section 7)...");

        try
        {
            // 1. SceneLoader session cleanup
            SceneLoader.CleanupNetworkSession();

            // 2. Dọn dẹp disconnect và left players
            NetworkGameManager.ResetSessionData();

            // 3. Xóa danh sách setup player cũ
            NetworkGamePlayerSetup.Clear();
            NetworkGamePlayerNameSetup.Clear();

            // 4. Reset Role Sync & Bridge & Role Config
            NetworkRoleSync.ResetStaticSessionState();
            NetworkRoleActionBridge.ResetAllRoleActionBridgeData();
            ThuongOnlineRoleConfigAdapter.Instance?.ResetCaches();

            // 5. Reset Voting & UI
            NetworkPlayerVote.ResetNetworkVotes();
            var cards = FindObjectsByType<NetworkVoteCardUI>(FindObjectsSortMode.None);
            if (cards != null)
            {
                foreach (var c in cards)
                {
                    if (c != null) c.SetSelected(false);
                }
            }

            if (ThuongOnlineVoteNetworkAdapter.Instance != null)
            {
                ThuongOnlineVoteNetworkAdapter.Instance.ResetCaches();
            }

            // 6. Reset GameOver UI
            if (NetworkGameOverManager.Instance != null)
            {
                NetworkGameOverManager.Instance.ResetUIState();
            }

            // 7. Reset Phase Timer & Phase Sync
            if (NetworkGameTimer.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkGameTimer.Instance.TimeRemaining.Value = 0f;
            }
            var timerController = FindAnyObjectByType<NetworkPhaseTimerController>();
            if (timerController != null)
            {
                timerController.ResetForNewMatch();
            }

            // 8. RoomManager & Heartbeat
            RoomManager.StopHeartbeat();
            RoomManager.CurrentRoomCode = null;
            RoomManager.CurrentVoiceChannelId = null;
            RoomManager.IsHostOfCurrentRoom = false;

            Debug.Log("[NetworkReturnToMenuController] ResetAllNetworkingData hoàn tất thành công. Trận mới sẽ hoàn toàn độc lập.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkReturnToMenuController] ResetAllNetworkingData ngoại lệ: {ex.Message}");
        }
    }
}
