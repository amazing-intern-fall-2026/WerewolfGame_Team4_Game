using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Quản lý việc tạo, tra cứu và duy trì phòng chơi (Lobby) 6 chữ số.
/// Hỗ trợ cả 2 chế độ:
/// 1. Local/MPPM Hub (qua Shared Registry giữa các process trên cùng máy/LAN)
/// 2. UGS Cloud (Unity Lobby Service & Relay nếu đã kích hoạt trên Unity Dashboard)
/// </summary>
public static class RoomManager
{
    public const int ROOM_CODE_LENGTH = 6;
    public const int DEFAULT_MAX_PLAYERS = 10;
    public const ushort BASE_PORT = 7777;

    public static string CurrentRoomCode { get; set; }
    public static string CurrentVoiceChannelId { get; set; }
    public static string CurrentLobbyId { get; set; } // UGS Lobby Id nếu có
    public static bool IsHostOfCurrentRoom { get; set; }
    private static CancellationTokenSource heartbeatCts;

    private static readonly string SharedRegistryFilePath = Path.Combine(Path.GetTempPath(), "Werewolf_LocalLobbies.json");
    private static readonly object FileLock = new object();

    [Serializable]
    public class RoomSessionData
    {
        public string roomCode;
        public string sessionId;
        public string hostAddress;
        public ushort hostPort;
        public string voiceChannelId;
        public string relayJoinCode;
        public int currentPlayers;
        public int maxPlayers;
        public long lastHeartbeat;
        public bool isClosed;
    }

    [Serializable]
    private class RegistryWrapper
    {
        public List<RoomSessionData> rooms = new List<RoomSessionData>();
    }

    // =========================================================
    // 1. SINH VÀ VALIDATE MÃ PHÒNG 6 CHỮ SỐ
    // =========================================================

    /// <summary>
    /// Sinh mã phòng ngẫu nhiên gồm chính xác 6 chữ số (000000 - 999999).
    /// Giữ nguyên dạng string (bảo toàn số 0 ở đầu, ví dụ "004281").
    /// </summary>
    public static string GenerateRoomCode()
    {
        var rand = new System.Random();
        string code;
        int attempts = 0;

        do
        {
            code = rand.Next(0, 1000000).ToString("D6");
            attempts++;
        }
        while (IsRoomCodeActiveInLocalRegistry(code) && attempts < 100);

        return code;
    }

