using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
                    Debug.LogWarning($"ROOM MANAGER | UGS Relay không khả dụng (dùng Direct Port): {relayEx.Message}");
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
                            { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, sessionData.relayJoinCode ?? string.Empty) }
                        }
                    };

                    Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("Werewolf_" + roomCode, maxPlayers, options);
                    CurrentLobbyId = lobby.Id;
                    Debug.Log($"ROOM MANAGER | Đã tạo UGS Lobby: {lobby.Id} với RoomCode: {roomCode}");
                }
                catch (Exception lobbyEx)
                {
                    Debug.LogWarning($"ROOM MANAGER | UGS Lobby Service không khả dụng (dùng Local Hub): {lobbyEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ROOM MANAGER | Bỏ qua UGS Cloud, chạy ở chế độ Local Hub: {ex.Message}");
        }

        // Cập nhật lại registry sau khi có thêm thông tin
        RegisterRoomInLocalRegistry(sessionData);
        return sessionData;
    }

    // =========================================================
    // 4. TRA CỨU PHÒNG (CLIENT)
    // =========================================================

    public static async Task<RoomLookupResult> LookupRoomAsync(string roomCode)
    {
        if (!IsValidRoomCode(roomCode))
        {
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
                return new RoomLookupResult
                {
                    success = false,
                    errorMessage = "Phòng này đã đóng hoặc kết thúc."
                };
            }

            if (localRoom.currentPlayers >= localRoom.maxPlayers)
            {
                return new RoomLookupResult
                {
                    success = false,
                    errorMessage = "Phòng đã đầy người chơi."
                };
            }

            return new RoomLookupResult
            {
                success = true,
                sessionData = localRoom
            };
        }

        // 2. Nếu không có trong Local Registry, thử tìm qua UGS Lobby Service (nếu online)
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                var queryOptions = new QueryLobbiesOptions
                {
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.S1, roomCode, QueryFilter.OpOptions.EQ)
                    }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                if (response != null && response.Results != null && response.Results.Count > 0)
                {
                    Lobby ugsLobby = response.Results[0];

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

                    return new RoomLookupResult
                    {
                        success = true,
                        sessionData = cloudSession
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ROOM MANAGER | Lỗi khi tra cứu UGS Lobby: {ex.Message}");
        }

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
        if (string.IsNullOrEmpty(CurrentRoomCode))
            return;

        string codeToClose = CurrentRoomCode;
        string lobbyIdToClose = CurrentLobbyId;

        // Xóa / Đánh dấu đóng trong Local Registry
        RemoveRoomFromLocalRegistry(codeToClose);

        // Đóng trên UGS Lobby nếu là Host
        if (IsHostOfCurrentRoom && !string.IsNullOrEmpty(lobbyIdToClose))
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyIdToClose);
                Debug.Log($"ROOM MANAGER | Đã xóa UGS Lobby {lobbyIdToClose}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ROOM MANAGER | Lỗi khi xóa UGS Lobby: {ex.Message}");
            }
        }

        CurrentRoomCode = null;
        CurrentVoiceChannelId = null;
        CurrentLobbyId = null;
        IsHostOfCurrentRoom = false;
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
}

public class RoomLookupResult
{
    public bool success;
    public string errorMessage;
    public RoomManager.RoomSessionData sessionData;
}
