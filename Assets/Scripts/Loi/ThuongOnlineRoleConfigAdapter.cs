using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Thuong.Online;

/// <summary>
/// Adapter tích hợp chức năng chỉnh vai trò và phân vai cho Thương Online.
/// Tuân thủ nghiêm ngặt các quy định:
///   1. TUYỆT ĐỐI KHÔNG SỬA CODE CỦA THƯƠNG (0 diff trong Assets/Scripts/Thuong/).
///   2. KHÔNG DÙNG REFLECTION, không truy cập private field hoặc hack trạng thái nội bộ.
///   3. Single Source of Truth: Cấu hình của Host quyết định 100% vai trò xuất hiện trong trận.
///   4. Phân vai đồng bộ: Can thiệp an toàn vào sự kiện PhaseChanged (Setup -> RoleReveal) để gán đúng 100% vai trò Host đã chọn.
///   5. Bố cục UI Lobby: Tách 8 nút chức năng thành 2 hàng responsive, không cắt chữ, dễ bấm trên điện thoại.
/// </summary>
public class ThuongOnlineRoleConfigAdapter : MonoBehaviour
{
    private static ThuongOnlineRoleConfigAdapter instance;
    public static ThuongOnlineRoleConfigAdapter Instance => instance;

    public const string RoleConfigSyncMessage = "loi.online.role_config_sync.v1";
    public const string RoleConfigRequestMessage = "loi.online.role_config_request.v1";

    private readonly Dictionary<RoleType, int> activeRoleAmounts = new Dictionary<RoleType, int>();
    private bool isCustomized = false;
    public bool IsCustomized => isCustomized;

    private uint configRevision = 0;
    private bool isMessagingRegistered = false;
    private bool isGameplayEventsSubscribed = false;

    // Cache kết quả phân vai chính thức để ngăn chặn mọi luồng ghi đè (Anti-Overwrite)
    private static readonly Dictionary<int, RoleType> confirmedAssignedRoles = new Dictionary<int, RoleType>();

