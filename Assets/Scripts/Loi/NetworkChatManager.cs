using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kênh chat trong game:
/// - Village (0): Chat Làng (người chơi còn sống).
/// - Monster (1): Chat Monster (chỉ dành cho thành viên phe Monster còn sống).
/// - Dead (2): Kênh Người Chết (chỉ dành cho người chơi đã chết).
/// </summary>
public enum ChatChannel : byte
{
    Village = 0,
    Monster = 1,
    Dead = 2
}

/// <summary>
/// Hệ thống đồng bộ tin nhắn Chat Multiplayer qua Netcode for GameObjects.
/// Quản lý gửi/nhận tin nhắn giữa Host và các Client, kiểm tra tính hợp lệ và phân quyền độc lập:
/// 1. Kênh Chat Làng: Dành cho tất cả người chơi trong làng.
/// 2. Kênh Chat Monster: Chỉ dành riêng cho người chơi thuộc phe Monster.
///    - Xác thực Server nghiêm ngặt (không tin tưởng dữ liệu Client).
///    - Bạch Khuyển chỉ được cấp quyền Chat Monster sau khi Server xác nhận thức tỉnh thành công.
///    - Lịch sử và nội dung hai kênh hoàn toàn tách biệt.
/// </summary>
public class NetworkChatManager : NetworkBehaviour
{
    public const int MaxMessageLength = 120;
    public const int MaxHistoryCount = 60;

    public const string MonsterChatRequestMessage = "loi.monsterchat.request.v1";
    public const string MonsterChatDeliveryMessage = "loi.monsterchat.delivery.v1";
    public const string MonsterChatPermissionSyncMessage = "loi.monsterchat.permsync.v1";

    public const string DeadChatRequestMessage = "loi.deadchat.request.v1";
    public const string DeadChatDeliveryMessage = "loi.deadchat.delivery.v1";
    public const string DeadChatMemberSyncMessage = "loi.deadchat.membersync.v1";

    [Serializable]
    public struct DeadMemberInfo : IEquatable<DeadMemberInfo>
    {
        public ulong ClientId;
        public int PlayerId;
        public string Name;

        public bool Equals(DeadMemberInfo other)
        {
            return ClientId == other.ClientId &&
                   PlayerId == other.PlayerId &&
                   string.Equals(Name, other.Name, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public struct ChatMessageEntry : INetworkSerializable, IEquatable<ChatMessageEntry>
    {
        public ulong SenderClientId;
        public FixedString64Bytes SenderName;
        public FixedString512Bytes MessageText;
        public long TimestampTicks;
        public bool IsSystem;
        public byte Channel; // 0 = Village, 1 = Monster

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SenderClientId);
            serializer.SerializeValue(ref SenderName);
            serializer.SerializeValue(ref MessageText);
            serializer.SerializeValue(ref TimestampTicks);
            serializer.SerializeValue(ref IsSystem);
            serializer.SerializeValue(ref Channel);
        }

        public bool Equals(ChatMessageEntry other)
        {
            return SenderClientId == other.SenderClientId &&
                   TimestampTicks == other.TimestampTicks &&
                   Channel == other.Channel &&
                   MessageText.Equals(other.MessageText);
        }
    }

    public static NetworkChatManager Instance { get; private set; }

    public static event Action<ChatMessageEntry> OnChatMessageReceived;
    public static event Action<ChatMessageEntry> OnVillageChatMessageReceived;
    public static event Action<ChatMessageEntry> OnMonsterChatMessageReceived;
    public static event Action<ChatMessageEntry> OnDeadChatMessageReceived;
    public static event Action<bool> OnMonsterChatPermissionChanged;
    public static event Action<bool> OnDeadChatPermissionChanged;
    public static event Action<IReadOnlyList<DeadMemberInfo>> OnDeadChatMembersChanged;
    public static event Action OnChatHistoryCleared;

    private readonly List<ChatMessageEntry> messageHistory = new List<ChatMessageEntry>();
    private readonly List<ChatMessageEntry> villageHistory = new List<ChatMessageEntry>();
    private readonly List<ChatMessageEntry> monsterHistory = new List<ChatMessageEntry>();
    private readonly List<ChatMessageEntry> deadHistory = new List<ChatMessageEntry>();
    private readonly List<DeadMemberInfo> deadMembers = new List<DeadMemberInfo>();

    private readonly Dictionary<ulong, uint> deadChatSequences = new Dictionary<ulong, uint>();
    private readonly Dictionary<ulong, double> deadLastSentAt = new Dictionary<ulong, double>();
    private readonly Dictionary<ulong, string> deadLastSentText = new Dictionary<ulong, string>();
    private uint localDeadSequence;

    public IReadOnlyList<ChatMessageEntry> MessageHistory => messageHistory;
    public IReadOnlyList<ChatMessageEntry> VillageHistory => villageHistory;
    public IReadOnlyList<ChatMessageEntry> MonsterHistory => monsterHistory;
    public IReadOnlyList<ChatMessageEntry> DeadHistory => deadHistory;
    public IReadOnlyList<DeadMemberInfo> DeadMembers => deadMembers;

    private static bool localMonsterChatPermission = false;
    public static bool LocalMonsterChatPermission => localMonsterChatPermission;

