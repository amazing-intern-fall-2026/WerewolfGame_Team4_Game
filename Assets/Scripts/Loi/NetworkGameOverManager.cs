using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý trạng thái và hiển thị Game Over UI trên cả Host và Client:
///   - Tự động kích hoạt khi Game Over (từ GameRoleManager hoặc NetworkPhaseSync)
///   - Hiển thị UI overlay kết thúc ván: tên phe thắng, nút Chơi Lại (Host) và Về Lobby (Tất cả)
///   - Cung cấp OnGUI dự phòng để bảo đảm các nút luôn luôn xuất hiện trong mọi tình huống
/// </summary>
public class NetworkGameOverManager : MonoBehaviour
{
    private static NetworkGameOverManager _instance;

    public static NetworkGameOverManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<NetworkGameOverManager>();
                if (_instance == null)
                {
                    var go = new GameObject("NetworkGameOverManager");
                    _instance = go.AddComponent<NetworkGameOverManager>();
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        _ = Instance;
    }

    // =========================================================
    // STATE
    // =========================================================

    public bool isGameOver { get; private set; }
    public string currentWinner { get; private set; } = string.Empty;

    private bool uiShown;
    private GameObject gameOverPanel;
    private GameObject dedicatedCanvasGO;
    private NetworkPhaseSync cachedPhaseSync;
    private bool isRestarting;
    private float gameOverWaitTimer = 0f;
    private float shamanKillDelayTimer = 0f;
    public float ShamanKillDelayTimer => shamanKillDelayTimer;

    public void SetShamanKillDelay(float seconds = 5.0f)
    {
        shamanKillDelayTimer = Mathf.Max(shamanKillDelayTimer, seconds);
        Debug.Log($"[NetworkGameOverManager] Pháp Sư dùng Bùa Hại -> Chờ {shamanKillDelayTimer:F1}s trước khi kiểm tra điều kiện thắng.");
    }

    public static void TriggerShamanKillDelay(float seconds = 5.0f)
    {
        if (_instance != null)
        {
            _instance.SetShamanKillDelay(seconds);
        }
    }

    public void ResetUIState()
    {
        isGameOver = false;
        uiShown = false;
        isRestarting = false;
        currentWinner = string.Empty;
        gameOverWaitTimer = 0f;
        shamanKillDelayTimer = 0f;
        if (gameOverPanel != null)
        {
            Destroy(gameOverPanel);
            gameOverPanel = null;
        }
        if (dedicatedCanvasGO != null)
        {
            Destroy(dedicatedCanvasGO);
            dedicatedCanvasGO = null;
        }
    }