    /// <summary>
    /// Kiểm tra định dạng mã phòng có đúng 6 chữ số không.
    /// </summary>
    public static bool IsValidRoomCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != ROOM_CODE_LENGTH)
            return false;

        for (int i = 0; i < code.Length; i++)
        {
            if (!char.IsDigit(code[i]))
                return false;
        }

        return true;
    }

    // =========================================================
    // 2. TÌM CỔNG UDP KHẢ DỤNG CHO HOST (TRÁNH XUNG ĐỘT TRÊN CÙNG MÁY)
    // =========================================================

    public static bool IsPortInUse(ushort port)
    {
        // 1. Kiểm tra xem port có đang được đăng ký bởi phòng nào còn active trong Local Registry không
        var wrapper = LoadRegistry();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var room in wrapper.rooms)
        {
            if (!room.isClosed && room.hostPort == port && room.roomCode != CurrentRoomCode)
            {
                // Nếu phòng còn hoạt động trong vòng 5 phút
                if (now - room.lastHeartbeat < 300)
                {
                    return true;
                }
            }
        }

        // 2. Thử bind socket thực tế với ExclusiveAddressUse trên cả 127.0.0.1 và Any
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            }
            using (var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket2.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
                socket2.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            return false; // Cổng hoàn toàn rảnh
        }
        catch
        {
            return true; // Cổng đã bị chiếm
        }
    }

    public static ushort GetAvailableUdpPort(ushort startingPort = BASE_PORT)
    {
        for (ushort port = startingPort; port < startingPort + 100; port++)
        {
            if (!IsPortInUse(port))
            {
                return port;
            }
        }
        return startingPort;
    }

    // =========================================================
    // 3. ĐĂNG KÝ VÀ QUẢN LÝ PHÒNG (HOST)
    // =========================================================

    public static RoomSessionData CreateLocalRoomSession(int maxPlayers = DEFAULT_MAX_PLAYERS)
    {
        string roomCode = GenerateRoomCode();
        ushort hostPort = GetAvailableUdpPort(BASE_PORT);
        string sessionId = "sess-" + roomCode + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string voiceChannel = "ww-" + roomCode + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var sessionData = new RoomSessionData
        {
            roomCode = roomCode,
            sessionId = sessionId,
            hostAddress = "127.0.0.1",
            hostPort = hostPort,
            voiceChannelId = voiceChannel,
            relayJoinCode = string.Empty,
            currentPlayers = 1,
            maxPlayers = maxPlayers,
            lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            isClosed = false
        };

        CurrentRoomCode = roomCode;
        CurrentVoiceChannelId = voiceChannel;
        IsHostOfCurrentRoom = true;

        RegisterRoomInLocalRegistry(sessionData);
        return sessionData;
    }

    public static async Task<RoomSessionData> CreateRoomSessionAsync(int maxPlayers = DEFAULT_MAX_PLAYERS)
    {
        // 0. Nếu đang còn giữ Lobby cũ, đóng sạch sẽ trước khi tạo mới để tránh lỗi "Player already in lobby"
        if (!string.IsNullOrEmpty(CurrentLobbyId) || !string.IsNullOrEmpty(CurrentRoomCode))
        {
            await CloseCurrentRoomAsync();
        }

        string roomCode = GenerateRoomCode();
        ushort hostPort = GetAvailableUdpPort(BASE_PORT);
        string sessionId = "sess-" + roomCode + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string voiceChannel = "ww-" + roomCode + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var sessionData = new RoomSessionData
        {
            roomCode = roomCode,
            sessionId = sessionId,
            hostAddress = "127.0.0.1",
            hostPort = hostPort,
            voiceChannelId = voiceChannel,
            relayJoinCode = string.Empty,
            currentPlayers = 1,
            maxPlayers = maxPlayers,
            lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            isClosed = false
        };

        CurrentRoomCode = roomCode;
        CurrentVoiceChannelId = voiceChannel;
        IsHostOfCurrentRoom = true;

        // Lưu vào Local Shared Registry (hoạt động 100% giữa các process trên cùng máy)
        RegisterRoomInLocalRegistry(sessionData);

        // Thử đăng ký lên UGS Lobby & Relay nếu UGS đã sẵn sàng
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                // Thử tạo Relay allocation
                try
                {
                    Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
                    string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    sessionData.relayJoinCode = relayJoinCode;
                }
                catch (Exception relayEx)
                {
                    Debug.LogWarning($"[RoomManager] UGS Relay không khả dụng (dùng Direct Port): {relayEx.Message}");
                }

                // Thử tạo UGS Lobby
                try
                {
                    var options = new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "RoomCode", new DataObject(DataObject.VisibilityOptions.Public, roomCode, DataObject.IndexOptions.S1) },
                            { "VoiceChannelId", new DataObject(DataObject.VisibilityOptions.Public, voiceChannel) },
                            { "HostPort", new DataObject(DataObject.VisibilityOptions.Public, hostPort.ToString()) },
                            { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, sessionData.relayJoinCode ?? string.Empty) }
                        }
                    };

                    Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("Werewolf_" + roomCode, maxPlayers, options);
                    CurrentLobbyId = lobby.Id;

                    // Log dữ liệu thực tế Host theo yêu cầu Section 2.C
                    string dataKeys = options.Data != null ? string.Join(", ", options.Data.Keys) : "none";
                    string roomCodeVis = options.Data != null && options.Data.ContainsKey("RoomCode") ? options.Data["RoomCode"].Visibility.ToString() : "unknown";
                    Debug.Log($"[RoomManager] Host tạo Lobby thành công:");
                    Debug.Log($"[RoomManager] - Lobby ID: {lobby.Id}");
                    Debug.Log($"[RoomManager] - Lobby Name: {lobby.Name}");
                    Debug.Log($"[RoomManager] - Room Code: {roomCode}");
                    Debug.Log($"[RoomManager] - Lobby Data keys: [{dataKeys}]");
                    Debug.Log($"[RoomManager] - Visibility của Room Code: {roomCodeVis}");
                    Debug.Log($"[RoomManager] - Lobby State: Active, IsPrivate={lobby.IsPrivate}, IsLocked={lobby.IsLocked}");

                    // Khởi động Heartbeat định kỳ 15s để giữ Lobby sống trên UGS
                    StartHeartbeat(lobby.Id);
                }
                catch (Exception lobbyEx)
                {
                    Debug.LogWarning($"[RoomManager] UGS Lobby Service không khả dụng (dùng Local Hub): {lobbyEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoomManager] Bỏ qua UGS Cloud, chạy ở chế độ Local Hub: {ex.Message}");
        }

        // Cập nhật lại registry sau khi có thêm thông tin
        RegisterRoomInLocalRegistry(sessionData);
        return sessionData;
    }

    public static async Task UpdateHostPortInLobbyAsync(ushort newPort)
    {
        if (string.IsNullOrEmpty(CurrentLobbyId) || !IsHostOfCurrentRoom)
            return;

        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                var updateOptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "HostPort", new DataObject(DataObject.VisibilityOptions.Public, newPort.ToString()) }
                    }
                };
                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobbyId, updateOptions);
                Debug.Log($"[RoomManager] Đã cập nhật HostPort={newPort} lên UGS Lobby {CurrentLobbyId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoomManager] Lỗi khi cập nhật HostPort lên UGS Lobby: {ex.Message}");
        }
    }

    // =========================================================
    // 4. TRA CỨU PHÒNG (CLIENT)
    // =========================================================

    public static async Task<RoomLookupResult> LookupRoomAsync(string rawRoomCode)
    {
        string roomCode = rawRoomCode?.Trim();

        // Log Client Room Code nhập vào theo yêu cầu Section 2.C
        Debug.Log($"[RoomManager] Client Room Code nhập vào: '{roomCode}'");

        if (!IsValidRoomCode(roomCode))
        {
            Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: Mã '{roomCode}' không đúng định dạng (phải gồm đúng 6 chữ số).");
            return new RoomLookupResult
            {
                success = false,
                errorMessage = "Mã phòng không đúng định dạng (phải gồm đúng 6 chữ số)."
            };
        }

        // 1. Kiểm tra trong Local Registry trước (cực nhanh và tối ưu cho MPPM / LAN)
        RoomSessionData localRoom = GetRoomFromLocalRegistry(roomCode);
        if (localRoom != null)
        {
            if (localRoom.isClosed)
            {
                Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: Phòng '{roomCode}' trong Local Registry đã đóng hoặc kết thúc.");
                return new RoomLookupResult
                {
                    success = false,
                    errorMessage = "Phòng này đã đóng hoặc kết thúc."
                };
            }

            if (localRoom.currentPlayers >= localRoom.maxPlayers)
            {
                Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: Phòng '{roomCode}' trong Local Registry đã đầy người chơi.");
                return new RoomLookupResult
                {
                    success = false,
                    errorMessage = "Phòng đã đầy người chơi."
                };
            }

            Debug.Log($"[RoomManager] Client tìm thấy phòng '{roomCode}' trong Local Registry (Port: {localRoom.hostPort})");
            return new RoomLookupResult
            {
                success = true,
                sessionData = localRoom
            };
        }

        // 2. Nếu không có trong Local Registry, thử tìm qua UGS Lobby Service (nếu online) với retry có giới hạn
        string lastFailureReason = null;
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                // Log Query Filter đang sử dụng theo yêu cầu Section 2.C
                Debug.Log($"[RoomManager] Client Query Filter: S1 EQ '{roomCode}'");

                var queryOptions = new QueryLobbiesOptions
                {
                    Count = 10,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.S1, roomCode, QueryFilter.OpOptions.EQ)
                    }
                };

                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                    int resultCount = response?.Results?.Count ?? 0;

                    // Log Số Lobby nhận được theo yêu cầu Section 2.C
                    Debug.Log($"[RoomManager] Client số Lobby nhận được: {resultCount} (Lần thử {attempt}/{maxAttempts})");

                    if (response != null && response.Results != null && response.Results.Count > 0)
                    {
                        for (int i = 0; i < response.Results.Count; i++)
                        {
                            var l = response.Results[i];
                            string lCode = l.Data != null && l.Data.ContainsKey("RoomCode") ? l.Data["RoomCode"].Value : "Unknown";
                            // Log Room Code của từng Lobby trả về theo yêu cầu Section 2.C
                            Debug.Log($"[RoomManager] Lobby #{i + 1} trả về: ID={l.Id}, Name={l.Name}, RoomCode={lCode}, Slots={l.AvailableSlots}/{l.MaxPlayers}, IsLocked={l.IsLocked}");
                        }

                        Lobby ugsLobby = response.Results[0];

                        if (ugsLobby.IsLocked)
                        {
                            lastFailureReason = $"Phòng '{roomCode}' đang bị khóa hoặc trận đấu đã bắt đầu.";
                            Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: {lastFailureReason}");
                            return new RoomLookupResult
                            {
                                success = false,
                                errorMessage = "Phòng đang bị khóa hoặc trận đấu đã bắt đầu."
                            };
                        }

                        if (ugsLobby.AvailableSlots <= 0)
                        {
                            lastFailureReason = $"Phòng '{roomCode}' đã đầy người chơi.";
                            Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: {lastFailureReason}");
                            return new RoomLookupResult
                            {
                                success = false,
                                errorMessage = "Phòng đã đầy người chơi."
                            };
                        }

                        string voiceChannel = ugsLobby.Data != null && ugsLobby.Data.ContainsKey("VoiceChannelId")
                            ? ugsLobby.Data["VoiceChannelId"].Value
                            : "ww-" + roomCode;

                        string relayCode = ugsLobby.Data != null && ugsLobby.Data.ContainsKey("RelayJoinCode")
                            ? ugsLobby.Data["RelayJoinCode"].Value
                            : string.Empty;

                        ushort port = BASE_PORT;
                        if (ugsLobby.Data != null && ugsLobby.Data.ContainsKey("HostPort"))
                        {
                            ushort.TryParse(ugsLobby.Data["HostPort"].Value, out port);
                        }

                        var cloudSession = new RoomSessionData
                        {
                            roomCode = roomCode,
                            sessionId = ugsLobby.Id,
                            hostAddress = "127.0.0.1",
                            hostPort = port,
                            voiceChannelId = voiceChannel,
                            relayJoinCode = relayCode,
                            currentPlayers = ugsLobby.Players != null ? ugsLobby.Players.Count : 1,
                            maxPlayers = ugsLobby.MaxPlayers,
                            lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            isClosed = false
                        };

                        Debug.Log($"[RoomManager] Tra cứu UGS Lobby thành công: RoomCode={roomCode}, LobbyId={ugsLobby.Id}, Voice={voiceChannel}");

                        return new RoomLookupResult
                        {
                            success = true,
                            sessionData = cloudSession
                        };
                    }

                    // Nếu chưa tìm thấy kết quả và còn lượt thử, chờ một khoảng ngắn trước khi thử lại (UGS indexing delay)
                    if (attempt < maxAttempts)
                    {
                        int delayMs = 800 + (attempt * 400); // 1200ms, 1600ms
                        Debug.Log($"[RoomManager] Chưa thấy Lobby '{roomCode}' trên index UGS, đang chờ {delayMs}ms để thử lại (lần {attempt + 1}/{maxAttempts})...");
                        await Task.Delay(delayMs);
                    }
                }

                lastFailureReason = $"Không tìm thấy Lobby nào trên UGS với RoomCode '{roomCode}' sau {maxAttempts} lần truy vấn.";
            }
            else
            {
                lastFailureReason = "Chưa kết nối Unity Services hoặc không có mạng/Authentication.";
            }
        }
        catch (Exception ex)
        {
            lastFailureReason = $"Ngoại lệ khi tra cứu UGS Lobby: {ex.Message}";
            Debug.LogWarning($"[RoomManager] {lastFailureReason}");
        }

        // Log Lý do không tìm thấy phòng theo yêu cầu Section 2.C
        Debug.LogWarning($"[RoomManager] Lý do không tìm thấy phòng: {lastFailureReason ?? "Phòng không tồn tại hoặc đã hết hạn."}");

        return new RoomLookupResult
        {
            success = false,
            errorMessage = $"Không tìm thấy phòng với mã '{roomCode}'. Vui lòng kiểm tra lại!"
        };
    }

    // =========================================================
    // 5. ĐÓNG PHÒNG VÀ RỜI PHÒNG
    // =========================================================

    public static async Task CloseCurrentRoomAsync()
    {
        StopHeartbeat();

        string codeToClose = CurrentRoomCode;
        string lobbyIdToClose = CurrentLobbyId;

        CurrentRoomCode = null;
        CurrentVoiceChannelId = null;
        CurrentLobbyId = null;
        IsHostOfCurrentRoom = false;

        // Xóa / Đánh dấu đóng trong Local Registry
        if (!string.IsNullOrEmpty(codeToClose))
        {
            RemoveRoomFromLocalRegistry(codeToClose);
        }

        // Đóng trên UGS Lobby nếu có LobbyId
        if (!string.IsNullOrEmpty(lobbyIdToClose))
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyIdToClose);
                    Debug.Log($"[RoomManager] Đã xóa UGS Lobby {lobbyIdToClose}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomManager] Lỗi khi xóa UGS Lobby {lobbyIdToClose}: {ex.Message}");
            }
        }
    }

    // =========================================================
    // 5B. HEARTBEAT CHO UGS LOBBY
    // =========================================================

    public static void StartHeartbeat(string lobbyId)
    {
        StopHeartbeat();
        if (string.IsNullOrEmpty(lobbyId))
            return;

        heartbeatCts = new CancellationTokenSource();
        var token = heartbeatCts.Token;
        _ = RunHeartbeatLoopAsync(lobbyId, token);
        Debug.Log($"[RoomManager] Đã bắt đầu Heartbeat cho UGS Lobby {lobbyId}");
    }

    public static void StopHeartbeat()
    {
        if (heartbeatCts != null)
        {
            try
            {
                heartbeatCts.Cancel();
            }
            catch { }
            heartbeatCts.Dispose();
            heartbeatCts = null;
            Debug.Log("[RoomManager] Đã dừng Heartbeat Lobby.");
        }
    }

    private static async Task RunHeartbeatLoopAsync(string lobbyId, CancellationToken token)
    {
        while (!token.IsCancellationRequested && !string.IsNullOrEmpty(CurrentLobbyId) && CurrentLobbyId == lobbyId && IsHostOfCurrentRoom)
        {
            try
            {
                await Task.Delay(15000, token);
                if (token.IsCancellationRequested) break;

                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                    UpdateHeartbeatInLocalRegistry(CurrentRoomCode);
                    Debug.Log($"[RoomManager] Heartbeat ping gửi thành công cho Lobby {lobbyId}");
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomManager] Cảnh báo Heartbeat ping cho Lobby {lobbyId}: {ex.Message}");
            }
        }
    }

    public static void UpdateHeartbeatInLocalRegistry(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode)) return;
        var wrapper = LoadRegistry();
        var room = wrapper.rooms.Find(r => r.roomCode == roomCode);
        if (room != null)
        {
            room.lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveRegistry(wrapper);
        }
    }

    // =========================================================
    // 6. THAO TÁC FILE LOCAL REGISTRY (THREAD-SAFE & MULTI-PROCESS SAFE)
    // =========================================================

    private static RegistryWrapper LoadRegistry()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(SharedRegistryFilePath))
                    return new RegistryWrapper();

                string json = File.ReadAllText(SharedRegistryFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new RegistryWrapper();

                var wrapper = JsonUtility.FromJson<RegistryWrapper>(json);
                return wrapper ?? new RegistryWrapper();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ROOM MANAGER | Lỗi khi đọc file registry: {ex.Message}");
                return new RegistryWrapper();
            }
        }
    }

    private static void SaveRegistry(RegistryWrapper wrapper)
    {
        lock (FileLock)
        {
            try
            {
                // Dọn dẹp các phòng quá 2 giờ không có hoạt động
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                wrapper.rooms.RemoveAll(r => (now - r.lastHeartbeat > 7200) || r.isClosed);

                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(SharedRegistryFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ROOM MANAGER | Lỗi khi ghi file registry: {ex.Message}");
            }
        }
    }

    public static void RegisterRoomInLocalRegistry(RoomSessionData data)
    {
        var wrapper = LoadRegistry();
        wrapper.rooms.RemoveAll(r => r.roomCode == data.roomCode);
        wrapper.rooms.Add(data);
        SaveRegistry(wrapper);
        Debug.Log($"ROOM MANAGER | Đã lưu phòng '{data.roomCode}' vào Local Registry (Port: {data.hostPort}, Voice: {data.voiceChannelId})");
    }

    private static RoomSessionData GetRoomFromLocalRegistry(string roomCode)
    {
        var wrapper = LoadRegistry();
        return wrapper.rooms.Find(r => r.roomCode == roomCode && !r.isClosed);
    }

    private static bool IsRoomCodeActiveInLocalRegistry(string roomCode)
    {
        var wrapper = LoadRegistry();
        return wrapper.rooms.Exists(r => r.roomCode == roomCode && !r.isClosed);
    }

    private static void RemoveRoomFromLocalRegistry(string roomCode)
    {
        var wrapper = LoadRegistry();
        var room = wrapper.rooms.Find(r => r.roomCode == roomCode);
        if (room != null)
        {
            room.isClosed = true;
            SaveRegistry(wrapper);
            Debug.Log($"ROOM MANAGER | Đã đóng phòng '{roomCode}' trong Local Registry");
        }
    }

    public static void UpdatePlayerCountInRegistry(string roomCode, int delta)
    {
        if (string.IsNullOrEmpty(roomCode)) return;
        var wrapper = LoadRegistry();
        var room = wrapper.rooms.Find(r => r.roomCode == roomCode);
        if (room != null)
        {
            room.currentPlayers = Mathf.Max(1, room.currentPlayers + delta);
            room.lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveRegistry(wrapper);
        }
    }

    public static void SetPlayerCountInRegistry(string roomCode, int count)
    {
        if (string.IsNullOrEmpty(roomCode)) return;
        var wrapper = LoadRegistry();
        var room = wrapper.rooms.Find(r => r.roomCode == roomCode);
        if (room != null)
        {
            room.currentPlayers = Mathf.Max(1, count);
            room.lastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveRegistry(wrapper);
        }
    }
}

public class RoomLookupResult
{
    public bool success;
    public string errorMessage;
    public RoomManager.RoomSessionData sessionData;
}
