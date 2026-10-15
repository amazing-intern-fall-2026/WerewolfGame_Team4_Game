using System.Collections;
using System.Collections.Generic;
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

    public void ResetUIState()
    {
        isGameOver = false;
        uiShown = false;
        isRestarting = false;
        currentWinner = string.Empty;
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
    }

    private void Start()
    {
        EnableDev2EliminationWin();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
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
            if (string.IsNullOrEmpty(winner)) winner = "Draw";
            TriggerGameOver(winner);
        }
    }

    private void OnNetworkPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        if (newPhase == GamePhase.GameOver)
        {
            string winner = string.Empty;
            if (cachedPhaseSync != null)
                winner = cachedPhaseSync.Winner.Value.ToString();
            if (string.IsNullOrEmpty(winner) && GameRoleManager.Instance != null)
                winner = GameRoleManager.Instance.Winner;
            if (string.IsNullOrEmpty(winner))
                winner = "Draw";

            ShowGameOverUI(winner);
        }
    }

    // =========================================================
    // UPDATE CHECK (Liên tục kiểm tra để không bao giờ bị miss UI)
    // =========================================================

    private void Update()
    {
        // 1. Kiểm tra điều kiện thắng tức thời trên Server (VD: 1 Dân vs 1 DogSpirit -> DogSpirit thắng ngay)
        CheckServerWinCondition();

        if (uiShown) return;

        // Kết nối cachedPhaseSync nếu chưa có
        if (cachedPhaseSync == null)
        {
            cachedPhaseSync = FindFirstObjectByType<NetworkPhaseSync>();
            if (cachedPhaseSync != null)
            {
                cachedPhaseSync.CurrentPhase.OnValueChanged -= OnNetworkPhaseChanged;
                cachedPhaseSync.CurrentPhase.OnValueChanged += OnNetworkPhaseChanged;
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
            if (string.IsNullOrEmpty(winner))
                winner = "Draw";

            ShowGameOverUI(winner);
        }
    }

    /// <summary>
    /// Kiểm tra điều kiện thắng của Server mạng mà không cần can thiệp code Dev2:
    ///   - Phe Quái vật (DogSpirit): Thắng khi số lượng Quái vật >= số lượng Dân làng và phe khác (VD: 1 Dân vs 1 DogSpirit -> DogSpirit thắng)
    ///   - Phe Dân làng: Thắng khi diệt sạch quái vật
    ///   - Hòa: Khi tất cả đều chết
    /// </summary>
    private void CheckServerWinCondition()
    {
        if (isGameOver || GameEnded) return;

        bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServer) return;

        // Không kiểm tra trong giai đoạn khởi tạo / phân vai
        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentPhase == GamePhase.Setup)
            return;

        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.Setup)
            return;

        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null || PlayerManager.Instance.players.Count == 0)
            return;

        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveWhiteWolf = 0;
        int aliveThirdParty = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            bool isMonster = player.faction == FactionType.Monster ||
                             player.roleType == RoleType.DogSpirit ||
                             player.roleType == RoleType.SerpentSpirit ||
                             player.roleType == RoleType.Ogre;

            if ((player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf) &&
                player.isWhiteHoundAwakened)
                aliveWhiteWolf++;
            else if (isMonster)
                aliveMonsters++;
            else if (player.roleType == RoleType.Madman || player.roleType == RoleType.FoxSpirit || player.roleType == RoleType.Killer)
                aliveThirdParty++;
            else
                aliveVillagers++;
        }

        // 1. Monster (DogSpirit/Ma Sói) thắng khi số lượng Monster >= tổng số người phe khác còn sống (và có ít nhất 1 Monster)
        // Ví dụ: 1 Dân vs 1 DogSpirit -> DogSpirit áp đảo Dân và thắng ngay lập tức!
        if (aliveMonsters > 0 && aliveMonsters >= aliveVillagers + aliveWhiteWolf + aliveThirdParty)
        {
            Debug.Log($"[NetworkWinCondition] DogSpirit/Monster ({aliveMonsters}) >= Dân làng & phe khác ({aliveVillagers + aliveWhiteWolf + aliveThirdParty}) -> DogSpirit thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.WerewolfWin();
            TriggerGameOver("Werewolves");
            return;
        }

        // 2. Villagers thắng khi diệt sạch quái vật (và vẫn còn Dân làng sống)
        if (aliveMonsters == 0 && aliveVillagers > 0 && aliveWhiteWolf == 0 && aliveThirdParty == 0)
        {
            Debug.Log($"[NetworkWinCondition] Quái vật đã bị tiêu diệt hết -> Dân làng thắng!");
            if (GameRoleManager.Instance != null) GameRoleManager.Instance.VillagerWin();
            TriggerGameOver("Villagers");
            return;
        }

        // 3. Nếu tất cả đều chết -> Hòa
        if (aliveMonsters == 0 && aliveVillagers == 0 && aliveWhiteWolf == 0 && aliveThirdParty == 0)
        {
            Debug.Log($"[NetworkWinCondition] Tất cả người chơi đã chết -> Hòa!");
            TriggerGameOver("Draw");
            return;
        }
    }

    // =========================================================
    // TRIGGER GAME OVER
    // =========================================================

    /// <summary>Gọi để kích hoạt Game Over và hiển thị UI.</summary>
    public void TriggerGameOver(string winner)
    {
        isGameOver = true;
        currentWinner = winner;

        Debug.Log($"GAME OVER MANAGER | TriggerGameOver! Winner: {winner}");

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
        }

        ShowGameOverUI(winner);
    }

    // =========================================================
    // GAME OVER UI (Đồng bộ, tức thời, không trễ)
    // =========================================================

    public void ShowGameOverUI(string winner)
    {
        if (uiShown && gameOverPanel != null) return;
        uiShown = true;
        isGameOver = true;
        currentWinner = winner;

        Debug.Log($"GAME OVER MANAGER | ShowGameOverUI. Winner: {winner}");

        // Mở chuột để người chơi có thể click nút ngay lập tức
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildGameOverUIDirect(winner);
    }

    private void BuildGameOverUIDirect(string winner)
    {
        // 1. Đảm bảo có EventSystem để nhận tương tác chuột
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // 2. Tạo Canvas overlay riêng biệt với sortingOrder tối đa (32767) để đảm bảo không bị đè
        if (dedicatedCanvasGO == null)
        {
            dedicatedCanvasGO = new GameObject("GameOverDedicatedCanvas");
            Canvas canvas = dedicatedCanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
        cardRect.sizeDelta = new Vector2(600, 420);

        Image cardBg = card.GetComponent<Image>();
        cardBg.color = new Color(0.12f, 0.12f, 0.22f, 1f);

        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.7f, 0.5f, 1f, 0.9f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // --- Tiêu đề ---
        CreateLabel(card.transform, "TitleLabel",
            "KẾT THÚC VÁN",
            new Vector2(0, 145), new Vector2(520, 50),
            24, new Color(0.95f, 0.8f, 1f));

        // --- Phe thắng ---
        CreateLabel(card.transform, "WinnerLabel",
            FormatWinner(winner),
            new Vector2(0, 65), new Vector2(540, 70),
            30, Color.white);

        // --- Mô tả phụ ---
        string sub = GetWinnerSubtext(winner);
        if (!string.IsNullOrEmpty(sub))
        {
            CreateLabel(card.transform, "SubLabel",
                sub,
                new Vector2(0, -10), new Vector2(500, 45),
                16, new Color(0.8f, 0.8f, 0.9f));
        }

        // --- Nút bấm ---
        bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        float btnY = -140f;

        if (isHost)
        {
            // Host thấy 2 nút: Chơi Lại và Về Lobby
            CreateButton(card.transform, "PlayAgainBtn",
                "CHƠI LẠI",
                new Vector2(-130f, btnY), new Vector2(210, 56),
                new Color(0.15f, 0.65f, 0.25f),
                OnPlayAgainClicked);

            CreateButton(card.transform, "LobbyBtn",
                "VỀ LOBBY",
                new Vector2(130f, btnY), new Vector2(210, 56),
                new Color(0.65f, 0.20f, 0.25f),
                OnLobbyClicked);
        }
        else
        {
            // Client thấy nút Về Lobby ở giữa
            CreateButton(card.transform, "LobbyBtn",
                "VỀ LOBBY",
                new Vector2(0f, btnY), new Vector2(240, 56),
                new Color(0.65f, 0.20f, 0.25f),
                OnLobbyClicked);
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
            float w = 500;
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
                if (GUI.Button(new Rect(x + 35, y + 175, 200, 55), "CHƠI LẠI"))
                {
                    OnPlayAgainClicked();
                }

                if (GUI.Button(new Rect(x + 265, y + 175, 200, 55), "VỀ LOBBY"))
                {
                    OnLobbyClicked();
                }
            }
            else
            {
                if (GUI.Button(new Rect(x + 150, y + 175, 200, 55), "VỀ LOBBY"))
                {
                    OnLobbyClicked();
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

            // BƯỚC 3: Reset trạng thái sống/chết của toàn bộ Player
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && PlayerManager.Instance != null)
            {
                var connectedClients = NetworkManager.Singleton.ConnectedClients;
                if (PlayerManager.Instance.players != null)
                {
                    PlayerManager.Instance.players.RemoveAll(p => p == null || !connectedClients.ContainsKey((ulong)p.playerID));
                }

                // Đảm bảo tất cả client đang kết nối đều có PlayerData trong PlayerManager
                foreach (var kvp in connectedClients)
                {
                    ulong clientId = kvp.Key;
                    if (PlayerManager.Instance.GetplayerByID((int)clientId) == null)
                    {
                        string pName = NetworkGamePlayerNameSetup.GetName(clientId);
                        PlayerManager.Instance.RegisterPlayer(new PlayerData
                        {
                            playerID = (int)clientId,
                            playerName = pName,
                            isAlive = true,
                            votePower = 1,
                            hasVoted = false,
                            hasUseNightAction = false,
                            status = new PlayerStatus()
                        });
                    }
                }
            }

            if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
            {
                foreach (var player in PlayerManager.Instance.players)
                {
                    player?.ResetForNewMatch();
                }
            }

            // Đồng bộ trạng thái Alive sang NetworkPlayerStateSync và PlayerController trên tất cả Client
            NetworkPlayerStateSync.ResetAllPlayersAlive();

            // BƯỚC 4: Reset trạng thái kỹ năng và dữ liệu Role theo API hiện có
            NetworkRoleActionBridge.Instance?.ResetForNewMatch();
            NetworkSkillUI.Instance?.ResetForNewMatch();
            NetworkHunterDeathUI.Instance?.ResetForNewMatch();

            // BƯỚC 5: Reset night cycle, vote data và phase về trạng thái khởi tạo ván mới
            if (NetworkPhaseSync.Instance != null)
            {
                NetworkPhaseSync.Instance.CurrentNightCycle.Value = 0;
                NetworkPhaseSync.Instance.Winner.Value = default;
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

            // Reset RoleSync để Server gửi lại Role mới cho tất cả Client
            NetworkRoleSync.ResetAllRoleSyncs();

            // BƯỚC 7: Đồng bộ phase và thông báo reset đến tất cả Client
            if (NetworkGameManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkGameManager.Instance.ChangeGameState(NetworkGameState.Lobby);
            }

            if (NetworkPhaseSync.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkPhaseSync.Instance.CurrentPhase.Value = GamePhase.RoleReveal;
                NetworkPhaseSync.Instance.ResetClientMatchStateClientRpc();
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

            // 3. Reset Winner property
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

    private void EnableDev2EliminationWin()
    {
        if (WinConditionManager.Instance != null)
        {
            try
            {
                var wcmType = typeof(WinConditionManager);
                var elimField = wcmType.GetField("useEliminationWin", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (elimField != null) elimField.SetValue(WinConditionManager.Instance, true);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkGameOverManager] Không thể bật useEliminationWin qua reflection: {ex.Message}");
            }
        }
    }

    private void OnLobbyClicked()
    {
        Debug.Log("GAME OVER | Người chơi bấm Về Lobby.");
        if (gameOverPanel != null) Destroy(gameOverPanel);
        if (dedicatedCanvasGO != null) Destroy(dedicatedCanvasGO);
        uiShown = false;
        isGameOver = false;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LeaveLobby();
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
        UnityEngine.Events.UnityAction onClick)
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
        tmp.fontSize = 18;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    // =========================================================
    // DISPLAY TEXT
    // =========================================================

    private static string FormatWinner(string winner) => winner switch
    {
        "Villagers"  => "DÂN LÀNG CHIẾN THẮNG",
        "Werewolves" or "DogSpirit" or "Monster" => "PHE MA SÓI (DOGSPIRIT) CHIẾN THẮNG",
        "White Wolf" => "BẠCH KHUYỂN CHIẾN THẮNG",
        "Lovers"     => "ĐÔI TÌNH NHÂN CHIẾN THẮNG",
        "Madman"     => "KẺ ĐIÊN CHIẾN THẮNG",
        "Fox Spirit" => "CÁO TINH CHIẾN THẮNG",
        "Killer"     => "SÁT NHÂN CHIẾN THẮNG",
        "Neutral"    => "TRUNG LẬP CHIẾN THẮNG",
        "Draw"       => "HÒA - KHÔNG AI THẮNG",
        _            => $"{winner.ToUpper()} CHIẾN THẮNG"
    };

    private static string GetWinnerSubtext(string winner) => winner switch
    {
        "Villagers"  => "Tất cả quái vật đã bị tiêu diệt.",
        "Werewolves" or "DogSpirit" or "Monster" => "Ma Sói (Dogspirit) đã áp đảo dân làng.",
        "White Wolf" => "Bạch Khuyển thức tỉnh và chiến thắng.",
        "Lovers"     => "Chỉ còn lại đôi tình nhân cuối cùng.",
        "Madman"     => "Kẻ Điên bị xử tử ngay ngày đầu tiên!",
        "Fox Spirit" => "Cáo Tinh đã mê hoặc thành công hai người.",
        "Killer"     => "Sát Nhân là người cuối cùng còn lại.",
        _            => string.Empty
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
