using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DefaultExecutionOrder(-50)]
public class NetworkRoleActionBridge : MonoBehaviour
{
    public static NetworkRoleActionBridge Instance { get; private set; }

    private bool phaseInitialized;
    private GamePhase lastPhase;

    // Đảm bảo Role Lobby chỉ được áp dụng một lần
    private bool lobbyRolesApplied;
    public bool LobbyRolesApplied => lobbyRolesApplied;

    private int lastNotifiedDogSpiritNightCycle = -1;
    private int lastNotifiedDogSpiritVictimId = -1;
    private readonly HashSet<int> bittenWhiteHoundPlayerIds = new HashSet<int>();

    public bool HasWhiteHoundBeenBittenTonight(int playerId)
    {
        return bittenWhiteHoundPlayerIds.Contains(playerId);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetForNewMatch()
    {
        lobbyRolesApplied = false;
        phaseInitialized = false;
        lastNotifiedDogSpiritNightCycle = -1;
        lastNotifiedDogSpiritVictimId = -1;
        bittenWhiteHoundPlayerIds.Clear();
        ResetNightActions();
        Debug.Log("ROLE ACTION BRIDGE: Reset For New Match hoàn tất.");
    }

    private void OnEnable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted +=
            OnActionAccepted;
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted -=
            OnActionAccepted;
    }

    private void Update()
    {
        NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        // ==========================================
        // APPLY ROLE TỪ LOBBY
        // ==========================================

        if (!lobbyRolesApplied)
        {
            TryApplyLobbyRoles();
        }

        // ==========================================
        // PHASE
        // ==========================================

        if (NetworkPhaseSync.Instance == null)
            return;

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        if (!phaseInitialized)
        {
            phaseInitialized = true;
            lastPhase = currentPhase;

            if (currentPhase == GamePhase.Night)
                ResetNightActions();

            return;
        }

        if (currentPhase == lastPhase)
            return;

        Debug.Log(
            "ROLE ACTION BRIDGE | Phase: "
            + lastPhase
            + " → "
            + currentPhase
        );

        lastPhase = currentPhase;

        if (currentPhase == GamePhase.Night)
        {
            ResetNightActions();
        }
        else if (currentPhase != GamePhase.Setup)
        {
            SyncAwakenedWhiteHoundsForNextDay();
        }
    }

