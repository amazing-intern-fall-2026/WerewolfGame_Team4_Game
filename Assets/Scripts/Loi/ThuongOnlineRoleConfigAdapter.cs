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
/// Adapter tích hợp chức năng chỉnh vai trò và số lượng vai trò cho Thương Online.
///   - Không sửa code của Thương (0 diff trong Assets/Scripts/Thuong/).
///   - Tự động bootstrap runtime.
///   - Injected nút "CHỈNH VAI TRÒ" (Host) hoặc "XEM VAI TRÒ" (Client) vào Lobby của Thương.
///   - Đồng bộ cấu hình qua CustomMessagingManager:
///       * Host đổi cấu hình -> Server lưu -> Broadcast tới toàn bộ Client.
///       * Client chỉ xem; Server từ chối mọi yêu cầu chỉnh sửa từ Client.
///       * Client mới kết nối tự động nhận cấu hình mới nhất từ Host.
///   - Phân vai: Can thiệp an toàn vào sự kiện PhaseChanged (Setup -> RoleReveal) để gán đúng số lượng vai trò đã cấu hình.
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
    private GameObject injectedLobbyButton;
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
        SubscribeGameplayEvents();
    }

    private void Update()
    {
        EnsureNetworkRegistration();
        CheckLobbyUIInjection();
        MonitorPlayerCountChanges();
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
        interceptedRoleButton = null;
        string activeScene = SceneManager.GetActiveScene().name;

        // Nếu về MainMenu hoặc Offline hoặc NetworkManager tắt -> Reset
        if (activeScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            activeScene.IndexOf("Offline", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
        {
            ResetCaches();
            return;
        }

        // Adapter này chỉ phục vụ OnlineSceneGamePlay của Thương, không can thiệp vào scene Game của Lợi
        if (activeScene != "OnlineSceneGamePlay")
        {
            return;
        }

        SubscribeGameplayEvents();
        // Tuyệt đối không ghi đè nếu NetworkGameRoleSetup đã có cấu hình từ Lobby
        if (!isCustomized && !NetworkGameRoleSetup.HasSetup())
        {
            InitializeDefaults(GetCurrentPlayerCount());
        }
    }

    // ==========================================
    // DEFAULTS & GETTERS
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
        if (ThuongOnlineSession.Active != null)
        {
            var serverPlayersField = typeof(ThuongOnlineSession).GetField("serverPlayers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (serverPlayersField != null)
            {
                var dict = serverPlayersField.GetValue(ThuongOnlineSession.Active) as System.Collections.IDictionary;
                if (dict != null && dict.Count > 0)
                {
                    return dict.Count;
                }
            }

            var lobbyField = typeof(ThuongOnlineSession).GetField("lobby",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (lobbyField != null)
            {
                var list = lobbyField.GetValue(ThuongOnlineSession.Active) as System.Collections.ICollection;
                if (list != null && list.Count > 0)
                {
                    return list.Count;
                }
            }
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int count = NetworkManager.Singleton.ConnectedClientsIds.Count;
            if (count > 0) return count;
        }

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
    // LOBBY UI INJECTION
    // ==========================================

    private void CheckLobbyUIInjection()
    {
        // Tìm Online Lobby Canvas trong OnlineSceneGamePlay
        var lobbyCanvasGo = GameObject.Find("Online Lobby Canvas");
        if (lobbyCanvasGo == null) return;

        var card = lobbyCanvasGo.transform.Find("Lobby shade/Lobby Card");
        if (card == null) return;

        // Hook nút "PHÂN VAI" (Lobby Role) để kiểm soát điều kiện bắt đầu
        HookLobbyRoleButton(card);

        if (injectedLobbyButton != null) return;

        // Tìm 2 nút bên cạnh để căn chỉnh vị trí hài hòa
        var mapBtn = card.Find("Map settings button")?.GetComponent<RectTransform>();
        var rulesBtn = card.Find("Room rules button")?.GetComponent<RectTransform>();

        if (mapBtn != null && rulesBtn != null)
        {
            // Điều chỉnh vị trí 2 nút cũ để dành khoảng trống cho nút VAI TRÒ
            mapBtn.anchorMin = new Vector2(0.025f, 0.035f);
            mapBtn.anchorMax = new Vector2(0.098f, 0.12f);

            rulesBtn.anchorMin = new Vector2(0.104f, 0.035f);
            rulesBtn.anchorMax = new Vector2(0.179f, 0.12f);
        }

        // Tạo nút VAI TRÒ ngay cạnh LUẬT VÁN
        bool isHost = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        string buttonLabel = isHost ? "CHỈNH VAI TRÒ" : "XEM VAI TRÒ";

        injectedLobbyButton = CreateLobbyButton("Role config button", card, buttonLabel,
            new Vector2(0.185f, 0.035f), new Vector2(0.265f, 0.12f), OpenRoleConfigUI);
    }

    private void HookLobbyRoleButton(Transform card)
    {
        var roleBtnTransform = card.Find("Lobby Role");
        if (roleBtnTransform == null) return;

        var roleBtn = roleBtnTransform.GetComponent<Button>();
        if (roleBtn == null || interceptedRoleButton == roleBtn) return;

        interceptedRoleButton = roleBtn;
        roleBtn.onClick.RemoveAllListeners();
        roleBtn.onClick.AddListener(HandleLobbyRoleButtonClick);
    }

    private void HandleLobbyRoleButtonClick()
    {
        var session = ThuongOnlineSession.Active;
        if (session == null) return;

        // Nếu đã phân vai rồi -> mở xem vai trò của bản thân
        if (session.RolesAssigned)
        {
            session.ViewLocalRole();
            return;
        }

        // Nếu chưa phân vai -> Chỉ Server/Host mới có quyền bấm phân vai
        bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServer)
        {
            Debug.LogWarning("[ThuongRoleConfig] Chỉ Host mới có quyền bấm Phân Vai!");
            return;
        }

        int totalRoles = GetTotalRoleAmount();
        int playerCount = GetCurrentPlayerCount();

        // Kiểm tra điều kiện: Tổng vai trò phải khớp với số người chơi
        if (isCustomized && totalRoles != playerCount)
        {
            Debug.LogWarning($"[ThuongRoleConfig] Số vai trò đã chọn ({totalRoles}) không khớp với số người chơi ({playerCount})! Không thể phân vai.");
            OpenRoleConfigUI();
            return;
        }

        // Gọi method AssignLobbyRolesOnServer() của Thương qua reflection
        var assignMethod = typeof(ThuongOnlineSession).GetMethod(
            "AssignLobbyRolesOnServer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
        );

        if (assignMethod != null)
        {
            assignMethod.Invoke(session, null);
        }
        else
        {
            Debug.LogError("[ThuongRoleConfig] Không tìm thấy method AssignLobbyRolesOnServer trên ThuongOnlineSession!");
        }
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
        img.color = new Color(0.42f, 0.28f, 0.16f, 1f);
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
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = 18;
        tmp.enableAutoSizing = true;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        tmp.color = ThuongMedievalTheme.Cream;
        ThuongMedievalTheme.Active?.StyleText(tmp);

        return go;
    }

    // ==========================================
    // PHÂN VAI (ROLE ASSIGNMENT OVERRIDE HOOK)
    // ==========================================

    private void SubscribeGameplayEvents()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= HandlePhaseChanged;
            GameRoleManager.Instance.PhaseChanged += HandlePhaseChanged;
        }
    }

    private void UnsubscribeGameplayEvents()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(GamePhase previous, GamePhase next)
    {
        // Khi game bắt đầu từ Setup chuyển sang RoleReveal
        if (previous == GamePhase.Setup && next == GamePhase.RoleReveal)
        {
            bool isServer = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
            if (isServer)
            {
                ApplyCustomRolesIfConfigured();
            }
        }
    }

    /// <summary>
    /// Gán vai trò theo đúng cấu hình đã lưu của Host.
    /// Thực hiện bảo mật: Mỗi người chơi nhận đúng vai trò, đồng bộ qua hệ thống private state của Thương.
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

        int totalRoles = 0;
        List<RoleType> rolePool = new List<RoleType>();
        foreach (var data in roleSetup)
        {
            if (data.Amount <= 0) continue;
            totalRoles += data.Amount;
            for (int i = 0; i < data.Amount; i++)
            {
                rolePool.Add(data.Role);
            }
        }

        var validPlayers = pm.players.Where(p => p != null && p.isAlive).ToList();
        validPlayers.Sort((a, b) => a.playerID.CompareTo(b.playerID));

        if (totalRoles != validPlayers.Count)
        {
            Debug.LogWarning($"[ThuongRoleConfig] Số vai trò cấu hình ({totalRoles}) khác số người chơi thực tế ({validPlayers.Count}). Bỏ qua override vai trò tùy chỉnh.");
            return false;
        }

        // Shuffle ngẫu nhiên danh sách vai trò
        var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        for (int i = rolePool.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            RoleType temp = rolePool[i];
            rolePool[i] = rolePool[j];
            rolePool[j] = temp;
        }

        // Clear danh sách role cũ trong RoleManager
        rm.playerRoles ??= new Dictionary<int, BaseRole>();
        rm.playerRoles.Clear();

        var expectedAssignments = new Dictionary<int, RoleType>();

        // Gán từng vai trò theo cấu hình cho từng người chơi
        for (int i = 0; i < validPlayers.Count; i++)
        {
            PlayerData player = validPlayers[i];
            RoleType roleType = rolePool[i];
            expectedAssignments[player.playerID] = roleType;

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

            Debug.Log($"[ThuongRoleConfig] Đã gán vai trò tùy chỉnh: Player {player.playerID} ({player.playerName}) -> {roleType} ({role.faction})");
        }

        // Lên lịch xác thực và khôi phục để bảo đảm cấu hình Host không bị can thiệp
        if (instance != null)
        {
            instance.StartCoroutine(instance.PostAssignVerificationRoutine(expectedAssignments));
        }

        Debug.Log($"[ThuongRoleConfig] Hoàn tất phân vai tùy chỉnh cho {validPlayers.Count} người chơi theo đúng cấu hình Host!");
        return true;
    }

    private System.Collections.IEnumerator PostAssignVerificationRoutine(
        Dictionary<int, RoleType> expectedAssignments)
    {
        yield return null;

        // 1. Kiểm tra và khôi phục nếu có vai trò nào bị thay đổi ngoài ý muốn
        if (expectedAssignments != null && PlayerManager.Instance != null && RoleManager.Instance != null)
        {
            foreach (var kvp in expectedAssignments)
            {
                int pId = kvp.Key;
                RoleType expRole = kvp.Value;
                PlayerData p = PlayerManager.Instance.GetplayerByID(pId);
                if (p != null && (p.roleType != expRole || !RoleManager.Instance.playerRoles.TryGetValue(pId, out BaseRole curRole) || curRole == null || curRole.roleType != expRole))
                {
                    Debug.LogWarning($"[ThuongRoleConfig] Phát hiện vai trò của Player {pId} bị thay đổi thành {(p != null ? p.roleType.ToString() : "null")}. Đang khôi phục về {expRole}...");
                    BaseRole restored = RoleCatalog.Create(expRole, p);
                    if (restored != null)
                    {
                        p.roleType = expRole;
                        p.faction = restored.faction;
                        if (RoleManager.Instance.roleDefinitions != null)
                        {
                            p.roleDefinition = RoleManager.Instance.roleDefinitions.Find(expRole);
                        }
                        p.NotifyChanged();
                        RoleManager.Instance.playerRoles[pId] = restored;
                        restored.OnGameStart();
                    }
                }
            }
        }

        // 2. Đồng bộ các NetworkRoleSync nếu có trong scene
        NetworkRoleSync.SyncAllRoleSyncs();

        // 3. Xác thực và broadcast lại trong ThuongOnlineSession nếu đang chơi Thương Online
        if (ThuongOnlineSession.Active != null)
        {
            var session = ThuongOnlineSession.Active;

            var sendPrivate = typeof(ThuongOnlineSession).GetMethod("SendAllPrivateStates",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            sendPrivate?.Invoke(session, null);

            var broadcastRoster = typeof(ThuongOnlineSession).GetMethod("BroadcastRoster",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            broadcastRoster?.Invoke(session, null);

            var broadcastSnapshot = typeof(ThuongOnlineSession).GetMethod("BroadcastSnapshot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            broadcastSnapshot?.Invoke(session, null);

            Debug.Log("[ThuongRoleConfig] Đã xác thực và đồng bộ toàn bộ vai trò tùy chỉnh sau phân vai!");
        }
    }

    // ==========================================
    // RESET KHI RỜI PHÒNG / TẠO PHÒNG MỚI (TC11, TC12)
    // ==========================================

    public void ResetCaches()
    {
        isCustomized = false;
        configRevision = 0;
        injectedLobbyButton = null;
        lastObservedPlayerCount = 0;
        NetworkGameRoleSetup.Clear();
        InitializeDefaults(GetCurrentPlayerCount());
        ThuongOnlineRoleConfigDialog.HideDialog();
        Debug.Log("[ThuongRoleConfig] TC11, TC12 - Đã reset toàn bộ dữ liệu cấu hình vai trò của phòng.");
    }
}

