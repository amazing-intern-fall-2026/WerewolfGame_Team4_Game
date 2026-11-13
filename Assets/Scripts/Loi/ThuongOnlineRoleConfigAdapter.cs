using System;
using System.Collections.Generic;
using System.Linq;
using Thuong.Online;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Owns the lobby's role quantities, never the per-player assignment. The session
// validates this configuration and RoleManager shuffles recipients once per round.
public class ThuongOnlineRoleConfigAdapter : MonoBehaviour
{
    public static ThuongOnlineRoleConfigAdapter Instance { get; private set; }
    public const string RoleConfigSyncMessage = "loi.online.role_config_sync.v2";
    public const string RoleConfigRequestMessage = "loi.online.role_config_request.v2";
    private readonly Dictionary<RoleType, int> activeRoleAmounts = new();
    private readonly Dictionary<ulong, float> requestTimes = new();
    private bool isCustomized;
    public bool IsCustomized => isCustomized;
    private uint configRevision;
    private NetworkManager registeredNetwork;
    private CustomMessagingManager registeredMessaging;
    private GameObject injectedLobbyButton;
    private int lastObservedPlayerCount = -1;
    private ThuongMatchMode observedMode;
    private ThuongRolePreset observedPreset;

    public bool CanEdit => (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer) &&
        (ThuongOnlineSession.Active == null || ThuongOnlineSession.Active.CanConfigureRolePool);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<ThuongOnlineRoleConfigAdapter>();
        if (existing != null) { Instance = existing; return; }
        Instance = new GameObject("[ThuongOnlineRoleConfigAdapter]").AddComponent<ThuongOnlineRoleConfigAdapter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
        ThuongOnlineWinAdapter.EnsureInstance();
        InitializeDefaults(GetCurrentPlayerCount());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterNetwork();
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        injectedLobbyButton = null;
        ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
        ThuongOnlineWinAdapter.EnsureInstance();
        if (scene.name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0 ||
            scene.name.IndexOf("Offline", StringComparison.OrdinalIgnoreCase) >= 0)
        { ResetCaches(); return; }
        ImportSavedConfiguration();
    }

    private void Update()
    {
        EnsureNetworkRegistration();
        ImportSavedConfiguration();
        var rules = ThuongOnlineSession.Active?.RoomRules ?? new ThuongRoomRules();
        int count = GetCurrentPlayerCount();
        if (count != lastObservedPlayerCount || rules.mode != observedMode || rules.preset != observedPreset)
        {
            lastObservedPlayerCount = count; observedMode = rules.mode; observedPreset = rules.preset;
            if (!isCustomized && (registeredNetwork == null || registeredNetwork.IsServer))
            { InitializeDefaults(count); BroadcastConfigToClients(); }
        }
        CheckLobbyUIInjection();
    }

    public static Dictionary<RoleType, int> DefaultRolesForRoom(int count, ThuongRoomRules rules)
    {
        count = Mathf.Clamp(count, 1, 25);
        var pool = RolePoolBuilder.Build(count, 0, rules.AllowedRoles);
        int cap = Mathf.Max(1, (count - 1) / 2);
        int monsters = pool.Count(role => role == RoleType.DogSpirit || Array.IndexOf(RoleCatalog.MonsterSpecials, role) >= 0);
        for (int i = 0; i < pool.Count && monsters > cap; i++)
            if (Array.IndexOf(RoleCatalog.MonsterSpecials, pool[i]) >= 0)
            { pool[i] = RoleType.Villager; monsters--; }
        return RoleCatalog.AllConcreteRoles.ToDictionary(role => role, role => pool.Count(value => value == role));
    }

    public static Dictionary<RoleType, int> GetDefaultRolesForPlayerCount(int count) => DefaultRolesForRoom(count, new ThuongRoomRules());
    public void InitializeDefaults(int count)
    {
        var defaults = DefaultRolesForRoom(count, ThuongOnlineSession.Active?.RoomRules ?? new ThuongRoomRules());
        activeRoleAmounts.Clear();
        foreach (var entry in defaults) activeRoleAmounts[entry.Key] = entry.Value;
        // Preview defaults must not mark NetworkGameRoleSetup as host-confirmed.
    }

    private void ImportSavedConfiguration()
    {
        if (isCustomized || !NetworkGameRoleSetup.HasSetup()) return;
        activeRoleAmounts.Clear();
        foreach (var role in RoleCatalog.AllConcreteRoles) activeRoleAmounts[role] = 0;
        foreach (var entry in NetworkGameRoleSetup.GetRoles()) activeRoleAmounts[entry.Role] = entry.Amount;
        isCustomized = true;
    }

    public int GetCurrentPlayerCount()
    {
        if (ThuongOnlineSession.Active != null && ThuongOnlineSession.Active.ConnectedPlayerCount > 0)
            return ThuongOnlineSession.Active.ConnectedPlayerCount;
        var net = NetworkManager.Singleton;
        if (net != null && net.IsListening && net.ConnectedClientsIds.Count > 0) return net.ConnectedClientsIds.Count;
        if (PlayerManager.Instance?.players?.Count > 0) return PlayerManager.Instance.players.Count(player => player != null && !player.isDisconnected);
        return 2;
    }

    public int GetRoleAmount(RoleType role) => activeRoleAmounts.TryGetValue(role, out int amount) ? amount : 0;
    public int GetTotalRoleAmount() => activeRoleAmounts.Values.Sum();
    public Dictionary<RoleType, int> GetCurrentConfig() => new(activeRoleAmounts);
    public bool IsRoleSupported(RoleType role) => Array.IndexOf(RoleCatalog.AllConcreteRoles, role) >= 0 &&
        (ThuongOnlineSession.Active == null || Array.IndexOf(ThuongOnlineSession.Active.ConfigurableRoleTypes, role) >= 0);

    public bool ValidateConfiguration(IDictionary<RoleType, int> config, out string error)
    {
        if (config == null) { error = "Thiếu cấu hình vai trò."; return false; }
        return RolePoolBuilder.TryBuildConfigured(GetCurrentPlayerCount(),
            config.Select(entry => new RoleAmountData(entry.Key, entry.Value)),
            ThuongOnlineSession.Active?.ConfigurableRoleTypes ?? RoleCatalog.AllConcreteRoles, out _, out error);
    }

    public void HostSetRoleAmount(RoleType role, int amount)
    {
        if (!CanEdit || !IsRoleSupported(role)) return;
        activeRoleAmounts[role] = Mathf.Clamp(amount, 0, 25); isCustomized = true;
        SaveToNetworkGameRoleSetup(); BroadcastConfigToClients();
    }

    public void HostSaveConfig(Dictionary<RoleType, int> config)
    {
        if (!CanEdit) return;
        if (!ValidateConfiguration(config, out string error)) { Debug.LogWarning(error); return; }
        activeRoleAmounts.Clear();
        foreach (var role in RoleCatalog.AllConcreteRoles) activeRoleAmounts[role] = config.TryGetValue(role, out int amount) ? amount : 0;
        isCustomized = true;
        SaveToNetworkGameRoleSetup(); BroadcastConfigToClients();
    }

    public void HostRestoreDefaultConfig()
    {
        if (!CanEdit) return;
        isCustomized = false; NetworkGameRoleSetup.Clear();
        InitializeDefaults(GetCurrentPlayerCount()); BroadcastConfigToClients();
    }

    private void SaveToNetworkGameRoleSetup() => NetworkGameRoleSetup.Save(activeRoleAmounts
        .Where(entry => entry.Value > 0).Select(entry => new RoleAmountData(entry.Key, entry.Value)).ToList());

    private void EnsureNetworkRegistration()
    {
        var net = NetworkManager.Singleton;
        var messaging = net != null && net.IsListening ? net.CustomMessagingManager : null;
        if (net == registeredNetwork && messaging == registeredMessaging) return;
        UnregisterNetwork();
        if (net == null || messaging == null) return;
        registeredNetwork = net; registeredMessaging = messaging;
        net.OnClientConnectedCallback += HandleClientConnected;
        messaging.RegisterNamedMessageHandler(RoleConfigSyncMessage, HandleReceiveRoleConfigSync);
        messaging.RegisterNamedMessageHandler(RoleConfigRequestMessage, HandleReceiveRoleConfigRequest);
        if (net.IsServer) BroadcastConfigToClients();
        else { using var writer = new FastBufferWriter(1, Allocator.Temp); messaging.SendNamedMessage(RoleConfigRequestMessage, NetworkManager.ServerClientId, writer); }
    }

    private void UnregisterNetwork()
    {
        if (registeredNetwork != null) registeredNetwork.OnClientConnectedCallback -= HandleClientConnected;
        registeredMessaging?.UnregisterNamedMessageHandler(RoleConfigSyncMessage);
        registeredMessaging?.UnregisterNamedMessageHandler(RoleConfigRequestMessage);
        registeredNetwork = null; registeredMessaging = null; requestTimes.Clear();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (registeredNetwork != null && registeredNetwork.IsServer && clientId != registeredNetwork.LocalClientId) SendConfigToClient(clientId);
    }

    public void BroadcastConfigToClients()
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening || !net.IsServer || net.CustomMessagingManager == null) return;
        configRevision++;
        foreach (ulong clientId in net.ConnectedClientsIds)
            if (clientId != net.LocalClientId) SendConfigToClient(clientId);
    }

    public void SendConfigToClient(ulong clientId)
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsListening || !net.IsServer || net.CustomMessagingManager == null || !net.ConnectedClientsIds.Contains(clientId)) return;
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(configRevision); writer.WriteValueSafe(isCustomized);
        var entries = activeRoleAmounts.Where(entry => entry.Value > 0).ToArray();
        writer.WriteValueSafe((ushort)entries.Length);
        foreach (var entry in entries) { writer.WriteValueSafe((byte)entry.Key); writer.WriteValueSafe((ushort)entry.Value); }
        net.CustomMessagingManager.SendNamedMessage(RoleConfigSyncMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
    }

    private void HandleReceiveRoleConfigRequest(ulong sender, FastBufferReader reader)
    {
        if (registeredNetwork == null || !registeredNetwork.IsServer || sender == registeredNetwork.LocalClientId ||
            !registeredNetwork.ConnectedClientsIds.Contains(sender)) return;
        if (requestTimes.TryGetValue(sender, out float time) && Time.unscaledTime - time < .5f) return;
        requestTimes[sender] = Time.unscaledTime;
        SendConfigToClient(sender); // A request can only read the configuration.
    }

    private void HandleReceiveRoleConfigSync(ulong sender, FastBufferReader reader)
    {
        if (registeredNetwork == null || registeredNetwork.IsServer || sender != NetworkManager.ServerClientId) return;
        try
        {
            reader.ReadValueSafe(out uint revision); reader.ReadValueSafe(out bool customized); reader.ReadValueSafe(out ushort count);
            if (revision < configRevision || count > RoleCatalog.AllConcreteRoles.Length) return;
            var received = RoleCatalog.AllConcreteRoles.ToDictionary(role => role, _ => 0);
            var seen = new HashSet<RoleType>();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte role); reader.ReadValueSafe(out ushort amount);
                if (!received.ContainsKey((RoleType)role) || !seen.Add((RoleType)role) || amount > 25) return;
                received[(RoleType)role] = amount;
            }
            configRevision = revision; isCustomized = customized; activeRoleAmounts.Clear();
            foreach (var entry in received) activeRoleAmounts[entry.Key] = entry.Value;
            if (customized) SaveToNetworkGameRoleSetup(); else NetworkGameRoleSetup.Clear();
            if (ThuongOnlineRoleConfigDialog.IsShowing)
                ThuongOnlineRoleConfigDialog.Instance.UpdateSynchronizedConfig(activeRoleAmounts, GetCurrentPlayerCount());
        }
        catch (Exception error) when (error is OverflowException || error is ArgumentException)
        { Debug.LogWarning("Bỏ qua cấu hình vai trò không hợp lệ."); }
    }

    private void CheckLobbyUIInjection()
    {
        var card = GameObject.Find("Online Lobby Canvas")?.transform.Find("Lobby shade/Lobby Card");
        if (card == null) return;
        if (injectedLobbyButton == null)
        {
            injectedLobbyButton = new GameObject("Role config button", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = injectedLobbyButton.GetComponent<RectTransform>(); rect.SetParent(card, false);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = injectedLobbyButton.GetComponent<Image>(); image.color = new Color(.42f, .28f, .16f);
            ThuongMedievalTheme.Active?.StylePanel(image, "Role config button");
            var button = injectedLobbyButton.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(OpenRoleConfigUI);
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = label.GetComponent<RectTransform>(); textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(.04f, .04f); textRect.anchorMax = new Vector2(.96f, .96f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TMP_Text>(); text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 18;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.color = ThuongMedievalTheme.Cream; ThuongMedievalTheme.Active?.StyleText(text);
        }
        injectedLobbyButton.GetComponentInChildren<TMP_Text>().text = CanEdit ? "CHỈNH VAI TRÒ" : "XEM VAI TRÒ";
        // Preserve main's two-row layout without intercepting the session's buttons.
        PositionLobbyButton(card, "Map settings button", 0, true);
        PositionLobbyButton(card, "Room rules button", 1, true);
        PositionLobbyButton(card, "Role config button", 2, true);
        PositionLobbyButton(card, "New player guide", 3, true);
        PositionLobbyButton(card, "Lobby Ready", 0, false);
        PositionLobbyButton(card, "Lobby Role", 1, false);
        PositionLobbyButton(card, "Lobby Start", 2, false);
        PositionLobbyButton(card, "Leave lobby", 3, false);
    }

    private static void PositionLobbyButton(Transform card, string name, int column, bool upperRow)
    {
        var rect = card.Find(name) as RectTransform;
        if (rect == null) return;
        float x = .025f + column * .24f;
        rect.anchorMin = new Vector2(x, upperRow ? .075f : .012f);
        rect.anchorMax = new Vector2(column == 3 ? .975f : x + .22f, upperRow ? .13f : .067f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.GetComponentInChildren<TMP_Text>();
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12; text.fontSizeMax = 18;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OpenRoleConfigUI() => ThuongOnlineRoleConfigDialog.ShowDialog(CanEdit, GetCurrentConfig(), GetCurrentPlayerCount(), HostSaveConfig);

    public static bool DoesCurrentPlayersMatchHostConfig()
    {
        var players = PlayerManager.Instance?.players;
        var roles = RoleManager.Instance?.playerRoles;
        if (!NetworkGameRoleSetup.HasSetup() || players == null || roles == null ||
            !RolePoolBuilder.TryBuildConfigured(players.Count, NetworkGameRoleSetup.GetRoles(),
                RoleCatalog.AllConcreteRoles, out var pool, out _)) return false;
        return roles.Count == players.Count && players.All(player => player != null &&
            roles.TryGetValue(player.playerID, out var role) && role.owner == player && role.roleType == player.roleType) &&
            RoleCatalog.AllConcreteRoles.All(role => players.Count(player => player.roleType == role) == pool.Count(value => value == role));
    }

    // Compatibility for callers outside the online session. Online assignment has
    // a single owner and must never be repeated by a phase callback or coroutine.
    public static bool ApplyCustomRolesIfConfigured(int? seed = null)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer) return false;
        if (ThuongOnlineSession.Active != null || !NetworkGameRoleSetup.HasSetup() ||
            PlayerManager.Instance?.players == null || RoleManager.Instance == null) return false;
        var manager = RoleManager.Instance;
        if (!RolePoolBuilder.TryBuildConfigured(PlayerManager.Instance.players.Count, NetworkGameRoleSetup.GetRoles(),
            RoleCatalog.AllConcreteRoles, out var pool, out _)) return false;
        ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
        var previous = manager.exactRolePoolOverride;
        try { manager.exactRolePoolOverride = pool; manager.AssignRole(seed ?? UnityEngine.Random.Range(0, int.MaxValue)); }
        finally
        {
            manager.exactRolePoolOverride = previous;
            ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
        }
        return manager.playerRoles.Count == pool.Count;
    }

    public void ResetCaches()
    {
        isCustomized = false; configRevision = 0; lastObservedPlayerCount = -1;
        NetworkGameRoleSetup.Clear(); InitializeDefaults(GetCurrentPlayerCount());
        ThuongOnlineRoleConfigDialog.HideDialog();
    }
}