    public void SyncAwakenedWhiteHoundsForNextDay()
    {
        if (PlayerManager.Instance?.players == null)
            return;

        bool anyDogSpiritAlive = PlayerManager.Instance.players.Exists(p => p != null && p.isAlive &&
            (p.roleType == RoleType.DogSpirit || p.roleType == RoleType.SerpentSpirit || p.roleType == RoleType.Ogre));

        bool anyAwakened = false;
        foreach (PlayerData player in PlayerManager.Instance.players)
        {
            if (player == null)
                continue;

            bool isWh = player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf;
            bool wasBitten = bittenWhiteHoundPlayerIds.Contains(player.playerID);

            // Cú cắn của DogSpirit vào Bạch Khuyển là để thức tỉnh chuyển hóa, TUYỆT ĐỐI KHÔNG làm Bạch Khuyển chết!
            if (isWh && wasBitten && !player.isAlive)
            {
                if (player.lastDeathCause != DeathCause.Vote && player.lastDeathCause != DeathCause.Poison)
                {
                    player.isAlive = true;
                    NetworkPlayerStateSync.SyncGameplayAliveState(player.playerID, true);
                    Debug.LogWarning($"ROLE ACTION BRIDGE | Khôi phục isAlive = true cho WhiteHound (Player {player.playerID + 1}) do bị DogSpirit cắn thức tỉnh!");
                }
            }

            if (!player.isAlive)
                continue;

            bool isBittenOrAwakened = player.isWhiteHoundAwakened ||
                                      player.faction == FactionType.Monster ||
                                      wasBitten ||
                                      !anyDogSpiritAlive;

            if (!isBittenOrAwakened)
                continue;

            if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf || player.roleType == RoleType.DogSpirit)
            {
                player.isWhiteHoundAwakened = true;
                player.faction = FactionType.Monster;
                bittenWhiteHoundPlayerIds.Add(player.playerID);

                // Sang ngày hôm sau, WhiteHound chính thức gia nhập phe DogSpirit
                if (player.roleType != RoleType.DogSpirit)
                {
                    player.roleType = RoleType.DogSpirit;
                    Debug.Log($"ROLE ACTION BRIDGE | Player {player.playerID + 1} (Bạch Khuyển) chính thức gia nhập DogSpirit sang ngày hôm sau!");
                }

                if (RoleManager.Instance?.playerRoles != null)
                {
                    if (!RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out BaseRole role) ||
                        role == null || role.roleType != RoleType.DogSpirit)
                    {
                        RoleManager.Instance.playerRoles[player.playerID] = RoleCatalog.Create(RoleType.DogSpirit, player);
                    }
                    else
                    {
                        role.faction = FactionType.Monster;
                    }
                }

                if (RoleManager.Instance?.roleDefinitions != null)
                {
                    player.roleDefinition = RoleManager.Instance.roleDefinitions.Find(RoleType.DogSpirit);
                }

                player.NotifyChanged();
                anyAwakened = true;
            }
        }

        if (anyAwakened)
        {
            NetworkRoleSync.SyncAllRoleSyncs();
        }
    }

    // =====================================================
    // APPLY ROLE SETUP TỪ LOBBY
    // =====================================================

    public void TryApplyLobbyRoles()
    {
        if (!NetworkGameRoleSetup.HasSetup())
            return;

        RoleManager roleManager =
            RoleManager.Instance;

        if (roleManager == null)
            roleManager =
                FindFirstObjectByType<RoleManager>();

        if (roleManager == null)
            return;

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
            playerManager =
                FindFirstObjectByType<PlayerManager>();

        if (playerManager == null)
            return;

        if (
            playerManager.players == null ||
            playerManager.players.Count == 0
        )
        {
            return;
        }

        // Chờ tất cả connected clients hoàn tất đăng ký vào PlayerManager
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            playerManager.players.Count < Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            return;
        }

        // RoleManager phải AssignRole trước.
        // Nếu chưa có role thì chờ frame tiếp theo.
        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            return;
        }

        List<RoleAmountData> roleSetup =
            NetworkGameRoleSetup.GetRoles();

        if (roleSetup == null || roleSetup.Count == 0)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không có Role Setup từ Lobby."
            );

            return;
        }

        int totalRoles = 0;

        foreach (RoleAmountData data in roleSetup)
        {
            totalRoles += data.Amount;
        }

        int playerCount =
            playerManager.players.Count;

        if (totalRoles != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Cấu hình Role không hợp lệ: Số Role ("
                + totalRoles
                + ") != Số Player ("
                + playerCount
                + "). Không bắt đầu chia role."
            );

            lobbyRolesApplied = true;
            return;
        }

        // ==========================================
        // BUILD ROLE POOL TỪ LOBBY
        // ==========================================

        List<RoleType> selectedRoles =
            new List<RoleType>();

        foreach (RoleAmountData data in roleSetup)
        {
            if (data.Amount <= 0)
                continue;

            for (int i = 0; i < data.Amount; i++)
            {
                selectedRoles.Add(
                    data.Role
                );
            }
        }

        if (selectedRoles.Count != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Không thể tạo Role Pool."
            );

            return;
        }

        // ==========================================
        // SORT PLAYER THEO ID
        // ==========================================

        List<PlayerData> players =
            new List<PlayerData>(
                playerManager.players
            );

        players.RemoveAll(
            player => player == null
        );

        players.Sort(
            (a, b) =>
                a.playerID.CompareTo(
                    b.playerID
                )
        );

        if (players.Count != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Player list không hợp lệ."
            );

            return;
        }

        // ==========================================
        // SHUFFLE ROLE
        // ==========================================

        ShuffleRoles(
            selectedRoles
        );

        // ==========================================
        // CLEAR ROLE CŨ
        // ==========================================

        roleManager.playerRoles.Clear();

        // ==========================================
        // APPLY ROLE MỚI
        // ==========================================

        for (int i = 0; i < players.Count; i++)
        {
            PlayerData player =
                players[i];

            RoleType roleType =
                selectedRoles[i];

            BaseRole role =
                RoleCatalog.Create(
                    roleType,
                    player
                );

            if (role == null)
            {
                Debug.LogError(
                    "ROLE ACTION BRIDGE | "
                    + "Không tạo được Role "
                    + roleType
                    + " cho Player "
                    + player.playerID
                );

                continue;
            }

            player.roleType =
                roleType;

            player.faction =
                role.faction;

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

            if (roleManager.roleDefinitions != null)
            {
                player.roleDefinition = roleManager.roleDefinitions.Find(roleType);
            }
            player.NotifyChanged();

            roleManager.playerRoles[
                player.playerID
            ] = role;

            role.OnGameStart();

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Lobby Role Applied | "
                + "Player "
                + player.playerID
                + " → "
                + roleType
            );
        }

        lobbyRolesApplied = true;

        NetworkPlayerAction.ResetServerActionData();

        // Đồng bộ ngay vai trò mới đến tất cả Client
        NetworkRoleSync.SyncAllRoleSyncs();

        // ==========================================
        // DEBUG
        // ==========================================

        Debug.Log(
            "========================================"
        );

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "LOBBY ROLE SETUP APPLIED"
        );

        foreach (PlayerData player in players)
        {
            Debug.Log(
                "Player "
                + player.playerID
                + " → "
                + player.roleType
            );
        }

        Debug.Log(
            "========================================"
        );
    }

    // =====================================================
    // SHUFFLE ROLE
    // =====================================================

    private void ShuffleRoles(
        List<RoleType> roles)
    {
        System.Random random =
            new System.Random();

        for (
            int i = roles.Count - 1;
            i > 0;
            i--
        )
        {
            int j =
                random.Next(
                    i + 1
                );

            RoleType temp =
                roles[i];

            roles[i] = roles[j];

            roles[j] = temp;
        }
    }

    // =====================================================
    // RESET NIGHT
    // =====================================================

    private void ResetNightActions()
    {
        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
            playerManager =
                FindFirstObjectByType<PlayerManager>();

        if (playerManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy PlayerManager."
            );

            return;
        }

        if (playerManager.players == null)
            return;

        int resetCount = 0;

        foreach (PlayerData player in playerManager.players)
        {
            if (player == null)
                continue;

            player.hasUseNightAction = false;
            resetCount++;
        }

        lastNotifiedDogSpiritNightCycle = -1;
        lastNotifiedDogSpiritVictimId = -1;

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "RESET NIGHT ACTION | "
            + resetCount
            + " PlayerData"
        );
    }

    // =====================================================
    // ACTION ACCEPTED
    // =====================================================

    private void OnActionAccepted(
        NetworkActionEvent actionEvent)
    {
        Debug.Log(
            "HUNTER DEBUG | OnActionAccepted được gọi."
        );

        int requesterID =
            (int)actionEvent.RequesterPlayerId;

        int targetID =
            (int)actionEvent.TargetPlayerId;

        ulong requesterClientId =
            actionEvent.RequesterPlayerId;

        Debug.Log(
            "HUNTER DEBUG | Requester = "
            + requesterID
            + " | Target = "
            + targetID
        );

        // =====================================================
        // ROLE MANAGER
        // =====================================================

        RoleManager roleManager =
            RoleManager.Instance;

        if (roleManager == null)
        {
            roleManager =
                FindFirstObjectByType<RoleManager>();
        }

        if (roleManager == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | RoleManager NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy RoleManager!"
            );

            return;
        }

        // =====================================================
        // ĐẢM BẢO ROLE ĐÃ ĐƯỢC ASSIGN
        // =====================================================

        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            Debug.LogWarning(
                "HUNTER DEBUG | playerRoles đang rỗng. "
                + "Đang AssignRole()."
            );

            roleManager.AssignRole();

            TryApplyLobbyRoles();
        }

        // =====================================================
        // LẤY ROLE REQUESTER
        // =====================================================

        if (
            !roleManager.playerRoles.TryGetValue(
                requesterID,
                out BaseRole role
            )
        )
        {
            Debug.LogError(
                "HUNTER DEBUG | Không tìm thấy Role của Player "
                + requesterID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Role của Player!"
            );

            return;
        }

        if (role == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Role NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Role đang NULL!"
            );

            return;
        }

        Debug.Log(
            "HUNTER DEBUG | Role của Player "
            + requesterID
            + " = "
            + role.roleType
        );

        // =====================================================
        // PHASE SYNC
        // =====================================================

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | NetworkPhaseSync NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "NetworkPhaseSync đang NULL!"
            );

            return;
        }

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        Debug.Log(
            "HUNTER DEBUG | Current Phase = "
            + currentPhase
        );

        // =====================================================
        // PLAYER MANAGER
        // =====================================================

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
        {
            playerManager =
                FindFirstObjectByType<PlayerManager>();
        }

        if (playerManager == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | PlayerManager NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy PlayerManager!"
            );

            return;
        }

        // =====================================================
        // REQUESTER
        // =====================================================

        PlayerData requester =
            playerManager.GetplayerByID(
                requesterID
            );

        if (requester == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Requester NULL | ID = "
                + requesterID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Requester!"
            );

            return;
        }

        // =====================================================
        // TARGET
        // =====================================================

        PlayerData target =
            playerManager.GetplayerByID(
                targetID
            );

        if (target == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Target NULL | ID = "
                + targetID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Target!"
            );

            return;
        }

        Debug.Log(
            "HUNTER DEBUG | Requester Alive = "
            + requester.isAlive
            + " | Target Alive = "
            + target.isAlive
        );

        // =====================================================
        // HUNTER
        // =====================================================

        if (role.roleType == RoleType.Hunter)
        {
            Debug.Log(
                "HUNTER DEBUG | Hunter branch được gọi."
            );

            // -------------------------------------------------
            // Hunter phải chết
            // -------------------------------------------------

            if (requester.isAlive)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Hunter vẫn còn sống."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Hunter phải chết mới có thể bắn trả thù!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | Hunter đã chết."
            );

            // -------------------------------------------------
            // Cho phép bắn trong các phase game đang chạy
            // -------------------------------------------------

            if (currentPhase == GamePhase.Setup || currentPhase == GamePhase.GameOver)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Sai phase: "
                    + currentPhase
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không thể bắn ở phase hiện tại!"
                );

                return;
            }

            // -------------------------------------------------
            // Kiểm tra Hunter còn quyền bắn (chỉ 1 lần duy nhất)
            // -------------------------------------------------

            if (!requester.hasHunterTrap || requester.hasUsedHunterShot)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Hunter đã sử dụng quyền bắn."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Thợ Săn đã sử dụng quyền bắn hoặc không có bẫy!"
                );

                return;
            }

            // -------------------------------------------------
            // Target phải còn sống và không phải bản thân
            // -------------------------------------------------

            if (!target.isAlive)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Target đã chết."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không thể bắn người đã chết!"
                );

                return;
            }

            if (targetID == requesterID)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Thợ Săn không thể tự bắn chính mình!"
                );

                return;
            }

            // Đánh dấu đã dùng quyền bắn
            requester.hasHunterTrap = false;
            requester.hasUsedHunterShot = true;
            requester.hunterTargetID = targetID;

            // -------------------------------------------------
            // Lấy HunterRole
            // -------------------------------------------------

            HunterRole hunterRole =
                role as HunterRole;

            if (hunterRole != null)
            {
                hunterRole.SetTrap(targetID);
            }

            // =================================================
            // DEATH RESOLVER
            // =================================================

            if (DeathResolver.Instance == null)
            {
                Debug.LogError(
                    "HUNTER DEBUG | DeathResolver NULL."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "DeathResolver đang NULL!"
                );

                return;
            }

            // =================================================
            // GIẾT TARGET NGAY
            // =================================================

            bool killed =
                DeathResolver.Instance.TryKillPlayer(
                    targetID,
                    DeathCause.Trap
                );

            Debug.Log(
                "HUNTER DEBUG | TryKillPlayer result = "
                + killed
            );

            if (!killed)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Bẫy không thể giết Target!"
                );

                return;
            }

            // =================================================
            // SUCCESS
            // =================================================

            Debug.Log(
                "Hunter Player "
                + (requesterID + 1)
                + " bắn phát đạn trả thù → Player "
                + (targetID + 1)
                + " chết ngay."
            );

            SendResult(
                requesterClientId,
                true,
                $"Thợ Săn đã bắn tiêu diệt {target.DisplayName}!"
            );

            // Ngay lập tức kiểm tra lại điều kiện thắng thua
            if (NetworkGameOverManager.Instance != null)
            {
                NetworkGameOverManager.Instance.CheckServerWinCondition();
            }
            if (WinConditionManager.Instance != null)
            {
                WinConditionManager.Instance.CheckWinCondition();
            }

            return;
        }

        // =====================================================
        // NORMAL NIGHT ACTION
        // =====================================================

        if (currentPhase != GamePhase.Night)
        {
            SendResult(
                requesterClientId,
                false,
                "Không thể Action ngoài Night!"
            );

            return;
        }

        // =====================================================
        // NORMAL DEV2 ACTION
        // =====================================================

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "UseNightAbility("
            + targetID
            + ")"
        );

        bool success;

        // =====================================================
        // GUARDIAN SELF PROTECT
        // =====================================================

        if (
            role.roleType == RoleType.VillageGuardian &&
            requesterID == targetID
        )
        {
            GuardianRole guardianRole =
                role as GuardianRole;

            if (guardianRole == null)
            {
                Debug.LogError(
                    "ROLE ACTION BRIDGE | "
                    + "Không tìm thấy GuardianRole."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không tìm thấy GuardianRole!"
                );

                return;
            }

            // =================================================
            // REQUESTER VALIDATION
            // =================================================

            if (requester == null || !requester.isAlive)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Người chơi đã bị loại."
                );

                return;
            }

            if (!role.HasNightAbility)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Role này không có kỹ năng chủ động ban đêm."
                );

                return;
            }

            // =================================================
            // KHÔNG CHECK requester.hasUseNightAction Ở ĐÂY
            // =================================================
            //
            // NetworkPlayerAction đã quản lý việc dùng Skill
            // theo CurrentNightCycle.
            //
            // Night 1 → dùng được
            // Night 2 → dùng lại được
            // Night 3 → dùng lại được
            //
            // Nếu dùng lại trong cùng Night,
            // NetworkPlayerAction sẽ chặn.

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Guardian tự bảo vệ | Player "
                + (requesterID + 1)
            );

            // =================================================
            // DEV2 GUARDIAN LOGIC
            // =================================================
            //
            // Gọi trực tiếp GuardianRole để giữ nguyên
            // logic Dev2:
            //
            // - Có thể tự bảo vệ
            // - Không thể bảo vệ cùng một người
            //   trong hai đêm liên tiếp
            //

            success =
                guardianRole.TryUseNightAbility(
                    targetID,
                    out string guardianFeedback
                );

            if (!success)
            {
                Debug.LogWarning(
                    "ROLE ACTION BRIDGE | "
                    + "Guardian từ chối tự bảo vệ | "
                    + guardianFeedback
                );

                SendResult(
                    requesterClientId,
                    false,
                    guardianFeedback
                );

                return;
            }

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Guardian tự bảo vệ thành công."
            );

            SendResult(
                requesterClientId,
                true,
                guardianFeedback
            );

            return;
        }

        // =====================================================
        // SHAMAN (PHÁP SƯ) LOGIC
        // =====================================================

        if (role.roleType == RoleType.Shaman)
        {
            ShamanRole shamanRole = role as ShamanRole;
            if (shamanRole == null)
            {
                SendResult(requesterClientId, false, "Không tìm thấy ShamanRole!");
                return;
            }

            // Requester validation
            if (requester == null || !requester.isAlive)
            {
                SendResult(requesterClientId, false, "Người chơi đã bị loại.");
                return;
            }

            // Target validation
            if (target == null || !target.isAlive)
            {
                SendResult(requesterClientId, false, "Mục tiêu không tồn tại hoặc đã bị loại.");
                return;
            }

            bool isSave = actionEvent.SubActionId == 1; // 1 = Bùa Lợi (Cứu)
            bool isKill = actionEvent.SubActionId == 2; // 2 = Bùa Hại (Giết)

            if (!isSave && !isKill)
            {
                SendResult(requesterClientId, false, "Vui lòng chọn Bùa Lợi (Cứu) hoặc Bùa Hại (Giết)!");
                return;
            }

            // -------------------------------------------------
            // BÙA LỢI (SAVE / CỨU)
            // -------------------------------------------------
            if (isSave)
            {
                if (!shamanRole.HasGoodCharm)
                {
                    SendResult(requesterClientId, false, "Bùa Lợi (Cứu) đã được sử dụng trước đó!");
                    return;
                }

                // Tiêu hao Bùa Lợi trên ShamanRole
                shamanRole.UseGoodCharm(targetID);

                // Ghi nhận bảo vệ lên NightManager và PlayerStatus để chặn đòn cắn của Sói
                NightManager.Instance?.SetProtectedTarget(targetID);
                target.status ??= new PlayerStatus();
                target.status.isProtected = true;

                Debug.Log(
                    "ROLE ACTION BRIDGE | Shaman dùng Bùa Lợi cứu Player "
                    + (targetID + 1)
                    + (requesterID == targetID ? " (Tự cứu bản thân)" : "")
                );

                string saveMsg = requesterID == targetID
                    ? "Đã dùng Bùa Lợi tự cứu bản thân thành công!"
                    : $"Đã dùng Bùa Lợi cứu Player {targetID + 1} thành công!";

                SendResult(
                    requesterClientId,
                    true,
                    saveMsg
                );

                return;
            }

            // -------------------------------------------------
            // BÙA HẠI (KILL / GIẾT)
            // -------------------------------------------------
            if (isKill)
            {
                if (!shamanRole.HasBadCharm)
                {
                    SendResult(requesterClientId, false, "Bùa Hại (Giết) đã được sử dụng trước đó!");
                    return;
                }

                if (requesterID == targetID)
                {
                    SendResult(requesterClientId, false, "Không thể dùng Bùa Hại lên chính mình!");
                    return;
                }

                if (DeathResolver.Instance == null)
                {
                    Debug.LogError("ROLE ACTION BRIDGE | DeathResolver NULL.");
                    SendResult(requesterClientId, false, "DeathResolver đang NULL!");
                    return;
                }

                // Tiêu hao Bùa Hại trên ShamanRole
                shamanRole.UseBadCharm(targetID);

                // Tuyệt đối không dùng hiệu ứng Silenced thay cho Kill (xóa cờ Silenced sai của Dev2)
                if (target.status != null)
                {
                    target.status.isSilenced = false;
                }

                // Tiêu diệt mục tiêu qua DeathResolver chính thống của gameplay
                bool killed = DeathResolver.Instance.TryKillPlayer(
                    targetID,
                    DeathCause.Poison,
                    RoleType.Shaman
                );

                Debug.Log(
                    "ROLE ACTION BRIDGE | Shaman dùng Bùa Hại giết Player "
                    + (targetID + 1)
                    + " | Killed = "
                    + killed
                );

                if (killed)
                {
                    // Thiết lập thời gian chờ sau khi Pháp Sư dùng Kill trước khi kiểm tra điều kiện thắng
                    NetworkGameOverManager.TriggerShamanKillDelay(5.0f);
                }

                if (!killed)
                {
                    SendResult(
                        requesterClientId,
                        false,
                        "Mục tiêu được bảo vệ và không thể bị tiêu diệt!"
                    );

                    return;
                }

                SendResult(
                    requesterClientId,
                    true,
                    $"Đã dùng Bùa Hại tiêu diệt Player {targetID + 1} thành công!"
                );

                return;
            }
        }

        // =====================================================
        // NORMAL DEV2 ACTION
        // =====================================================

        string feedback;

        success =
            roleManager.UseNightAbility(
                requesterID,
                targetID,
                out feedback
            );

        // =====================================================
        // DEV2 TỪ CHỐI
        // =====================================================

        if (!success)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Dev2 từ chối Action: "
                + feedback
            );

            SendResult(
                requesterClientId,
                false,
                string.IsNullOrEmpty(feedback)
                    ? "Action không thành công!"
                    : feedback
            );

            return;
        }

        // =====================================================
        // ACTION THÀNH CÔNG
        // =====================================================

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "Action thành công! "
            + feedback
        );

        SendResult(
            requesterClientId,
            true,
            string.IsNullOrEmpty(feedback)
                ? "Action thành công!"
                : feedback
        );

        // =====================================================
        // SEER RESULT
        // =====================================================

        if (role.roleType == RoleType.Seer)
        {
            SendSeerResult(
                requesterID,
                target
            );
        }

        // =====================================================
        // DOGSPIRIT ATTACK -> NOTIFY SHAMAN
        // =====================================================

        if (role.roleType == RoleType.DogSpirit ||
            (role.roleType == RoleType.WhiteHound && requester != null && requester.isWhiteHoundAwakened))
        {
            if (role.roleType == RoleType.DogSpirit && target != null)
            {
                if (target.roleType == RoleType.WhiteHound || target.roleType == RoleType.WhiteWolf)
                {
                    bittenWhiteHoundPlayerIds.Add(targetID);
                    // QUAN TRỌNG: KHÔNG set target.isWhiteHoundAwakened = true ở đây!
                    // Vì DeathResolver.cs cần điều kiện (!target.isWhiteHoundAwakened == true) để ngăn cái chết (DeathOutcome.Prevented)
                    // và đánh thức WhiteHound thành quái vật. Nếu set true sớm ở đây, DeathResolver sẽ xem như WhiteHound đã thức tỉnh
                    // từ trước và cú cắn này sẽ GIẾT CHẾT WhiteHound!
                    Debug.Log($"ROLE ACTION BRIDGE | DogSpirit đã cắn WhiteHound (Player {targetID + 1}) -> Ghi nhận WhiteHound bị cắn đêm nay để thức tỉnh và ngăn Dân làng thắng!");
                }
            }

            NotifyShamanAboutDogSpiritTarget(
                targetID
            );
        }
    }

    private void SendResult(
        ulong clientId,
        bool success,
        string message)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (
            !NetworkManager.Singleton.ConnectedClients
                .TryGetValue(
                    clientId,
                    out NetworkClient client
                )
        )
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không tìm thấy Client "
                + clientId
            );

            return;
        }

        if (client.PlayerObject == null)
            return;

        NetworkPlayerAction networkAction =
            client.PlayerObject.GetComponent<
                NetworkPlayerAction
            >();

        if (networkAction == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không tìm thấy NetworkPlayerAction."
            );

            return;
        }

        networkAction.SendActionResultToClient(
            success,
            message,
            clientId
        );
    }

    // =====================================================
    // SEER RESULT
    // =====================================================

    private void SendSeerResult(
        int requesterID,
        PlayerData target)
    {
        if (target == null)
            return;

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
            return;

        if (
            !networkManager.ConnectedClients.TryGetValue(
                (ulong)requesterID,
                out NetworkClient client
            )
        )
        {
            return;
        }

        if (client.PlayerObject == null)
            return;

        NetworkPlayerAction networkAction =
            client.PlayerObject.GetComponent<
                NetworkPlayerAction
            >();

        if (networkAction == null)
            return;

        RoleType effectiveRole = target.roleType;
        bool anyDogSpiritAlive = PlayerManager.Instance?.players != null &&
                                 PlayerManager.Instance.players.Exists(p => p != null && p.isAlive &&
                                     (p.roleType == RoleType.DogSpirit || p.roleType == RoleType.SerpentSpirit || p.roleType == RoleType.Ogre));

        if ((target.roleType == RoleType.WhiteHound || target.roleType == RoleType.WhiteWolf || target.roleType == RoleType.DogSpirit) &&
            (target.isWhiteHoundAwakened || target.faction == FactionType.Monster || bittenWhiteHoundPlayerIds.Contains(target.playerID) || !anyDogSpiritAlive))
        {
            effectiveRole = RoleType.DogSpirit;
        }

        networkAction.SendSeerResultToClient(
            target.playerID,
            target.playerName,
            effectiveRole,
            (ulong)requesterID
        );
    }

    // =====================================================
    // SHAMAN NOTIFICATION: DOGSPIRIT ATTACK
    // =====================================================

    private void NotifyShamanAboutDogSpiritTarget(int victimId)
    {
        if (RoleManager.Instance == null || RoleManager.Instance.playerRoles == null)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        int currentNight = 1;
        if (NetworkPhaseSync.Instance != null)
        {
            currentNight = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        }
        else if (GameRoleManager.Instance != null)
        {
            currentNight = GameRoleManager.Instance.currentDay;
        }

        if (currentNight == lastNotifiedDogSpiritNightCycle && victimId == lastNotifiedDogSpiritVictimId)
        {
            return;
        }

        PlayerData victim = PlayerManager.Instance?.GetplayerByID(victimId);
        string victimName = victim != null ? victim.playerName : $"Player {victimId + 1}";

        bool notifiedAny = false;

        foreach (var pair in RoleManager.Instance.playerRoles)
        {
            if (pair.Value is ShamanRole sr && pair.Value.owner != null && pair.Value.owner.isAlive && sr.HasGoodCharm)
            {
                ulong clientId = (ulong)pair.Key;
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
                    continue;

                if (client.PlayerObject == null)
                    continue;

                NetworkPlayerAction networkAction = client.PlayerObject.GetComponent<NetworkPlayerAction>();
                if (networkAction == null)
                    continue;

                Debug.Log($"ROLE ACTION BRIDGE | Gửi thông báo DogSpirit cắn {victimName} (ID {victimId}) riêng cho Shaman Client {clientId}.");
                networkAction.SendShamanVictimPromptToClient(victimId, victimName, clientId);
                notifiedAny = true;
            }
        }

        if (notifiedAny)
        {
            lastNotifiedDogSpiritNightCycle = currentNight;
            lastNotifiedDogSpiritVictimId = victimId;

            // Ghi log nếu thời gian còn lại ít hơn 8 giây (PhaseTimeRemaining là private set, không thể gán từ ngoài)
            if (GameRoleManager.Instance != null && GameRoleManager.Instance.PhaseTimeRemaining < 8f)
            {
                Debug.LogWarning($"ROLE ACTION BRIDGE | Thời gian đêm còn {GameRoleManager.Instance.PhaseTimeRemaining:F1}s < 8s. " +
                    "Shaman có thể không kịp phản hồi. Cân nhắc tăng nightDuration trong GameRoleManager.");
            }
        }
    }
}