    // Lobby UI Elements
    private GameObject injectedLobbyButton;
    private GameObject roleGuardOverlay;
    private Button interceptedRoleButton;
    private int lastObservedPlayerCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        if (instance != null)
        {
            instance.OnSceneChanged();
        }
    }

    public static void EnsureInstance()
    {
        if (instance != null) return;

        var existing = FindAnyObjectByType<ThuongOnlineRoleConfigAdapter>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[ThuongOnlineRoleConfigAdapter]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ThuongOnlineRoleConfigAdapter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaults(GetCurrentPlayerCount());
    }

    private void Start()
    {
        SubscribeNetworkEvents();
        EnsureGameplayEventsSubscribed();
    }

    private void Update()
    {
        EnsureNetworkRegistration();
        EnsureGameplayEventsSubscribed();
        EnsureSetupPhaseInLobby();
        CheckLobbyUIInjection();
        UpdateRoleGuardOverlay();
        MonitorPlayerCountChanges();
        CheckRolesAssignedTransition();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        UnsubscribeNetworkEvents();
        UnsubscribeGameplayEvents();
    }

    private void OnSceneChanged()
    {
        injectedLobbyButton = null;
        roleGuardOverlay = null;
        interceptedRoleButton = null;
        isGameplayEventsSubscribed = false;

        string activeScene = SceneManager.GetActiveScene().name;

        // Nếu về MainMenu hoặc Offline -> Reset dữ liệu phòng cũ
        if (activeScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            activeScene.IndexOf("Offline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ResetCaches();
            return;
        }

        if (activeScene != "OnlineSceneGamePlay")
        {
            return;
        }

        EnsureGameplayEventsSubscribed();

        // Không ghi đè nếu đã có cấu hình tùy chỉnh
        if (!isCustomized && !NetworkGameRoleSetup.HasSetup())
        {
            InitializeDefaults(GetCurrentPlayerCount());
        }
    }

    /// <summary>
    /// Đảm bảo GameRoleManager.Instance.currentPhase ở trạng thái Setup khi đang ở trong Lobby chưa phân vai.
    /// Việc này đảm bảo khi BeginGame() gọi SetPhase(GamePhase.RoleReveal), sự kiện PhaseChanged(Setup, RoleReveal)
    /// BẮT BUỘC SẼ KÍCH HOẠT (vì Setup != RoleReveal). Nếu không có bước này, currentPhase mặc định là 0 (RoleReveal),
    /// dẫn đến SetPhase(RoleReveal) coi previous == phase và KHÔNG KÍCH HOẠT PhaseChanged!
    /// </summary>
    private void EnsureSetupPhaseInLobby()
    {
        if (GameRoleManager.Instance == null) return;

        bool inLobby = ThuongOnlineSession.Active != null && !ThuongOnlineSession.Active.RolesAssigned;
        if (inLobby && GameRoleManager.Instance.currentPhase != GamePhase.Setup)
        {
            GameRoleManager.Instance.currentPhase = GamePhase.Setup;
        }
    }

    /// <summary>
    /// Bảo vệ toàn vẹn vai trò (Anti-Overwrite):
    /// Ngăn chặn bất kỳ cơ chế nào (như BalanceOnlineOpeningRoles của Thương) tự ý thay đổi vai trò sau khi Host đã phân vai.
    /// </summary>
    private static bool isRestoringRole = false;
    private static readonly HashSet<int> loggedOverwrittenPlayers = new HashSet<int>();
    private bool lastObservedRolesAssigned = false;

    private static void HandlePlayerDataChanged(PlayerData player)
    {
        if (isRestoringRole) return;
        if (player == null) return;

        bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServer) return;

        if (confirmedAssignedRoles.TryGetValue(player.playerID, out RoleType expectedRole))
        {
            if (player.roleType != expectedRole)
            {
                isRestoringRole = true;
                try
                {
                    if (!loggedOverwrittenPlayers.Contains(player.playerID))
                    {
                        loggedOverwrittenPlayers.Add(player.playerID);
                        Debug.LogWarning($"[ThuongRoleConfig] Ngăn chặn ghi đè vai trò Player {player.playerID} ({player.playerName})! Bị thay đổi từ {expectedRole} thành {player.roleType} bởi hệ thống ngoài (BalanceOnlineOpeningRoles). Phục hồi ngay lập tức về {expectedRole} theo cấu hình Host.");
                    }
                    var rm = RoleManager.Instance;
                    BaseRole restored = RoleCatalog.Create(expectedRole, player);
                    player.roleType = expectedRole;
                    player.faction = restored.faction;
                    if (rm != null && rm.roleDefinitions != null)
                    {
                        player.roleDefinition = rm.roleDefinitions.Find(expectedRole);
                    }
                    if (rm != null && rm.playerRoles != null)
                    {
                        rm.playerRoles[player.playerID] = restored;
                    }
                    restored.OnGameStart();

                    // Thông báo đồng bộ mạng qua ThuongOnlineSession tới các clients
                    WinConditionManager.Instance?.NotifyMatchStateChanged();
                }
                finally
                {
                    isRestoringRole = false;
                }
            }
        }
    }

    /// <summary>
    /// Theo dõi chuyển trạng thái RolesAssigned của ThuongOnlineSession.
    /// Kích hoạt kiểm tra / áp dụng cấu hình Host đúng 1 lần duy nhất khi việc phân vai kết thúc.
    /// </summary>
    private void CheckRolesAssignedTransition()
    {
        if (ThuongOnlineSession.Active == null)
        {
            lastObservedRolesAssigned = false;
            return;
        }

        bool currentRolesAssigned = ThuongOnlineSession.Active.RolesAssigned;
        if (!lastObservedRolesAssigned && currentRolesAssigned)
        {
            // RolesAssigned vừa chuyển từ false sang true: Quá trình phân vai trên Server kết thúc
            OnRolesAssignedTransition();
        }
        else if (lastObservedRolesAssigned && !currentRolesAssigned)
        {
            // Quay lại lobby từ ván trước: Reset danh sách để chuẩn bị cho ván tiếp theo
            confirmedAssignedRoles.Clear();
            loggedOverwrittenPlayers.Clear();
            EnsureSetupPhaseInLobby();
        }

        lastObservedRolesAssigned = currentRolesAssigned;
    }

    private void OnRolesAssignedTransition()
    {
        bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServer) return;

        if (!NetworkGameRoleSetup.HasSetup()) return;

        // Nếu chưa từng phân vai cho ván này (ví dụ BeginGame không kích hoạt PhaseChanged)
        if (confirmedAssignedRoles.Count == 0)
        {
            ApplyCustomRolesIfConfigured();
        }
        else
        {
            // Đã phân vai trong BeginGame, chỉ kiểm tra xác thực toàn vẹn 1 lần duy nhất (không phân vai lại lần 2)
            VerifyAssignedRolesIntegrityOnce();
        }

        WinConditionManager.Instance?.NotifyMatchStateChanged();
    }

    private void VerifyAssignedRolesIntegrityOnce()
    {
        bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServer) return;

        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null) return;
        if (RoleManager.Instance == null || RoleManager.Instance.playerRoles == null) return;

        bool restoredAny = false;
        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null) continue;
            if (confirmedAssignedRoles.TryGetValue(player.playerID, out RoleType expectedRole))
            {
                if (player.roleType != expectedRole ||
                    !RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole role) ||
                    role == null || role.roleType != expectedRole)
                {
                    if (!loggedOverwrittenPlayers.Contains(player.playerID))
                    {
                        loggedOverwrittenPlayers.Add(player.playerID);
                        Debug.LogWarning($"[ThuongRoleConfig] Phát hiện vai trò Player {player.playerID} ({player.playerName}) bị thay đổi từ {expectedRole} thành {player.roleType} bởi hệ thống ngoài (BalanceOnlineOpeningRoles/External). Đang khôi phục về {expectedRole} theo cấu hình Host...");
                    }

                    isRestoringRole = true;
                    try
                    {
                        BaseRole restored = RoleCatalog.Create(expectedRole, player);
                        player.roleType = expectedRole;
                        player.faction = restored.faction;
                        if (RoleManager.Instance.roleDefinitions != null)
                        {
                            player.roleDefinition = RoleManager.Instance.roleDefinitions.Find(expectedRole);
                        }
                        player.NotifyChanged();

                        RoleManager.Instance.playerRoles[player.playerID] = restored;
                        restored.OnGameStart();
                        restoredAny = true;
                    }
                    finally
                    {
                        isRestoringRole = false;
                    }
                }
            }
        }

        if (restoredAny)
        {
            WinConditionManager.Instance?.NotifyMatchStateChanged();
        }
    }

    // ==========================================
    // DEFAULTS & GETTERS (ZERO REFLECTION)
    // ==========================================

    public void InitializeDefaults(int playerCount)
    {
        playerCount = Mathf.Max(1, playerCount);
        var defs = GetDefaultRolesForPlayerCount(playerCount);
        activeRoleAmounts.Clear();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            activeRoleAmounts[role] = defs.TryGetValue(role, out int amount) ? amount : 0;
        }

        SaveToNetworkGameRoleSetup();
    }

    public static Dictionary<RoleType, int> GetDefaultRolesForPlayerCount(int playerCount)
    {
        var result = new Dictionary<RoleType, int>();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            result[role] = 0;
        }

        playerCount = Mathf.Max(1, playerCount);

        if (playerCount == 1)
        {
            result[RoleType.Villager] = 1;
        }
        else if (playerCount == 2)
        {
            result[RoleType.DogSpirit] = 1;
            result[RoleType.Villager] = 1;
        }
        else if (playerCount == 3)
        {
            result[RoleType.DogSpirit] = 1;
            result[RoleType.Villager] = 1;
            result[RoleType.Hunter] = 1;
        }
        else if (playerCount == 4)
        {
            result[RoleType.DogSpirit] = 1;
            result[RoleType.Villager] = 2;
            result[RoleType.Hunter] = 1;
        }
        else if (playerCount == 5)
        {
            result[RoleType.DogSpirit] = 1;
            result[RoleType.Villager] = 2;
            result[RoleType.Hunter] = 1;
            result[RoleType.Seer] = 1;
        }
        else
        {
            int wolves = Mathf.Max(1, playerCount / 4);
            int specials = 3;
            int villagers = Mathf.Max(1, playerCount - wolves - specials);

            result[RoleType.DogSpirit] = wolves;
            result[RoleType.Hunter] = 1;
            result[RoleType.Seer] = 1;
            result[RoleType.VillageGuardian] = 1;
            result[RoleType.Villager] = villagers;
        }

        return result;
    }

    public int GetCurrentPlayerCount()
    {
        // 1. Kiểm tra NetworkManager connected clients
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int count = NetworkManager.Singleton.ConnectedClientsIds.Count;
            if (count > 0) return count;
        }

        // 2. Kiểm tra PlayerManager players
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null && PlayerManager.Instance.players.Count > 0)
        {
            return PlayerManager.Instance.players.Count;
        }

        return 2;
    }

    public int GetRoleAmount(RoleType role)
    {
        return activeRoleAmounts.TryGetValue(role, out int val) ? val : 0;
    }

    public int GetTotalRoleAmount()
    {
        return activeRoleAmounts.Values.Sum();
    }

    public Dictionary<RoleType, int> GetCurrentConfig()
    {
        return new Dictionary<RoleType, int>(activeRoleAmounts);
    }

    // ==========================================
    // HOST ACTIONS (TĂNG, GIẢM, LƯU, MẶC ĐỊNH)
    // ==========================================

    public void HostSetRoleAmount(RoleType role, int amount)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[ThuongRoleConfig] Client không được phép thay đổi cấu hình vai trò!");
            return;
        }

        isCustomized = true;
        activeRoleAmounts[role] = Mathf.Max(0, amount);
        SaveToNetworkGameRoleSetup();
        BroadcastConfigToClients();
    }

    public void HostSaveConfig(Dictionary<RoleType, int> newConfig)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[ThuongRoleConfig] Client không được phép lưu cấu hình vai trò!");
            return;
        }

        if (newConfig == null) return;

        isCustomized = true;
        activeRoleAmounts.Clear();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            activeRoleAmounts[role] = newConfig.TryGetValue(role, out int val) ? Mathf.Max(0, val) : 0;
        }

        SaveToNetworkGameRoleSetup();
        BroadcastConfigToClients();
        Debug.Log($"[ThuongRoleConfig] Host đã lưu cấu hình vai trò mới! Tổng vai trò: {GetTotalRoleAmount()}");
    }

    public void HostRestoreDefaultConfig()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        isCustomized = false;
        InitializeDefaults(GetCurrentPlayerCount());
        BroadcastConfigToClients();
        Debug.Log($"[ThuongRoleConfig] Host đã khôi phục cấu hình vai trò mặc định cho {GetCurrentPlayerCount()} người chơi.");
    }

    private void SaveToNetworkGameRoleSetup()
    {
        var list = new List<RoleAmountData>();
        foreach (var kvp in activeRoleAmounts)
        {
            if (kvp.Value > 0)
            {
                list.Add(new RoleAmountData(kvp.Key, kvp.Value));
            }
        }
        NetworkGameRoleSetup.Save(list);
    }

    private void MonitorPlayerCountChanges()
    {
        int count = GetCurrentPlayerCount();
        if (count != lastObservedPlayerCount)
        {
            lastObservedPlayerCount = count;
            bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
            if (isServer && !isCustomized)
            {
                InitializeDefaults(count);
                BroadcastConfigToClients();
            }
        }
    }

    // ==========================================
    // NETWORK SYNC & MESSAGING
    // ==========================================

    private void SubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnsubscribeNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        UnregisterCustomMessages();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ResetCaches();
        }
    }

    private void EnsureNetworkRegistration()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            isMessagingRegistered = false;
            return;
        }

        if (!isMessagingRegistered && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            RegisterCustomMessages();
        }
    }

    private void RegisterCustomMessages()
    {
        var mgr = NetworkManager.Singleton?.CustomMessagingManager;
        if (mgr == null) return;

        mgr.UnregisterNamedMessageHandler(RoleConfigSyncMessage);
        mgr.UnregisterNamedMessageHandler(RoleConfigRequestMessage);

        mgr.RegisterNamedMessageHandler(RoleConfigSyncMessage, HandleReceiveRoleConfigSync);
        mgr.RegisterNamedMessageHandler(RoleConfigRequestMessage, HandleReceiveRoleConfigRequest);

        isMessagingRegistered = true;
    }

    private void UnregisterCustomMessages()
    {
        var mgr = NetworkManager.Singleton?.CustomMessagingManager;
        if (mgr == null) return;

        mgr.UnregisterNamedMessageHandler(RoleConfigSyncMessage);
        mgr.UnregisterNamedMessageHandler(RoleConfigRequestMessage);
        isMessagingRegistered = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        // Khi Client mới tham gia Lobby, Host tự động gửi cấu hình mới nhất
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SendConfigToClient(clientId);
        }
    }

    public void BroadcastConfigToClients()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !NetworkManager.Singleton.IsServer)
            return;

        configRevision++;

        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        SerializeConfig(writer);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId) continue;
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                RoleConfigSyncMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableFragmentedSequenced
            );
        }
    }

    public void SendConfigToClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !NetworkManager.Singleton.IsServer)
            return;

        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        SerializeConfig(writer);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            RoleConfigSyncMessage,
            clientId,
            writer,
            NetworkDelivery.ReliableFragmentedSequenced
        );
    }

    private void SerializeConfig(FastBufferWriter writer)
    {
        writer.WriteValueSafe(configRevision);
        writer.WriteValueSafe(isCustomized);

        var nonZero = activeRoleAmounts.Where(kvp => kvp.Value > 0).ToList();
        writer.WriteValueSafe((ushort)nonZero.Count);
        foreach (var kvp in nonZero)
        {
            writer.WriteValueSafe((byte)kvp.Key);
            writer.WriteValueSafe((ushort)kvp.Value);
        }
    }

    private void HandleReceiveRoleConfigSync(ulong sender, FastBufferReader reader)
    {
        // Chỉ chấp nhận gói sync gửi từ Server
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            return;

        if (sender != NetworkManager.ServerClientId)
        {
            Debug.LogWarning($"[ThuongRoleConfig] Từ chối gói sync vai trò không đến từ Server (sender: {sender})");
            return;
        }

        reader.ReadValueSafe(out uint rev);
        reader.ReadValueSafe(out bool customized);
        reader.ReadValueSafe(out ushort count);

        var newConfig = new Dictionary<RoleType, int>();
        foreach (RoleType role in RoleCatalog.AllConcreteRoles)
        {
            newConfig[role] = 0;
        }

        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out byte roleByte);
            reader.ReadValueSafe(out ushort amount);
            newConfig[(RoleType)roleByte] = amount;
        }

        configRevision = rev;
        isCustomized = customized;

        activeRoleAmounts.Clear();
        foreach (var kvp in newConfig)
        {
            activeRoleAmounts[kvp.Key] = kvp.Value;
        }

        SaveToNetworkGameRoleSetup();

        // Cập nhật giao diện Client ngay khi nhận dữ liệu
        if (ThuongOnlineRoleConfigDialog.IsShowing)
        {
            ThuongOnlineRoleConfigDialog.Instance.UpdateSynchronizedConfig(activeRoleAmounts, GetCurrentPlayerCount());
        }

        Debug.Log($"[ThuongRoleConfig] Client đã nhận đồng bộ cấu hình vai trò mới từ Server (rev {rev}, total {GetTotalRoleAmount()})");
    }

    private void HandleReceiveRoleConfigRequest(ulong sender, FastBufferReader reader)
    {
        // TC08: Client cố gửi yêu cầu chỉnh vai trò -> Server từ chối
        if (sender != NetworkManager.ServerClientId)
        {
            Debug.LogWarning($"[ThuongRoleConfig] TC08 - Server TỪ CHỐI yêu cầu thay đổi cấu hình vai trò từ Client {sender}. Chỉ Host mới có quyền!");
            return;
        }
    }

    // ==========================================
    // LOBBY UI INJECTION & 2-ROW RESPONSIVE RESTRUCTURING
    // ==========================================

    private void CheckLobbyUIInjection()
    {
        var lobbyCanvasGo = GameObject.Find("Online Lobby Canvas");
        if (lobbyCanvasGo == null) return;

        var card = lobbyCanvasGo.transform.Find("Lobby shade/Lobby Card");
        if (card == null) return;

        // Tái cấu trúc bố cục 8 nút thành 2 hàng rõ ràng, giải quyết triệt để lỗi ép nhỏ và cắt chữ
        RestructureLobbyBottomButtons(card);
    }

    /// <summary>
    /// Sắp xếp 8 nút chức năng phía dưới của Lobby thành 2 hàng chuẩn responsive:
    ///   - Hàng 1 (Cấu hình & Trợ giúp): MAP, LUẬT VÁN, CHỈNH/XEM VAI TRÒ, HƯỚNG DẪN
    ///   - Hàng 2 (Thao tác Trận đấu): READY / SẴN SÀNG, PHÂN VAI, BẮT ĐẦU, RỜI PHÒNG
    /// Mỗi nút có chiều rộng 22% diện tích card, font chữ tự động co giãn 12-18px, không chồng lấn.
    /// </summary>
    private void RestructureLobbyBottomButtons(Transform card)
    {
        var mapBtn = card.Find("Map settings button")?.GetComponent<RectTransform>();
        var rulesBtn = card.Find("Room rules button")?.GetComponent<RectTransform>();
        var guideBtn = card.Find("New player guide")?.GetComponent<RectTransform>();

        var readyBtn = card.Find("Lobby Ready")?.GetComponent<RectTransform>();
        var roleBtn = card.Find("Lobby Role")?.GetComponent<RectTransform>();
        var startBtn = card.Find("Lobby Start")?.GetComponent<RectTransform>();
        var leaveBtn = card.Find("Leave lobby")?.GetComponent<RectTransform>();

        bool isHost = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        string roleBtnLabel = isHost ? "CHỈNH VAI TRÒ" : "XEM VAI TRÒ";

        // Tạo nút VAI TRÒ nếu chưa có
        if (injectedLobbyButton == null)
        {
            injectedLobbyButton = CreateLobbyButton("Role config button", card, roleBtnLabel,
                new Vector2(0.505f, 0.075f), new Vector2(0.725f, 0.130f), OpenRoleConfigUI);
        }
        else
        {
            var tmp = injectedLobbyButton.GetComponentInChildren<TMP_Text>();
            if (tmp != null && tmp.text != roleBtnLabel)
            {
                tmp.text = roleBtnLabel;
            }
        }

        // --- HÀNG 1 (Y: 0.075f đến 0.130f) ---
        if (mapBtn != null)
        {
            mapBtn.anchorMin = new Vector2(0.025f, 0.075f);
            mapBtn.anchorMax = new Vector2(0.245f, 0.130f);
            mapBtn.offsetMin = mapBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(mapBtn);
        }

        if (rulesBtn != null)
        {
            rulesBtn.anchorMin = new Vector2(0.265f, 0.075f);
            rulesBtn.anchorMax = new Vector2(0.485f, 0.130f);
            rulesBtn.offsetMin = rulesBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(rulesBtn);
        }

        if (injectedLobbyButton != null)
        {
            var roleConfigRect = injectedLobbyButton.GetComponent<RectTransform>();
            roleConfigRect.anchorMin = new Vector2(0.505f, 0.075f);
            roleConfigRect.anchorMax = new Vector2(0.725f, 0.130f);
            roleConfigRect.offsetMin = roleConfigRect.offsetMax = Vector2.zero;
            OptimizeButtonText(roleConfigRect);
        }

        if (guideBtn != null)
        {
            guideBtn.anchorMin = new Vector2(0.745f, 0.075f);
            guideBtn.anchorMax = new Vector2(0.975f, 0.130f);
            guideBtn.offsetMin = guideBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(guideBtn);
        }

        // --- HÀNG 2 (Y: 0.012f đến 0.067f) ---
        if (readyBtn != null)
        {
            readyBtn.anchorMin = new Vector2(0.025f, 0.012f);
            readyBtn.anchorMax = new Vector2(0.245f, 0.067f);
            readyBtn.offsetMin = readyBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(readyBtn);
        }

        if (roleBtn != null)
        {
            roleBtn.anchorMin = new Vector2(0.265f, 0.012f);
            roleBtn.anchorMax = new Vector2(0.485f, 0.067f);
            roleBtn.offsetMin = roleBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(roleBtn);

            // Nổi bật nút PHÂN VAI với tông màu gỗ ấm
            var roleImg = roleBtn.GetComponent<Image>();
            if (roleImg != null)
            {
                roleImg.color = new Color(0.46f, 0.32f, 0.15f, 1f);
            }

            // Hook vào nút Phân vai để đặt GameRoleManager về phase Setup trước khi BeginGame
            var btnComponent = roleBtn.GetComponent<Button>();
            if (btnComponent != null && interceptedRoleButton != btnComponent)
            {
                interceptedRoleButton = btnComponent;
                btnComponent.onClick.AddListener(HandleLobbyRoleButtonClick);
            }

            EnsureRoleGuardOverlay(roleBtn);
        }

        if (startBtn != null)
        {
            startBtn.anchorMin = new Vector2(0.505f, 0.012f);
            startBtn.anchorMax = new Vector2(0.725f, 0.067f);
            startBtn.offsetMin = startBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(startBtn);
        }

        if (leaveBtn != null)
        {
            leaveBtn.anchorMin = new Vector2(0.745f, 0.012f);
            leaveBtn.anchorMax = new Vector2(0.975f, 0.067f);
            leaveBtn.offsetMin = leaveBtn.offsetMax = Vector2.zero;
            OptimizeButtonText(leaveBtn);

            var leaveImg = leaveBtn.GetComponent<Image>();
            if (leaveImg != null)
            {
                leaveImg.color = new Color(0.42f, 0.18f, 0.16f, 1f);
            }
        }
    }

    private void HandleLobbyRoleButtonClick()
    {
        // Khi Host click Phân vai, luôn đảm bảo GameRoleManager ở Setup phase để kích hoạt PhaseChanged khi BeginGame
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.currentPhase = GamePhase.Setup;
        }
    }

    private void OptimizeButtonText(RectTransform btnRect)
    {
        var tmp = btnRect.GetComponentInChildren<TMP_Text>();
        if (tmp == null) return;

        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    /// <summary>
    /// Tạo overlay bảo vệ trên nút "PHÂN VAI" (Lobby Role):
    /// - Khi Host cấu hình tổng vai trò khác số người chơi, overlay sẽ chặn click và hiển thị cảnh báo + mở bảng chỉnh vai trò.
    /// - Khi cấu hình khớp chuẩn, overlay tự tắt, click chuyển thẳng vào sự kiện gốc của Thương mà KHÔNG CẦN REFLECTION.
    /// </summary>
    private void EnsureRoleGuardOverlay(RectTransform roleBtn)
    {
        if (roleGuardOverlay != null) return;

        var overlayGo = new GameObject("RoleGuardOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = overlayGo.GetComponent<RectTransform>();
        rect.SetParent(roleBtn, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = overlayGo.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        var btn = overlayGo.GetComponent<Button>();
        btn.onClick.AddListener(HandleRoleGuardClicked);

        roleGuardOverlay = overlayGo;
    }

    private void UpdateRoleGuardOverlay()
    {
        if (roleGuardOverlay == null) return;

        bool isHost = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        bool rolesAlreadyAssigned = ThuongOnlineSession.Active != null && ThuongOnlineSession.Active.RolesAssigned;

        if (!isHost || rolesAlreadyAssigned)
        {
            if (roleGuardOverlay.activeSelf) roleGuardOverlay.SetActive(false);
            return;
        }

        int totalRoles = GetTotalRoleAmount();
        int playerCount = GetCurrentPlayerCount();

        // Chặn phân vai nếu tổng số lượng vai trò không khớp sĩ số
        bool shouldBlock = isCustomized && (totalRoles != playerCount);
        if (roleGuardOverlay.activeSelf != shouldBlock)
        {
            roleGuardOverlay.SetActive(shouldBlock);
        }
    }

    private void HandleRoleGuardClicked()
    {
        int totalRoles = GetTotalRoleAmount();
        int playerCount = GetCurrentPlayerCount();

        Debug.LogWarning($"[ThuongRoleConfig] Số vai trò đã chọn ({totalRoles}) không khớp với số người chơi ({playerCount})! Vui lòng điều chỉnh lại vai trò trước khi phân vai.");
        OpenRoleConfigUI();
    }

    private void OpenRoleConfigUI()
    {
        bool isHost = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        int players = GetCurrentPlayerCount();

        ThuongOnlineRoleConfigDialog.ShowDialog(
            isHost: isHost,
            initialConfig: activeRoleAmounts,
            playerCount: players,
            onSave: HostSaveConfig
        );
    }

    private GameObject CreateLobbyButton(
        string name,
        Transform parent,
        string label,
        Vector2 min,
        Vector2 max,
        UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.24f, 0.36f, 0.22f, 1f);
        ThuongMedievalTheme.Active?.StylePanel(img, name);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(go.transform, false);
        textRect.anchorMin = new Vector2(0.04f, 0.04f);
        textRect.anchorMax = new Vector2(0.96f, 0.96f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGo.GetComponent<TMP_Text>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.fontSizeMin = 12;
        tmp.fontSizeMax = 18;
        tmp.enableAutoSizing = true;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.color = ThuongMedievalTheme.Cream;
        ThuongMedievalTheme.Active?.StyleText(tmp);

        return go;
    }

    // ==========================================
    // PHÂN VAI (ROLE ASSIGNMENT OVERRIDE HOOK - ZERO REFLECTION)
    // ==========================================

    private void EnsureGameplayEventsSubscribed()
    {
        if (GameRoleManager.Instance != null && !isGameplayEventsSubscribed)
        {
            GameRoleManager.Instance.PhaseChanged -= HandlePhaseChanged;
            GameRoleManager.Instance.PhaseChanged += HandlePhaseChanged;
            isGameplayEventsSubscribed = true;
        }
    }

    private void UnsubscribeGameplayEvents()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= HandlePhaseChanged;
        }
        isGameplayEventsSubscribed = false;
    }

    private void HandlePhaseChanged(GamePhase previous, GamePhase next)
    {
        // Khi game bắt đầu chuyển sang RoleReveal
        if (next == GamePhase.RoleReveal)
        {
            bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
            if (isServer)
            {
                if (confirmedAssignedRoles.Count == 0)
                {
                    ApplyCustomRolesIfConfigured();
                }
            }
        }
    }

    /// <summary>
    /// Gán vai trò theo đúng 100% cấu hình đã lưu của Host (Single Source of Truth).
    ///   - Chỉ xáo trộn ngẫu nhiên người nhận, không thay đổi loại hoặc số lượng vai trò đã cấu hình.
    ///   - Sử dụng 100% API công khai: PlayerManager.Instance.players, RoleCatalog.Create, RoleManager.Instance.playerRoles.
    ///   - Hoàn tất đồng bộ ngay trong phase RoleReveal trước khi các client hiển thị thẻ vai trò.
    /// </summary>
    public static bool ApplyCustomRolesIfConfigured(int? seed = null)
    {
        if (!NetworkGameRoleSetup.HasSetup())
        {
            Debug.Log("[ThuongRoleConfig] Không có cấu hình vai trò tùy chỉnh. Dùng phân vai mặc định của game.");
            return false;
        }

        PlayerManager pm = PlayerManager.Instance;
        RoleManager rm = RoleManager.Instance;

        if (pm == null || rm == null || pm.players == null || pm.players.Count == 0)
        {
            Debug.LogWarning("[ThuongRoleConfig] PlayerManager hoặc RoleManager chưa sẵn sàng.");
            return false;
        }

        List<RoleAmountData> roleSetup = NetworkGameRoleSetup.GetRoles();
        if (roleSetup == null || roleSetup.Count == 0) return false;

        var validPlayers = pm.players.Where(p => p != null && p.isAlive).ToList();
        validPlayers.Sort((a, b) => a.playerID.CompareTo(b.playerID));

        if (validPlayers.Count == 0) return false;

        // Tạo pool vai trò chính xác theo cấu hình Host
        List<RoleType> rolePool = new List<RoleType>();
        foreach (var data in roleSetup)
        {
            if (data.Amount <= 0) continue;
            for (int i = 0; i < data.Amount; i++)
            {
                rolePool.Add(data.Role);
            }
        }

        // Đảm bảo sĩ số vai trò khớp với số người chơi:
        // Nếu Host chọn ít hơn số người chơi thực tế -> bổ sung Dân làng (Villager), TUYỆT ĐỐI không tự ý sinh vai trò đặc biệt như Shaman!
        while (rolePool.Count < validPlayers.Count)
        {
            rolePool.Add(RoleType.Villager);
        }
        // Nếu rolePool nhiều hơn số người chơi thực tế -> tỉa bớt Dân làng trước
        while (rolePool.Count > validPlayers.Count)
        {
            int villagerIndex = rolePool.LastIndexOf(RoleType.Villager);
            if (villagerIndex >= 0)
            {
                rolePool.RemoveAt(villagerIndex);
            }
            else
            {
                rolePool.RemoveAt(rolePool.Count - 1);
            }
        }

        // Shuffle ngẫu nhiên người nhận vai trò bằng thuật toán Fisher-Yates
        var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        for (int i = rolePool.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            RoleType temp = rolePool[i];
            rolePool[i] = rolePool[j];
            rolePool[j] = temp;
        }

        // Xóa danh sách role cũ trong RoleManager để gán danh sách mới
        rm.playerRoles ??= new Dictionary<int, BaseRole>();
        rm.playerRoles.Clear();
        confirmedAssignedRoles.Clear();

        // Gán từng vai trò theo cấu hình Host cho từng người chơi bằng API công khai
        for (int i = 0; i < validPlayers.Count; i++)
        {
            PlayerData player = validPlayers[i];
            RoleType roleType = rolePool[i];

            BaseRole role = RoleCatalog.Create(roleType, player);
            if (role == null)
            {
                Debug.LogError($"[ThuongRoleConfig] Không thể tạo BaseRole cho {roleType} (Player {player.playerID})");
                continue;
            }

            player.roleType = roleType;
            player.faction = role.faction;
            player.votePower = 1;
            player.hasUseNightAction = false;
            player.hasVoted = false;
            player.isAlive = true;
            player.wasVotedOut = false;
            player.isWhiteHoundAwakened = false;
            player.hasHunterTrap = false;
            player.hasUsedHunterShot = false;
            player.hunterTargetID = -1;
            player.loverID = -1;
            player.status = new PlayerStatus();
            player.effects ??= new List<StatusEffectInstance>();
            player.effects.Clear();

            if (rm.roleDefinitions != null)
            {
                player.roleDefinition = rm.roleDefinitions.Find(roleType);
            }
            player.NotifyChanged();

            rm.playerRoles[player.playerID] = role;
            role.OnGameStart();

            // Lưu vào danh sách xác nhận để khóa vai trò chống ghi đè
            confirmedAssignedRoles[player.playerID] = roleType;
            player.Changed -= HandlePlayerDataChanged;
            player.Changed += HandlePlayerDataChanged;

            Debug.Log($"[ThuongRoleConfig] ĐÃ GÁN VAI TRÒ CHÍNH THỨC: Player {player.playerID} ({player.playerName}) -> {roleType} ({role.faction})");
        }

        // Thông báo đồng bộ mạng qua ThuongOnlineSession tới các clients
        WinConditionManager.Instance?.NotifyMatchStateChanged();

        Debug.Log($"[ThuongRoleConfig] Hoàn tất phân vai tùy chỉnh cho {validPlayers.Count} người chơi theo đúng 100% cấu hình Host!");
        return true;
    }

    // ==========================================
    // RESET KHI RỜI PHÒNG / TẠO PHÒNG MỚI (TC11, TC12)
    // ==========================================

    public void ResetCaches()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null) p.Changed -= HandlePlayerDataChanged;
            }
        }

        isCustomized = false;
        configRevision = 0;
        injectedLobbyButton = null;
        roleGuardOverlay = null;
        interceptedRoleButton = null;
        lastObservedPlayerCount = 0;
        confirmedAssignedRoles.Clear();
        loggedOverwrittenPlayers.Clear();
        lastObservedRolesAssigned = false;
        NetworkGameRoleSetup.Clear();
        InitializeDefaults(GetCurrentPlayerCount());
        ThuongOnlineRoleConfigDialog.HideDialog();
        Debug.Log("[ThuongRoleConfig] TC11 - Đã reset toàn bộ dữ liệu cấu hình vai trò của phòng cũ.");
    }
}