    // =========================================================
    // AWAKE / START / DESTROY
    // =========================================================

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureLoiMatchEvaluatorInstalled();
    }

    private void Start()
    {
        EnableDev2EliminationWin();
        EnsureLoiMatchEvaluatorInstalled();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        EnsureLoiMatchEvaluatorInstalled();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        UnsubscribeFromEvents();
    }

    // =========================================================
    // SUBSCRIBE / UNSUBSCRIBE EVENTS
    // =========================================================

    private void SubscribeToEvents()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= OnDev2PhaseChanged;
            GameRoleManager.Instance.PhaseChanged += OnDev2PhaseChanged;
        }

        if (cachedPhaseSync == null)
            cachedPhaseSync = FindFirstObjectByType<NetworkPhaseSync>();

        if (cachedPhaseSync != null)
        {
            cachedPhaseSync.CurrentPhase.OnValueChanged -= OnNetworkPhaseChanged;
            cachedPhaseSync.CurrentPhase.OnValueChanged += OnNetworkPhaseChanged;
            cachedPhaseSync.Winner.OnValueChanged -= OnNetworkWinnerChanged;
            cachedPhaseSync.Winner.OnValueChanged += OnNetworkWinnerChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= OnDev2PhaseChanged;
        }

        if (cachedPhaseSync != null)
        {
            cachedPhaseSync.CurrentPhase.OnValueChanged -= OnNetworkPhaseChanged;
            cachedPhaseSync.Winner.OnValueChanged -= OnNetworkWinnerChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"[NetworkGameOverManager] Client {clientId} đã ngắt kết nối.");
            NetworkGameManager.LeftPlayerClientIds.Add(clientId);
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.RemoveDisconnectedPlayer((int)clientId);
            }
        }
    }

    // =========================================================
    // EVENT HANDLERS
    // =========================================================

    private void OnDev2PhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        if (newPhase == GamePhase.GameOver)
        {
            string winner = GameRoleManager.Instance?.Winner;
            if (string.IsNullOrEmpty(winner)) winner = ResolveAllDeadWinner();
            TriggerGameOver(winner);
        }
    }

    private void OnNetworkPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        if (newPhase == GamePhase.GameOver)
        {
            string winner = string.Empty;
            if (cachedPhaseSync != null && cachedPhaseSync.Winner.Value.Length > 0)
                winner = cachedPhaseSync.Winner.Value.ToString();
            if (string.IsNullOrEmpty(winner) && GameRoleManager.Instance != null)
                winner = GameRoleManager.Instance.Winner;

            if (!string.IsNullOrEmpty(winner))
            {
                ShowGameOverUI(winner);
            }
            else
            {
                Debug.Log("[NetworkGameOverManager] Phase đổi sang GameOver nhưng Winner chưa nhận được, chờ RPC/NetworkVariable...");
            }
        }
    }

    private void OnNetworkWinnerChanged(Unity.Collections.FixedString64Bytes oldWinner, Unity.Collections.FixedString64Bytes newWinner)
    {
        string winner = newWinner.ToString();
        if (!string.IsNullOrEmpty(winner))
        {
            Debug.Log($"[NetworkGameOverManager] OnNetworkWinnerChanged: Winner = {winner}");
            ShowGameOverUI(winner);
        }
    }

    // =========================================================
    // UPDATE CHECK (Liên tục kiểm tra để không bao giờ bị miss UI)
    // =========================================================

    private void Update()
    {
        EnsureLoiMatchEvaluatorInstalled();

        if (shamanKillDelayTimer > 0f)
        {
            shamanKillDelayTimer -= Time.deltaTime;
        }

        // 1. Kiểm tra điều kiện thắng tức thời trên Server (VD: 1 Dân vs 1 DogSpirit -> DogSpirit thắng ngay)
        CheckServerWinCondition();

        if (uiShown)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // Kết nối cachedPhaseSync nếu chưa có
        if (cachedPhaseSync == null)
        {
            cachedPhaseSync = FindFirstObjectByType<NetworkPhaseSync>();
            if (cachedPhaseSync != null)
            {
                cachedPhaseSync.CurrentPhase.OnValueChanged -= OnNetworkPhaseChanged;
                cachedPhaseSync.CurrentPhase.OnValueChanged += OnNetworkPhaseChanged;
                cachedPhaseSync.Winner.OnValueChanged -= OnNetworkWinnerChanged;
                cachedPhaseSync.Winner.OnValueChanged += OnNetworkWinnerChanged;
            }
        }

        // Nếu bất kỳ hệ thống nào xác nhận đã Game Over, bật UI ngay
        if (GameEnded)
        {
            string winner = string.Empty;
            if (cachedPhaseSync != null && cachedPhaseSync.Winner.Value.Length > 0)
                winner = cachedPhaseSync.Winner.Value.ToString();
            if (string.IsNullOrEmpty(winner) && GameRoleManager.Instance != null)
                winner = GameRoleManager.Instance.Winner;
            if (string.IsNullOrEmpty(winner) && !string.IsNullOrEmpty(currentWinner))
                winner = currentWinner;

            if (!string.IsNullOrEmpty(winner))
            {
                ShowGameOverUI(winner);
            }
            else
            {
                gameOverWaitTimer += Time.deltaTime;
                bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
                if (isServer && gameOverWaitTimer >= 2.0f)
                {
                    string resolved = ResolveAllDeadWinner();
                    Debug.LogWarning($"[NetworkGameOverManager] Server timeout xác định Winner, tự động phân định: {resolved}");
                    TriggerGameOver(resolved);
                }
            }
        }
        else
        {
            gameOverWaitTimer = 0f;
        }
    }

    /// <summary>
    /// Kiểm tra điều kiện thắng của Server mạng theo luật hiện có:
    ///   - Chỉ có 3 kết quả: DÂN LÀNG CHIẾN THẮNG, MA SÓI CHIẾN THẮNG, PHE THỨ 3 CHIẾN THẮNG (Không có Hòa)
    ///   - Phe Quái vật (DogSpirit/Ma Sói): Thắng khi số lượng Quái vật >= số lượng Dân làng và phe khác (VD: 1 Dân vs 1 DogSpirit -> Ma Sói thắng)
    ///     hoặc hết số ngày tối đa (MaxDay) mà dân chưa diệt sạch sói
    ///   - Phe Dân làng: Thắng khi diệt sạch quái vật hoặc tiến độ nhiệm vụ đạt 100%
    ///   - Phe thứ 3: Kẻ Điên (bị vote ngày 1), Cáo Tinh (mê hoặc 2 người), Bạch Khuyển (thức tỉnh còn sống và quái vật sạch), Sát Nhân, Cặp đôi
    ///   - Khi tất cả cùng chết hoặc nhiều phe cùng bị tiêu diệt: Phân định theo đòn kết liễu cuối cùng (Thợ săn trả thù, treo cổ, sói cắn...)
    /// </summary>
    /// <summary>
    /// Kiểm tra xem phase hiện tại có đang trong thời gian đếm ngược (timer chưa hết) hay không.
    /// Nếu phase timer còn đang chạy (> 0), server không được tính điều kiện thắng / kết thúc ván
    /// để người chơi kịp hoàn tất các hành động của mình (cứu, giết, bẫy, thảo luận, vote...).
    /// </summary>
    public static bool IsPhaseTimerActive()
    {
        // 1. Kiểm tra NetworkGameTimer nếu có
        var netTimer = NetworkGameTimer.Instance ?? UnityEngine.Object.FindFirstObjectByType<NetworkGameTimer>();
        if (netTimer != null && netTimer.IsSpawned && netTimer.TimeRemaining != null)
        {
            if (netTimer.TimeRemaining.Value > 0.05f)
            {
                // Nếu có NetworkPhaseSync, chỉ coi là active nếu phase hiện tại là phase có đếm giờ
                if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.IsSpawned)
                {
                    var phase = NetworkPhaseSync.Instance.CurrentPhase.Value;
                    if (phase == GamePhase.Night || phase == GamePhase.DayStart || phase == GamePhase.Task ||
                        phase == GamePhase.Discussion || phase == GamePhase.Voting || phase == GamePhase.RoleReveal)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        // 2. Kiểm tra GameRoleManager và DayTimer
        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentState != GameState.GameOver)
        {
            // Nếu đang ở phase resolution thì không coi là active timer
            if (GameRoleManager.Instance.currentPhase == GamePhase.ResolveNight ||
                GameRoleManager.Instance.currentPhase == GamePhase.ResolveVote ||
                GameRoleManager.Instance.currentPhase == GamePhase.DayResolution ||
                GameRoleManager.Instance.currentPhase == GamePhase.GameOver)
            {
                return false;
            }

            if (GameRoleManager.Instance.currentState == GameState.Day)
            {
                if (DayTimer.Instance != null && DayTimer.Instance.TimeRemaining > 0.05f)
                    return true;
            }
            else
            {
                if (GameRoleManager.Instance.PhaseTimeRemaining > 0.05f)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem có Thợ Săn nào đã chết nhưng vẫn còn quyền bắn phát đạn trả thù chưa sử dụng hay không.
    /// Nếu có, hoãn việc kết thúc ván để Thợ Săn bắn xong rồi mới xác định số người chơi và cho ra kết quả.
    /// </summary>
    public static bool HasPendingHunterDeathShot()
    {
        EnsureLoiMatchEvaluatorInstalled();

        if (PlayerManager.Instance?.players == null) return false;
        foreach (var p in PlayerManager.Instance.players)
        {
            if (p == null) continue;
            if (p.roleType == RoleType.Hunter && !p.isAlive && p.hasHunterTrap && !p.hasUsedHunterShot)
            {
                // Nếu người chơi này đã rời phòng hoặc ngắt kết nối thì bỏ qua
                if (p.isDisconnected || NetworkGameManager.IsPlayerLeft((ulong)p.playerID))
                    continue;

                return true;
            }
        }
        return false;
    }

    public void CheckServerWinCondition()
    {
        EnsureLoiMatchEvaluatorInstalled();

        if (isGameOver || GameEnded) return;

        bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServer) return;

        // Nếu Pháp Sư vừa dùng Bùa Hại (Kill), tạm hoãn kiểm tra để các action đêm và người chơi kịp thấy kết quả
        if (shamanKillDelayTimer > 0f)
            return;

        // Không kiểm tra trong giai đoạn khởi tạo / phân vai
        if (GameRoleManager.Instance != null &&
            (GameRoleManager.Instance.currentPhase == GamePhase.Setup ||
             (GameRoleManager.Instance.currentPhase == GamePhase.RoleReveal &&
              GameRoleManager.Instance.currentState == GameState.RoleReveal)))
            return;

        if (NetworkPhaseSync.Instance != null &&
            (NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.Setup ||
             NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.RoleReveal))
            return;

        if (NetworkGameRoleSetup.HasSetup() && NetworkRoleActionBridge.Instance != null && !NetworkRoleActionBridge.Instance.LobbyRolesApplied)
            return;

        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null || PlayerManager.Instance.players.Count == 0)
            return;

        // Tiến độ nhiệm vụ đạt 100% -> Dân làng thắng ngay lập tức
        if (TaskManager.Instance != null && TaskManager.Instance.progress >= 100)
        {
            Debug.Log("[NetworkWinCondition] Tiến độ nhiệm vụ đạt 100% -> Dân làng thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.VillagerWin();
            TriggerGameOver("Villagers");
            return;
        }

        // Chưa hết time của phase thì không tính điều kiện thắng (chờ phase kết thúc để mọi vai trò hoàn tất action)
        if (IsPhaseTimerActive())
            return;

        // 1. Kiểm tra Kẻ Điên (Madman) bị xử tử ngày 1
        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentDay == 1 &&
            PlayerManager.Instance.players.Exists(p => p != null && p.roleType == RoleType.Madman && !p.isAlive && p.wasVotedOut))
        {
            Debug.Log("[NetworkWinCondition] Kẻ Điên bị xử tử ngày 1 -> Phe thứ 3 thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.MadmanWin();
            TriggerGameOver("Madman");
            return;
        }

        // 2. Kiểm tra Cáo Tinh (Fox Spirit) đã mê hoặc đủ 2 người
        if (WinConditionManager.Instance != null && WinConditionManager.Instance.FoxSpiritHasWon())
        {
            Debug.Log("[NetworkWinCondition] Cáo Tinh đã mê hoặc đủ 2 người -> Phe thứ 3 thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.FoxSpiritWin();
            TriggerGameOver("Fox Spirit");
            return;
        }

        // 3. Kiểm tra Đôi Tình Nhân là 2 người sống sót cuối cùng
        if (LoversAreLastTwoAlive())
        {
            Debug.Log("[NetworkWinCondition] Cặp đôi là 2 người sống sót cuối cùng -> Phe thứ 3 thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.LoversWin();
            TriggerGameOver("Lovers");
            return;
        }

        // Không kết thúc ván khi Thợ Săn đã chết nhưng vẫn còn phát đạn trả thù chưa dùng.
        // Phải để Thợ Săn hoàn thành chức năng bắn rồi mới xác định số người chơi và cho ra kết quả!
        if (HasPendingHunterDeathShot())
        {
            Debug.Log("[NetworkWinCondition] Thợ Săn đã chết nhưng chưa hoàn thành phát đạn bắn báo thù -> Tạm hoãn Game Over để Thợ Săn bắn!");
            return;
        }

        // Đảm bảo đồng bộ WhiteHound (khôi phục isAlive nếu bị cắn, thức tỉnh gia nhập DogSpirit nếu bị cắn hoặc DogSpirit chết)
        SynchronizeWhiteHoundState();

        int aliveDogSpirits = 0;
        int aliveWhiteHounds = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf)
            {
                aliveWhiteHounds++;
            }
            else if (player.roleType == RoleType.DogSpirit || player.roleType == RoleType.SerpentSpirit || player.roleType == RoleType.Ogre)
            {
                aliveDogSpirits++;
            }
            else if (RoleManager.Instance?.playerRoles != null &&
                     RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole r) &&
                     r != null && r.faction == FactionType.Monster)
            {
                aliveDogSpirits++;
            }
        }

        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveThirdParty = 0;
        int aliveKillers = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            bool isWhiteHound = player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf;

            bool roleIsMonster = RoleManager.Instance?.playerRoles != null &&
                                 RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole assignedRole) &&
                                 assignedRole != null && assignedRole.faction == FactionType.Monster;

            // Bạch Khuyển thức tỉnh nếu:
            // 1. Đã được đánh dấu thức tỉnh (isWhiteHoundAwakened)
            // 2. Thuộc phe Monster
            // 3. Đã bị DogSpirit cắn trong đêm
            // 4. Hoặc DogSpirit đã chết (aliveDogSpirits == 0): Bạch Khuyển tự động thức tỉnh thành Quái vật duy trì ván game!
            bool isAwakenedWhiteHound = isWhiteHound && (
                player.isWhiteHoundAwakened ||
                roleIsMonster ||
                player.faction == FactionType.Monster ||
                (NetworkRoleActionBridge.Instance != null && NetworkRoleActionBridge.Instance.HasWhiteHoundBeenBittenTonight(player.playerID)) ||
                aliveDogSpirits == 0
            );

            if (isWhiteHound && isAwakenedWhiteHound)
            {
                player.isWhiteHoundAwakened = true;
                player.faction = FactionType.Monster;
                player.roleType = RoleType.DogSpirit;
                if (RoleManager.Instance?.playerRoles != null &&
                    RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole whiteHoundRole) &&
                    whiteHoundRole is WhiteHound wh)
                {
                    wh.BecomeMonster();
                }
            }

            bool isMonster = isAwakenedWhiteHound ||
                             roleIsMonster ||
                             player.faction == FactionType.Monster ||
                             player.roleType == RoleType.DogSpirit ||
                             player.roleType == RoleType.SerpentSpirit ||
                             player.roleType == RoleType.Ogre;

            bool isThirdParty = !isMonster && !isWhiteHound && (
                                player.faction == FactionType.Neutral ||
                                player.roleType == RoleType.Madman ||
                                player.roleType == RoleType.Jester ||
                                player.roleType == RoleType.Lover ||
                                player.roleType == RoleType.FoxSpirit ||
                                player.roleType == RoleType.Piper ||
                                player.roleType == RoleType.Killer ||
                                player.roleType == RoleType.SerialKiller ||
                                player.roleType == RoleType.DeathHerald);

            if (isMonster)
            {
                aliveMonsters++;
            }
            else if (isThirdParty)
            {
                aliveThirdParty++;
                if (player.roleType == RoleType.Killer || player.roleType == RoleType.SerialKiller)
                    aliveKillers++;
            }
            else if (!isWhiteHound)
            {
                // Bạch Khuyển tuyệt đối KHÔNG BAO GIỜ được cộng vào aliveVillagers!
                aliveVillagers++;
            }
        }

        // 5. Phe thứ 3 (Sát Nhân / Cáo Tinh / Kẻ Điên...): thắng khi đã loại hết Quái vật (kể cả WhiteHound thức tỉnh) và Dân làng
        if (aliveThirdParty > 0 && aliveMonsters == 0 && aliveVillagers == 0)
        {
            if (aliveKillers > 0)
            {
                Debug.Log("[NetworkWinCondition] Sát Nhân là người sống sót cuối cùng -> Phe thứ 3 thắng!");
                if (GameRoleManager.Instance != null) GameRoleManager.Instance.KillerWin();
                TriggerGameOver("Killer");
            }
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive && (p.roleType == RoleType.FoxSpirit || p.roleType == RoleType.Piper)))
            {
                Debug.Log("[NetworkWinCondition] Cáo Tinh là người sống sót cuối cùng -> Phe thứ 3 thắng!");
                if (GameRoleManager.Instance != null) GameRoleManager.Instance.FoxSpiritWin();
                TriggerGameOver("Fox Spirit");
            }
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive && (p.roleType == RoleType.Madman || p.roleType == RoleType.Jester)))
            {
                Debug.Log("[NetworkWinCondition] Kẻ Điên là người sống sót cuối cùng -> Phe thứ 3 thắng!");
                if (GameRoleManager.Instance != null) GameRoleManager.Instance.MadmanWin();
                TriggerGameOver("Madman");
            }
            else
            {
                Debug.Log("[NetworkWinCondition] Phe thứ 3 là người sống sót cuối cùng -> Phe thứ 3 thắng!");
                if (GameRoleManager.Instance != null) GameRoleManager.Instance.KillerWin();
                TriggerGameOver("ThirdParty");
            }
            return;
        }

        // 6. Ma Sói (Phe DogSpirit + WhiteHound thức tỉnh) thắng khi số Monster >= số Dân làng & phe khác (VD: 1 Dân vs 1 Bạch Khuyển thức tỉnh -> Monster thắng!)
        if (aliveMonsters > 0 && aliveMonsters >= aliveVillagers + aliveThirdParty)
        {
            Debug.Log($"[NetworkWinCondition] Phe DogSpirit/Monster ({aliveMonsters}) >= Dân làng & phe khác ({aliveVillagers + aliveThirdParty}) -> Ma Sói thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.WerewolfWin();
            TriggerGameOver("Werewolves");
            return;
        }

        //    b. Hết số ngày tối đa (MaxDay) mà quái vật vẫn chưa bị diệt sạch:
        if (aliveMonsters > 0 && GameRoleManager.Instance != null && WinConditionManager.Instance != null &&
            GameRoleManager.Instance.currentDay >= WinConditionManager.Instance.MaxDay)
        {
            Debug.Log("[NetworkWinCondition] Hết thời gian tối đa mà Dân làng chưa diệt sạch sói -> Ma Sói thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.WerewolfWin();
            TriggerGameOver("Werewolves");
            return;
        }

        // 7. Dân Làng chỉ thắng khi đã diệt sạch cả Quái vật, Bạch Khuyển (WhiteHound) LẪN toàn bộ Phe thứ 3
        if (aliveMonsters == 0 && aliveWhiteHounds == 0 && aliveVillagers > 0 && aliveThirdParty == 0)
        {
            Debug.Log("[NetworkWinCondition] Quái vật, Bạch Khuyển và Phe thứ 3 đã bị tiêu diệt hết -> Dân làng thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.VillagerWin();
            TriggerGameOver("Villagers");
            return;
        }

        // 8. Trường hợp TẤT CẢ NGƯỜI CHƠI ĐỀU CHẾT hoặc nhiều phe cùng bị tiêu diệt:
        //    Phân định phe thắng theo luật hiện có, không bao giờ Hòa:
        if (aliveMonsters == 0 && aliveVillagers == 0 && aliveThirdParty == 0)
        {
            string winner = ResolveAllDeadWinner();
            Debug.Log($"[NetworkWinCondition] Tất cả người chơi đã chết -> Phân định phe thắng: {winner}");
            TriggerGameOver(winner);
            return;
        }
    }

    /// <summary>
    /// Phân định người thắng khi tất cả người chơi đều chết hoặc nhiều phe chết cùng lúc.
    /// Dựa vào nguyên nhân chết cuối cùng theo luật của game:
    /// - Nếu Thợ Săn bắn quái vật cuối cùng: Dân Làng thắng.
    /// - Nếu Quái vật bị treo cổ: Dân Làng thắng.
    /// - Nếu Dân làng bị sói cắn chết: Ma Sói thắng.
    /// - Nếu Sát nhân giết: Phe thứ 3 thắng.
    /// - Mặc định theo luật loại bỏ quái vật: Dân Làng thắng vì quái vật đã bị diệt sạch.
    /// </summary>
    private string ResolveAllDeadWinner()
    {
        if (PlayerManager.Instance?.players != null)
        {
            // 1. Kiểm tra Kẻ Điên bị vote ngày 1
            if (PlayerManager.Instance.players.Exists(p => p != null && p.roleType == RoleType.Madman && p.wasVotedOut))
                return "Madman";

            // 2. Kiểm tra Cáo Tinh
            if (WinConditionManager.Instance != null && WinConditionManager.Instance.FoxSpiritHasWon())
                return "Fox Spirit";

            // 3. Phân tích nguyên nhân chết cuối cùng
            bool monsterKilledByHunter = false;
            bool monsterKilledByVote = false;
            bool villagerKilledByWolf = false;
            bool villagerKilledByKiller = false;

            foreach (var p in PlayerManager.Instance.players)
            {
                if (p == null) continue;
                bool isMonster = p.faction == FactionType.Monster ||
                                 p.roleType == RoleType.DogSpirit ||
                                 p.roleType == RoleType.SerpentSpirit ||
                                 p.roleType == RoleType.Ogre ||
                                 p.roleType == RoleType.WhiteHound ||
                                 p.roleType == RoleType.WhiteWolf;

                if (isMonster)
                {
                    if (p.lastDeathCause == DeathCause.Trap) monsterKilledByHunter = true;
                    if (p.lastDeathCause == DeathCause.Vote || p.wasVotedOut) monsterKilledByVote = true;
                }
                else if (p.faction == FactionType.Villager)
                {
                    if (p.lastDeathCause == DeathCause.Monster) villagerKilledByWolf = true;
                    if (p.lastDeathCause == DeathCause.Killer) villagerKilledByKiller = true;
                }
            }

            // Nếu Thợ Săn dùng phát đạn cuối diệt quái vật cuối cùng, Dân Làng thắng vì đã diệt sạch Quái vật
            if (monsterKilledByHunter)
                return "Villagers";

            // Nếu Quái vật bị treo cổ ở ban ngày cuối cùng, Dân Làng thắng
            if (monsterKilledByVote && !villagerKilledByWolf)
                return "Villagers";

            // Nếu Sát Nhân là người ra đòn kết liễu
            if (villagerKilledByKiller && !villagerKilledByWolf)
                return "Killer";

            // Nếu Quái vật cắn chết dân làng cuối cùng
            if (villagerKilledByWolf)
                return "Werewolves";
        }

        // Theo luật loại bỏ quái vật của WinConditionManager: khi toàn bộ quái vật bị diệt, Dân Làng chiến thắng
        return "Villagers";
    }

    private bool LoversAreLastTwoAlive()
    {
        if (PlayerManager.Instance?.players == null) return false;
        PlayerData first = null, second = null;
        int count = 0;
        foreach (var p in PlayerManager.Instance.players)
        {
            if (p != null && p.isAlive)
            {
                count++;
                if (count > 2) return false;
                if (first == null) first = p; else second = p;
            }
        }
        return count == 2 && first != null && second != null &&
               first.loverID == second.playerID && second.loverID == first.playerID;
    }

    // =========================================================
    // TRIGGER GAME OVER
    // =========================================================

    private static void SetDev2GameOver(string winner)
    {
        var grm = GameRoleManager.Instance;
        if (grm == null) return;

        if (string.IsNullOrEmpty(winner))
            winner = "Villagers";

        if (WinConditionManager.Instance != null && WinConditionManager.Instance.HasMatchEvaluator(grm))
        {
            grm.FinishEvaluatedMatch(winner);
        }
        else
        {
            grm.FinishLocalMatch(winner);
        }

        if (grm.currentState != GameState.GameOver || grm.currentPhase != GamePhase.GameOver)
        {
            grm.currentState = GameState.GameOver;
            grm.currentPhase = GamePhase.GameOver;
        }
    }

    public void TriggerGameOver(string winner)
    {
        if (string.IsNullOrEmpty(winner))
        {
            winner = ResolveAllDeadWinner();
        }

        isGameOver = true;
        currentWinner = winner;

        // Cập nhật trạng thái GameRoleManager sang GameOver để NetworkPhaseSync không ghi đè ngược về phase cũ
        SetDev2GameOver(winner);

        Debug.Log($"GAME OVER MANAGER | TriggerGameOver! Winner: {winner} -> {FormatWinner(winner)}");

        // Chuyển NetworkGameState sang GameOver nếu là Server
        if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkGameManager.Instance.ChangeGameState(NetworkGameState.GameOver);
        }

        // Đồng bộ sang NetworkPhaseSync nếu là Server
        if (NetworkPhaseSync.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkPhaseSync.Instance.Winner.Value = new Unity.Collections.FixedString64Bytes(winner);
            if (NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.GameOver)
            {
                NetworkPhaseSync.Instance.CurrentPhase.Value = GamePhase.GameOver;
            }
            NetworkPhaseSync.Instance.NotifyGameOverClientRpc(winner);
        }

        ShowGameOverUI(winner);
    }

    // =========================================================
    // GAME OVER UI (Đồng bộ, tức thời, không trễ)
    // =========================================================

    public void ShowGameOverUI(string winner)
    {
        if (string.IsNullOrEmpty(winner))
        {
            Debug.Log("GAME OVER MANAGER | ShowGameOverUI được gọi với winner rỗng, đang đợi đồng bộ từ Server...");
            return;
        }

        if (uiShown && gameOverPanel != null)
        {
            // Nếu winner thay đổi và khác với hiện tại, cập nhật lại toàn bộ UI
            if (currentWinner != winner)
            {
                Debug.Log($"GAME OVER MANAGER | Cập nhật lại UI GameOver: '{currentWinner}' -> '{winner}'");
                currentWinner = winner;
                BuildGameOverUIDirect(winner);
            }
            return;
        }

        uiShown = true;
        isGameOver = true;
        currentWinner = winner;

        Debug.Log($"GAME OVER MANAGER | ShowGameOverUI. Winner: {winner} -> {FormatWinner(winner)}");

        // Mở chuột để người chơi có thể click nút ngay lập tức
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildGameOverUIDirect(winner);
    }

    private void BuildGameOverUIDirect(string winner)
    {
        // 1. Đảm bảo có EventSystem để nhận tương tác chuột
        var existingEventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingEventSystem == null)
        {
            var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }
        else
        {
            existingEventSystem.gameObject.SetActive(true);
            existingEventSystem.enabled = true;
        }

        // 2. Tạo Canvas overlay riêng biệt với sortingOrder tối đa (32767) để đảm bảo không bị đè
        if (dedicatedCanvasGO == null)
        {
            dedicatedCanvasGO = new GameObject("GameOverDedicatedCanvas");
            Canvas canvas = dedicatedCanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;

            var scaler = dedicatedCanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            dedicatedCanvasGO.AddComponent<GraphicRaycaster>();
        }

        // Xóa panel cũ nếu có
        if (gameOverPanel != null)
        {
            Destroy(gameOverPanel);
        }

        // --- Overlay tối toàn màn hình ---
        gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
        gameOverPanel.transform.SetParent(dedicatedCanvasGO.transform, false);

        RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = gameOverPanel.GetComponent<Image>();
        panelBg.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);

        // --- Card trung tâm ---
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(gameOverPanel.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(860, 540);

        Image cardBg = card.GetComponent<Image>();
        cardBg.color = new Color(0.12f, 0.12f, 0.22f, 1f);

        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.7f, 0.5f, 1f, 0.9f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // --- Tiêu đề ---
        CreateLabel(card.transform, "TitleLabel",
            "KẾT THÚC VÁN",
            new Vector2(0, 190), new Vector2(720, 60),
            42, new Color(0.95f, 0.8f, 1f));

        // --- Phe thắng ---
        CreateLabel(card.transform, "WinnerLabel",
            FormatWinner(winner),
            new Vector2(0, 95), new Vector2(760, 80),
            46, Color.white);

        // --- Mô tả phụ ---
        string sub = GetWinnerSubtext(winner);
        if (!string.IsNullOrEmpty(sub))
        {
            CreateLabel(card.transform, "SubLabel",
                sub,
                new Vector2(0, 0), new Vector2(740, 60),
                26, new Color(0.8f, 0.8f, 0.9f));
        }

        // --- Nút bấm ---
        bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        float btnY = -180f;

        if (isHost)
        {
            // Host thấy 3 nút: Chơi Lại, Về Lobby (cùng phòng), và Rời Phòng (về MainMenu)
            // Khoảng cách giữa các nút chính xác là 26px
            CreateButton(card.transform, "PlayAgainBtn",
                "CHƠI LẠI",
                new Vector2(-270f, btnY), new Vector2(244, 88),
                new Color(0.15f, 0.65f, 0.25f),
                OnPlayAgainClicked, 28);

            CreateButton(card.transform, "LobbyBtn",
                "VỀ LOBBY",
                new Vector2(0f, btnY), new Vector2(244, 88),
                new Color(0.20f, 0.45f, 0.75f),
                OnReturnToLobbyClicked, 28);

            CreateButton(card.transform, "LeaveBtn",
                "RỜI PHÒNG",
                new Vector2(270f, btnY), new Vector2(244, 88),
                new Color(0.65f, 0.20f, 0.25f),
                OnLeaveRoomClicked, 28);
        }
        else
        {
            // Client thấy 2 nút: Về Lobby (cùng phòng) và Rời Phòng (về MainMenu)
            // Khoảng cách giữa 2 nút chính xác là 28px
            CreateButton(card.transform, "LobbyBtn",
                "VỀ LOBBY",
                new Vector2(-184f, btnY), new Vector2(340, 88),
                new Color(0.20f, 0.45f, 0.75f),
                OnReturnToLobbyClicked, 28);

            CreateButton(card.transform, "LeaveBtn",
                "RỜI PHÒNG",
                new Vector2(184f, btnY), new Vector2(340, 88),
                new Color(0.65f, 0.20f, 0.25f),
                OnLeaveRoomClicked, 28);
        }
    }

    // =========================================================
    // DỰ PHÒNG OnGUI (Đảm bảo nút luôn luôn hiển thị 100%)
    // =========================================================

    private void OnGUI()
    {
        if (!isGameOver) return;

        // Nếu vì bất kỳ lý do gì gameOverPanel chưa hiện trên màn hình, OnGUI sẽ vẽ trực tiếp
        if (gameOverPanel == null)
        {
            GUI.depth = -1000;
            float w = 520;
            float h = 280;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Box(new Rect(x, y, w, h), "=== KẾT THÚC VÁN ===");

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x + 10, y + 40, w - 20, 40), FormatWinner(currentWinner), titleStyle);

            string sub = GetWinnerSubtext(currentWinner);
            if (!string.IsNullOrEmpty(sub))
            {
                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x + 10, y + 90, w - 20, 30), sub, subStyle);
            }

            bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
            if (isHost)
            {
                if (GUI.Button(new Rect(x + 15, y + 175, 145, 55), "CHƠI LẠI"))
                {
                    OnPlayAgainClicked();
                }

                if (GUI.Button(new Rect(x + 180, y + 175, 145, 55), "VỀ LOBBY"))
                {
                    OnReturnToLobbyClicked();
                }

                if (GUI.Button(new Rect(x + 345, y + 175, 145, 55), "RỜI PHÒNG"))
                {
                    OnLeaveRoomClicked();
                }
            }
            else
            {
                if (GUI.Button(new Rect(x + 65, y + 175, 185, 55), "VỀ LOBBY"))
                {
                    OnReturnToLobbyClicked();
                }

                if (GUI.Button(new Rect(x + 270, y + 175, 185, 55), "RỜI PHÒNG"))
                {
                    OnLeaveRoomClicked();
                }
            }
        }
    }

    // =========================================================
    // BUTTON HANDLERS
    // =========================================================

    private void OnPlayAgainClicked()
    {
        bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        if (!isHost)
        {
            Debug.LogWarning("[NetworkGameOverManager] Chỉ Host/Server mới có quyền khởi tạo ván mới.");
            return;
        }

        if (isRestarting)
        {
            Debug.LogWarning("[NetworkGameOverManager] Đang trong quá trình khởi tạo ván mới, bỏ qua thao tác click trùng.");
            return;
        }

        isRestarting = true;
        Debug.Log("GAME OVER | Host bấm Chơi lại. Bắt đầu quy trình khởi tạo ván mới...");

        StartNewMatchServer();
    }

    private void StartNewMatchServer()
    {
        try
        {
            // BƯỚC 1: Dừng timer và chặn action của ván cũ
            DayTimer.Instance?.StopTimer();

            // BƯỚC 2: Xóa dữ liệu action và kết quả tạm thời của ván cũ
            NetworkPlayerAction.ResetServerActionData();
            NightManager.Instance?.ResetForNewMatch();
            VoteManager.Instance?.StartVote();
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.progress = 0;
                TaskManager.Instance.currentTasks.Clear();
            }

            LoverManager.Instance?.ResetForNewMatch();

            // BƯỚC 3: Dọn dẹp kết nối mồ côi và xác định danh sách người chơi active thực tế
            var activeIds = new List<ulong>();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                // Ngắt kết nối dứt điểm các client đã rời phòng / văng game
                foreach (ulong leftId in NetworkGameManager.LeftPlayerClientIds.ToArray())
                {
                    if (NetworkManager.Singleton.ConnectedClients.ContainsKey(leftId))
                    {
                        Debug.Log($"[NetworkGameOverManager] Ngắt kết nối dứt điểm client {leftId} trước khi Chơi lại.");
                        NetworkManager.Singleton.DisconnectClient(leftId);
                    }
                }

                foreach (ulong cid in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (!NetworkGameManager.LeftPlayerClientIds.Contains(cid))
                    {
                        activeIds.Add(cid);
                    }
                }
            }

            // Thiết lập danh sách người chơi chính thức cho ván đấu
            NetworkGamePlayerSetup.SetActiveMatchClientIds(activeIds);

            // Tiêu hủy mọi PlayerController GameObject mồ côi trên Server
            PlayerController[] allControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in allControllers)
            {
                if (pc != null)
                {
                    ulong cid = pc.OwnerClientId;
                    if (!activeIds.Contains(cid) || NetworkGameManager.LeftPlayerClientIds.Contains(cid))
                    {
                        Debug.Log($"[NetworkGameOverManager] Tiêu hủy PlayerController GameObject mồ côi của client {cid}.");
                        var no = pc.GetComponent<NetworkObject>();
                        if (no != null && no.IsSpawned)
                        {
                            no.Despawn(true);
                        }
                        Destroy(pc.gameObject);
                    }
                }
            }

            // Cập nhật PlayerManager trên Server
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.disconnectedPlayers?.Clear();
                PlayerManager.Instance.PurgeNonActivePlayers();

                foreach (ulong cid in activeIds)
                {
                    if (PlayerManager.Instance.GetplayerByID((int)cid) == null)
                    {
                        string pName = NetworkGamePlayerNameSetup.GetName(cid);
                        PlayerManager.Instance.RegisterPlayer(new PlayerData
                        {
                            playerID = (int)cid,
                            playerName = pName,
                            isAlive = true,
                            votePower = 1,
                            hasVoted = false,
                            hasUseNightAction = false,
                            status = new PlayerStatus()
                        });
                    }
                }

                if (PlayerManager.Instance.players != null)
                {
                    foreach (var player in PlayerManager.Instance.players)
                    {
                        player?.ResetForNewMatch();
                    }
                }
            }

            // Đồng bộ trạng thái Alive sang NetworkPlayerStateSync và PlayerController trên tất cả Client
            NetworkPlayerStateSync.ResetAllPlayersAlive();

            // BƯỚC 4: Reset trạng thái kỹ năng và dữ liệu Role theo API hiện có
            NetworkRoleActionBridge.Instance?.ResetForNewMatch();
            NetworkSkillUI.Instance?.ResetForNewMatch();
            NetworkHunterDeathUI.Instance?.ResetForNewMatch();
            NetworkRoleSync.ResetAllRoleSyncs();

            // BƯỚC 5: Reset night cycle, vote data và phase về trạng thái khởi tạo ván mới
            if (NetworkPhaseSync.Instance != null)
            {
                NetworkPhaseSync.Instance.CurrentNightCycle.Value = 0;
                NetworkPhaseSync.Instance.Winner.Value = default;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    NetworkPhaseSync.Instance.CurrentPhase.Value = GamePhase.RoleReveal;
                }
            }

            var timerController = FindFirstObjectByType<NetworkPhaseTimerController>();
            if (timerController != null)
            {
                timerController.ResetForNewMatch();
            }

            // BƯỚC 5b: Reset GameRoleManager của Dev2 bằng Reflection (không can thiệp sửa file của Dev2)
            ResetGameRoleManagerDev2();

            // Reset UI local
            ResetUIState();

            // Đồng bộ roster và left player sang tất cả Client trước khi chia Role
            if (NetworkPhaseSync.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkPhaseSync.Instance.SyncActiveSessionClientRpc(
                    activeIds.ToArray(),
                    NetworkGameManager.LeftPlayerClientIds.ToArray()
                );
                NetworkPhaseSync.Instance.ResetClientMatchStateClientRpc(activeIds.ToArray());
            }

            // Tự động điều chỉnh cấu hình Role từ Lobby cho khớp đúng sĩ số người chơi thực tế
            int remainingPlayerCount = activeIds.Count;
            if (NetworkGameRoleSetup.HasSetup() && NetworkGameRoleSetup.GetTotalRoleAmount() != remainingPlayerCount)
            {
                Debug.Log($"[NetworkGameOverManager] Số role ({NetworkGameRoleSetup.GetTotalRoleAmount()}) không khớp sĩ số còn lại ({remainingPlayerCount}). Reset để tự cân bằng vai trò.");
                NetworkGameRoleSetup.Clear();
            }

            // BƯỚC 6: Khởi tạo Role và dữ liệu ván mới theo cơ chế hiện tại
            if (GameRoleManager.Instance != null)
            {
                GameRoleManager.Instance.BeginGame();
            }

            // Nếu phòng có cấu hình Role từ Lobby, áp dụng ngay
            if (NetworkRoleActionBridge.Instance != null && NetworkGameRoleSetup.HasSetup())
            {
                NetworkRoleActionBridge.Instance.TryApplyLobbyRoles();
            }

            // Đồng bộ lại Role mới cho tất cả Client
            NetworkRoleSync.SyncAllRoleSyncs();

            // BƯỚC 7: Đồng bộ phase và thông báo reset đến tất cả Client
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkGameManager.Instance.ChangeGameState(NetworkGameState.Lobby);
            }

            if (NetworkPhaseSync.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkPhaseSync.Instance.CurrentPhase.Value = GamePhase.RoleReveal;
            }

            Debug.Log("[NetworkGameOverManager] Khởi tạo ván mới hoàn tất thành công!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NetworkGameOverManager] Lỗi khi khởi tạo ván mới: {ex}");
        }
        finally
        {
            isRestarting = false;
        }
    }

    /// <summary>
    /// Reset GameRoleManager của Dev2 bằng Reflection để bắt đầu ván mới:
    ///   - Không sửa code của Dev2 (Assets/Scripts/Thuong/)
    ///   - Reset started = false để GameRoleManager.BeginGame() chạy được lại
    ///   - Reset currentState, currentPhase, currentDay, Winner, PhaseTimeRemaining
    ///   - Đảm bảo useEliminationWin = true trên WinConditionManager
    /// </summary>
    private void ResetGameRoleManagerDev2()
    {
        if (GameRoleManager.Instance == null) return;

        var grm = GameRoleManager.Instance;
        var grmType = typeof(GameRoleManager);

        try
        {
            // 1. Reset field 'started'
            var startedField = grmType.GetField("started", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (startedField != null) startedField.SetValue(grm, false);

            // 2. Reset currentState, currentPhase, currentDay
            grm.currentState = GameState.RoleReveal;
            grm.currentPhase = GamePhase.Setup;
            grm.currentDay = 1;

            // 3. Reset Winner và Result property
            var winnerProp = grmType.GetProperty("Winner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (winnerProp != null && winnerProp.CanWrite)
            {
                winnerProp.SetValue(grm, null);
            }
            else
            {
                var winnerBacking = grmType.GetField("<Winner>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (winnerBacking != null) winnerBacking.SetValue(grm, null);
            }

            var resultProp = grmType.GetProperty("Result", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (resultProp != null && resultProp.CanWrite)
            {
                resultProp.SetValue(grm, null);
            }
            else
            {
                var resultBacking = grmType.GetField("<Result>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (resultBacking != null) resultBacking.SetValue(grm, null);
            }

            // 4. Reset PhaseTimeRemaining property
            var phaseTimeProp = grmType.GetProperty("PhaseTimeRemaining", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (phaseTimeProp != null && phaseTimeProp.CanWrite)
            {
                phaseTimeProp.SetValue(grm, 0f);
            }
            else
            {
                var phaseTimeBacking = grmType.GetField("<PhaseTimeRemaining>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (phaseTimeBacking != null) phaseTimeBacking.SetValue(grm, 0f);
            }

            // 5. Reset resolving flag
            var resolvingField = grmType.GetField("resolving", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (resolvingField != null) resolvingField.SetValue(grm, false);

            // 6. Kích hoạt useEliminationWin trên WinConditionManager
            EnableDev2EliminationWin();

            Debug.Log("[NetworkGameOverManager] Dev2 GameRoleManager & WinConditionManager reset qua reflection thành công.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NetworkGameOverManager] Lỗi reset Dev2 GameRoleManager qua reflection: {ex}");
        }
    }

    /// <summary>
    /// Đồng bộ trạng thái Bạch Khuyển (WhiteHound):
    /// 1. Nếu bị DogSpirit cắn trong đêm: KHÔNG BAO GIỜ CHẾT (cú cắn là để chuyển hóa). Luôn đảm bảo isAlive = true!
    /// 2. Khi bị cắn hoặc DogSpirit đã chết: Thức tỉnh thành Quái vật (Monster) và chính thức chuyển role thành DogSpirit
    ///    để duy trì ván game, ngăn chặn tuyệt đối trường hợp Dân Làng thắng sớm khi WhiteHound còn sống!
    /// </summary>
    public static void SynchronizeWhiteHoundState()
    {
        if (NetworkRoleActionBridge.Instance != null)
        {
            NetworkRoleActionBridge.Instance.SyncAwakenedWhiteHoundsForNextDay();
        }

        if (PlayerManager.Instance?.players == null) return;

        bool anyDogSpiritAlive = false;
        foreach (var p in PlayerManager.Instance.players)
        {
            if (p != null && p.isAlive &&
                (p.roleType == RoleType.DogSpirit || p.roleType == RoleType.SerpentSpirit || p.roleType == RoleType.Ogre))
            {
                anyDogSpiritAlive = true;
                break;
            }
        }

        foreach (var p in PlayerManager.Instance.players)
        {
            if (p == null) continue;
            bool isWh = p.roleType == RoleType.WhiteHound || p.roleType == RoleType.WhiteWolf;
            bool wasBitten = NetworkRoleActionBridge.Instance != null &&
                             NetworkRoleActionBridge.Instance.HasWhiteHoundBeenBittenTonight(p.playerID);

            if (isWh)
            {
                // Cú cắn của DogSpirit vào Bạch Khuyển là để thức tỉnh chuyển hóa, TUYỆT ĐỐI KHÔNG làm Bạch Khuyển chết!
                if (wasBitten && !p.isAlive)
                {
                    if (p.lastDeathCause != DeathCause.Vote && p.lastDeathCause != DeathCause.Poison)
                    {
                        p.isAlive = true;
                        NetworkPlayerStateSync.SyncGameplayAliveState(p.playerID, true);
                        Debug.LogWarning($"[NetworkGameOverManager] WhiteHound (Player {p.playerID + 1}) bị cắn đêm nay -> Đảm bảo isAlive = true!");
                    }
                }

                // Nếu bị cắn hoặc DogSpirit gốc đã chết, WhiteHound thức tỉnh và chính thức mang role DogSpirit:
                if (wasBitten || !anyDogSpiritAlive || p.isWhiteHoundAwakened || p.faction == FactionType.Monster)
                {
                    p.isWhiteHoundAwakened = true;
                    p.faction = FactionType.Monster;
                    p.roleType = RoleType.DogSpirit;

                    if (RoleManager.Instance?.playerRoles != null)
                    {
                        if (!RoleManager.Instance.playerRoles.TryGetValue(p.playerID, out BaseRole role) ||
                            role == null || role.roleType != RoleType.DogSpirit)
                        {
                            RoleManager.Instance.playerRoles[p.playerID] = RoleCatalog.Create(RoleType.DogSpirit, p);
                        }
                        else
                        {
                            role.faction = FactionType.Monster;
                        }
                    }

                    if (RoleManager.Instance?.roleDefinitions != null)
                    {
                        p.roleDefinition = RoleManager.Instance.roleDefinitions.Find(RoleType.DogSpirit);
                    }
                }
            }
        }
    }

    private void EnableDev2EliminationWin()
    {
        if (WinConditionManager.Instance != null)
        {
            try
            {
                var wcmType = typeof(WinConditionManager);
                var elimField = wcmType.GetField("useEliminationWin", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (elimField != null) elimField.SetValue(WinConditionManager.Instance, false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkGameOverManager] Không thể tắt useEliminationWin qua reflection: {ex.Message}");
            }
        }

        EnsureLoiMatchEvaluatorInstalled();
    }

    /// <summary>
    /// Đăng ký bộ đánh giá điều kiện thắng của Loi vào WinConditionManager mà không cần sửa code bên Thuong.
    /// </summary>
    public static void EnsureLoiMatchEvaluatorInstalled()
    {
        var wcm = WinConditionManager.Instance;
        var game = GameRoleManager.Instance;
        if (wcm == null || game == null) return;

        // Luôn đảm bảo useEliminationWin = false để tránh fallback lỗi của Thuong
        try
        {
            var elimField = typeof(WinConditionManager).GetField("useEliminationWin", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            elimField?.SetValue(wcm, false);
        }
        catch { }

        if (wcm.HasMatchEvaluator(game)) return;
        if (Thuong.Online.ThuongOnlineSession.Active != null) return;
        if (game.gameObject != null && game.gameObject.scene.name == Thuong.Online.ThuongOnlineBootstrap.SceneName) return;

        try
        {
            if (game.gameObject.scene == wcm.gameObject.scene)
            {
                wcm.InstallMatchEvaluator(game, EvaluateLoiMatchWinCondition);
            }
            else
            {
                var wcmType = typeof(WinConditionManager);
                var ownerField = wcmType.GetField("matchEvaluatorOwner", BindingFlags.Instance | BindingFlags.NonPublic);
                var evalField = wcmType.GetField("matchEvaluator", BindingFlags.Instance | BindingFlags.NonPublic);
                ownerField?.SetValue(wcm, game);
                evalField?.SetValue(wcm, (System.Action<bool>)EvaluateLoiMatchWinCondition);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NetworkGameOverManager] Không thể đăng ký MatchEvaluator: {ex.Message}");
        }
    }

    private static void EvaluateLoiMatchWinCondition(bool dayFinished)
    {
        var game = GameRoleManager.Instance;
        if (game == null || game.currentState == GameState.GameOver) return;

        if (_instance != null && _instance.shamanKillDelayTimer > 0f) return;

        if (_instance != null && _instance.isActiveAndEnabled && NetworkManager.Singleton != null)
        {
            _instance.CheckServerWinCondition();
            return;
        }

        // Đánh giá trực tiếp khi chạy local / unit test không có NetworkManager
        if (dayFinished && game.currentDay == 1 &&
            PlayerManager.Instance != null &&
            PlayerManager.Instance.players.Exists(player =>
                player != null && player.roleType == RoleType.Madman &&
                !player.isAlive && player.wasVotedOut))
        {
            SetDev2GameOver("Madman");
            return;
        }

        if (WinConditionManager.Instance != null && WinConditionManager.Instance.FoxSpiritHasWon())
        {
            SetDev2GameOver("Fox Spirit");
            return;
        }

        if (TaskManager.Instance != null && TaskManager.Instance.progress >= 100)
        {
            SetDev2GameOver("Villagers");
            return;
        }

        // Chưa hết time của phase thì không tính điều kiện thắng
        if (IsPhaseTimerActive())
            return;

        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null || PlayerManager.Instance.players.Count == 0)
            return;

        // Đảm bảo đồng bộ WhiteHound (khôi phục isAlive nếu bị cắn, thức tỉnh gia nhập DogSpirit nếu bị cắn hoặc DogSpirit chết)
        SynchronizeWhiteHoundState();

        int aliveDogSpirits = 0;
        int aliveWhiteHounds = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf)
            {
                aliveWhiteHounds++;
            }
            else if (player.roleType == RoleType.DogSpirit || player.roleType == RoleType.SerpentSpirit || player.roleType == RoleType.Ogre)
            {
                aliveDogSpirits++;
            }
            else if (RoleManager.Instance?.playerRoles != null &&
                     RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole r) &&
                     r != null && r.faction == FactionType.Monster)
            {
                aliveDogSpirits++;
            }
        }

        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveThirdParty = 0;
        int aliveKillers = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            bool isWhiteHound = player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf;

            bool roleIsMonster = RoleManager.Instance?.playerRoles != null &&
                                 RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole assignedRole) &&
                                 assignedRole != null && assignedRole.faction == FactionType.Monster;

            // Bạch Khuyển thức tỉnh nếu:
            // 1. Đã được đánh dấu thức tỉnh (isWhiteHoundAwakened)
            // 2. Thuộc phe Monster
            // 3. Đã bị DogSpirit cắn trong đêm
            // 4. Hoặc DogSpirit đã chết (aliveDogSpirits == 0): Bạch Khuyển tự động thức tỉnh thành Quái vật duy trì ván game!
            bool isAwakenedWhiteHound = isWhiteHound && (
                player.isWhiteHoundAwakened ||
                roleIsMonster ||
                player.faction == FactionType.Monster ||
                (NetworkRoleActionBridge.Instance != null && NetworkRoleActionBridge.Instance.HasWhiteHoundBeenBittenTonight(player.playerID)) ||
                aliveDogSpirits == 0
            );

            if (isWhiteHound && isAwakenedWhiteHound)
            {
                player.isWhiteHoundAwakened = true;
                player.faction = FactionType.Monster;
                player.roleType = RoleType.DogSpirit;
                if (RoleManager.Instance?.playerRoles != null &&
                    RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole whiteHoundRole) &&
                    whiteHoundRole is WhiteHound wh)
                {
                    wh.BecomeMonster();
                }
            }

            bool isMonster = isAwakenedWhiteHound ||
                             roleIsMonster ||
                             player.faction == FactionType.Monster ||
                             player.roleType == RoleType.DogSpirit ||
                             player.roleType == RoleType.SerpentSpirit ||
                             player.roleType == RoleType.Ogre;

            bool isThirdParty = !isMonster && !isWhiteHound && (
                                player.faction == FactionType.Neutral ||
                                player.roleType == RoleType.Madman ||
                                player.roleType == RoleType.Jester ||
                                player.roleType == RoleType.Lover ||
                                player.roleType == RoleType.FoxSpirit ||
                                player.roleType == RoleType.Piper ||
                                player.roleType == RoleType.Killer ||
                                player.roleType == RoleType.SerialKiller ||
                                player.roleType == RoleType.DeathHerald);

            if (isMonster)
            {
                aliveMonsters++;
            }
            else if (isThirdParty)
            {
                aliveThirdParty++;
                if (player.roleType == RoleType.Killer || player.roleType == RoleType.SerialKiller)
                    aliveKillers++;
            }
            else if (!isWhiteHound)
            {
                // Bạch Khuyển tuyệt đối KHÔNG BAO GIỜ được cộng vào aliveVillagers!
                aliveVillagers++;
            }
        }

        if (_instance != null && _instance.LoversAreLastTwoAlive())
        {
            SetDev2GameOver("Lovers");
            return;
        }

        if (aliveThirdParty > 0 && aliveMonsters == 0 && aliveVillagers == 0)
        {
            if (aliveKillers > 0)
                SetDev2GameOver("Killer");
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive && (p.roleType == RoleType.FoxSpirit || p.roleType == RoleType.Piper)))
                SetDev2GameOver("Fox Spirit");
            else if (PlayerManager.Instance.players.Exists(p => p != null && p.isAlive && (p.roleType == RoleType.Madman || p.roleType == RoleType.Jester)))
                SetDev2GameOver("Madman");
            else
                SetDev2GameOver("Killer");
            return;
        }

        if (dayFinished && WinConditionManager.Instance != null && game.currentDay >= WinConditionManager.Instance.MaxDay && aliveMonsters > 0)
        {
            SetDev2GameOver("Werewolves");
            return;
        }

        // Dân làng chỉ chiến thắng khi đã loại hết cả Quái vật, Bạch Khuyển (WhiteHound) LẪN toàn bộ Phe thứ 3
        if (aliveMonsters == 0 && aliveWhiteHounds == 0 && aliveVillagers > 0 && aliveThirdParty == 0)
        {
            SetDev2GameOver("Villagers");
        }
        else if (aliveMonsters > 0 && aliveMonsters >= aliveVillagers + aliveThirdParty)
        {
            SetDev2GameOver("Werewolves");
        }
    }

    private void OnReturnToLobbyClicked()
    {
        Debug.Log("GAME OVER | Người chơi bấm Về Lobby.");

        bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        if (isHost)
        {
            ReturnToLobbyServer();
        }
        else if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.IsSpawned)
        {
            NetworkPhaseSync.Instance.RequestReturnToLobbyServerRpc();
        }
        else
        {
            RequestReturnToLobbyServerRpc();
        }
    }

    public void ReturnToLobbyServer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkGameOverManager] Chỉ Host/Server mới có quyền chuyển phòng về Lobby.");
            return;
        }

        Debug.Log("GAME OVER | Host thực hiện đưa cả phòng về Lobby, giữ kết nối và Room Code.");

        DayTimer.Instance?.StopTimer();
        ResetGameRoleManagerDev2();

        if (NetworkPhaseSync.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkPhaseSync.Instance.CurrentNightCycle.Value = 0;
            NetworkPhaseSync.Instance.Winner.Value = default;
            NetworkPhaseSync.Instance.CurrentPhase.Value = GamePhase.Setup;
            NetworkPhaseSync.Instance.HideGameOverUIClientRpc();
        }

        ResetUIState();

        // Reset NetworkGameRoleSetup để Lobby thiết lập role mới cho ván kế tiếp
        NetworkGameRoleSetup.Clear();
        NetworkRoleSync.ResetAllRoleSyncs();
        NetworkPlayerAction.ResetServerActionData();

        // Ngắt kết nối dứt điểm các client đã rời phòng / văng game trước khi tải Lobby
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            foreach (ulong leftId in NetworkGameManager.LeftPlayerClientIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.ContainsKey(leftId))
                {
                    Debug.Log($"[NetworkGameOverManager] Ngắt kết nối client {leftId} trước khi về Lobby.");
                    NetworkManager.Singleton.DisconnectClient(leftId);
                }
            }
        }

        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"GAME OVER | Nhận yêu cầu Về Lobby từ Client {clientId}. Host đưa cả phòng về Lobby...");
        ReturnToLobbyServer();
    }

    private void OnLeaveRoomClicked()
    {
        Debug.Log("GAME OVER | Người chơi bấm Rời phòng về MainMenu.");
        if (gameOverPanel != null) Destroy(gameOverPanel);
        if (dedicatedCanvasGO != null) Destroy(dedicatedCanvasGO);
        uiShown = false;
        isGameOver = false;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LeaveRoomToMainMenu();
        }
        else
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    // =========================================================
    // UI HELPERS
    // =========================================================

    private static void CreateLabel(Transform parent, string name, string text,
        Vector2 anchoredPos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        var sceneTmp = FindFirstObjectByType<TMPro.TextMeshProUGUI>();
        if (sceneTmp != null && sceneTmp != tmp && sceneTmp.font != null)
        {
            tmp.font = sceneTmp.font;
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    private static void CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, Color bgColor,
        UnityEngine.Events.UnityAction onClick, int fontSize = 28)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.white * 0.8f;
        outline.effectDistance = new Vector2(2f, -2f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.35f;
        colors.pressedColor = bgColor * 0.65f;
        colors.selectedColor = bgColor * 1.1f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        var sceneTmp = FindFirstObjectByType<TMPro.TextMeshProUGUI>();
        if (sceneTmp != null && sceneTmp != tmp && sceneTmp.font != null)
        {
            tmp.font = sceneTmp.font;
        }

        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    // =========================================================
    // DISPLAY TEXT (CHỈ CÓ 3 KẾT QUẢ THEO YÊU CẦU: DÂN LÀNG, MA SÓI, PHE THỨ 3)
    // =========================================================

    public static string FormatWinner(string winner) => winner switch
    {
        "Villagers"                              => "DÂN LÀNG CHIẾN THẮNG",
        "Werewolves" or "DogSpirit" or "Monster" => "MA SÓI CHIẾN THẮNG",
        "White Wolf" or "WhiteHound"             => "PHE THỨ 3 CHIẾN THẮNG",
        "Lovers"                                 => "PHE THỨ 3 CHIẾN THẮNG",
        "Madman"                                 => "PHE THỨ 3 CHIẾN THẮNG",
        "Fox Spirit" or "Fox"                    => "PHE THỨ 3 CHIẾN THẮNG",
        "Killer"                                 => "PHE THỨ 3 CHIẾN THẮNG",
        "Neutral" or "ThirdParty"                => "PHE THỨ 3 CHIẾN THẮNG",
        _                                        => "DÂN LÀNG CHIẾN THẮNG"
    };

    public static string GetWinnerSubtext(string winner) => winner switch
    {
        "Villagers"                              => "Tất cả quái vật đã bị tiêu diệt.",
        "Werewolves" or "DogSpirit" or "Monster" => "Ma Sói (DogSpirit) đã áp đảo dân làng.",
        "White Wolf" or "WhiteHound"             => "Bạch Khuyển thức tỉnh và chiến thắng.",
        "Lovers"                                 => "Chỉ còn lại đôi tình nhân cuối cùng.",
        "Madman"                                 => "Kẻ Điên bị xử tử ngay ngày đầu tiên!",
        "Fox Spirit" or "Fox"                    => "Cáo Tinh đã mê hoặc thành công hai người.",
        "Killer"                                 => "Sát Nhân là người cuối cùng còn lại.",
        "Neutral" or "ThirdParty"                => "Phe thứ 3 đã đạt điều kiện chiến thắng.",
        _                                        => "Tất cả quái vật đã bị tiêu diệt."
    };

    // =========================================================
    // PUBLIC UTILITY GUARD
    // =========================================================

    /// <summary>Guard dùng bởi các script khác để chặn action và timer sau Game Over.</summary>
    public static bool GameEnded
    {
        get
        {
            var mgr = Instance;
            return (mgr != null && mgr.isGameOver) ||
                (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.GameOver) ||
                (GameRoleManager.Instance != null &&
                 (GameRoleManager.Instance.currentState == GameState.GameOver ||
                  GameRoleManager.Instance.currentPhase == GamePhase.GameOver));
        }
    }
}