    private static bool localDeadChatPermission = false;
    public static bool LocalDeadChatPermission => localDeadChatPermission;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RegisterNetworkMessageHandlers();
        SubscribeEvents();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        RegisterNetworkMessageHandlers();
        RefreshLocalMonsterChatPermission();
        Debug.Log($"[CHAT] NetworkChatManager Spawned | IsServer={IsServer} | LocalClientId={NetworkManager.Singleton?.LocalClientId}");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnregisterNetworkMessageHandlers();
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RegisterNetworkMessageHandlers();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnregisterNetworkMessageHandlers();
    }

    private void SubscribeEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= HandleLocalWhiteHoundAwakened;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged += HandleLocalWhiteHoundAwakened;

        NetworkPlayerStateSync.OnLocalPlayerDeath -= HandleLocalPlayerDeathEvent;
        NetworkPlayerStateSync.OnLocalPlayerDeath += HandleLocalPlayerDeathEvent;

        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
            DeathResolver.Instance.DeathResolved += HandleDeathResolved;
        }

        SubscribePlayerDataEvents();
    }

    private void UnsubscribeEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        NetworkRoleSync.OnLocalWhiteHoundAwakenedChanged -= HandleLocalWhiteHoundAwakened;
        NetworkPlayerStateSync.OnLocalPlayerDeath -= HandleLocalPlayerDeathEvent;

        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
        }

        UnsubscribePlayerDataEvents();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (DeathResolver.Instance != null)
        {
            DeathResolver.Instance.DeathResolved -= HandleDeathResolved;
            DeathResolver.Instance.DeathResolved += HandleDeathResolved;
        }
        SubscribePlayerDataEvents();
        RegisterNetworkMessageHandlers();

        string name = scene.name;
        if (name == "MainMenu" || name == "OnlineSceneLobby" || name == "SceneLobby")
        {
            ResetDeadChatState();
            ClearHistory();
            localMonsterChatPermission = false;
            OnMonsterChatPermissionChanged?.Invoke(false);
        }
        else
        {
            RefreshLocalMonsterChatPermission();
            RefreshLocalDeadChatPermission();
        }
    }

    private readonly List<PlayerData> subscribedPlayers = new();

    private void SubscribePlayerDataEvents()
    {
        UnsubscribePlayerDataEvents();
        if (PlayerManager.Instance?.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null)
                {
                    p.Changed += HandlePlayerDataChanged;
                    subscribedPlayers.Add(p);
                }
            }
        }
    }

    private void UnsubscribePlayerDataEvents()
    {
        foreach (var p in subscribedPlayers)
        {
            if (p != null) p.Changed -= HandlePlayerDataChanged;
        }
        subscribedPlayers.Clear();
    }

    private void HandlePlayerDataChanged(PlayerData player)
    {
        if (player == null) return;
        int localId = NetworkWhiteHoundNotificationUI.GetLocalPlayerId();
        if (localId >= 0 && player.playerID == localId)
        {
            RefreshLocalMonsterChatPermission();
            RefreshLocalDeadChatPermission();
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SyncDeadChatStateOnServer();
        }
    }

    private void HandleLocalWhiteHoundAwakened(bool awakened)
    {
        // Yêu cầu: Bạch Khuyển không nằm trong kênh chat của Monster
        RefreshLocalMonsterChatPermission();
    }

    private void HandleLocalPlayerDeathEvent()
    {
        RefreshLocalMonsterChatPermission();
        RefreshLocalDeadChatPermission();
    }

    /// <summary>
    /// Xử lý sự kiện từ DeathResolver trên Server:
    /// Bạch Khuyển không nằm trong kênh chat của Monster; người chơi chết chuyển sang kênh Người Chết.
    /// </summary>
    private void HandleDeathResolved(DeathResult result)
    {
        RefreshLocalMonsterChatPermission();
        RefreshLocalDeadChatPermission();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SyncDeadChatStateOnServer();
        }
    }

    // =========================================================
    // 1. ĐĂNG KÝ VÀ XỬ LÝ GÓI TIN MẠNG CHO MONSTER CHAT
    // =========================================================

    private void RegisterNetworkMessageHandlers()
    {
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && net.CustomMessagingManager != null)
        {
            try
            {
                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatRequestMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(MonsterChatRequestMessage, ReceiveMonsterChatRequest);

                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatDeliveryMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(MonsterChatDeliveryMessage, ReceiveMonsterChatDelivery);

                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatPermissionSyncMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(MonsterChatPermissionSyncMessage, ReceiveMonsterChatPermissionSync);

                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatRequestMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(DeadChatRequestMessage, ReceiveDeadChatRequest);

                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatDeliveryMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(DeadChatDeliveryMessage, ReceiveDeadChatDelivery);

                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatMemberSyncMessage);
                net.CustomMessagingManager.RegisterNamedMessageHandler(DeadChatMemberSyncMessage, ReceiveDeadChatMemberSync);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CHAT MONSTER] Lỗi đăng ký handlers: {ex.Message}");
            }
        }
    }

    private void UnregisterNetworkMessageHandlers()
    {
        var net = NetworkManager.Singleton;
        if (net != null && net.CustomMessagingManager != null)
        {
            try
            {
                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatRequestMessage);
                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatDeliveryMessage);
                net.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterChatPermissionSyncMessage);
                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatRequestMessage);
                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatDeliveryMessage);
                net.CustomMessagingManager.UnregisterNamedMessageHandler(DeadChatMemberSyncMessage);
            }
            catch { }
        }
    }

    /// <summary>
    /// Server nhận yêu cầu gửi tin nhắn Monster từ Client.
    /// </summary>
    private void ReceiveMonsterChatRequest(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsServer) return;

        reader.ReadValueSafe(out FixedString512Bytes text);
        string sanitized = text.ToString();
        ProcessMonsterChatMessage(senderClientId, sanitized);
    }

    /// <summary>
    /// Client nhận tin nhắn Monster từ Server.
    /// </summary>
    private void ReceiveMonsterChatDelivery(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[CHAT MONSTER] Bỏ qua tin nhắn không đến từ Server.");
            return;
        }

        // Client-side guard: Chỉ hiển thị nếu local player có quyền Monster
        if (!HasLocalMonsterChatPermission())
        {
            Debug.LogWarning("[CHAT MONSTER] Client cục bộ không có quyền Monster, từ chối nhận.");
            return;
        }

        var entry = new ChatMessageEntry();
        reader.ReadValueSafe(out entry.SenderClientId);
        reader.ReadValueSafe(out entry.SenderName);
        reader.ReadValueSafe(out entry.MessageText);
        reader.ReadValueSafe(out entry.TimestampTicks);
        reader.ReadValueSafe(out entry.IsSystem);
        reader.ReadValueSafe(out entry.Channel);

        AddMessageInternal(entry);
    }

    /// <summary>
    /// Client nhận đồng bộ quyền Chat Monster từ Server.
    /// </summary>
    private void ReceiveMonsterChatPermissionSync(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            return;
        }

        reader.ReadValueSafe(out bool hasPermission);
        Debug.Log($"[CHAT MONSTER] Client nhận cập nhật quyền Chat Monster từ Server: hasPermission={hasPermission}");
        SetLocalMonsterChatPermission(hasPermission);
    }

    private void ReceiveDeadChatRequest(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsServer) return;

        reader.ReadValueSafe(out uint sequence);
        reader.ReadValueSafe(out FixedString512Bytes text);
        ProcessDeadChatMessage(senderClientId, text.ToString(), sequence);
    }

    private void ReceiveDeadChatDelivery(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[CHAT DEAD] Bỏ qua tin nhắn không đến từ Server.");
            return;
        }

        if (!HasLocalDeadChatPermission())
        {
            Debug.LogWarning("[CHAT DEAD] Người chơi chưa chết hoặc không có quyền Dead Chat, từ chối nhận.");
            return;
        }

        var entry = new ChatMessageEntry();
        reader.ReadValueSafe(out entry.SenderClientId);
        reader.ReadValueSafe(out entry.SenderName);
        reader.ReadValueSafe(out entry.MessageText);
        reader.ReadValueSafe(out entry.TimestampTicks);
        reader.ReadValueSafe(out entry.IsSystem);
        reader.ReadValueSafe(out entry.Channel);
        entry.Channel = (byte)ChatChannel.Dead;

        if (!IsDuplicateEntry(deadHistory, entry))
        {
            AddMessageInternal(entry);
        }
    }

    private void ReceiveDeadChatMemberSync(ulong senderClientId, FastBufferReader reader)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsClient || senderClientId != NetworkManager.ServerClientId)
            return;

        reader.ReadValueSafe(out bool hasPermission);
        reader.ReadValueSafe(out ushort count);

        var synced = new List<DeadMemberInfo>(count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ulong cid);
            reader.ReadValueSafe(out int pid);
            reader.ReadValueSafe(out FixedString64Bytes name);
            if (hasPermission)
            {
                synced.Add(new DeadMemberInfo
                {
                    ClientId = cid,
                    PlayerId = pid,
                    Name = name.ToString()
                });
            }
        }

        ApplyDeadChatMemberSync(hasPermission, synced);
    }

    // =========================================================
    // 2. PHÂN QUYỀN VÀ XÁC THỰC PHE MONSTER
    // =========================================================

    /// <summary>
    /// Kiểm tra người chơi có thuộc phe Monster không.
    /// </summary>
    public static bool IsPlayerMonster(PlayerData player)
    {
        if (player == null || !player.isAlive || player.isDisconnected) return false;
        // Yêu cầu: Bạch Khuyển (kể cả khi đã thức tỉnh thành Sói Trắng) KHÔNG nằm trong kênh chat của Monster
        if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf)
            return false;
        if (player.roleType == RoleType.DogSpirit || player.roleType == RoleType.SerpentSpirit || player.roleType == RoleType.Ogre)
            return true;
        if (player.faction == FactionType.Monster)
            return true;
        return false;
    }

    /// <summary>
    /// Server xác thực ClientId có thuộc phe Monster hợp lệ (và còn sống) không.
    /// Tuyệt đối không tin tưởng dữ liệu do Client gửi lên.
    /// </summary>
    public static bool IsClientMonster(ulong clientId)
    {
        if (IsClientDead(clientId)) return false;

        // 1. Kiểm tra qua ThuongOnlineSession nếu đang trong Thương Online
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            return session.IsServerPlayerMonster(clientId);
        }

        // 2. Kiểm tra PlayerManager theo matching clientId
        if (PlayerManager.Instance?.players != null)
        {
            var p = PlayerManager.Instance.GetplayerByID((int)clientId);
            if (p != null && IsPlayerMonster(p)) return true;
        }

        // 3. Kiểm tra qua danh sách người chơi trong PlayerManager
        if (PlayerManager.Instance?.players != null)
        {
            foreach (var pl in PlayerManager.Instance.players)
            {
                if (pl != null && (ulong)pl.playerID == clientId && IsPlayerMonster(pl))
                    return true;
            }
        }

        return false;
    }

    public static bool IsPlayerDead(PlayerData player)
    {
        return player != null && !player.isAlive && !player.isDisconnected;
    }

    /// <summary>
    /// Server xác thực ClientId đã chết và đủ quyền vào kênh Người Chết hay chưa.
    /// Tuyệt đối không tin tưởng trạng thái do Client tự gửi lên.
    /// </summary>
    public static bool IsClientDead(ulong clientId)
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            return session.IsServerPlayerDead(clientId);
        }

        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening)
        {
            if (net.ConnectedClients != null && net.ConnectedClients.TryGetValue(clientId, out var client) && client != null && client.PlayerObject != null)
            {
                var stateSync = client.PlayerObject.GetComponent<NetworkPlayerStateSync>();
                if (stateSync != null)
                {
                    if (stateSync.State.Value == NetworkPlayerStateType.Disconnected) return false;
                    if (stateSync.State.Value == NetworkPlayerStateType.Dead) return true;
                }
            }
        }

        if (PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID((int)clientId);
            if (p != null) return IsPlayerDead(p);
        }

        return false;
    }

    public static List<ulong> GetDeadClientIds()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            var members = session.GetServerDeadChatMembers();
            var ids = new List<ulong>(members.Count);
            foreach (var m in members) ids.Add(m.ClientId);
            return ids;
        }

        var list = new List<ulong>();
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening)
        {
            foreach (ulong cId in net.ConnectedClientsIds)
            {
                if (IsClientDead(cId)) list.Add(cId);
            }
            return list;
        }

        if (PlayerManager.Instance?.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (IsPlayerDead(p)) list.Add((ulong)p.playerID);
            }
        }
        return list;
    }

    public static bool HasLocalDeadChatPermission()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            return session.IsLocalPlayerDeadInMatch();
        }

        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && net.LocalClient != null && net.LocalClient.PlayerObject != null)
        {
            var stateSync = net.LocalClient.PlayerObject.GetComponent<NetworkPlayerStateSync>();
            if (stateSync != null)
            {
                return stateSync.State.Value == NetworkPlayerStateType.Dead;
            }
        }

        int localPlayerId = NetworkWhiteHoundNotificationUI.GetLocalPlayerId();
        if (localPlayerId >= 0 && PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localPlayerId);
            if (p != null)
            {
                return IsPlayerDead(p);
            }
        }

        return localDeadChatPermission;
    }

    public static void SetLocalDeadChatPermission(bool hasPermission)
    {
        if (localDeadChatPermission != hasPermission)
        {
            localDeadChatPermission = hasPermission;
            Debug.Log($"[CHAT DEAD] Quyền Dead Chat cục bộ thay đổi: {hasPermission}");
            OnDeadChatPermissionChanged?.Invoke(hasPermission);
        }
    }

    public static void RefreshLocalDeadChatPermission()
    {
        bool allowed = HasLocalDeadChatPermission();
        SetLocalDeadChatPermission(allowed);
    }

    /// <summary>
    /// Lấy danh sách ClientId của tất cả thành viên Monster trên Server.
    /// </summary>
    public static List<ulong> GetMonsterClientIds()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            return session.GetMonsterClientIds();
        }

        var list = new List<ulong>();
        var net = NetworkManager.Singleton;
        if (net != null)
        {
            foreach (ulong cId in net.ConnectedClientsIds)
            {
                if (IsClientMonster(cId))
                {
                    list.Add(cId);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// Kiểm tra người chơi cục bộ có quyền xem/sử dụng Chat Monster hay không.
    /// </summary>
    public static bool HasLocalMonsterChatPermission()
    {
        if (HasLocalDeadChatPermission()) return false;

        // 1. Kiểm tra vai trò cục bộ qua ThuongOnlineSession: Bạch Khuyển không nằm trong kênh chat của Monster
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            if (!session.IsLocalPlayerAliveInMatch()) return false;
            var role = session.GetPrivateRole();
            if (role == RoleType.WhiteHound || role == RoleType.WhiteWolf)
                return false;
            if (role == RoleType.DogSpirit || role == RoleType.SerpentSpirit || role == RoleType.Ogre)
                return true;
        }

        // 2. Kiểm tra qua NetworkRoleSync cục bộ
        if (NetworkRoleSync.LocalInstance != null)
        {
            var role = NetworkRoleSync.LocalInstance.LocalRole;
            if (role == RoleType.WhiteHound || role == RoleType.WhiteWolf)
                return false;
            if (role == RoleType.DogSpirit || role == RoleType.SerpentSpirit || role == RoleType.Ogre)
                return true;
        }

        // 3. Kiểm tra qua PlayerManager cục bộ
        int localPlayerId = NetworkWhiteHoundNotificationUI.GetLocalPlayerId();
        if (localPlayerId >= 0 && PlayerManager.Instance != null)
        {
            var p = PlayerManager.Instance.GetplayerByID(localPlayerId);
            if (p != null)
            {
                if (p.roleType == RoleType.WhiteHound || p.roleType == RoleType.WhiteWolf)
                    return false;
                if (IsPlayerMonster(p))
                    return true;
            }
        }

        if (localMonsterChatPermission) return true;

        return false;
    }

    /// <summary>
    /// Cập nhật quyền Chat Monster cho người chơi cục bộ.
    /// </summary>
    public static void SetLocalMonsterChatPermission(bool hasPermission)
    {
        if (localMonsterChatPermission != hasPermission)
        {
            localMonsterChatPermission = hasPermission;
            Debug.Log($"[CHAT MONSTER] Quyền Chat Monster cục bộ thay đổi: {hasPermission}");
            OnMonsterChatPermissionChanged?.Invoke(hasPermission);
        }
    }

    public static void RefreshLocalMonsterChatPermission()
    {
        bool allowed = HasLocalMonsterChatPermission();
        SetLocalMonsterChatPermission(allowed);
    }

    /// <summary>
    /// Server cấp quyền Chat Monster cho một PlayerId cụ thể (ví dụ khi Bạch Khuyển thức tỉnh).
    /// </summary>
    public void SyncMonsterPermissionForPlayer(int playerId)
    {
        if (PlayerManager.Instance != null)
        {
            var pl = PlayerManager.Instance.GetplayerByID(playerId);
            if (pl != null && (pl.roleType == RoleType.WhiteHound || pl.roleType == RoleType.WhiteWolf))
            {
                // Bạch Khuyển không nằm trong kênh chat của Monster
                return;
            }
        }

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            // Offline hoặc mock test
            SetLocalMonsterChatPermission(true);
            return;
        }

        if (net.IsServer)
        {
            ulong targetClientId = NetworkWhiteHoundNotificationUI.GetClientIdForPlayerId(playerId);
            if (targetClientId == net.LocalClientId)
            {
                SetLocalMonsterChatPermission(true);
            }
            else if (targetClientId != ulong.MaxValue && net.ConnectedClients.ContainsKey(targetClientId))
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(true);
                net.CustomMessagingManager.SendNamedMessage(
                    MonsterChatPermissionSyncMessage,
                    targetClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced
                );
                Debug.Log($"[CHAT MONSTER] Server đã gửi gói tin cấp quyền Chat Monster cho Client {targetClientId} (Player {playerId + 1}).");
            }
        }
    }

    // =========================================================
    // 3. GỬI & XỬ LÝ TIN NHẮN THEO KÊNH
    // =========================================================

    /// <summary>
    /// Gửi tin nhắn từ người chơi cục bộ lên Server theo kênh chỉ định (mặc định: Chat Làng).
    /// </summary>
    public void SendChatMessage(string rawText, ChatChannel channel = ChatChannel.Village)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return;

        string sanitized = rawText.Trim();
        if (sanitized.Length > MaxMessageLength)
        {
            sanitized = sanitized.Substring(0, MaxMessageLength);
        }

        if (channel == ChatChannel.Dead)
        {
            SendDeadChatMessage(sanitized);
            return;
        }

        if (HasLocalDeadChatPermission())
        {
            Debug.LogWarning($"[CHAT] Người chơi đã chết không thể gửi tin nhắn vào kênh {channel}!");
            return;
        }

        if (channel == ChatChannel.Monster)
        {
            SendMonsterChatMessage(sanitized);
            return;
        }

        // Kênh Chat Làng
        if (Thuong.Online.ThuongOnlineSession.Active != null)
        {
            Thuong.Online.ThuongOnlineSession.Active.SendChatText(sanitized);
            return;
        }

        if (!CanSendUnderCurrentRules(out string reason))
        {
            Debug.LogWarning($"[CHAT] Không thể gửi tin nhắn làng: {reason}");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SendMessageServerRpc(sanitized, (byte)ChatChannel.Village);
        }
        else
        {
            // Chế độ xem trước cục bộ (Offline/Preview trong Editor)
            var previewEntry = new ChatMessageEntry
            {
                SenderClientId = 0,
                SenderName = new FixedString64Bytes("Bạn"),
                MessageText = new FixedString512Bytes(sanitized),
                TimestampTicks = DateTime.UtcNow.Ticks,
                IsSystem = false,
                Channel = (byte)ChatChannel.Village
            };
            AddMessageInternal(previewEntry);
        }
    }

    /// <summary>
    /// Gửi tin nhắn trong kênh Chat Monster.
    /// </summary>
    public void SendMonsterChatMessage(string text)
    {
        // 1. Kiểm tra sơ bộ phía Client
        if (!HasLocalMonsterChatPermission())
        {
            Debug.LogWarning("[CHAT MONSTER] Bạn không thuộc phe Monster hoặc chưa thức tỉnh, không thể gửi tin nhắn Chat Monster!");
            return;
        }

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            // Môi trường offline / mock unit test
            var entry = new ChatMessageEntry
            {
                SenderClientId = 0,
                SenderName = new FixedString64Bytes("Bạn (Monster)"),
                MessageText = new FixedString512Bytes(text),
                TimestampTicks = DateTime.UtcNow.Ticks,
                IsSystem = false,
                Channel = (byte)ChatChannel.Monster
            };
            AddMessageInternal(entry);
            return;
        }

        if (net.IsServer)
        {
            ProcessMonsterChatMessage(net.LocalClientId, text);
        }
        else
        {
            using var writer = new FastBufferWriter(560, Allocator.Temp);
            writer.WriteValueSafe(new FixedString512Bytes(text));
            net.CustomMessagingManager.SendNamedMessage(MonsterChatRequestMessage, NetworkManager.ServerClientId, writer);
        }
    }

    /// <summary>
    /// Xử lý tin nhắn Chat Monster trên Server:
    /// - Kiểm tra quyền senderClientId nghiêm ngặt.
    /// - Chỉ phát tán tới ClientId của các thành viên phe Monster.
    /// </summary>
    public bool ProcessMonsterChatMessage(ulong senderClientId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // 1. Xác thực Server: Người gửi có thực sự là Monster không?
        if (!IsClientMonster(senderClientId))
        {
            Debug.LogWarning($"[CHAT MONSTER] TỪ CHỐI! Client {senderClientId} không thuộc phe Monster hoặc chưa thức tỉnh hợp lệ!");
            return false;
        }

        string senderName = ResolveSenderName(senderClientId);
        var entry = new ChatMessageEntry
        {
            SenderClientId = senderClientId,
            SenderName = new FixedString64Bytes(senderName),
            MessageText = new FixedString512Bytes(text),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = false,
            Channel = (byte)ChatChannel.Monster
        };

        // 2. Thu thập danh sách tất cả ClientId của thành viên Monster
        List<ulong> monsterClientIds = GetMonsterClientIds();

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            AddMessageInternal(entry);
            return true;
        }

        // 3. Chỉ gửi tin nhắn đến các Client thuộc phe Monster
        foreach (ulong recipientClientId in monsterClientIds)
        {
            if (recipientClientId == net.LocalClientId)
            {
                AddMessageInternal(entry);
            }
            else if (net.ConnectedClients.ContainsKey(recipientClientId))
            {
                using var writer = new FastBufferWriter(600, Allocator.Temp);
                writer.WriteValueSafe(entry.SenderClientId);
                writer.WriteValueSafe(entry.SenderName);
                writer.WriteValueSafe(entry.MessageText);
                writer.WriteValueSafe(entry.TimestampTicks);
                writer.WriteValueSafe(entry.IsSystem);
                writer.WriteValueSafe(entry.Channel);

                net.CustomMessagingManager.SendNamedMessage(
                    MonsterChatDeliveryMessage,
                    recipientClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced
                );
            }
        }

        Debug.Log($"[CHAT MONSTER] Server đã phát tán tin nhắn Monster từ {senderName} tới {monsterClientIds.Count} thành viên Monster.");
        return true;
    }

    public void SendDeadChatMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string sanitized = SanitizeChatContent(text);
        if (string.IsNullOrEmpty(sanitized)) return;

        if (!HasLocalDeadChatPermission())
        {
            Debug.LogWarning("[CHAT DEAD] Người chơi còn sống không thể gửi tin nhắn vào kênh Người Chết!");
            return;
        }

        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            if (session.SendDeadChatThroughSession(sanitized))
                return;
        }

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            var entry = new ChatMessageEntry
            {
                SenderClientId = (ulong)Mathf.Max(0, NetworkWhiteHoundNotificationUI.GetLocalPlayerId()),
                SenderName = new FixedString64Bytes(GetLocalSenderName()),
                MessageText = new FixedString512Bytes(sanitized),
                TimestampTicks = DateTime.UtcNow.Ticks,
                IsSystem = false,
                Channel = (byte)ChatChannel.Dead
            };
            AddMessageInternal(entry);
            return;
        }

        uint seq = ++localDeadSequence;
        if (net.IsServer)
        {
            ProcessDeadChatMessage(net.LocalClientId, sanitized, seq);
        }
        else
        {
            using var writer = new FastBufferWriter(568, Allocator.Temp);
            writer.WriteValueSafe(seq);
            writer.WriteValueSafe(new FixedString512Bytes(sanitized));
            net.CustomMessagingManager.SendNamedMessage(DeadChatRequestMessage, NetworkManager.ServerClientId, writer);
        }
    }

    public bool ProcessDeadChatMessage(ulong senderClientId, string rawText, uint clientSequence = 0)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return false;

        if (!IsClientDead(senderClientId))
        {
            Debug.LogWarning($"[CHAT DEAD] TỪ CHỐI! Client {senderClientId} chưa chết, không thể gửi tin nhắn Dead Chat!");
            return false;
        }

        string sanitized = SanitizeChatContent(rawText);
        if (string.IsNullOrEmpty(sanitized)) return false;

        if (clientSequence != 0 && deadChatSequences.TryGetValue(senderClientId, out uint prevSeq) && clientSequence <= prevSeq)
        {
            return false;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (deadLastSentAt.TryGetValue(senderClientId, out double lastTime) &&
            now - lastTime < 0.35 &&
            deadLastSentText.TryGetValue(senderClientId, out string lastText) &&
            string.Equals(lastText, sanitized, StringComparison.Ordinal))
        {
            return false;
        }

        if (clientSequence != 0) deadChatSequences[senderClientId] = clientSequence;
        deadLastSentAt[senderClientId] = now;
        deadLastSentText[senderClientId] = sanitized;

        string senderName = ResolveSenderName(senderClientId);
        var entry = new ChatMessageEntry
        {
            SenderClientId = senderClientId,
            SenderName = new FixedString64Bytes(senderName),
            MessageText = new FixedString512Bytes(sanitized),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = false,
            Channel = (byte)ChatChannel.Dead
        };

        List<ulong> deadClientIds = GetDeadClientIds();
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            AddMessageInternal(entry);
            return true;
        }

        foreach (ulong recipientClientId in deadClientIds)
        {
            if (recipientClientId == net.LocalClientId)
            {
                if (!IsDuplicateEntry(deadHistory, entry))
                {
                    AddMessageInternal(entry);
                }
            }
            else if (net.ConnectedClients.ContainsKey(recipientClientId))
            {
                using var writer = new FastBufferWriter(600, Allocator.Temp);
                writer.WriteValueSafe(entry.SenderClientId);
                writer.WriteValueSafe(entry.SenderName);
                writer.WriteValueSafe(entry.MessageText);
                writer.WriteValueSafe(entry.TimestampTicks);
                writer.WriteValueSafe(entry.IsSystem);
                writer.WriteValueSafe(entry.Channel);

                net.CustomMessagingManager.SendNamedMessage(
                    DeadChatDeliveryMessage,
                    recipientClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced
                );
            }
        }

        return true;
    }

    public void SyncDeadChatStateOnServer()
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        if (session != null)
        {
            session.BroadcastDeadChatMembersOnServer();
            return;
        }

        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening)
        {
            var localMembers = BuildStandaloneDeadMembers();
            ApplyDeadChatMemberSync(HasLocalDeadChatPermission(), localMembers);
            return;
        }

        if (!net.IsServer) return;

        var members = BuildStandaloneDeadMembers();
        foreach (ulong clientId in net.ConnectedClientsIds)
        {
            bool allowed = IsClientDead(clientId);
            SendDeadMemberSyncToClient(net, clientId, allowed, members);
        }
    }

    internal static void SendDeadMemberSyncToClient(NetworkManager net, ulong clientId, bool allowed, List<DeadMemberInfo> members)
    {
        if (net == null) return;
        if (clientId == net.LocalClientId)
        {
            if (Instance != null)
            {
                Instance.ApplyDeadChatMemberSync(allowed, allowed ? members : new List<DeadMemberInfo>());
            }
            else
            {
                SetLocalDeadChatPermission(allowed);
            }
            return;
        }

        if (net.CustomMessagingManager == null || !net.ConnectedClients.ContainsKey(clientId)) return;

        int count = allowed && members != null ? members.Count : 0;
        using var writer = new FastBufferWriter(Mathf.Max(64, 16 + count * 80), Allocator.Temp);
        writer.WriteValueSafe(allowed);
        writer.WriteValueSafe((ushort)count);
        for (int i = 0; i < count; i++)
        {
            writer.WriteValueSafe(members[i].ClientId);
            writer.WriteValueSafe(members[i].PlayerId);
            writer.WriteValueSafe(new FixedString64Bytes(members[i].Name ?? string.Empty));
        }

        net.CustomMessagingManager.SendNamedMessage(
            DeadChatMemberSyncMessage,
            clientId,
            writer,
            NetworkDelivery.ReliableSequenced
        );
    }

    public void ApplyDeadChatMemberSync(bool hasPermission, List<DeadMemberInfo> members)
    {
        SetLocalDeadChatPermission(hasPermission);

        bool changed = false;
        if (!hasPermission || members == null)
        {
            if (deadMembers.Count > 0)
            {
                deadMembers.Clear();
                changed = true;
            }
        }
        else if (deadMembers.Count != members.Count)
        {
            deadMembers.Clear();
            deadMembers.AddRange(members);
            changed = true;
        }
        else
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (!deadMembers[i].Equals(members[i]))
                {
                    deadMembers.Clear();
                    deadMembers.AddRange(members);
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            OnDeadChatMembersChanged?.Invoke(deadMembers);
        }
    }

    private List<DeadMemberInfo> BuildStandaloneDeadMembers()
    {
        var list = new List<DeadMemberInfo>();
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening)
        {
            foreach (ulong cid in net.ConnectedClientsIds)
            {
                if (!IsClientDead(cid)) continue;
                list.Add(new DeadMemberInfo
                {
                    ClientId = cid,
                    PlayerId = (int)cid,
                    Name = ResolveSenderName(cid)
                });
            }
            return list;
        }

        if (PlayerManager.Instance?.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (IsPlayerDead(p))
                {
                    list.Add(new DeadMemberInfo
                    {
                        ClientId = (ulong)p.playerID,
                        PlayerId = p.playerID,
                        Name = p.DisplayName
                    });
                }
            }
        }
        return list;
    }

    private static string SanitizeChatContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim();
        if (trimmed.Length > MaxMessageLength) trimmed = trimmed.Substring(0, MaxMessageLength);
        return trimmed.Replace('<', '‹').Replace('>', '›');
    }

    private static bool IsDuplicateEntry(List<ChatMessageEntry> history, ChatMessageEntry entry)
    {
        for (int i = history.Count - 1; i >= 0 && i >= history.Count - 8; i--)
        {
            if (history[i].Equals(entry)) return true;
        }
        return false;
    }

    /// <summary>
    /// ServerRpc nhận tin nhắn từ Client cho Netcode standalone mode.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SendMessageServerRpc(string text, byte channel = 0, ServerRpcParams rpcParams = default)
    {
        if (Thuong.Online.ThuongOnlineSession.Active != null) return;
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (string.IsNullOrWhiteSpace(text))
            return;

        if (channel == (byte)ChatChannel.Dead)
        {
            ProcessDeadChatMessage(senderClientId, text);
            return;
        }

        if (IsClientDead(senderClientId))
        {
            Debug.LogWarning($"[CHAT] TỪ CHỐI! Người chơi đã chết Client {senderClientId} không được gửi vào kênh {(ChatChannel)channel}.");
            return;
        }

        if (channel == (byte)ChatChannel.Monster)
        {
            ProcessMonsterChatMessage(senderClientId, text);
            return;
        }

        string sanitized = text.Trim();
        if (sanitized.Length > MaxMessageLength)
        {
            sanitized = sanitized.Substring(0, MaxMessageLength);
        }

        string senderName = ResolveSenderName(senderClientId);

        var entry = new ChatMessageEntry
        {
            SenderClientId = senderClientId,
            SenderName = new FixedString64Bytes(senderName),
            MessageText = new FixedString512Bytes(sanitized),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = false,
            Channel = (byte)ChatChannel.Village
        };

        ReceiveMessageClientRpc(entry);
    }

    [ClientRpc]
    private void ReceiveMessageClientRpc(ChatMessageEntry entry)
    {
        AddMessageInternal(entry);
    }

    /// <summary>
    /// Gửi tin nhắn thông báo hệ thống (chỉ Server/Host được gọi).
    /// </summary>
    public void BroadcastSystemMessage(string systemText, ChatChannel channel = ChatChannel.Village)
    {
        if (string.IsNullOrWhiteSpace(systemText))
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            return;

        var entry = new ChatMessageEntry
        {
            SenderClientId = ulong.MaxValue,
            SenderName = new FixedString64Bytes("Hệ thống"),
            MessageText = new FixedString512Bytes(systemText.Trim()),
            TimestampTicks = DateTime.UtcNow.Ticks,
            IsSystem = true,
            Channel = (byte)channel
        };

        if (channel == ChatChannel.Monster)
        {
            ProcessMonsterChatMessage(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0, systemText);
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ReceiveMessageClientRpc(entry);
        }
        else
        {
            AddMessageInternal(entry);
        }
    }

    private void AddMessageInternal(ChatMessageEntry entry)
    {
        messageHistory.Add(entry);
        while (messageHistory.Count > MaxHistoryCount)
        {
            messageHistory.RemoveAt(0);
        }

        if (entry.Channel == (byte)ChatChannel.Dead)
        {
            deadHistory.Add(entry);
            while (deadHistory.Count > MaxHistoryCount)
            {
                deadHistory.RemoveAt(0);
            }
            OnDeadChatMessageReceived?.Invoke(entry);
        }
        else if (entry.Channel == (byte)ChatChannel.Monster)
        {
            monsterHistory.Add(entry);
            while (monsterHistory.Count > MaxHistoryCount)
            {
                monsterHistory.RemoveAt(0);
            }
            OnMonsterChatMessageReceived?.Invoke(entry);
        }
        else
        {
            villageHistory.Add(entry);
            while (villageHistory.Count > MaxHistoryCount)
            {
                villageHistory.RemoveAt(0);
            }
            OnVillageChatMessageReceived?.Invoke(entry);
        }

        OnChatMessageReceived?.Invoke(entry);
    }

    internal void ReceiveThuongMessage(ChatMessageEntry entry)
    {
        var session = Thuong.Online.ThuongOnlineSession.Active;
        bool isDeadChannel = session != null && session.CommunicationAudience == Thuong.Online.ThuongCommunicationAudience.Dead;
        entry.Channel = isDeadChannel ? (byte)ChatChannel.Dead : (byte)ChatChannel.Village;

        var targetList = isDeadChannel ? deadHistory : villageHistory;
        if (IsDuplicateEntry(targetList, entry)) return;

        AddMessageInternal(entry);
    }

    public string ResolveSenderName(ulong clientId)
    {
        if (Thuong.Online.ThuongOnlineSession.Active != null)
            return Thuong.Online.ThuongOnlineSession.Active.ChatPlayerName(clientId);

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client != null && client.PlayerObject != null)
        {
            var nameSync = client.PlayerObject.GetComponent<NetworkPlayerNameSync>();
            if (nameSync != null && nameSync.HasValidName())
            {
                return nameSync.GetPlayerName();
            }
        }

        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (NetworkRoleSync.LocalInstance != null)
            {
                var localNameSync = NetworkRoleSync.LocalInstance.GetComponent<NetworkPlayerNameSync>();
                if (localNameSync != null && localNameSync.HasValidName())
                    return localNameSync.GetPlayerName();
            }
        }

        return "Player " + (clientId + 1);
    }

    public string GetLocalSenderName()
    {
        ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        return ResolveSenderName(localId);
    }

    public bool CanSendUnderCurrentRules(out string reason)
    {
        var thuong = Thuong.Online.ThuongOnlineSession.Active;
        if (thuong != null) { reason = thuong.ChatPermissionReason; return thuong.CanSendChat; }
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử tin nhắn của cả ba kênh.
    /// </summary>
    public void ClearHistory()
    {
        messageHistory.Clear();
        villageHistory.Clear();
        monsterHistory.Clear();
        deadHistory.Clear();
        OnChatHistoryCleared?.Invoke();
    }

    /// <summary>
    /// Reset toàn bộ trạng thái, lịch sử, danh sách thành viên và quyền kênh Người Chết khi bắt đầu ván mới hoặc rời phòng.
    /// </summary>
    public void ResetDeadChatState()
    {
        deadHistory.Clear();
        deadChatSequences.Clear();
        deadLastSentAt.Clear();
        deadLastSentText.Clear();
        localDeadSequence = 0;
        ApplyDeadChatMemberSync(false, null);
        OnChatHistoryCleared?.Invoke();
    }
}
