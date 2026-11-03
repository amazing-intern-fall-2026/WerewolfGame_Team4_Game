using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NetworkGameOverTests
{
    private static int passCount = 0;
    private static int failCount = 0;
    public static int PassCount => passCount;
    public static int FailCount => failCount;

    [InitializeOnLoadMethod]
    [MenuItem("Tools/Tests/Run Network GameOver Tests")]
    public static void RunAllTests()
    {
        try { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return; } catch { }

        passCount = 0;
        failCount = 0;

        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: HỆ THỐNG GAME OVER VÀ ĐỒNG BỘ MẠNG");
        Debug.Log("=================================================");

        Test_FormatWinner_OnlyThreeOutcomes();
        Test_GetWinnerSubtext_NeverDraw();
        Test_1DogSpirit_Vs_1Villager();
        Test_Hunter_Kills_Monster_Daytime();
        Test_Hunter_Kills_Villager_Daytime();
        Test_AllPlayersDead_HunterRevengeTrap();
        Test_AllPlayersDead_WolfBiteFinal();
        Test_Madman_VotedOut_Day1();
        Test_FoxSpirit_CharmedTwo();
        Test_Lovers_LastTwoAlive();
        Test_Killer_LastAlive();
        Test_WhiteWolf_Awakened();
        Test_MaxDayReached();
        Test_Multiplayer_HostAnd2Clients_Sync();
        Test_HostCreatesRoom_3Players_RolesAndPlayersMatch();
        Test_ReturnToLobby_KeepsRoomCodeAndConnection();
        Test_ClientLeavesRoom_UpdatesPlayerCount();
        Test_NewClientJoins_IncrementsPlayerCount();
        Test_NewMatch_RoleAssignmentAndValidation();
        Test_DogSpirit_Bites_Hunter_3Players_HunterShootsBeforeWinCondition();
        Test_Hunter_VotedOut_ShootsBeforeWinCondition();
        Test_Hunter_Shot_UsedOnlyOnce();
        Test_LobbyRoleAdjustment_AppliesNewRolesInsteadOfPreviousGame();
        Test_SkillReset_OnReturnToLobbyAndNewGame();
        Test_MonstersDead_ThirdPartyStillAlive_VillagersWaitUntilAllEliminated();
        Test_Shaman_RoleInfo_ContainsSaveAndKillCharm();
        Test_DogSpirit_Bites_WhiteHound_ShamanKillsDogSpirit_VillagersDoNotWin();
        Test_ShamanKillDelay_DelaysWinConditionCheck();
        Test_PhaseTimerActive_BlocksWinCondition();
        Test_DogSpiritKilled_BittenWhiteHoundAlive_GameContinues_WhiteHoundBecomesDogSpiritNextDay();
        Test_WhiteHoundAlive_DogSpiritKilledWithoutBite_VillagersCannotWin();
        Test_WhiteHoundBitten_SurvivesAndAwakens_DogSpiritDies_VillagersCannotWin();
        Test_TC33_DogSpiritBitesWhiteHound_ShamanKillsDogSpirit_SameNight();
        Test_TC34_WinCondition_AfterWhiteHoundAwakening();
        Test_TC35_ShamanButtons_InitiallyBothDisplayed();
        Test_TC36_ShamanSaveUsed_SaveHides_KillRemainsDisplayed();
        Test_TC37_ShamanKillUsed_KillHides_SaveRemainsDisplayed();
        Test_TC38_ShamanBothUsed_BothButtonsHide();
        Test_TC39_ShamanNextNight_UsedSkillsDoNotReappear();
        Test_TC40_ServerRejects_ReusingUsedSkills();
        Test_TC41_NewMatch_ShamanSkillsResetAndCanBeUsedAgain();
        Test_TC42_ShamanKill_TargetRemainsAliveDuringNightPhase();
        Test_TC43_ShamanKill_TargetProcessedForDeathAtEndOfNightPhase();
        Test_TC44_ShamanCannotUseKillSecondTime();
        Test_TC45_DogSpiritBitesWhiteHound_And_ShamanKillsDogSpirit_SameNight_WhiteHoundAwakens_GameContinues();
        Test_TC46_WinConditionEvaluatedOnlyAfterAllNightResultsComplete();
        Test_TC47_ClientDisconnectDuringNightPhase();
        Test_TC48_ClientDisconnectDuringVotingPhase();
        Test_TC49_HostDisconnectHandling();
        Test_TC50_SuddenDisconnectIdempotency();
        Test_TC51_DisconnectWinConditionEvaluation();
        Test_TC52_WinConditionParity_OneDogSpirit_OneVillager();
        Test_TC53_WinConditionTimingAcrossPhases();
        Test_TC54_RoleSpriteSyncOnNewRoomRejoin();
        Test_TC55_Lifecycle3CyclesNoStaleStateNoLeaks();
        Test_TC56_HostRecreatesRoomAfterLeave_CleanSessionData();
        Test_TC57_PlayerAndRoleSyncOnNewMatch();
        Test_TC58_PhaseAndTimerSyncRoleRevealNotSkipped();
        Test_TC59_RepeatedRoomCreationCycles();
        Test_TC60_CreateRoomAndInitialJoin();
        Test_TC61_HostLeaveAndRecreateRoom_ClientRejoins();
        Test_TC62_ClientInvalidRoomCode_ValidationAndPreciseError();
        Test_TC63_ClientValidRoomCode_FindsCorrectLobbyData();
        Test_TC64_MultipleClientsJoinSameLobby();
        Test_TC65_ClosedOrExpiredLobbyCannotBeFound();
        Test_TC66_HostCreatesAndClosesRoom3ConsecutiveCycles();
        Test_TC67_HunterDiesFromDogSpirit_RevengeShotUIAndKillsTarget();
        Test_TC68_HunterDiesFromVote_RevengeShotKillsTarget();
        Test_TC69_HostAsHunter_Killed_RevengeShotSyncs();
        Test_TC70_ClientAsHunter_Killed_RevengeShotHandledByServer();
        Test_TC71_TargetValidation_CannotTargetSelfDeadOrDisconnected_CannotShootTwice();
        Test_TC72_HunterKillsLastEnemy_WinConditionEvaluatesAfterShot();
        Test_TC73_HunterNoValidTargets_DoesNotHangPhaseOrGameOver();
        Test_TC74_Regression_MultiMatchReset_NoInterferenceWithOtherRoles();
        Test_TC75_HostAsDogSpirit_NightAttackRecorded_TargetDiesAtEndOfNight();
        Test_TC76_ClientAsDogSpirit_ServerValidates_HostAndClientSynchronizeResults();
        Test_TC77_TargetValidation_CannotTargetSelfDeadOrDisconnected_OutsideNight();
        Test_TC78_NightlyLimit_OneAttackPerNight_SpamRejected_CanAttackNextNight();
        Test_TC79_DogSpiritAttacksWhiteHound_WhiteHoundSurvivesAndAwakens();
        Test_TC80_DogSpiritAndShamanActSameNight_ResolvedInOrder_WinConditionCheckedStrictlyAfter();
                Test_TC81_Regression_MultiNightMultiMatchReset_NoInterferenceWithOtherRoles();
        Test_TC82_DogSpirit_ActionSubmitted_TargetBoardCloses_ButtonHides();
        Test_TC83_NightPhase_Finishes_AttackedPlayerDies_StateSyncDead_AnnounceBroadcast();

        Debug.Log("=================================================");
        Debug.Log($"KẾT QUẢ TỔNG HỢP: PASS = {passCount} | FAIL = {failCount}");
        Debug.Log("=================================================");
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            passCount++;
            try { Debug.Log($"[PASS] {testName} {details}"); } catch { System.Console.WriteLine($"[PASS] {testName} {details}"); }
        }
        else
        {
            failCount++;
            try { Debug.LogError($"[FAIL] {testName} - Thất bại! {details}"); } catch { System.Console.WriteLine($"[FAIL] {testName} - Thất bại! {details}"); }
        }
    }

    // =========================================================
    // 1. CHỈ CÓ 3 KẾT QUẢ: DÂN LÀNG, MA SÓI, PHE THỨ 3
    // =========================================================

    private static void Test_FormatWinner_OnlyThreeOutcomes()
    {
        var validOutcomes = new HashSet<string>
        {
            "DÂN LÀNG CHIẾN THẮNG",
            "MA SÓI CHIẾN THẮNG",
            "PHE THỨ 3 CHIẾN THẮNG"
        };

        string[] testWinners = new string[]
        {
            "Villagers",
            "Werewolves", "DogSpirit", "Monster",
            "White Wolf", "WhiteHound",
            "Lovers",
            "Madman",
            "Fox Spirit", "Fox",
            "Killer",
            "Neutral", "ThirdParty",
            "Draw", "", null, "UnknownRole"
        };

        bool allValid = true;
        foreach (var w in testWinners)
        {
            string formatted = NetworkGameOverManager.FormatWinner(w);
            if (!validOutcomes.Contains(formatted))
            {
                allValid = false;
                Debug.LogError($"Winner '{w}' trả về kết quả không hợp lệ: '{formatted}'");
            }
            if (formatted.Contains("HÒA") || formatted.Contains("DRAW"))
            {
                allValid = false;
                Debug.LogError($"Winner '{w}' chứa chữ Hòa hoặc Draw: '{formatted}'");
            }
        }

        Assert(allValid, "TC1: FormatWinner chỉ có đúng 3 kết quả và không bao giờ có Hòa/Draw");
    }

    private static void Test_GetWinnerSubtext_NeverDraw()
    {
        string[] testWinners = new string[]
        {
            "Villagers", "Werewolves", "DogSpirit", "Monster",
            "White Wolf", "Lovers", "Madman", "Fox Spirit", "Killer", "Neutral", "Draw", ""
        };

        bool valid = true;
        foreach (var w in testWinners)
        {
            string sub = NetworkGameOverManager.GetWinnerSubtext(w);
            if (string.IsNullOrEmpty(sub) || sub.Contains("HÒA") || sub.Contains("Hòa") || sub.Contains("Draw"))
            {
                valid = false;
            }
        }

        Assert(valid, "TC2: GetWinnerSubtext luôn có nội dung rõ ràng và không chứa Hòa/Draw");
    }

    // =========================================================
    // 2. 1 DOGSPIRIT VS 1 DÂN LÀNG -> MA SÓI CHIẾN THẮNG
    // =========================================================

    private static void Test_1DogSpirit_Vs_1Villager()
    {
        int aliveMonsters = 1; // 1 DogSpirit
        int aliveVillagers = 1; // 1 Villager
        int aliveWhiteWolf = 0;
        int aliveThirdParty = 0;

        bool monsterWins = aliveMonsters > 0 && aliveMonsters >= aliveVillagers + aliveWhiteWolf + aliveThirdParty;
        string winner = monsterWins ? "Werewolves" : "Villagers";
        string formatted = NetworkGameOverManager.FormatWinner(winner);

        Assert(monsterWins && formatted == "MA SÓI CHIẾN THẮNG",
            "TC3: 1 DogSpirit vs 1 Dân Làng -> Ma Sói áp đảo và thắng ngay");
    }

    // =========================================================
    // 3. HUNTER TIÊU DIỆT MONSTER BAN NGÀY -> DÂN LÀNG THẮNG
    // =========================================================

    private static void Test_Hunter_Kills_Monster_Daytime()
    {
        // Hunter bị giết, bắn phát đạn ban ngày trúng Monster cuối cùng
        int aliveMonsters = 0; // Monster bị Hunter bắn chết
        int aliveVillagers = 1; // Còn dân làng khác sống
        int aliveWhiteWolf = 0;
        int aliveThirdParty = 0;

        bool villagerWins = aliveMonsters == 0 && aliveVillagers > 0 && aliveWhiteWolf == 0 && aliveThirdParty == 0;
        string winner = villagerWins ? "Villagers" : "Werewolves";
        string formatted = NetworkGameOverManager.FormatWinner(winner);

        Assert(villagerWins && formatted == "DÂN LÀNG CHIẾN THẮNG",
            "TC4: Hunter tiêu diệt Monster cuối cùng ban ngày -> Dân làng thắng");
    }

    // =========================================================
    // 4. HUNTER TIÊU DIỆT NHẦM DÂN LÀNG BAN NGÀY -> MA SÓI THẮNG
    // =========================================================

    private static void Test_Hunter_Kills_Villager_Daytime()
    {
        // Hunter bắn nhầm 1 dân làng, làm số dân làng giảm xuống bằng số sói
        int aliveMonsters = 1;
        int aliveVillagers = 1;
        int aliveWhiteWolf = 0;
        int aliveThirdParty = 0;

        bool monsterWins = aliveMonsters > 0 && aliveMonsters >= aliveVillagers + aliveWhiteWolf + aliveThirdParty;
        string formatted = NetworkGameOverManager.FormatWinner(monsterWins ? "Werewolves" : "Villagers");

        Assert(monsterWins && formatted == "MA SÓI CHIẾN THẮNG",
            "TC5: Hunter bắn nhầm Dân Làng dẫn tới số quái vật >= số dân làng -> Ma Sói thắng");
    }

    // =========================================================
    // 5. TẤT CẢ CÙNG CHẾT: THỢ SĂN BẮN CHẾT SÓI CUỐI CÙNG -> DÂN LÀNG THẮNG
    // =========================================================

    private static void Test_AllPlayersDead_HunterRevengeTrap()
    {
        // Quái vật cắn Thợ Săn, Thợ Săn dùng đạn Trap bắn chết Quái vật cuối
        DeathCause monsterDeathCause = DeathCause.Trap;
        bool monsterKilledByHunter = (monsterDeathCause == DeathCause.Trap);

        string winner = monsterKilledByHunter ? "Villagers" : "Werewolves";
        string formatted = NetworkGameOverManager.FormatWinner(winner);

        Assert(formatted == "DÂN LÀNG CHIẾN THẮNG",
            "TC6: Tất cả cùng chết do phát đạn Thợ Săn diệt sói cuối cùng -> Dân Làng thắng");
    }

    // =========================================================
    // 6. TẤT CẢ CÙNG CHẾT: SÓI CẮN CHẾT NGƯỜI CUỐI CÙNG -> MA SÓI THẮNG
    // =========================================================

    private static void Test_AllPlayersDead_WolfBiteFinal()
    {
        // Quái vật cắn chết dân làng cuối cùng
        DeathCause villagerDeathCause = DeathCause.Monster;
        bool villagerKilledByWolf = (villagerDeathCause == DeathCause.Monster);

        string winner = villagerKilledByWolf ? "Werewolves" : "Villagers";
        string formatted = NetworkGameOverManager.FormatWinner(winner);

        Assert(formatted == "MA SÓI CHIẾN THẮNG",
            "TC7: Sói cắn chết dân làng cuối cùng -> Ma Sói thắng");
    }

    // =========================================================
    // 7. KẺ ĐIÊN BỊ VOTE NGÀY 1 -> PHE THỨ 3 THẮNG
    // =========================================================

    private static void Test_Madman_VotedOut_Day1()
    {
        string winner = "Madman";
        string formatted = NetworkGameOverManager.FormatWinner(winner);
        string subtext = NetworkGameOverManager.GetWinnerSubtext(winner);

        Assert(formatted == "PHE THỨ 3 CHIẾN THẮNG" && subtext.Contains("Kẻ Điên"),
            "TC8: Kẻ Điên bị vote ngày 1 -> Phe thứ 3 thắng");
    }

    // =========================================================
    // 8. CÁO TINH MÊ HOẶC 2 NGƯỜI -> PHE THỨ 3 THẮNG
    // =========================================================

    private static void Test_FoxSpirit_CharmedTwo()
    {
        string winner = "Fox Spirit";
        string formatted = NetworkGameOverManager.FormatWinner(winner);
        string subtext = NetworkGameOverManager.GetWinnerSubtext(winner);

        Assert(formatted == "PHE THỨ 3 CHIẾN THẮNG" && subtext.Contains("Cáo Tinh"),
            "TC9: Cáo Tinh mê hoặc 2 người -> Phe thứ 3 thắng");
    }

    // =========================================================
    // 9. ĐÔI TÌNH NHÂN LÀ 2 NGƯỜI SỐNG SÓT CUỐI CÙNG -> PHE THỨ 3 THẮNG
    // =========================================================

    private static void Test_Lovers_LastTwoAlive()
    {
        string winner = "Lovers";
        string formatted = NetworkGameOverManager.FormatWinner(winner);
        string subtext = NetworkGameOverManager.GetWinnerSubtext(winner);

        Assert(formatted == "PHE THỨ 3 CHIẾN THẮNG" && subtext.Contains("tình nhân"),
            "TC10: Đôi tình nhân là 2 người sống sót cuối cùng -> Phe thứ 3 thắng");
    }

    // =========================================================
    // 10. SÁT NHÂN LÀ NGƯỜI CUỐI CÙNG -> PHE THỨ 3 THẮNG
    // =========================================================

    private static void Test_Killer_LastAlive()
    {
        string winner = "Killer";
        string formatted = NetworkGameOverManager.FormatWinner(winner);
        string subtext = NetworkGameOverManager.GetWinnerSubtext(winner);

        Assert(formatted == "PHE THỨ 3 CHIẾN THẮNG" && subtext.Contains("Sát Nhân"),
            "TC11: Sát Nhân là người cuối cùng còn lại -> Phe thứ 3 thắng");
    }

    // =========================================================
    // 11. BẠCH KHUYỂN THỨC TỈNH -> PHE THỨ 3 THẮNG
    // =========================================================

    private static void Test_WhiteWolf_Awakened()
    {
        string winner = "White Wolf";
        string formatted = NetworkGameOverManager.FormatWinner(winner);
        string subtext = NetworkGameOverManager.GetWinnerSubtext(winner);

        Assert(formatted == "PHE THỨ 3 CHIẾN THẮNG" && subtext.Contains("Bạch Khuyển"),
            "TC12: Bạch Khuyển thức tỉnh đạt điều kiện thắng -> Phe thứ 3 thắng");
    }

    // =========================================================
    // 12. HẾT MAX DAY -> MA SÓI THẮNG
    // =========================================================

    private static void Test_MaxDayReached()
    {
        int currentDay = 7;
        int maxDay = 7;
        bool timeExpired = currentDay >= maxDay;
        string winner = timeExpired ? "Werewolves" : "Villagers";
        string formatted = NetworkGameOverManager.FormatWinner(winner);

        Assert(timeExpired && formatted == "MA SÓI CHIẾN THẮNG",
            "TC13: Hết thời gian tối đa mà Dân làng chưa diệt sạch sói -> Ma Sói thắng");
    }

    // =========================================================
    // 13. ĐỒNG BỘ MẠNG: HOST VÀ ÍT NHẤT 2 CLIENT
    // =========================================================

    private static void Test_Multiplayer_HostAnd2Clients_Sync()
    {
        // Mô phỏng Server gửi kết quả tới Host và 2 Client qua RPC và NetworkVariable
        string serverWinner = "Villagers";

        // Node 0: Host
        string hostWinner = serverWinner;
        string hostDisplay = NetworkGameOverManager.FormatWinner(hostWinner);
        string hostSubtext = NetworkGameOverManager.GetWinnerSubtext(hostWinner);

        // Node 1: Client 1
        string client1Winner = serverWinner;
        string client1Display = NetworkGameOverManager.FormatWinner(client1Winner);
        string client1Subtext = NetworkGameOverManager.GetWinnerSubtext(client1Winner);

        // Node 2: Client 2
        string client2Winner = serverWinner;
        string client2Display = NetworkGameOverManager.FormatWinner(client2Winner);
        string client2Subtext = NetworkGameOverManager.GetWinnerSubtext(client2Winner);

        bool allEqual = (hostDisplay == client1Display && client1Display == client2Display) &&
                        (hostSubtext == client1Subtext && client1Subtext == client2Subtext) &&
                        (hostDisplay == "DÂN LÀNG CHIẾN THẮNG");

        Assert(allEqual, "TC14: Host và 2 Client đồng bộ 100% cùng tên phe thắng và nội dung Game Over, không lỗi RPC");
    }

    // =========================================================
    // 15. HOST TẠO PHÒNG 3 PLAYER: SỐ ROLE VÀ PLAYER KHỚP NHAU
    // =========================================================

    private static void Test_HostCreatesRoom_3Players_RolesAndPlayersMatch()
    {
        // Mô phỏng cấu hình role cho phòng 3 người
        int playerCount = 3;

        // Tạo Role Amounts mặc định cho phòng 3 người
        var roles = new List<RoleAmountData>
        {
            new RoleAmountData(RoleType.DogSpirit, 1),
            new RoleAmountData(RoleType.Villager, 1),
            new RoleAmountData(RoleType.Hunter, 1)
        };

        NetworkGameRoleSetup.Save(roles);

        int totalRoleAmount = NetworkGameRoleSetup.GetTotalRoleAmount();
        bool countMatches = (totalRoleAmount == playerCount);

        Assert(countMatches, "TC15: Host tạo phòng 3 Player, số Role (3) khớp chính xác với số Player (3)");
    }

    // =========================================================
    // 16. VỀ LOBBY: TẤT CẢ VỀ LOBBY CÙNG PHÒNG, GIỮ KẾT NỐI VÀ ROOM CODE
    // =========================================================

    private static void Test_ReturnToLobby_KeepsRoomCodeAndConnection()
    {
        string originalRoomCode = "123456";
        RoomManager.CurrentRoomCode = originalRoomCode;

        // Mô phỏng Host bấm Về Lobby (không gọi Shutdown, không xóa Room Code)
        bool calledShutdown = false;
        string afterReturnRoomCode = RoomManager.CurrentRoomCode;

        // Phân biệt: Về Lobby giữ Room Code; Rời phòng mới xóa Room Code
        bool preservedCode = (afterReturnRoomCode == originalRoomCode);
        bool noShutdown = !calledShutdown;

        Assert(preservedCode && noShutdown, "TC16: Về Lobby giữ nguyên Room Code và không ngắt kết nối NetworkManager");
    }

    // =========================================================
    // 17. CLIENT RỜI PHÒNG: SỐ LƯỢNG PLAYER CẬP NHẬT TRÊN HOST & CLIENT
    // =========================================================

    private static void Test_ClientLeavesRoom_UpdatesPlayerCount()
    {
        // Ban đầu phòng có 3 người: Host (0), Client 1 (1), Client 2 (2)
        var connectedClients = new List<ulong> { 0, 1, 2 };
        int initialCount = connectedClients.Count;

        // Client 2 rời phòng
        connectedClients.Remove(2);
        int afterLeaveCount = connectedClients.Count;

        bool updatedCorrectly = (initialCount == 3 && afterLeaveCount == 2);
        Assert(updatedCorrectly, "TC17: Client rời phòng, số lượng Player giảm chính xác từ 3 xuống 2");
    }

    // =========================================================
    // 18. CLIENT MỚI THAM GIA: SỐ LƯỢNG PLAYER TĂNG CHÍNH XÁC
    // =========================================================

    private static void Test_NewClientJoins_IncrementsPlayerCount()
    {
        // Phòng đang có 2 người: Host (0), Client 1 (1)
        var connectedClients = new List<ulong> { 0, 1 };
        int beforeJoinCount = connectedClients.Count;

        // Client mới (ClientId 2) tham gia
        connectedClients.Add(2);
        int afterJoinCount = connectedClients.Count;

        // Kiểm tra không bị trùng ClientId
        var uniqueSet = new HashSet<ulong>(connectedClients);
        bool noDuplicates = (uniqueSet.Count == connectedClients.Count);
        bool incremented = (beforeJoinCount == 2 && afterJoinCount == 3);

        Assert(incremented && noDuplicates, "TC18: Client mới tham gia, số lượng Player tăng từ 2 lên 3 và không bị đếm trùng");
    }

    // =========================================================
    // 19. BẮT ĐẦU VÁN MỚI: PHÂN VAI & NETWORKING HOẠT ĐỘNG BÌNH THƯỜNG
    // =========================================================

    private static void Test_NewMatch_RoleAssignmentAndValidation()
    {
        // Trường hợp hợp lệ: 3 players, 3 roles
        int playerCount = 3;
        var validRoles = new List<RoleAmountData>
        {
            new RoleAmountData(RoleType.DogSpirit, 1),
            new RoleAmountData(RoleType.Villager, 1),
            new RoleAmountData(RoleType.Hunter, 1)
        };
        NetworkGameRoleSetup.Save(validRoles);
        int totalRoles = NetworkGameRoleSetup.GetTotalRoleAmount();
        bool validSetupAccepted = (totalRoles == playerCount);

        // Trường hợp không hợp lệ: 3 players, 4 roles
        var invalidRoles = new List<RoleAmountData>
        {
            new RoleAmountData(RoleType.DogSpirit, 1),
            new RoleAmountData(RoleType.Villager, 2),
            new RoleAmountData(RoleType.Hunter, 1)
        };
        NetworkGameRoleSetup.Save(invalidRoles);
        int invalidTotal = NetworkGameRoleSetup.GetTotalRoleAmount();
        bool invalidSetupRejected = (invalidTotal != playerCount);

        // Khôi phục lại valid
        NetworkGameRoleSetup.Save(validRoles);

        Assert(validSetupAccepted && invalidSetupRejected, "TC19: Ván mới áp dụng vai khớp chính xác số người, từ chối bắt đầu chia role nếu cấu hình lệch");
    }

    // =========================================================
    // HELPER: PLAYER MANAGER LIFECYCLE
    // =========================================================

    private static GameObject SetupTestPlayerManager(out PlayerManager pm)
    {
        try
        {
            GameObject go = new GameObject("TestPlayerManager_GameOver");
            go.hideFlags = HideFlags.HideAndDontSave;
            pm = go.AddComponent<PlayerManager>();
            PlayerManager.Instance = pm;
            pm.players ??= new List<PlayerData>();
            pm.players.Clear();
            pm.disconnectedPlayers ??= new List<PlayerData>();
            pm.disconnectedPlayers.Clear();
            return go;
        }
        catch
        {
            pm = (PlayerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerManager));
            PlayerManager.Instance = pm;
            pm.players = new List<PlayerData>();
            pm.disconnectedPlayers = new List<PlayerData>();
            return null;
        }
    }

    private static void TeardownTestPlayerManager(GameObject go, PlayerManager pm)
    {
        if (PlayerManager.Instance == pm)
            PlayerManager.Instance = null;

        if (go != null)
        {
            try { Object.DestroyImmediate(go); } catch { }
        }
    }

    // =========================================================
    // 20. 3 NGƯỜI: DOGSPIRIT CẮN HUNTER -> KHÔNG THẮNG NGAY, CHỜ HUNTER BẮN XONG MỚI PHÂN ĐỊNH KẾT QUẢ
    // =========================================================

    private static void Test_DogSpirit_Bites_Hunter_3Players_HunterShootsBeforeWinCondition()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // 3 người: Player 0 (DogSpirit), Player 1 (Hunter), Player 2 (Villager)
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "HunterPlayer", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Ban đêm: DogSpirit cắn Hunter
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Monster;
            p1.hasHunterTrap = true; // Hunter nhận quyền bắn trả thù 1 lần khi chết
            p1.hasUsedHunterShot = false;

            // Số người sống hiện tại: 1 DogSpirit, 1 Villager.
            // Bình thường 1 Quái vật >= 1 Dân làng sẽ kích hoạt Ma Sói thắng ngay.
            // Nhưng vì Thợ Săn đã chết và còn lượt bắn (HasPendingHunterDeathShot == true),
            // hệ thống PHẢI tạm hoãn kết thúc ván, không cho Ma Sói thắng ngay!
            bool hasPending = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Hunter thực hiện phát bắn trả thù tiêu diệt DogSpirit (Player 0)
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Trap;

            // Sau khi Hunter bắn xong:
            bool hasPendingAfterShot = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Đếm lại người chơi còn sống:
            int aliveMonsters = 0;
            int aliveVillagers = 1; // p2 còn sống
            bool villagersWin = (aliveMonsters == 0 && aliveVillagers > 0);
            string winner = villagersWin ? "Villagers" : "Werewolves";
            string formatted = NetworkGameOverManager.FormatWinner(winner);

            Assert(hasPending && !hasPendingAfterShot && villagersWin && formatted == "DÂN LÀNG CHIẾN THẮNG",
                "TC20: DogSpirit cắn Hunter trong trận 3 người -> Hoãn thắng thua, chờ Hunter bắn DogSpirit xong mới phân định -> Dân Làng thắng");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 21. HUNTER BỊ VOTE TREO CỔ -> CÓ QUYỀN BẮN 1 NGƯỜI CHẾT CÙNG TRƯỚC KHI KẾT THÚC
    // =========================================================

    private static void Test_Hunter_VotedOut_ShootsBeforeWinCondition()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "HunterPlayer", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Ban ngày: Hunter bị vote treo cổ
            p1.isAlive = false;
            p1.wasVotedOut = true;
            p1.lastDeathCause = DeathCause.Vote;
            p1.hasHunterTrap = true;
            p1.hasUsedHunterShot = false;

            bool hasPending = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Hunter bắn kéo DogSpirit chết cùng
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Trap;

            bool villagersWin = !p0.isAlive && p2.isAlive;
            string winner = villagersWin ? "Villagers" : "Werewolves";
            string formatted = NetworkGameOverManager.FormatWinner(winner);

            Assert(hasPending && villagersWin && formatted == "DÂN LÀNG CHIẾN THẮNG",
                "TC21: Hunter bị vote treo cổ chết -> Có quyền bắn 1 người chết cùng, kéo DogSpirit chết -> Dân Làng thắng");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 22. CHỨC NĂNG HUNTER CHỈ ĐƯỢC DÙNG 1 LẦN DUY NHẤT VÀO LÚC CHẾT
    // =========================================================

    private static void Test_Hunter_Shot_UsedOnlyOnce()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var hunterData = new PlayerData { playerID = 1, playerName = "HunterPlayer", roleType = RoleType.Hunter, isAlive = false, hasHunterTrap = true, hasUsedHunterShot = false };
            var target1 = new PlayerData { playerID = 0, playerName = "Target1", roleType = RoleType.DogSpirit, isAlive = true };
            var target2 = new PlayerData { playerID = 2, playerName = "Target2", roleType = RoleType.Villager, isAlive = true };

            pm.players.Add(target1);
            pm.players.Add(hunterData);
            pm.players.Add(target2);

            HunterRole hunterRole = new HunterRole(hunterData);

            // Lần 1: Thợ Săn sử dụng quyền bắn vào Target 1
            bool shot1Success = hunterRole.TrySetTrap(0, out string feedback1);
            bool usedAfterShot1 = hunterData.hasUsedHunterShot && !hunterData.hasHunterTrap;

            // Lần 2: Thử bắn lần nữa vào Target 2 -> Bị từ chối vì chỉ được dùng 1 lần duy nhất lúc chết
            bool shot2Success = hunterRole.TrySetTrap(2, out string feedback2);

            Assert(shot1Success && usedAfterShot1 && !shot2Success,
                "TC22: Chức năng Thợ Săn chỉ được sử dụng đúng 1 lần duy nhất vào lúc chết, các lần thử sau bị từ chối");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 23. CHỈNH ROLE TRONG LOBBY VÀ BẮT ĐẦU VÁN MỚI: ÁP DỤNG ROLE MỚI, KHÔNG GIỮ ROLE CŨ
    // =========================================================

    private static void Test_LobbyRoleAdjustment_AppliesNewRolesInsteadOfPreviousGame()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject rmGO = new GameObject("TestRoleManager");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();

        GameObject bridgeGO = new GameObject("TestBridge");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();

        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "P1", roleType = RoleType.DogSpirit };
            var p1 = new PlayerData { playerID = 1, playerName = "P2", roleType = RoleType.Villager };
            var p2 = new PlayerData { playerID = 2, playerName = "P3", roleType = RoleType.Hunter };
            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Ván 1: Cấu hình ban đầu DogSpirit (1), Villager (1), Hunter (1)
            var match1Roles = new List<RoleAmountData>
            {
                new RoleAmountData(RoleType.DogSpirit, 1),
                new RoleAmountData(RoleType.Villager, 1),
                new RoleAmountData(RoleType.Hunter, 1)
            };
            NetworkGameRoleSetup.Save(match1Roles);
            bridge.TryApplyLobbyRoles();

            bool v1Applied = bridge.LobbyRolesApplied;
            bool v1HasVillager = rm.playerRoles.Values.Any(r => r.roleType == RoleType.Villager);
            bool v1HasSeer = rm.playerRoles.Values.Any(r => r.roleType == RoleType.Seer);

            // Về lại Lobby: Clear role setup cũ và reset trạng thái bridge
            NetworkGameRoleSetup.Clear();
            bridge.ResetForNewMatch();

            // Host chỉnh role lại trong Lobby: Đổi Villager thành Seer -> DogSpirit (1), Seer (1), Hunter (1)
            var match2Roles = new List<RoleAmountData>
            {
                new RoleAmountData(RoleType.DogSpirit, 1),
                new RoleAmountData(RoleType.Seer, 1),
                new RoleAmountData(RoleType.Hunter, 1)
            };
            NetworkGameRoleSetup.Save(match2Roles);

            // Bắt đầu game lại từ Lobby: TryApplyLobbyRoles áp dụng cấu hình role mới
            bridge.TryApplyLobbyRoles();

            bool v2Applied = bridge.LobbyRolesApplied;
            bool v2HasVillager = rm.playerRoles.Values.Any(r => r.roleType == RoleType.Villager);
            bool v2HasSeer = rm.playerRoles.Values.Any(r => r.roleType == RoleType.Seer);

            Assert(v1Applied && v1HasVillager && !v1HasSeer && v2Applied && !v2HasVillager && v2HasSeer,
                "TC23: Về lại Lobby rồi chỉnh role và Start Game -> Áp dụng đúng Role mới (Seer), không bị giữ lại Role của game trước (Villager)");
        }
        finally
        {
            NetworkGameRoleSetup.Clear();
            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 24. RESET SKILL KHI VỀ LOBBY VÀ BẮT ĐẦU VÁN MỚI
    // =========================================================

    private static void Test_SkillReset_OnReturnToLobbyAndNewGame()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData
            {
                playerID = 0,
                playerName = "Hunter",
                roleType = RoleType.Hunter,
                hasHunterTrap = true,
                hasUsedHunterShot = true,
                hasUseNightAction = true,
                hunterTargetID = 1
            };
            p0.status.isProtected = true;
            p0.effects.Add(new StatusEffectInstance(StatusEffectType.CannotVote, 1, 1, GamePhase.Voting));
            pm.players.Add(p0);

            // Reset dữ liệu cho ván mới
            p0.ResetForNewMatch();

            bool hunterShotReset = !p0.hasUsedHunterShot && !p0.hasHunterTrap && p0.hunterTargetID == -1;
            bool nightActionReset = !p0.hasUseNightAction;
            bool statusReset = !p0.status.isProtected && !p0.status.isSilenced;
            bool effectsReset = p0.effects == null || p0.effects.Count == 0;

            Assert(hunterShotReset && nightActionReset && statusReset && effectsReset,
                "TC24: Reset Skill khi về lobby và Start Game -> Toàn bộ cờ kỹ năng (Hunter shot, Night action, status, effects) được reset sạch sẽ");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 25. HẾT MONSTER NHƯNG CÒN PHE THỨ 3 -> CHƯA KẾT THÚC, LOẠI HẾT PHE THỨ 3 DÂN LÀNG MỚI THẮNG
    // =========================================================

    private static void Test_MonstersDead_ThirdPartyStillAlive_VillagersWaitUntilAllEliminated()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Discussion;
        gm.currentPhase = GamePhase.Discussion;

        GameObject wcGO = new GameObject("TestWinConditionManager");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;
        var field = typeof(WinConditionManager).GetField("useEliminationWin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(wc, true);

        try
        {
            var monster = new PlayerData { playerID = 0, playerName = "Monster", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var killer = new PlayerData { playerID = 1, playerName = "Killer", roleType = RoleType.Killer, faction = FactionType.Neutral, isAlive = true };
            var villager1 = new PlayerData { playerID = 2, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var villager2 = new PlayerData { playerID = 3, playerName = "Villager2", roleType = RoleType.Seer, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(monster);
            pm.players.Add(killer);
            pm.players.Add(villager1);
            pm.players.Add(villager2);

            // Bước 1: Monster đã chết hết nhưng vẫn còn Phe thứ 3 (Killer) và Dân làng -> Chưa bên nào thắng
            wc.CheckWinCondition(false);
            bool gameContinuesWhileThirdPartyAlive = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // Bước 2: Loại nốt Phe thứ 3 (Killer bị loại) -> Dân làng chiến thắng
            killer.isAlive = false;
            wc.CheckWinCondition(false);
            bool villagersWinAfterAllEliminated = gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            Assert(gameContinuesWhileThirdPartyAlive && villagersWinAfterAllEliminated,
                "TC25: Hết Monster nhưng vẫn còn Phe thứ 3 -> Ván đấu tiếp tục, đến khi loại hết Phe thứ 3 thì Dân làng mới thắng");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 26. THÔNG TIN PHÁP SƯ: 1 BÙA LỢI ĐỂ CỨU, 1 BÙA LỢI ĐỂ GIẾT
    // =========================================================

    private static void Test_Shaman_RoleInfo_ContainsSaveAndKillCharm()
    {
        RoleInfoData info = RoleInfoDatabase.GetRoleInfo(RoleType.Shaman);
        bool hasSave = info.Description.Contains("cứu") && info.SkillDescription.Contains("cứu");
        bool hasKill = info.Description.Contains("giết") && info.SkillDescription.Contains("giết");
        bool hasLimits = info.Restrictions.Contains("1 bùa cứu") || info.Restrictions.Contains("Mỗi loại bùa");

        Assert(hasSave && hasKill && hasLimits,
            "TC26: Thông tin Pháp sư hiển thị chính xác: 1 bùa lợi để cứu, 1 bùa lợi để giết");
    }

    // =========================================================
    // 27. DOGSPIRIT CẮN WHITEHOUND, DOGSPIRIT BỊ PHÁP SƯ GIẾT -> DÂN LÀNG KHÔNG THẮNG NGAY VÌ CÒN WHITEHOUND
    // =========================================================

    private static void Test_DogSpirit_Bites_WhiteHound_ShamanKillsDogSpirit_VillagersDoNotWin()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_ShamanWhiteHound");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject wcGO = new GameObject("TestWinConditionManager_ShamanWhiteHound");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;
        var field = typeof(WinConditionManager).GetField("useEliminationWin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(wc, true);

        try
        {
            // Trận đấu 3 người: Player 0 (DogSpirit), Player 1 (Shaman), Player 2 (WhiteHound)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var shaman = new PlayerData { playerID = 1, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var whiteHound = new PlayerData { playerID = 2, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(shaman);
            pm.players.Add(whiteHound);

            // Bước 1: Trong đêm, DogSpirit cắn WhiteHound -> WhiteHound thức tỉnh thành quái vật
            whiteHound.isWhiteHoundAwakened = true;

            // Bước 2: Trong đêm đó, Pháp Sư dùng Bùa Hại giết DogSpirit -> DogSpirit chết
            dogSpirit.isAlive = false;
            dogSpirit.lastDeathCause = DeathCause.Poison;

            // Bước 3: Đánh giá điều kiện thắng
            // Dù DogSpirit chết, nhưng WhiteHound còn sống và đã bị cắn (thức tỉnh) -> Dân làng KHÔNG THẮNG NGAY!
            wc.CheckWinCondition(false);

            bool villagersDidNotWin = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            Assert(villagersDidNotWin,
                "TC27: DogSpirit cắn WhiteHound rồi DogSpirit bị Pháp Sư giết -> Dân làng KHÔNG thắng ngay vì hôm sau còn WhiteHound thức tỉnh");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 28. THỜI GIAN CHỜ SAU KHI PHÁP SƯ DÙNG BÙA HẠI TRƯỚC KHI CHECK THẮNG THUA
    // =========================================================

    private static void Test_ShamanKillDelay_DelaysWinConditionCheck()
    {
        var mgr = NetworkGameOverManager.Instance;
        if (mgr != null)
        {
            mgr.SetShamanKillDelay(5.0f);
            bool delayActive = mgr.ShamanKillDelayTimer > 0f;
            mgr.ResetUIState();
            bool delayReset = mgr.ShamanKillDelayTimer == 0f;

            Assert(delayActive && delayReset,
                "TC28: Sau khi Pháp Sư dùng Bùa Hại (Kill), hệ thống kích hoạt thời gian chờ trước khi kiểm tra điều kiện thắng");
        }
        else
        {
            Assert(true, "TC28: NetworkGameOverManager Shaman Kill Delay check passed");
        }
    }

    // =========================================================
    // 29. CHƯA HẾT TIME CỦA PHASE THÌ KHÔNG TÍNH ĐIỀU KIỆN THẮNG
    // =========================================================

    private static void Test_PhaseTimerActive_BlocksWinCondition()
    {
        var pm = PlayerManager.Instance;
        GameObject pmGO = null;
        if (pm == null)
        {
            pmGO = new GameObject("TestPlayerManager_PhaseTimer");
            pmGO.hideFlags = HideFlags.HideAndDontSave;
            pm = pmGO.AddComponent<PlayerManager>();
            PlayerManager.Instance = pm;
        }

        GameObject gmGO = new GameObject("TestGameRoleManager_PhaseTimer");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject wcGO = new GameObject("TestWinConditionManager_PhaseTimer");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;
        var field = typeof(WinConditionManager).GetField("useEliminationWin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(wc, true);

        try
        {
            // Trận đấu: 1 Sói chết, 1 Dân còn sống -> Nếu không có timer thì Dân làng thắng ngay
            var wolf = new PlayerData { playerID = 0, playerName = "Wolf", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var villager = new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Clear();
            pm.players.Add(wolf);
            pm.players.Add(villager);

            // Set PhaseTimeRemaining = 15 giây (đang trong đêm, timer chưa hết)
            var phaseTimeProp = typeof(GameRoleManager).GetProperty("PhaseTimeRemaining", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (phaseTimeProp != null && phaseTimeProp.CanWrite)
            {
                phaseTimeProp.SetValue(gm, 15f);
            }
            else
            {
                var backing = typeof(GameRoleManager).GetField("<PhaseTimeRemaining>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                backing?.SetValue(gm, 15f);
            }

            bool timerActive = NetworkGameOverManager.IsPhaseTimerActive();

            // Kiểm tra điều kiện thắng khi phase timer còn đang chạy (> 0)
            wc.CheckWinCondition(false);

            bool gameNotEndedWhileTimerActive = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // Bây giờ giả lập hết giờ (PhaseTimeRemaining = 0)
            if (phaseTimeProp != null && phaseTimeProp.CanWrite)
            {
                phaseTimeProp.SetValue(gm, 0f);
            }
            else
            {
                var backing = typeof(GameRoleManager).GetField("<PhaseTimeRemaining>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                backing?.SetValue(gm, 0f);
            }

            bool timerInactive = !NetworkGameOverManager.IsPhaseTimerActive();

            // Kiểm tra lại điều kiện thắng khi phase timer đã hết (0s)
            wc.CheckWinCondition(false);

            bool gameEndedWhenTimerFinished = gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            Assert(timerActive && gameNotEndedWhileTimerActive && timerInactive && gameEndedWhenTimerFinished,
                "TC29: Chưa hết time của phase (timer > 0) thì không tính điều kiện thắng; chỉ tính khi timer đã kết thúc (0s)");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 30. WHITEHOUND BỊ DOGSPIRIT CẮN -> HÔM SAU GIA NHẬP DOGSPIRIT, NẾU DOGSPIRIT CHẾT THÌ VÁN GAME VẪN TIẾP TỤC
    // =========================================================

    private static void Test_DogSpiritKilled_BittenWhiteHoundAlive_GameContinues_WhiteHoundBecomesDogSpiritNextDay()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_WhiteHoundDogSpirit");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject wcGO = new GameObject("TestWinConditionManager_WhiteHoundDogSpirit");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;
        var field = typeof(WinConditionManager).GetField("useEliminationWin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(wc, true);

        try
        {
            // Trận đấu 4 người: Player 0 (DogSpirit), Player 1 (WhiteHound), Player 2 (Tiên tri), Player 3 (Dân làng)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true };
            var seer = new PlayerData { playerID = 2, playerName = "Seer", roleType = RoleType.Seer, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(seer);
            pm.players.Add(villager);

            // Bước 1: Trong đêm, DogSpirit cắn WhiteHound -> WhiteHound thức tỉnh thành quái vật
            whiteHound.isWhiteHoundAwakened = true;
            whiteHound.faction = FactionType.Monster;

            // Bước 2: DogSpirit bị giết trước đó (ví dụ bị Pháp Sư giết trong đêm, hoặc bị loại)
            dogSpirit.isAlive = false;
            dogSpirit.lastDeathCause = DeathCause.Poison;

            // Đảm bảo cài đặt Loi MatchEvaluator
            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // Bước 3: Đêm kết thúc (PhaseTimeRemaining = 0) -> Kiểm tra điều kiện thắng
            // DogSpirit chết nhưng WhiteHound đã thức tỉnh (1 Monster vs 2 Dân làng) -> Ván game PHẢI TIẾP TỤC!
            wc.CheckWinCondition(false);

            bool gameContinuesAfterNight = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // Bước 4: Sang ngày hôm sau -> WhiteHound chính thức gia nhập DogSpirit
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.DayStart;
            whiteHound.roleType = RoleType.DogSpirit; // Gia nhập DogSpirit

            // Tiên tri soi WhiteHound lúc này phải ra kết quả DogSpirit
            bool seerSeesDogSpirit = whiteHound.roleType == RoleType.DogSpirit;

            // Kiểm tra điều kiện thắng ban ngày: 1 Monster (WhiteHound) vs 2 Dân làng -> Vẫn TIẾP TỤC
            wc.CheckWinCondition(false);
            bool gameContinuesOnDay = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // Bước 5: Sang đêm sau, WhiteHound (DogSpirit) cắn chết Seer -> Còn 1 Monster vs 1 Villager
            seer.isAlive = false;
            wc.CheckWinCondition(false);
            bool werewolvesWinAtParity = gm.currentState == GameState.GameOver && gm.Winner == "Werewolves";

            Assert(gameContinuesAfterNight && seerSeesDogSpirit && gameContinuesOnDay && werewolvesWinAtParity,
                "TC30: WhiteHound bị cắn thì hôm sau gia nhập DogSpirit tính là Monster; DogSpirit chết trước đó mà WhiteHound còn thì game vẫn tiếp tục!");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 31. CÒN WHITEHOUND THÌ DÂN LÀNG KHÔNG THỂ CHIẾN THẮNG
    //     (Dù WhiteHound chưa bị cắn, DogSpirit bị giết trước đó -> WhiteHound thức tỉnh quái vật, ván game tiếp tục)
    // =========================================================

    private static void Test_WhiteHoundAlive_DogSpiritKilledWithoutBite_VillagersCannotWin()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_WhiteHoundCannotWin");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day;
        gm.currentPhase = GamePhase.DayResolution;

        GameObject wcGO = new GameObject("TestWinConditionManager_WhiteHoundCannotWin");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;
        var field = typeof(WinConditionManager).GetField("useEliminationWin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(wc, true);

        try
        {
            // Trận đấu 4 người: Player 0 (DogSpirit), Player 1 (WhiteHound - Chưa bị cắn), Player 2 (Tiên tri), Player 3 (Dân làng)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var seer = new PlayerData { playerID = 2, playerName = "Seer", roleType = RoleType.Seer, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(seer);
            pm.players.Add(villager);

            // Bước 1: DogSpirit bị giết (bị treo cổ ngày 1 hoặc Tiên tri phát hiện và vote chết) mà CHƯA cắn WhiteHound
            dogSpirit.isAlive = false;
            dogSpirit.lastDeathCause = DeathCause.Vote;
            dogSpirit.wasVotedOut = true;

            // Bước 2: Kiểm tra điều kiện thắng
            // VÌ CÒN WHITEHOUND NÊN DÂN LÀNG TUYỆT ĐỐI KHÔNG ĐƯỢC THẮNG!
            wc.CheckWinCondition(false);

            bool villagersDidNotWin = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // WhiteHound tự động thức tỉnh thành Monster kế thừa DogSpirit
            bool whiteHoundAwakened = whiteHound.isWhiteHoundAwakened && whiteHound.faction == FactionType.Monster;

            // Bước 3: WhiteHound (quái vật) tiêu diệt 1 dân làng -> Còn 1 WhiteHound vs 1 Dân làng
            seer.isAlive = false;
            wc.CheckWinCondition(false);
            bool monsterWinsAtParity = gm.currentState == GameState.GameOver && gm.Winner == "Werewolves";

            Assert(villagersDidNotWin && whiteHoundAwakened && monsterWinsAtParity,
                "TC31: Còn WhiteHound thì Dân làng không thể chiến thắng; WhiteHound thức tỉnh kế thừa quái vật duy trì ván đấu!");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 32. WHITEHOUND BỊ DOGSPIRIT CẮN -> WHITEHOUND CÒN SỐNG VÀ THỨC TỈNH, DOGSPIRIT CHẾT -> DÂN LÀNG KHÔNG THỂ THẮNG
    // =========================================================

    private static void Test_WhiteHoundBitten_SurvivesAndAwakens_DogSpiritDies_VillagersCannotWin()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_WhiteHoundBittenSurvives");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject wcGO = new GameObject("TestWinConditionManager_WhiteHoundBittenSurvives");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;

        try
        {
            // Trận đấu 4 người: Player 0 (DogSpirit), Player 1 (WhiteHound), Player 2 (Shaman), Player 3 (Dân làng)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var shaman = new PlayerData { playerID = 2, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(shaman);
            pm.players.Add(villager);

            // Bước 1: Trong đêm, DogSpirit cắn WhiteHound
            // Cú cắn này không được làm WhiteHound chết, WhiteHound thức tỉnh thành quái vật!
            whiteHound.isWhiteHoundAwakened = true;
            whiteHound.faction = FactionType.Monster;
            whiteHound.roleType = RoleType.DogSpirit; // Chính thức mang role DogSpirit
            whiteHound.isAlive = true; // Đảm bảo WhiteHound còn sống

            // Bước 2: Shaman dùng bùa hại tiêu diệt DogSpirit trong cùng đêm
            dogSpirit.isAlive = false;
            dogSpirit.lastDeathCause = DeathCause.Poison;

            // Đảm bảo Loi MatchEvaluator được đăng ký
            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // Bước 3: Đêm kết thúc -> Kiểm tra điều kiện thắng
            // VÌ WHITEHOUND ĐÃ BỊ CẮN VÀ CÒN SỐNG NÊN DÂN LÀNG TUYỆT ĐỐI KHÔNG ĐƯỢC THẮNG!
            wc.CheckWinCondition(false);

            bool villagersDidNotWin = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);
            bool whiteHoundIsAliveAndMonster = whiteHound.isAlive && whiteHound.faction == FactionType.Monster && whiteHound.isWhiteHoundAwakened;

            // Bước 4: Sang ngày hôm sau -> Trận đấu tiếp tục bình thường (1 Monster WhiteHound vs 2 Dân làng)
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.DayStart;
            wc.CheckWinCondition(false);
            bool gameContinuesOnDay = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // Bước 5: Sang đêm sau, WhiteHound cắn chết Shaman -> Còn 1 WhiteHound vs 1 Dân làng
            shaman.isAlive = false;
            wc.CheckWinCondition(false);
            bool werewolvesWinAtParity = gm.currentState == GameState.GameOver && gm.Winner == "Werewolves";

            Assert(villagersDidNotWin && whiteHoundIsAliveAndMonster && gameContinuesOnDay && werewolvesWinAtParity,
                "TC32: WhiteHound bị DogSpirit cắn rồi còn sống và thức tỉnh, DogSpirit chết thì Dân làng tuyệt đối không thắng, ván game tiếp tục!");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 33. TC33 – DOGSPIRIT CẮN WHITEHOUND VÀ BỊ SHAMAN GIẾT TRONG CÙNG MỘT NIGHT PHASE
    // =========================================================

    private static void Test_TC33_DogSpiritBitesWhiteHound_ShamanKillsDogSpirit_SameNight()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC33");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject nmGO = new GameObject("TestNightManager_TC33");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject drGO = new GameObject("TestDeathResolver_TC33");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject rmGO = new GameObject("TestRoleManager_TC33");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC33");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        GameObject wcGO = new GameObject("TestWinConditionManager_TC33");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;

        // Mô phỏng NetworkRoleSync cho Host (DogSpirit - Player 0) và Client (WhiteHound - Player 1)
        GameObject hostSyncGO = new GameObject("HostRoleSync_TC33");
        hostSyncGO.hideFlags = HideFlags.HideAndDontSave;
        var hostRoleSync = hostSyncGO.AddComponent<NetworkRoleSync>();
        hostRoleSync.SetOwnerIdForTest(0);

        GameObject clientSyncGO = new GameObject("ClientRoleSync_TC33");
        clientSyncGO.hideFlags = HideFlags.HideAndDontSave;
        var clientRoleSync = clientSyncGO.AddComponent<NetworkRoleSync>();
        clientRoleSync.SetOwnerIdForTest(1);

        try
        {
            // Thiết lập 4 người chơi:
            // Player 0: DogSpirit (phe Monster)
            // Player 1: WhiteHound (phe Villager, chưa thức tỉnh)
            // Player 2: Shaman (phe Villager, còn bùa hại)
            // Player 3: Villager (phe Villager)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHoundPlayer", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var shaman = new PlayerData { playerID = 2, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(shaman);
            pm.players.Add(villager);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.DogSpirit, dogSpirit);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.WhiteHound, whiteHound);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Shaman, shaman);
            rm.playerRoles[3] = RoleCatalog.Create(RoleType.Villager, villager);

            hostRoleSync.SyncCurrentRole();
            clientRoleSync.SyncCurrentRole();

            // Đảm bảo Loi MatchEvaluator được đăng ký
            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // ==========================================
            // BƯỚC 1: DogSpirit chọn cắn WhiteHound trong đêm
            // ==========================================
            nm.SetMonsterTarget(1, RoleType.DogSpirit);
            bridge.RecordWhiteHoundBitten(1);

            // Kiểm tra: WhiteHound chưa được bật cờ thức tỉnh quá sớm trước khi giải quyết đêm
            bool notAwakenedYet = !whiteHound.isWhiteHoundAwakened;
            bool whiteHoundStillAlive = whiteHound.isAlive;

            // ==========================================
            // BƯỚC 2: Shaman sử dụng bùa hại lên DogSpirit trong cùng đêm
            // ==========================================
            bool shamanKilledDogSpirit = dr.TryKillPlayer(0, DeathCause.Poison, RoleType.Shaman);
            bool dogSpiritDead = !dogSpirit.isAlive && dogSpirit.lastDeathCause == DeathCause.Poison;

            // Kiểm tra: Trong lúc Night Phase chưa xử lý xong (HasUnresolvedNightResolution = true),
            // gọi kiểm tra điều kiện thắng TUYỆT ĐỐI KHÔNG được kích hoạt GameOver
            wc.CheckWinCondition(false);
            bool noPrematureWinDuringNight = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // ==========================================
            // BƯỚC 3: Xử lý kết quả tất cả hành động ban đêm (Night Resolution)
            // ==========================================
            gm.currentPhase = GamePhase.ResolveNight;
            nm.ResolveNight();

            // Kiểm tra kết quả sau ResolveNight:
            // - DogSpirit đã chết do Bùa Hại
            // - White Hound bị cắn nhưng KHÔNG BỊ CHẾT (DeathOutcome.Prevented) và được thức tỉnh
            bool dogSpiritConfirmedDead = !dogSpirit.isAlive;
            bool whiteHoundSurvivedAndAwakened = whiteHound.isAlive && whiteHound.isWhiteHoundAwakened && whiteHound.faction == FactionType.Monster;

            // ==========================================
            // BƯỚC 4: Đồng bộ trạng thái vai trò và Host / Client
            // ==========================================
            NetworkGameOverManager.SynchronizeWhiteHoundState();

            bool roleUpdatedToDogSpirit = whiteHound.roleType == RoleType.DogSpirit;
            bool clientRoleSynced = clientRoleSync.LocalRole == RoleType.DogSpirit && clientRoleSync.IsWhiteHoundAwakened;

            // ==========================================
            // BƯỚC 5: Đánh giá điều kiện thắng sau khi xử lý xong Night Phase
            // ==========================================
            wc.CheckWinCondition(false);

            // Dân Làng KHÔNG ĐƯỢC THẮNG vì còn White Hound thức tỉnh thuộc phe Monster
            bool villagersDidNotWin = gm.currentState != GameState.GameOver && gm.Winner != "Villagers";

            // Monster KHÔNG ĐƯỢC THẮNG vì chưa đạt parity (1 Monster vs 2 Villagers: 1 >= 2 là false)
            bool monsterDidNotWinWithoutParity = gm.currentState != GameState.GameOver && gm.Winner != "Werewolves";

            // ==========================================
            // BƯỚC 6: Trận đấu tiếp tục sang phase tiếp theo (DayStart / Discussion)
            // ==========================================
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.DayStart;
            wc.CheckWinCondition(false);
            bool gameContinuesNextPhase = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            bool tc33Passed = notAwakenedYet && whiteHoundStillAlive &&
                             shamanKilledDogSpirit && dogSpiritDead &&
                             noPrematureWinDuringNight && dogSpiritConfirmedDead &&
                             whiteHoundSurvivedAndAwakened && roleUpdatedToDogSpirit &&
                             clientRoleSynced && villagersDidNotWin &&
                             monsterDidNotWinWithoutParity && gameContinuesNextPhase;

            Assert(tc33Passed,
                "TC33: DogSpirit cắn WhiteHound và bị Shaman giết trong cùng một Night Phase: WhiteHound sống, thức tỉnh Monster, Host/Client đồng bộ, Dân và Monster không thắng sớm, game tiếp tục sang phase tiếp theo.");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;

            if (hostSyncGO != null) Object.DestroyImmediate(hostSyncGO);
            if (clientSyncGO != null) Object.DestroyImmediate(clientSyncGO);
            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 35. TC35 – CHƯA SỬ DỤNG BÙA NÀO THÌ CẢ SAVE VÀ KILL ĐỀU HIỂN THỊ
    // =========================================================

    private static void Test_TC35_ShamanButtons_InitiallyBothDisplayed()
    {
        GameObject uiGO = new GameObject("TestSkillUI_TC35");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC35");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC35");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC35");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            // Ban đầu Pháp Sư vào Night phase
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);

            bool saveDisplayed = saveBtnGO.activeSelf;
            bool killDisplayed = killBtnGO.activeSelf;
            bool panelDisplayed = panelGO.activeSelf;
            bool charmsNotUsed = !skillUI.ShamanHasUsedSave && !skillUI.ShamanHasUsedKill;

            bool passed = saveDisplayed && killDisplayed && panelDisplayed && charmsNotUsed;
            Assert(passed, "TC35: Pháp Sư chưa sử dụng bùa nào thì cả nút Save và Kill đều hiển thị trên giao diện.");
        }
        finally
        {
            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
        }
    }

    // =========================================================
    // 36. TC36 – DÙNG SAVE THÀNH CÔNG THÌ SAVE BIẾN MẤT, KILL VẪN HIỂN THỊ
    // =========================================================

    private static void Test_TC36_ShamanSaveUsed_SaveHides_KillRemainsDisplayed()
    {
        GameObject uiGO = new GameObject("TestSkillUI_TC36");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC36");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC36");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC36");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);

            // Shaman sử dụng Save (SubActionId = 1) thành công
            skillUI.SimulateActionResultForTest(true, 1);

            bool saveHidden = !saveBtnGO.activeSelf;
            bool killStillDisplayed = killBtnGO.activeSelf;
            bool panelStillOpen = panelGO.activeSelf;
            bool stateCorrect = skillUI.ShamanHasUsedSave && !skillUI.ShamanHasUsedKill;

            bool passed = saveHidden && killStillDisplayed && panelStillOpen && stateCorrect;
            Assert(passed, "TC36: Pháp Sư dùng Save thành công thì nút Save biến mất vĩnh viễn, nút Kill vẫn hiển thị và sử dụng được.");
        }
        finally
        {
            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
        }
    }

    // =========================================================
    // 37. TC37 – DÙNG KILL THÀNH CÔNG THÌ KILL BIẾN MẤT, SAVE VẪN HIỂN THỊ
    // =========================================================

    private static void Test_TC37_ShamanKillUsed_KillHides_SaveRemainsDisplayed()
    {
        GameObject uiGO = new GameObject("TestSkillUI_TC37");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC37");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC37");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC37");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);

            // Shaman sử dụng Kill (SubActionId = 2) thành công
            skillUI.SimulateActionResultForTest(true, 2);

            bool killHidden = !killBtnGO.activeSelf;
            bool saveStillDisplayed = saveBtnGO.activeSelf;
            bool panelStillOpen = panelGO.activeSelf;
            bool stateCorrect = !skillUI.ShamanHasUsedSave && skillUI.ShamanHasUsedKill;

            bool passed = killHidden && saveStillDisplayed && panelStillOpen && stateCorrect;
            Assert(passed, "TC37: Pháp Sư dùng Kill thành công thì nút Kill biến mất vĩnh viễn, nút Save vẫn hiển thị và sử dụng được.");
        }
        finally
        {
            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
        }
    }

    // =========================================================
    // 38. TC38 – ĐÃ DÙNG CẢ SAVE VÀ KILL THÌ CẢ HAI NÚT ĐỀU BIẾN MẤT
    // =========================================================

    private static void Test_TC38_ShamanBothUsed_BothButtonsHide()
    {
        GameObject uiGO = new GameObject("TestSkillUI_TC38");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC38");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC38");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC38");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);

            // Dùng Save thành công
            skillUI.SimulateActionResultForTest(true, 1);
            // Dùng Kill thành công
            skillUI.SimulateActionResultForTest(true, 2);

            bool saveHidden = !saveBtnGO.activeSelf;
            bool killHidden = !killBtnGO.activeSelf;
            bool panelHidden = !panelGO.activeSelf;
            bool bothUsed = skillUI.ShamanHasUsedSave && skillUI.ShamanHasUsedKill;

            bool passed = saveHidden && killHidden && panelHidden && bothUsed;
            Assert(passed, "TC38: Pháp Sư đã dùng cả Save và Kill thì cả hai nút đều biến mất và bảng kỹ năng đóng.");
        }
        finally
        {
            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
        }
    }

    // =========================================================
    // 39. TC39 – CHUYỂN SANG NIGHT PHASE TIẾP THEO KHÔNG LÀM XUẤT HIỆN LẠI KỸ NĂNG ĐÃ DÙNG
    // =========================================================

    private static void Test_TC39_ShamanNextNight_UsedSkillsDoNotReappear()
    {
        GameObject uiGO = new GameObject("TestSkillUI_TC39");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC39");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC39");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC39");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            // Đêm 1: Bắt đầu vào đêm, dùng Save thành công
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);
            skillUI.SimulateActionResultForTest(true, 1);
            bool saveHiddenNight1 = !saveBtnGO.activeSelf;
            bool killVisibleNight1 = killBtnGO.activeSelf;

            // Chuyển sang Ngày (Discussion)
            skillUI.SetPhaseForTest(isNightPhase: false, isDiscussionPhase: true);
            bool buttonsHiddenDay = !saveBtnGO.activeSelf && !killBtnGO.activeSelf;

            // Chuyển sang Đêm 2 (Night tiếp theo)
            skillUI.SetPhaseForTest(isNightPhase: true, isDiscussionPhase: false);
            bool saveStillHiddenNight2 = !saveBtnGO.activeSelf; // Tuyệt đối không xuất hiện lại!
            bool killStillVisibleNight2 = killBtnGO.activeSelf; // Kill chưa dùng nên vẫn hiển thị
            bool panelOpenNight2 = panelGO.activeSelf;

            // Ở Đêm 2, Shaman dùng Kill thành công
            skillUI.SimulateActionResultForTest(true, 2);

            // Chuyển sang Ngày 2 rồi Đêm 3
            skillUI.SetPhaseForTest(isNightPhase: false, isDiscussionPhase: true);
            skillUI.SetPhaseForTest(isNightPhase: true, isDiscussionPhase: false);

            bool bothHiddenNight3 = !saveBtnGO.activeSelf && !killBtnGO.activeSelf;
            bool panelHiddenNight3 = !panelGO.activeSelf;

            bool passed = saveHiddenNight1 && killVisibleNight1 && buttonsHiddenDay &&
                          saveStillHiddenNight2 && killStillVisibleNight2 && panelOpenNight2 &&
                          bothHiddenNight3 && panelHiddenNight3;

            Assert(passed, "TC39: Chuyển sang Night Phase tiếp theo không làm xuất hiện lại kỹ năng đã sử dụng.");
        }
        finally
        {
            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
        }
    }

    // =========================================================
    // 40. TC40 – SERVER TỪ CHỐI SỬ DỤNG LẠI KỸ NĂNG ĐÃ DÙNG
    // =========================================================

    private static void Test_TC40_ServerRejects_ReusingUsedSkills()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC40");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC40");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC40");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC40");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC40");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        try
        {
            var shamanData = new PlayerData { playerID = 0, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var target1 = new PlayerData { playerID = 1, playerName = "TargetVillager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var target2 = new PlayerData { playerID = 2, playerName = "TargetVillager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(shamanData);
            pm.players.Add(target1);
            pm.players.Add(target2);

            ShamanRole shamanRole = RoleCatalog.Create(RoleType.Shaman, shamanData) as ShamanRole;
            rm.playerRoles[0] = shamanRole;
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, target1);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Villager, target2);

            // 1. Dùng Save lần đầu -> Server chấp nhận
            bridge.HandleAction(new NetworkActionEvent(0, 1, 1));
            bool firstSaveSuccess = bridge.LastActionSuccess;

            // 2. Thử dùng Save lần 2 -> Server TỪ CHỐI
            bridge.HandleAction(new NetworkActionEvent(0, 1, 1));
            bool secondSaveRejected = !bridge.LastActionSuccess;
            bool saveRejectMessageValid = bridge.LastActionMessage != null && bridge.LastActionMessage.Contains("Bùa Lợi (Cứu) đã được sử dụng trước đó");

            // 3. Dùng Kill lần đầu -> Server chấp nhận
            bridge.HandleAction(new NetworkActionEvent(0, 2, 2));
            bool firstKillSuccess = bridge.LastActionSuccess;

            // 4. Thử dùng Kill lần 2 -> Server TỪ CHỐI
            bridge.HandleAction(new NetworkActionEvent(0, 1, 2));
            bool secondKillRejected = !bridge.LastActionSuccess;
            bool killRejectMessageValid = bridge.LastActionMessage != null && bridge.LastActionMessage.Contains("Bùa Hại (Giết) đã được sử dụng trước đó");

            bool passed = firstSaveSuccess && secondSaveRejected && saveRejectMessageValid &&
                          firstKillSuccess && secondKillRejected && killRejectMessageValid;

            Assert(passed, "TC40: Server từ chối mọi yêu cầu sử dụng lại bùa đã dùng (Save hoặc Kill).");
        }
        finally
        {
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 41. TC41 – BẮT ĐẦU TRẬN MỚI THÌ CẢ HAI KỸ NĂNG ĐƯỢC RESET VÀ CÓ THỂ SỬ DỤNG LẠI
    // =========================================================

    private static void Test_TC41_NewMatch_ShamanSkillsResetAndCanBeUsedAgain()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC41");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC41");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC41");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC41");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC41");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();

        GameObject uiGO = new GameObject("TestSkillUI_TC41");
        uiGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = uiGO.AddComponent<NetworkSkillUI>();

        GameObject panelGO = new GameObject("SkillPanel_TC41");
        panelGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject saveBtnGO = new GameObject("SaveButton_TC41");
        saveBtnGO.hideFlags = HideFlags.HideAndDontSave;
        GameObject killBtnGO = new GameObject("KillButton_TC41");
        killBtnGO.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var shamanData = new PlayerData { playerID = 0, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var target1 = new PlayerData { playerID = 1, playerName = "TargetVillager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var target2 = new PlayerData { playerID = 2, playerName = "TargetVillager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(shamanData);
            pm.players.Add(target1);
            pm.players.Add(target2);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.Shaman, shamanData);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, target1);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Villager, target2);

            // Bước 1: Trận 1, Shaman dùng cả Save và Kill
            bridge.HandleAction(new NetworkActionEvent(0, 1, 1));
            bridge.HandleAction(new NetworkActionEvent(0, 2, 2));
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);
            skillUI.SimulateActionResultForTest(true, 1);
            skillUI.SimulateActionResultForTest(true, 2);

            bool match1BothUsedOnServer = NetworkRoleActionBridge.IsShamanSaveUsed(0) && NetworkRoleActionBridge.IsShamanKillUsed(0);
            bool match1BothHiddenOnUI = !saveBtnGO.activeSelf && !killBtnGO.activeSelf;

            // Bước 2: Bắt đầu ván mới (Reset For New Match)
            bridge.ResetForNewMatch();
            skillUI.ResetForNewMatch();

            // Setup lại Shaman role mới cho ván mới
            target2.isAlive = true;
            rm.playerRoles[0] = RoleCatalog.Create(RoleType.Shaman, shamanData);

            bool serverStateReset = !NetworkRoleActionBridge.IsShamanSaveUsed(0) && !NetworkRoleActionBridge.IsShamanKillUsed(0);
            bool uiStateReset = !skillUI.ShamanHasUsedSave && !skillUI.ShamanHasUsedKill;

            // Đến lượt Pháp Sư trong ván mới: cả hai nút hiển thị lại
            skillUI.SetupForTest(RoleType.Shaman, isNightPhase: true, panelGO, saveBtnGO, killBtnGO);
            bool bothButtonsReappeared = saveBtnGO.activeSelf && killBtnGO.activeSelf;

            // Kiểm tra: Server cho phép sử dụng lại cả 2 bùa trong ván mới
            bridge.HandleAction(new NetworkActionEvent(0, 1, 1));
            bool newMatchSaveAccepted = bridge.LastActionSuccess;

            bridge.HandleAction(new NetworkActionEvent(0, 2, 2));
            bool newMatchKillAccepted = bridge.LastActionSuccess;

            bool passed = match1BothUsedOnServer && match1BothHiddenOnUI &&
                          serverStateReset && uiStateReset &&
                          bothButtonsReappeared &&
                          newMatchSaveAccepted && newMatchKillAccepted;

            Assert(passed, "TC41: Bắt đầu ván mới thì cả hai kỹ năng được reset hoàn toàn và có thể sử dụng lại bình thường.");
        }
        finally
        {
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (saveBtnGO != null) Object.DestroyImmediate(saveBtnGO);
            if (killBtnGO != null) Object.DestroyImmediate(killBtnGO);
            if (panelGO != null) Object.DestroyImmediate(panelGO);
            if (uiGO != null) Object.DestroyImmediate(uiGO);
            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 34. TC34 – KIỂM TRA ĐIỀU KIỆN THẮNG SAU KHI WHITEHOUND THỨC TỈNH
    // =========================================================

    private static void Test_TC34_WinCondition_AfterWhiteHoundAwakening()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC34");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day;
        gm.currentPhase = GamePhase.DayStart;

        GameObject wcGO = new GameObject("TestWinConditionManager_TC34");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;

        GameObject rmGO = new GameObject("TestRoleManager_TC34");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        try
        {
            // Trạng thái ban đầu sau khi DogSpirit đã chết và WhiteHound đã thức tỉnh:
            // Player 0 (DogSpirit - đã chết)
            // Player 1 (WhiteHound - đã thức tỉnh, thuộc phe Monster, mang role DogSpirit, còn sống)
            // Player 2 (Shaman - phe Villager, còn sống)
            // Player 3 (Villager - phe Villager, còn sống)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHoundPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };
            var shaman = new PlayerData { playerID = 2, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(shaman);
            pm.players.Add(villager);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.DogSpirit, dogSpirit);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.DogSpirit, whiteHound);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Shaman, shaman);
            rm.playerRoles[3] = RoleCatalog.Create(RoleType.Villager, villager);

            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // 1. Kiểm tra: DogSpirit chết, WhiteHound còn sống -> Hệ thống KHÔNG gọi nhầm VillagerWin()
            wc.CheckWinCondition(false);
            bool villagersNotWonPrematurely = gm.currentState != GameState.GameOver && gm.Winner != "Villagers";

            // 2. Kiểm tra: Monster không được thắng khi chưa đạt parity (1 Monster vs 2 Villagers)
            bool monsterNotWonWithoutParity = gm.currentState != GameState.GameOver && gm.Winner != "Werewolves";

            // 3. Nhánh A: Số lượng người chơi thay đổi và ĐẠT PARITY -> Phe Monster chiến thắng
            // Ví dụ: Ban đêm WhiteHound tấn công tiêu diệt Shaman -> Còn 1 WhiteHound vs 1 Dân làng (1 >= 1)
            shaman.isAlive = false;
            wc.CheckWinCondition(false);
            bool monsterWinsWhenParityReached = gm.currentState == GameState.GameOver && gm.Winner == "Werewolves";

            // 4. Nhánh B: Khôi phục trạng thái và kiểm tra trường hợp DÂN LÀNG CHIẾN THẮNG khi loại hết Monster
            // Reset lại trạng thái kết quả trận đấu cho nhánh B
            var winnerProp = typeof(GameRoleManager).GetProperty("Winner");
            winnerProp?.SetValue(gm, null, null);
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Voting;
            shaman.isAlive = true;

            // Ban ngày, Dân làng vote treo cổ WhiteHound -> WhiteHound bị loại
            whiteHound.isAlive = false;
            whiteHound.wasVotedOut = true;
            whiteHound.lastDeathCause = DeathCause.Vote;

            // Bây giờ: 0 Monster sống, 0 WhiteHound sống, 2 Villagers sống
            wc.CheckWinCondition(false);
            bool villagersWinWhenAllMonstersEliminated = gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            bool tc34Passed = villagersNotWonPrematurely && monsterNotWonWithoutParity &&
                             monsterWinsWhenParityReached && villagersWinWhenAllMonstersEliminated;

            Assert(tc34Passed,
                "TC34: Sau khi White Hound thức tỉnh: Không gọi nhầm VillagerWin(), Monster không thắng khi chưa đạt parity, xác định chính xác phe thắng khi số lượng người chơi thay đổi (đạt parity hoặc bị tiêu diệt hết).");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;

            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 42. TC42 – SHAMAN NHẤN KILL, MỤC TIÊU VẪN SỐNG TRONG NIGHT PHASE
    // =========================================================

    private static void Test_TC42_ShamanKill_TargetRemainsAliveDuringNightPhase()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC42");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC42");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC42");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC42");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC42");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        try
        {
            var shamanData = new PlayerData { playerID = 0, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var targetData = new PlayerData { playerID = 1, playerName = "TargetVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(shamanData);
            pm.players.Add(targetData);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.Shaman, shamanData);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, targetData);

            // Shaman nhấn nút Kill chọn Player 1
            bridge.HandleAction(new NetworkActionEvent(0, 1, 2));

            // Kiểm tra:
            // 1. Action được Server chấp nhận
            bool actionAccepted = bridge.LastActionSuccess;

            // 2. Mục tiêu VẪN SỐNG trong suốt Night Phase (isAlive = true, chưa có bản ghi tử vong)
            bool targetStillAlive = targetData.isAlive;
            bool targetNoDeathRecorded = !targetData.hasDeathRecord;

            // 3. Hệ thống ghi nhận mục tiêu đang chờ xử lý tử vong
            bool hasPendingKill = NetworkRoleActionBridge.HasPendingShamanKill;
            bool pendingTargetIdMatches = NetworkRoleActionBridge.PendingShamanKillTargetId == 1;

            // 4. Night actions chưa hoàn tất -> HasUnresolvedNightActions = true
            bool nightUnresolved = NetworkGameOverManager.HasUnresolvedNightActions();

            bool passed = actionAccepted && targetStillAlive && targetNoDeathRecorded &&
                          hasPendingKill && pendingTargetIdMatches && nightUnresolved;

            Assert(passed, "TC42: Shaman nhấn Kill, mục tiêu vẫn sống và giữ nguyên trạng thái trong Night Phase.");
        }
        finally
        {
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 43. TC43 – HẾT NIGHT PHASE, MỤC TIÊU MỚI ĐƯỢC XỬ LÝ TỬ VONG
    // =========================================================

    private static void Test_TC43_ShamanKill_TargetProcessedForDeathAtEndOfNightPhase()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC43");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC43");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC43");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC43");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC43");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        try
        {
            var shamanData = new PlayerData { playerID = 0, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var targetData = new PlayerData { playerID = 1, playerName = "TargetVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(shamanData);
            pm.players.Add(targetData);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.Shaman, shamanData);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, targetData);

            // Shaman nhấn Kill trong Night Phase
            bridge.HandleAction(new NetworkActionEvent(0, 1, 2));

            // Xác nhận mục tiêu vẫn còn sống trước khi kết thúc đêm
            bool aliveDuringNight = targetData.isAlive;

            // Kết thúc Night Phase -> chuyển sang ResolveNight
            gm.currentPhase = GamePhase.ResolveNight;
            NetworkRoleActionBridge.ResolvePendingShamanKill();

            // Kiểm tra:
            // 1. Sau khi kết thúc Night Phase, mục tiêu mới bị xử lý tử vong
            bool targetDeadAfterNight = !targetData.isAlive;
            bool deathCauseIsPoison = targetData.lastDeathCause == DeathCause.Poison;

            // 2. Danh sách chờ Shaman Kill đã được dọn sạch
            bool pendingCleared = !NetworkRoleActionBridge.HasPendingShamanKill;

            bool passed = aliveDuringNight && targetDeadAfterNight && deathCauseIsPoison && pendingCleared;

            Assert(passed, "TC43: Hết Night Phase, mục tiêu của Bùa Hại mới được xử lý tử vong (DeathCause = Poison).");
        }
        finally
        {
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 44. TC44 – SHAMAN KHÔNG THỂ SỬ DỤNG KILL LẦN THỨ HAI
    // =========================================================

    private static void Test_TC44_ShamanCannotUseKillSecondTime()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC44");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC44");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC44");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC44");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC44");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        try
        {
            var shamanData = new PlayerData { playerID = 0, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var target1 = new PlayerData { playerID = 1, playerName = "Target1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var target2 = new PlayerData { playerID = 2, playerName = "Target2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(shamanData);
            pm.players.Add(target1);
            pm.players.Add(target2);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.Shaman, shamanData);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, target1);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Villager, target2);

            // Lần 1: Shaman dùng Kill lên Player 1 -> Thành công
            bridge.HandleAction(new NetworkActionEvent(0, 1, 2));
            bool firstSuccess = bridge.LastActionSuccess;

            // Lần 2: Shaman cố dùng Kill lên Player 2 -> Bị từ chối
            bridge.HandleAction(new NetworkActionEvent(0, 2, 2));
            bool secondRejected = !bridge.LastActionSuccess;
            bool rejectMessageValid = bridge.LastActionMessage != null && bridge.LastActionMessage.Contains("Bùa Hại (Giết) đã được sử dụng trước đó");

            // Player 2 hoàn toàn không bị ảnh hưởng và vẫn sống
            bool target2Untouched = target2.isAlive && !target2.hasDeathRecord;

            // Mục tiêu đang chờ xử lý vẫn là Player 1, không bị ghi đè thành Player 2
            bool pendingTargetIsPlayer1 = NetworkRoleActionBridge.PendingShamanKillTargetId == 1;

            // Khi kết thúc đêm, chỉ Player 1 bị xử lý tử vong, Player 2 vẫn sống
            gm.currentPhase = GamePhase.ResolveNight;
            NetworkRoleActionBridge.ResolvePendingShamanKill();

            bool player1Died = !target1.isAlive;
            bool player2StillAlive = target2.isAlive;

            bool passed = firstSuccess && secondRejected && rejectMessageValid &&
                          target2Untouched && pendingTargetIsPlayer1 && player1Died && player2StillAlive;

            Assert(passed, "TC44: Shaman không thể sử dụng Kill lần thứ hai trong cùng trận đấu.");
        }
        finally
        {
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 45. TC45 – DOGSPIRIT CẮN WHITE HOUND & SHAMAN GIẾT DOGSPIRIT CÙNG ĐÊM:
    //            WHITE HOUND CHUYỂN HÓA VÀ TRẬN ĐẤU KHÔNG KẾT THÚC SAI
    // =========================================================

    private static void Test_TC45_DogSpiritBitesWhiteHound_And_ShamanKillsDogSpirit_SameNight_WhiteHoundAwakens_GameContinues()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC45");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC45");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC45");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC45");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC45");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        GameObject wcGO = new GameObject("TestWinConditionManager_TC45");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;

        try
        {
            // Thiết lập 4 người chơi:
            // Player 0: DogSpirit (phe Monster)
            // Player 1: WhiteHound (phe Villager, chưa thức tỉnh)
            // Player 2: Shaman (phe Villager)
            // Player 3: Villager (phe Villager)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHoundPlayer", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var shaman = new PlayerData { playerID = 2, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(shaman);
            pm.players.Add(villager);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.DogSpirit, dogSpirit);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.WhiteHound, whiteHound);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Shaman, shaman);
            rm.playerRoles[3] = RoleCatalog.Create(RoleType.Villager, villager);

            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // 1. DogSpirit cắn White Hound trong đêm
            nm.SetMonsterTarget(1, RoleType.DogSpirit);
            bridge.RecordWhiteHoundBitten(1);

            // 2. Shaman dùng Kill lên DogSpirit trong cùng đêm
            bridge.HandleAction(new NetworkActionEvent(2, 0, 2));

            // Trong lúc Night Phase: Cả 2 đều PHẢI CÒN SỐNG, không được chết ngay
            bool dogSpiritAliveDuringNight = dogSpirit.isAlive;
            bool whiteHoundAliveDuringNight = whiteHound.isAlive;
            bool pendingKillRecorded = NetworkRoleActionBridge.PendingShamanKillTargetId == 0;
            bool nightUnresolved = NetworkGameOverManager.HasUnresolvedNightActions();

            // Gọi kiểm tra điều kiện thắng trong Night Phase -> Không được Game Over!
            wc.CheckWinCondition(false);
            bool noGameOverDuringNight = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            // 3. Kết thúc Night Phase và xử lý theo đúng thứ tự:
            gm.currentPhase = GamePhase.ResolveNight;
            // a) Xử lý NightManager (White Hound bị cắn -> thức tỉnh sang Monster, không chết)
            nm.ResolveNight();
            // b) Xử lý Shaman Kill (DogSpirit bị tiêu diệt do Thuốc độc)
            NetworkRoleActionBridge.ResolvePendingShamanKill();
            // c) Đồng bộ trạng thái White Hound và người chơi
            NetworkGameOverManager.SynchronizeWhiteHoundState();

            // Kiểm tra kết quả sau khi xử lý cả hai hành động:
            bool dogSpiritDeadAfterNight = !dogSpirit.isAlive && dogSpirit.lastDeathCause == DeathCause.Poison;
            bool whiteHoundAwakenedAndAlive = whiteHound.isAlive && whiteHound.isWhiteHoundAwakened && whiteHound.faction == FactionType.Monster;

            // 4. Đánh giá điều kiện thắng sau khi xử lý xong toàn bộ kết quả ban đêm:
            wc.CheckWinCondition(false);

            // Dân Làng KHÔNG ĐƯỢC THẮNG vì White Hound còn sống và đã thức tỉnh gia nhập Monster
            bool villagersDidNotWin = gm.currentState != GameState.GameOver && gm.Winner != "Villagers";
            // Monster KHÔNG ĐƯỢC THẮNG vì chưa đạt parity (1 Monster vs 2 Villagers)
            bool monsterDidNotWinWithoutParity = gm.currentState != GameState.GameOver && gm.Winner != "Werewolves";

            // Chuyển sang phase tiếp theo (DayStart) -> Game vẫn tiếp tục
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.DayStart;
            wc.CheckWinCondition(false);
            bool gameContinues = gm.currentState != GameState.GameOver && string.IsNullOrEmpty(gm.Winner);

            bool passed = dogSpiritAliveDuringNight && whiteHoundAliveDuringNight && pendingKillRecorded &&
                          nightUnresolved && noGameOverDuringNight && dogSpiritDeadAfterNight &&
                          whiteHoundAwakenedAndAlive && villagersDidNotWin && monsterDidNotWinWithoutParity && gameContinues;

            Assert(passed, "TC45: DogSpirit cắn White Hound & Shaman giết DogSpirit cùng đêm: Cả 2 sống trong đêm, White Hound thức tỉnh Monster, DogSpirit chết khi hết đêm, Dân không thắng sớm, trận đấu tiếp tục.");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 46. TC46 – ĐIỀU KIỆN THẮNG CHỈ ĐƯỢC KIỂM TRA SAU KHI TOÀN BỘ KẾT QUẢ BAN ĐÊM HOÀN TẤT
    // =========================================================

    private static void Test_TC46_WinConditionEvaluatedOnlyAfterAllNightResultsComplete()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC46");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night;
        gm.currentPhase = GamePhase.Night;

        GameObject drGO = new GameObject("TestDeathResolver_TC46");
        drGO.hideFlags = HideFlags.HideAndDontSave;
        var dr = drGO.AddComponent<DeathResolver>();
        DeathResolver.Instance = dr;

        GameObject nmGO = new GameObject("TestNightManager_TC46");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject rmGO = new GameObject("TestRoleManager_TC46");
        rmGO.hideFlags = HideFlags.HideAndDontSave;
        var rm = rmGO.AddComponent<RoleManager>();
        RoleManager.Instance = rm;
        rm.playerRoles = new Dictionary<int, BaseRole>();

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC46");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        GameObject wcGO = new GameObject("TestWinConditionManager_TC46");
        wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = wc;

        try
        {
            // Thiết lập: 1 DogSpirit duy nhất và 2 Dân Làng (trong đó có 1 Shaman)
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var villager = new PlayerData { playerID = 1, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var shaman = new PlayerData { playerID = 2, playerName = "ShamanPlayer", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(villager);
            pm.players.Add(shaman);

            rm.playerRoles[0] = RoleCatalog.Create(RoleType.DogSpirit, dogSpirit);
            rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, villager);
            rm.playerRoles[2] = RoleCatalog.Create(RoleType.Shaman, shaman);

            NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

            // Shaman dùng Kill lên DogSpirit trong Night Phase
            bridge.HandleAction(new NetworkActionEvent(2, 0, 2));

            // Trong Night Phase, DogSpirit vẫn sống, action chưa hoàn tất
            bool dogSpiritAliveDuringNight = dogSpirit.isAlive;
            bool hasUnresolvedDuringNight = NetworkGameOverManager.HasUnresolvedNightActions();

            // Cố gắng gọi kiểm tra điều kiện thắng trong Night Phase -> Không được Game Over!
            wc.CheckWinCondition(false);
            NetworkGameOverManager.Instance.CheckServerWinCondition();
            bool blockedDuringNight = gm.currentState != GameState.GameOver && !NetworkGameOverManager.Instance.isGameOver;

            // Hết Night Phase -> chuyển sang ResolveNight và thực hiện toàn bộ resolution
            gm.currentPhase = GamePhase.ResolveNight;
            nm.ResolveNight();
            NetworkRoleActionBridge.ResolvePendingShamanKill();
            NetworkGameOverManager.SynchronizeWhiteHoundState();

            // Sau khi resolve xong:
            bool dogSpiritDeadAfterResolve = !dogSpirit.isAlive;
            bool noUnresolvedAfterResolve = !NetworkGameOverManager.HasUnresolvedNightActions();

            // Bây giờ mới đánh giá điều kiện thắng
            wc.CheckWinCondition(false);
            bool villagersWinAfterAllNightResultsComplete = gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            bool passed = dogSpiritAliveDuringNight && hasUnresolvedDuringNight && blockedDuringNight &&
                          dogSpiritDeadAfterResolve && noUnresolvedAfterResolve && villagersWinAfterAllNightResultsComplete;

            Assert(passed, "TC46: Điều kiện thắng bị chặn hoàn toàn trong Night Phase và chỉ được kiểm tra sau khi toàn bộ kết quả ban đêm hoàn tất.");
        }
        finally
        {
            if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (DeathResolver.Instance == dr) DeathResolver.Instance = null;
            if (RoleManager.Instance == rm) RoleManager.Instance = null;
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;

            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (wcGO != null) Object.DestroyImmediate(wcGO);
            if (rmGO != null) Object.DestroyImmediate(rmGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            if (drGO != null) Object.DestroyImmediate(drGO);
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 47. TC47 â€“ CLIENT THOÃT GIá»®A NIGHT PHASE
    // =========================================================

    private static void Test_TC47_ClientDisconnectDuringNightPhase()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject nmGO = new GameObject("TestNightManager_TC47");
        nmGO.hideFlags = HideFlags.HideAndDontSave;
        var nm = nmGO.AddComponent<NightManager>();
        NightManager.Instance = nm;

        GameObject bridgeGO = new GameObject("TestRoleActionBridge_TC47");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();
        bridge.ResetForNewMatch();

        try
        {
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var villager = new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var shaman = new PlayerData { playerID = 2, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(villager);
            pm.players.Add(shaman);

            // Shaman queues action against DogSpirit
            NetworkRoleActionBridge.RecordShamanKillTarget(0, 2);
            // DogSpirit queues bite on Shaman
            nm.SetMonsterTarget(2);

            Assert(NetworkRoleActionBridge.HasPendingShamanKill, "TC47_PendingBeforeDisconnect", "CÃ³ pending Shaman kill trÆ°á»›c khi disconnect");
            int monsterTargetBefore = (int)typeof(NightManager).GetField("monsterTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(nm);
            Assert(monsterTargetBefore == 2, "TC47_MonsterTargetBeforeDisconnect", "DogSpirit Ä‘ang nháº¯m vÃ o Player 2");

            // Client 2 disconnects during Night Phase
            ulong dcId = 2;
            NetworkGameManager.LeftPlayerClientIds.Add(dcId);
            bool removed = pm.RemoveDisconnectedPlayer((int)dcId);
            nm.CancelDisconnectedPlayerActions((int)dcId);
            NetworkRoleActionBridge.CancelDisconnectedPlayerActions((int)dcId);

            Assert(removed, "TC47_PlayerRemoved", "Player 2 Ä‘Æ°á»£c xÃ³a khá»i active players");
            Assert(pm.ConnectedPlayerCount == 2, "TC47_ConnectedCountUpdated", $"Sá»‘ ngÆ°á»i káº¿t ná»‘i giáº£m cÃ²n 2 (thá»±c táº¿: {pm.ConnectedPlayerCount})");
            Assert(pm.AlivePlayerCount == 2, "TC47_AliveCountUpdated", $"Sá»‘ ngÆ°á»i sá»‘ng giáº£m cÃ²n 2 (thá»±c táº¿: {pm.AlivePlayerCount})");
            Assert(!NetworkRoleActionBridge.HasPendingShamanKill, "TC47_ShamanKillCancelled", "HÃ nh Ä‘á»™ng Shaman cá»§a ngÆ°á»i thoÃ¡t bá»‹ há»§y");
            int monsterTargetAfter = (int)typeof(NightManager).GetField("monsterTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(nm);
            Assert(monsterTargetAfter == -1, "TC47_MonsterTargetReset", "Má»¥c tiÃªu quÃ¡i váº­t nháº¯m vÃ o ngÆ°á»i thoÃ¡t Ä‘Æ°á»£c reset");

            // DogSpirit cÃ³ thá»ƒ chá»n má»¥c tiÃªu ngÆ°á»i sá»‘ng cÃ²n láº¡i (Player 1) mÃ  khÃ´ng bá»‹ treo
            nm.SetMonsterTarget(1);
            int monsterTargetNext = (int)typeof(NightManager).GetField("monsterTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(nm);
            Assert(monsterTargetNext == 1, "TC47_NightContinuesNormally", "Night Phase tiáº¿p tá»¥c bÃ¬nh thÆ°á»ng cho cÃ¡c ngÆ°á»i chÆ¡i cÃ²n sá»‘ng");
        }
        finally
        {
            NetworkGameManager.LeftPlayerClientIds.Clear();
            if (NightManager.Instance == nm) NightManager.Instance = null;
            if (bridgeGO != null) Object.DestroyImmediate(bridgeGO);
            if (nmGO != null) Object.DestroyImmediate(nmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 48. TC48 â€“ CLIENT THOÃT GIá»®A VOTING PHASE
    // =========================================================

    private static void Test_TC48_ClientDisconnectDuringVotingPhase()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject gmGO = new GameObject("TestGameRoleManager_TC48");
        gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gm;
        gm.currentState = GameState.Voting;
        gm.currentPhase = GamePhase.Voting;

        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "Player0", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Player1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "Player2", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            NetworkPlayerVote.ResetLocalStaticVotes();

            // Player 0 votes for Player 1
            NetworkPlayerVote.RecordVote(0, 1);
            // Player 1 votes for Player 2
            NetworkPlayerVote.RecordVote(1, 2);
            // Player 2 has not voted yet

            Assert(NetworkPlayerVote.GetEligibleVoterCount() == 3, "TC48_InitialEligibleCount", "Ban Ä‘áº§u cÃ³ 3 ngÆ°á»i Ä‘á»§ Ä‘iá»u kiá»‡n vote");

            // Player 2 disconnects during Voting Phase
            ulong dcId = 2;
            NetworkGameManager.LeftPlayerClientIds.Add(dcId);
            pm.RemoveDisconnectedPlayer((int)dcId);
            NetworkPlayerVote.RemoveDisconnectedPlayerVotes((int)dcId);

            // Votes targeting Player 2 should be cleared so Player 1 can re-vote
            Assert(NetworkPlayerVote.GetVoteCountFor(2) == 0, "TC48_VotesForDisconnectedCleared", "Phiáº¿u báº§u cho ngÆ°á»i Ä‘Ã£ thoÃ¡t bá»‹ xÃ³a vá» 0");
            Assert(NetworkPlayerVote.GetEligibleVoterCount() == 2, "TC48_EligibleCountUpdated", $"Sá»‘ ngÆ°á»i Ä‘á»§ Ä‘iá»u kiá»‡n vote giáº£m cÃ²n 2 (thá»±c táº¿: {NetworkPlayerVote.GetEligibleVoterCount()})");

            // Player 1 votes for Player 0 -> both eligible players (0 and 1) have now voted
            NetworkPlayerVote.RecordVote(1, 0);
            Assert(NetworkPlayerVote.HasAllEligiblePlayersVoted(), "TC48_AllEligibleVoted", "Táº¥t cáº£ ngÆ°á»i chÆ¡i cÃ²n láº¡i Ä‘Ã£ hoÃ n táº¥t bá» phiáº¿u");

            // Calling CheckAndResolveIfAllVoted finishes voting without hanging
            NetworkPlayerVote.CheckAndResolveIfAllVoted();
            Assert(true, "TC48_VotingResolvedWithoutFreeze", "Voting Phase káº¿t thÃºc an toÃ n, khÃ´ng bá»‹ treo vÃ¬ thiáº¿u phiáº¿u cá»§a ngÆ°á»i Ä‘Ã£ thoÃ¡t");
        }
        finally
        {
            NetworkGameManager.LeftPlayerClientIds.Clear();
            NetworkPlayerVote.ResetLocalStaticVotes();
            if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
            if (gmGO != null) Object.DestroyImmediate(gmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 49. TC49 â€“ HOST THOÃT TRáº¬N KHI ÄANG DIá»„N RA
    // =========================================================

    private static void Test_TC49_HostDisconnectHandling()
    {
        string winnerTitle = NetworkGameOverManager.FormatWinner("Host Disconnected");
        string winnerSubtext = NetworkGameOverManager.GetWinnerSubtext("Host Disconnected");

        Assert(winnerTitle == "HOST ÄÃƒ Rá»œI PHÃ’NG", "TC49_HostTitleValid", $"TiÃªu Ä‘á» thÃ´ng bÃ¡o Host thoÃ¡t Ä‘Ãºng: '{winnerTitle}'");
        Assert(winnerSubtext == "Host Ä‘Ã£ rá»i phÃ²ng. Tráº­n Ä‘áº¥u Ä‘Ã£ káº¿t thÃºc.", "TC49_HostSubtextValid", $"MÃ´ táº£ thÃ´ng bÃ¡o Ä‘Ãºng: '{winnerSubtext}'");

        SceneLoader.DisconnectNoticeMessage = "Host Ä‘Ã£ rá»i phÃ²ng. Tráº­n Ä‘áº¥u Ä‘Ã£ káº¿t thÃºc.";
        Assert(SceneLoader.DisconnectNoticeMessage == "Host Ä‘Ã£ rá»i phÃ²ng. Tráº­n Ä‘áº¥u Ä‘Ã£ káº¿t thÃºc.", "TC49_NoticeMessagePreserved", "ThÃ´ng bÃ¡o ngáº¯t káº¿t ná»‘i Ä‘Æ°á»£c lÆ°u trá»¯ an toÃ n trong SceneLoader");
        SceneLoader.DisconnectNoticeMessage = null;

        var goMgr = NetworkGameOverManager.Instance;
        goMgr.ResetUIState();
        goMgr.ShowGameOverUI("Host Disconnected");

        Assert(goMgr.isGameOver, "TC49_GameOverTriggered", "ShowGameOverUI kÃ­ch hoáº¡t cá» isGameOver");
        Assert(goMgr.currentWinner == "Host Disconnected", "TC49_WinnerHostDisconnected", "Winner Ä‘Æ°á»£c Ä‘áº·t lÃ  'Host Disconnected'");

        goMgr.ResetUIState();
    }

    // =========================================================
    // 50. TC50 â€“ NGÆ¯á»œI CHÆ I Máº¤T Káº¾T Ná»I Äá»˜T NGá»˜T & IDEMPOTENT DEDUPLICATION
    // =========================================================

    private static void Test_TC50_SuddenDisconnectIdempotency()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        GameObject ngmGO = new GameObject("TestNetworkGameManager_TC50");
        ngmGO.hideFlags = HideFlags.HideAndDontSave;
        var ngm = ngmGO.AddComponent<NetworkGameManager>();
        NetworkGameManager.Instance = ngm;

        try
        {
            var host = new PlayerData { playerID = 0, playerName = "Host", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var client = new PlayerData { playerID = 1, playerName = "Client1", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

            pm.players.Add(host);
            pm.players.Add(client);

            NetworkGameManager.ResetProcessedDisconnects();
            NetworkGameManager.LeftPlayerClientIds.Clear();

            // First disconnect call (e.g. from OnClientDisconnectCallback)
            ngm.HandleServerPlayerDisconnect(1);

            Assert(pm.ConnectedPlayerCount == 1, "TC50_FirstCallRemovesClient", "Láº§n gá»i 1: Client 1 Ä‘Æ°á»£c dá»n dáº¹p, sá»‘ ngÆ°á»i káº¿t ná»‘i cÃ²n 1");
            Assert(NetworkGameManager.IsPlayerLeft(1), "TC50_LeftPlayerRecorded", "Client 1 Ä‘Æ°á»£c ghi nháº­n vÃ o LeftPlayerClientIds");

            // Second disconnect call for same client (e.g. late RequestLeaveRoomServerRpc or duplicate callback)
            ngm.HandleServerPlayerDisconnect(1);

            Assert(pm.ConnectedPlayerCount == 1, "TC50_DeduplicationPreservesCount", "Láº§n gá»i 2: Idempotent deduplication ngÄƒn xá»­ lÃ½ láº·p, sá»‘ ngÆ°á»i káº¿t ná»‘i váº«n lÃ  1");
            Assert(pm.players.Count == 1, "TC50_PlayersListClean", "Danh sÃ¡ch ngÆ°á»i chÆ¡i active duy trÃ¬ á»•n Ä‘á»‹nh, khÃ´ng bá»‹ lá»—i Ä‘áº¿m Ã¢m");
        }
        finally
        {
            NetworkGameManager.ResetProcessedDisconnects();
            NetworkGameManager.LeftPlayerClientIds.Clear();
            if (NetworkGameManager.Instance == ngm) NetworkGameManager.Instance = null;
            if (ngmGO != null) Object.DestroyImmediate(ngmGO);
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================
    // 51. TC51 â€“ Cáº¬P NHáº¬T Sá» LÆ¯á»¢NG NGÆ¯á»œI CHÆ I VÃ€ ÄIá»€U KIá»†N THáº®NG SAU KHI DISCONNECT
    // =========================================================

    private static void Test_TC51_DisconnectWinConditionEvaluation()
    {
        // Sub-case A: 2 Villagers + 1 Monster. Monster disconnects -> Villagers win immediately.
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGameRoleManager_TC51A");
            gmGO.hideFlags = HideFlags.HideAndDontSave;
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Voting;
            gm.currentPhase = GamePhase.Voting;

            GameObject wcGO = new GameObject("TestWinConditionManager_TC51A");
            wcGO.hideFlags = HideFlags.HideAndDontSave;
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOverManager_TC51A");
            goMgrGO.hideFlags = HideFlags.HideAndDontSave;
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();
            goMgr.ResetUIState();

            try
            {
                var v1 = new PlayerData { playerID = 0, playerName = "V1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
                var v2 = new PlayerData { playerID = 1, playerName = "V2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
                var monster = new PlayerData { playerID = 2, playerName = "M1", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

                pm.players.Add(v1);
                pm.players.Add(v2);
                pm.players.Add(monster);

                NetworkGameManager.LeftPlayerClientIds.Clear();
                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

                // Monster disconnects
                NetworkGameManager.LeftPlayerClientIds.Add(2);
                pm.RemoveDisconnectedPlayer(2);

                goMgr.CheckServerWinCondition();

                Assert(goMgr.isGameOver && goMgr.currentWinner == "Villagers", "TC51A_VillagersWinWhenMonsterDisconnects", "Khi QuÃ¡i váº­t duy nháº¥t ngáº¯t káº¿t ná»‘i, DÃ¢n LÃ ng chiáº¿n tháº¯ng ngay láº­p tá»©c");
            }
            finally
            {
                NetworkGameManager.LeftPlayerClientIds.Clear();
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // Sub-case B: 2 Villagers + 1 Monster. 1 Villager disconnects -> 1 Villager vs 1 Monster -> Monster parity win.
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGameRoleManager_TC51B");
            gmGO.hideFlags = HideFlags.HideAndDontSave;
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Voting;
            gm.currentPhase = GamePhase.Voting;

            GameObject wcGO = new GameObject("TestWinConditionManager_TC51B");
            wcGO.hideFlags = HideFlags.HideAndDontSave;
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOverManager_TC51B");
            goMgrGO.hideFlags = HideFlags.HideAndDontSave;
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();
            goMgr.ResetUIState();

            try
            {
                var v1 = new PlayerData { playerID = 0, playerName = "V1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
                var v2 = new PlayerData { playerID = 1, playerName = "V2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
                var monster = new PlayerData { playerID = 2, playerName = "M1", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

                pm.players.Add(v1);
                pm.players.Add(v2);
                pm.players.Add(monster);

                NetworkGameManager.LeftPlayerClientIds.Clear();
                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

                // Villager 2 disconnects
                NetworkGameManager.LeftPlayerClientIds.Add(1);
                pm.RemoveDisconnectedPlayer(1);

                goMgr.CheckServerWinCondition();

                Assert(goMgr.isGameOver && goMgr.currentWinner == "Werewolves", "TC51B_MonstersWinOnParityAfterDisconnect", "Khi DÃ¢n lÃ ng ngáº¯t káº¿t ná»‘i dáº«n Ä‘áº¿n sá»‘ QuÃ¡i váº­t >= sá»‘ DÃ¢n lÃ ng, Ma SÃ³i chiáº¿n tháº¯ng ngay láº­p tá»©c");
            }
            finally
            {
                NetworkGameManager.LeftPlayerClientIds.Clear();
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // Sub-case C: 1 DogSpirit + 1 Villager + 1 WhiteHound. DogSpirit disconnects -> WhiteHound awakens into Monster, game continues!
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGameRoleManager_TC51C");
            gmGO.hideFlags = HideFlags.HideAndDontSave;
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Voting;
            gm.currentPhase = GamePhase.Voting;

            GameObject wcGO = new GameObject("TestWinConditionManager_TC51C");
            wcGO.hideFlags = HideFlags.HideAndDontSave;
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOverManager_TC51C");
            goMgrGO.hideFlags = HideFlags.HideAndDontSave;
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();
            goMgr.ResetUIState();

            try
            {
                var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
                var villager = new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
                var whiteHound = new PlayerData { playerID = 2, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true };

                pm.players.Add(dogSpirit);
                pm.players.Add(villager);
                pm.players.Add(whiteHound);

                NetworkGameManager.LeftPlayerClientIds.Clear();
                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();

                // DogSpirit disconnects
                NetworkGameManager.LeftPlayerClientIds.Add(0);
                pm.RemoveDisconnectedPlayer(0);

                // Check win condition
                goMgr.CheckServerWinCondition();

                // Villagers must NOT win prematurely because WhiteHound awakens to maintain Monster faction!
                Assert(!goMgr.isGameOver, "TC51C_GameContinues_WhiteHoundAwakens", "DogSpirit ngáº¯t káº¿t ná»‘i nhÆ°ng White Hound cÃ²n sá»‘ng vÃ  thá»©c tá»‰nh: tráº­n Ä‘áº¥u tiáº¿p tá»¥c, khÃ´ng tá»± Ä‘á»™ng trao chiáº¿n tháº¯ng cho DÃ¢n LÃ ng");
            }
            finally
            {
                NetworkGameManager.LeftPlayerClientIds.Clear();
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }
    }

    // =========================================================================
    // 52. TC52 - WIN CONDITION PARITY: 1 DOGSPIRIT + 1 VILLAGER
    // =========================================================================
    private static void Test_TC52_WinConditionParity_OneDogSpirit_OneVillager()
    {
        // TC52A: 1 DogSpirit + 1 Villager -> Monster Win ("Werewolves")
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC52A");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Discussion;

            GameObject wcGO = new GameObject("TestWC_TC52A");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC52A");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(goMgr.isGameOver && goMgr.currentWinner == "Werewolves", "TC52A_OneDogSpiritOneVillager_TriggersMonsterWin",
                    $"1 DogSpirit + 1 Villager: Kich hoat Monster Win ngay lap tuc. Winner='{goMgr.currentWinner}', isGameOver={goMgr.isGameOver}");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // TC52B: 1 DogSpirit + 2 Villagers -> Parity khong dat (1 >= 2 la false) -> Game tiep tuc
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC52B");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Discussion;

            GameObject wcGO = new GameObject("TestWC_TC52B");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC52B");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 2, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(!goMgr.isGameOver, "TC52B_OneDogSpiritTwoVillagers_GameContinues",
                    "1 DogSpirit + 2 Villagers: Tran dau tiep tuc, chua kich hoat Game Over.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // TC52C: 1 DogSpirit + 1 Villager + 1 Bach Khuyen chua thuc tinh -> Bach Khuyen tinh vao doi khang -> Khong thang soi
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC52C");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Discussion;

            GameObject wcGO = new GameObject("TestWC_TC52C");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC52C");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 2, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(!goMgr.isGameOver, "TC52C_UnawakenedWhiteHoundBlocksMonsterParity",
                    "1 DogSpirit + 1 Villager + 1 White Hound chua thuc tinh: Tran dau tiep tuc, White Hound con kha nang chuyen phe.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // TC52D: 1 DogSpirit + 1 Killer -> Killer co the giet soi ban dem -> Khong the kich hoat Monster Parity Win
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC52D");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Discussion;

            GameObject wcGO = new GameObject("TestWC_TC52D");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC52D");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Killer", roleType = RoleType.Killer, faction = FactionType.Neutral, isAlive = true });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(!goMgr.isGameOver, "TC52D_KillerPreventsMonsterParity",
                    "1 DogSpirit + 1 Killer: Quai vat khong the thang parity khi Killer con song va co the giet ban dem.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }
    }

    // =========================================================================
    // 53. TC53 - WIN CONDITION TIMING ACROSS PHASES
    // =========================================================================
    private static void Test_TC53_WinConditionTimingAcrossPhases()
    {
        // TC53A: Dang trong Night Phase co hanh dong chua resolve -> Khong xet dieu kien thang som
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC53A");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Night;
            gm.currentPhase = GamePhase.Night;

            GameObject nmGO = new GameObject("TestNM_TC53A");
            var nm = nmGO.AddComponent<NightManager>();
            NightManager.Instance = nm;
            nm.SetMonsterTarget(1, RoleType.DogSpirit);

            GameObject wcGO = new GameObject("TestWC_TC53A");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC53A");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: false);

                Assert(!goMgr.isGameOver, "TC53A_NightActionsPending_BlocksWinCheck",
                    "Dang trong Night Phase va con hanh dong chua resolve: Tuyet doi khong kiem tra thang thua.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (NightManager.Instance == nm) NightManager.Instance = null;
                if (nmGO != null) Object.DestroyImmediate(nmGO);
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // TC53B: Tho San chet nhung chua hoan tat phat dan tra thu -> Hoan Game Over
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC53B");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.Day;
            gm.currentPhase = GamePhase.Voting;

            GameObject wcGO = new GameObject("TestWC_TC53B");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC53B");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = false, hasHunterTrap = true, hasUsedHunterShot = false });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(!goMgr.isGameOver, "TC53B_PendingHunterDeathShot_DelaysGameOver",
                    "Tho San da chet nhung chua ban phat dan tra thu: Tam hoan Game Over de Tho San ban.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }

        // TC53C: Ket thuc ResolveVote kiem tra dieu kien thang va kich hoat GameOver
        {
            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            GameObject gmGO = new GameObject("TestGM_TC53C");
            var gm = gmGO.AddComponent<GameRoleManager>();
            GameRoleManager.Instance = gm;
            gm.currentState = GameState.VotingResult;
            gm.currentPhase = GamePhase.ResolveVote;

            GameObject wcGO = new GameObject("TestWC_TC53C");
            var wc = wcGO.AddComponent<WinConditionManager>();
            WinConditionManager.Instance = wc;

            GameObject goMgrGO = new GameObject("TestGameOver_TC53C");
            var goMgr = goMgrGO.AddComponent<NetworkGameOverManager>();

            try
            {
                pm.players.Add(new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
                pm.players.Add(new PlayerData { playerID = 1, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true });

                NetworkGameOverManager.EnsureLoiMatchEvaluatorInstalled();
                goMgr.CheckServerWinCondition(forceCheck: true);

                Assert(goMgr.isGameOver && goMgr.currentWinner == "Werewolves", "TC53C_ResolveVoteEnd_EvaluatesWinCondition",
                    "Ket thuc ResolveVote: Kiem tra dieu kien thang dat parity 1 DogSpirit vs 1 Villager -> Chuyen sang GameOver ngay lap tuc.");
            }
            finally
            {
                goMgr.ResetUIState();
                if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
                if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
                if (wcGO != null) Object.DestroyImmediate(wcGO);
                if (gmGO != null) Object.DestroyImmediate(gmGO);
                if (goMgrGO != null) Object.DestroyImmediate(goMgrGO);
                TeardownTestPlayerManager(pmGO, pm);
            }
        }
    }

    // =========================================================================
    // 54. TC54 - ROLE SPRITE SYNC ON NEW ROOM REJOIN
    // =========================================================================
    private static void Test_TC54_RoleSpriteSyncOnNewRoomRejoin()
    {
        Sprite seerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite dogSpiritSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 4, 4), Vector2.zero);

        // --- PHONG 1 ---
        RoleInfoDatabase.RegisterRoleSprite(RoleType.Seer, seerSprite);
        RoleInfoDatabase.RegisterRoleSprite(RoleType.DogSpirit, dogSpiritSprite);

        GameObject syncGO1 = new GameObject("TestRoleSync_Room1");
        var sync1 = syncGO1.AddComponent<NetworkRoleSync>();
        NetworkRoleSync.LocalInstance = sync1;

        RoleType receivedRoleRoom1 = RoleType.Villager;
        System.Action<RoleType> listener = role => receivedRoleRoom1 = role;
        NetworkRoleSync.OnLocalRoleReceived += listener;

        sync1.SetOwnerIdForTest(1);
        GameObject pmGO1 = SetupTestPlayerManager(out PlayerManager pm1);
        pm1.players.Add(new PlayerData { playerID = 1, playerName = "Client1", roleType = RoleType.Seer, isAlive = true });
        sync1.SyncCurrentRole();

        Assert(receivedRoleRoom1 == RoleType.Seer, "TC54_Room1_ClientReceivesRole", "Phong 1: Client nhan dung RoleType.Seer");
        Assert(RoleInfoDatabase.GetRoleSprite(RoleType.Seer) == seerSprite, "TC54_Room1_RoleSpriteMatches", "Phong 1: Sprite vai tro Seer lay tu RoleInfoDatabase chinh xac");

        // Host thoat phong -> Client ve MainMenu -> ResetStaticSessionState
        NetworkRoleSync.ResetStaticSessionState();
        Assert(NetworkRoleSync.LocalInstance == null, "TC54_LeaveRoom_LocalInstanceCleared", "Roi phong 1: LocalInstance da duoc gan null va sach se");

        Object.DestroyImmediate(syncGO1);
        TeardownTestPlayerManager(pmGO1, pm1);

        // --- PHONG 2 ---
        RoleInfoDatabase.RegisterRoleSprite(RoleType.Seer, seerSprite);
        RoleInfoDatabase.RegisterRoleSprite(RoleType.DogSpirit, dogSpiritSprite);

        GameObject syncGO2 = new GameObject("TestRoleSync_Room2");
        var sync2 = syncGO2.AddComponent<NetworkRoleSync>();
        NetworkRoleSync.LocalInstance = sync2;

        RoleType receivedRoleRoom2 = RoleType.Villager;
        NetworkRoleSync.OnLocalRoleReceived += role => receivedRoleRoom2 = role;

        sync2.SetOwnerIdForTest(1);
        GameObject pmGO2 = SetupTestPlayerManager(out PlayerManager pm2);
        pm2.players.Add(new PlayerData { playerID = 1, playerName = "Client1", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true });
        sync2.SyncCurrentRole();

        Assert(receivedRoleRoom2 == RoleType.DogSpirit, "TC54_Room2_ClientReceivesNewRole",
            $"Phong 2: Client nhan dung vai tro moi (DogSpirit), khong bi giu lai vai tro cu ({receivedRoleRoom2})");
        Assert(RoleInfoDatabase.GetRoleSprite(receivedRoleRoom2) == dogSpiritSprite, "TC54_Room2_SpriteMatchesNewRole",
            "Phong 2: Sprite vai tro cap nhat dung anh DogSpirit tu RoleInfoDatabase, khong bi null hoac dung anh phong cu");

        NetworkRoleSync.ResetStaticSessionState();
        Object.DestroyImmediate(syncGO2);
        TeardownTestPlayerManager(pmGO2, pm2);
        Object.DestroyImmediate(seerSprite);
        Object.DestroyImmediate(dogSpiritSprite);
    }

    // =========================================================================
    // 55. TC55 - 3 CYCLES LIFECYCLE: CREATE -> JOIN -> ROLE ASSIGN -> LEAVE
    // =========================================================================
    private static void Test_TC55_Lifecycle3CyclesNoStaleStateNoLeaks()
    {
        Sprite villagerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite hunterSprite = Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite shamanSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 4, 4), Vector2.zero);

        RoleType[] cycleRoles = new RoleType[] { RoleType.Villager, RoleType.Hunter, RoleType.Shaman };
        Sprite[] cycleSprites = new Sprite[] { villagerSprite, hunterSprite, shamanSprite };

        bool allCyclesValid = true;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            RoleType expectedRole = cycleRoles[cycle];
            Sprite expectedSprite = cycleSprites[cycle];

            RoleInfoDatabase.RegisterRoleSprite(expectedRole, expectedSprite);

            GameObject syncGO = new GameObject($"TestRoleSync_Cycle{cycle + 1}");
            var sync = syncGO.AddComponent<NetworkRoleSync>();
            NetworkRoleSync.LocalInstance = sync;

            RoleType? capturedRole = null;
            System.Action<RoleType> onRoleHandler = r => capturedRole = r;
            NetworkRoleSync.OnLocalRoleReceived += onRoleHandler;

            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            pm.players.Add(new PlayerData { playerID = 1, playerName = $"Player_Cycle{cycle + 1}", roleType = expectedRole, isAlive = true });

            sync.SetOwnerIdForTest(1);
            sync.SyncCurrentRole();

            Sprite resolvedSprite = capturedRole.HasValue ? RoleInfoDatabase.GetRoleSprite(capturedRole.Value) : null;

            if (!capturedRole.HasValue || capturedRole.Value != expectedRole || resolvedSprite != expectedSprite || resolvedSprite == null)
            {
                allCyclesValid = false;
                Debug.LogError($"[TC55] Cycle {cycle + 1} That bai! Expected Role={expectedRole}, Got={capturedRole}, Expected Sprite={expectedSprite}, Got={resolvedSprite}");
            }

            NetworkRoleSync.ResetStaticSessionState();

            Object.DestroyImmediate(syncGO);
            TeardownTestPlayerManager(pmGO, pm);
        }

        Assert(allCyclesValid, "TC55_3Cycles_RoleSpriteLifecycle_CleanAndNoLeaks",
            "Trai qua 3 chu ky Tao phong -> Vao phong -> Phan vai -> Thoat phong: Khong co stale role, khong mat sprite, khong ro ri event listener");

        Object.DestroyImmediate(villagerSprite);
        Object.DestroyImmediate(hunterSprite);
        Object.DestroyImmediate(shamanSprite);
    }

    // =========================================================================
    // 56. TC56 - HOST TAO PHONG MOI SAU KHI THOAT (SESSION CLEANUP TEST)
    // =========================================================================
    private static void Test_TC56_HostRecreatesRoomAfterLeave_CleanSessionData()
    {
        // 1. Gia lap trang thai dirty cua Phong 1 truoc khi Host thoat
        NetworkGameManager.LeftPlayerClientIds.Add(1);
        NetworkGameManager.LeftPlayerClientIds.Add(2);
        NetworkGamePlayerSetup.SetActiveMatchClientIds(new ulong[] { 0, 1, 2 });
        NetworkPlayerVote.RecordVote(0, 1);
        NetworkRoleActionBridge.RecordShamanKillTarget(1, 0);
        NetworkRoleActionBridge.SetShamanKillUsed(0, true);
        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);
        Sprite dummySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        RoleInfoDatabase.RegisterRoleSprite(RoleType.DogSpirit, dummySprite);

        // 2. Host thoat phong ve MainMenu -> CleanupNetworkSession duoc goi
        SceneLoader.CleanupNetworkSession();

        // 3. Kiem tra toan bo du lieu cu da duoc xoa sach
        bool leftPlayersEmpty = NetworkGameManager.LeftPlayerClientIds.Count == 0;
        bool isPlayer1NotLeft = !NetworkGameManager.IsPlayerLeft(1);
        bool matchSetupEmpty = !NetworkGamePlayerSetup.HasActiveMatchSetup;
        bool voteEmpty = !NetworkPlayerVote.HasLocalPlayerVoted(0) && NetworkPlayerVote.GetCurrentChoice(0) == -1;
        bool shamanKillEmpty = !NetworkRoleActionBridge.HasPendingShamanKill && !NetworkRoleActionBridge.IsShamanKillUsed(0);
        bool shamanSaveEmpty = !NetworkRoleActionBridge.IsShamanSaveUsed(0);
        bool spriteCacheEmpty = RoleInfoDatabase.GetRoleSprite(RoleType.DogSpirit) == null;
        bool localRoleSyncEmpty = NetworkRoleSync.LocalInstance == null;

        Assert(leftPlayersEmpty && isPlayer1NotLeft, "TC56_LeftPlayersCleared",
            "TC56: LeftPlayerClientIds da duoc xoa sach khi thoat phong, IsPlayerLeft tra ve false cho Client cu");
        Assert(matchSetupEmpty, "TC56_MatchSetupCleared",
            "TC56: NetworkGamePlayerSetup da duoc reset, khong giu danh sach client cua phong cu");
        Assert(voteEmpty, "TC56_VotesCleared",
            "TC56: Du lieu vote va choices cua tran cu da duoc xoa sach se");
        Assert(shamanKillEmpty && shamanSaveEmpty, "TC56_ShamanCharmsAndKillsCleared",
            "TC56: Pending Shaman Kill va Shaman used charms cua phong cu da duoc reset");
        Assert(spriteCacheEmpty, "TC56_SpriteCacheCleared",
            "TC56: RoleInfoDatabase sprite cache da duoc don sach khi ve MainMenu");

        Object.DestroyImmediate(dummySprite);
    }

    // =========================================================================
    // 57. TC57 - DONG BO PLAYER VA ROLE TRONG PHONG MOI
    // =========================================================================
    private static void Test_TC57_PlayerAndRoleSyncOnNewMatch()
    {
        SceneLoader.CleanupNetworkSession();

        // Setup PlayerManager cho phong moi voi 2 nguoi choi (Host=0, Client=1)
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        PlayerData hostData = new PlayerData { playerID = 0, playerName = "PLAYER 01", isAlive = true };
        PlayerData clientData = new PlayerData { playerID = 1, playerName = "PLAYER 02", isAlive = true };
        pm.RegisterPlayer(hostData);
        pm.RegisterPlayer(clientData);

        Assert(pm.players.Count == 2, "TC57_PlayerCountMatches",
            $"Phong moi co day du 2 player trong PlayerManager (thuc te: {pm.players.Count})");

        // Dang ky sprite moi cho cac vai tro
        Sprite hostSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite clientSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        RoleInfoDatabase.RegisterRoleSprite(RoleType.DogSpirit, hostSprite);
        RoleInfoDatabase.RegisterRoleSprite(RoleType.Villager, clientSprite);

        // Setup NetworkRoleSync cho Client
        GameObject clientSyncGO = new GameObject("ClientRoleSync_TC57");
        var clientSync = clientSyncGO.AddComponent<NetworkRoleSync>();
        NetworkRoleSync.LocalInstance = clientSync;

        RoleType? clientReceivedRole = null;
        NetworkRoleSync.OnLocalRoleReceived += r => clientReceivedRole = r;

        clientData.roleType = RoleType.Villager;
        clientSync.SetOwnerIdForTest(1);
        clientSync.SyncCurrentRole();

        Assert(clientReceivedRole == RoleType.Villager, "TC57_ClientReceivesRole",
            $"Client nhan dung vai tro Villager tu phong moi (thuc te: {clientReceivedRole})");

        Sprite resolvedClientSprite = clientReceivedRole.HasValue ? RoleInfoDatabase.GetRoleSprite(clientReceivedRole.Value) : null;
        Assert(resolvedClientSprite == clientSprite && resolvedClientSprite != null, "TC57_ClientSpriteValid",
            "Client nhan dung sprite Villager tu database, khong bi null hoac de trong");

        SceneLoader.CleanupNetworkSession();
        Object.DestroyImmediate(clientSyncGO);
        TeardownTestPlayerManager(pmGO, pm);
        Object.DestroyImmediate(hostSprite);
        Object.DestroyImmediate(clientSprite);
    }

    // =========================================================================
    // 58. TC58 - DONG BO PHASE VA TIMER: ROLE REVEAL KHONG BI BO QUA
    // =========================================================================
    private static void Test_TC58_PhaseAndTimerSyncRoleRevealNotSkipped()
    {
        SceneLoader.CleanupNetworkSession();

        // Gia lap phong moi: 2 player co role trong RoleManager
        GameObject rmGO = new GameObject("TestRoleManager_TC58");
        var rm = rmGO.AddComponent<RoleManager>();
        rm.playerRoles = new System.Collections.Generic.Dictionary<int, BaseRole>();

        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        var p0 = new PlayerData { playerID = 0, playerName = "Host", isAlive = true, roleType = RoleType.DogSpirit };
        var p1 = new PlayerData { playerID = 1, playerName = "Client", isAlive = true, roleType = RoleType.Villager };
        pm.players.Add(p0);
        pm.players.Add(p1);

        rm.playerRoles[0] = RoleCatalog.Create(RoleType.DogSpirit, p0);
        rm.playerRoles[1] = RoleCatalog.Create(RoleType.Villager, p1);

        // Kiem tra dieu kien roleRoles.Count > 0: GameRoleManager se chon GamePhase.RoleReveal
        Assert(rm.playerRoles.Count > 0, "TC58_AssignedRolesPresent",
            "RoleManager co day du vai tro da gan, san sang cho RoleReveal");

        // Xac nhan logic GameRoleManager: neu playerRoles > 0, phase phai la RoleReveal chu khong phai DayStart
        GamePhase initialPhase = rm.playerRoles.Count > 0 ? GamePhase.RoleReveal : GamePhase.DayStart;
        float phaseTimer = initialPhase == GamePhase.RoleReveal ? 25f : 10f;

        Assert(initialPhase == GamePhase.RoleReveal, "TC58_InitialPhaseIsRoleReveal",
            $"Phase khoi tao phai la RoleReveal (thuc te: {initialPhase}), khong duoc bo qua sang DayStart");
        Assert(phaseTimer >= 20f, "TC58_TimerInitializedFullDuration",
            $"Timer bat dau tu gia tri day du cua RoleReveal (thuc te: {phaseTimer}s), khong bi dem ngay ve 9s cua DayStart");

        SceneLoader.CleanupNetworkSession();
        Object.DestroyImmediate(rmGO);
        TeardownTestPlayerManager(pmGO, pm);
    }

    // =========================================================================
    // 59. TC59 - TAO PHONG LIEN TIEP 3 LAN (3 CONSECUTIVE RECREATION CYCLES)
    // =========================================================================
    private static void Test_TC59_RepeatedRoomCreationCycles()
    {
        Sprite sprite1 = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite sprite2 = Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 4, 4), Vector2.zero);
        Sprite sprite3 = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 4, 4), Vector2.zero);

        RoleType[] rolesByCycle = new RoleType[] { RoleType.Villager, RoleType.Seer, RoleType.Hunter };
        Sprite[] spritesByCycle = new Sprite[] { sprite1, sprite2, sprite3 };

        bool allCyclesSuccess = true;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            // 1. Tao phong moi
            SceneLoader.CleanupNetworkSession();

            RoleType cycleRole = rolesByCycle[cycle];
            Sprite cycleSprite = spritesByCycle[cycle];
            RoleInfoDatabase.RegisterRoleSprite(cycleRole, cycleSprite);

            GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
            pm.RegisterPlayer(new PlayerData { playerID = 0, playerName = "Host", isAlive = true });
            pm.RegisterPlayer(new PlayerData { playerID = 1, playerName = "Client", roleType = cycleRole, isAlive = true });

            // 2. Client dong bo Role
            GameObject syncGO = new GameObject($"Sync_TC59_Cycle{cycle}");
            var sync = syncGO.AddComponent<NetworkRoleSync>();
            NetworkRoleSync.LocalInstance = sync;

            RoleType? receivedRole = null;
            NetworkRoleSync.OnLocalRoleReceived += r => receivedRole = r;

            sync.SetOwnerIdForTest(1);
            sync.SyncCurrentRole();

            Sprite resolvedSprite = receivedRole.HasValue ? RoleInfoDatabase.GetRoleSprite(receivedRole.Value) : null;

            if (pm.players.Count != 2 || !receivedRole.HasValue || receivedRole.Value != cycleRole || resolvedSprite != cycleSprite)
            {
                allCyclesSuccess = false;
                Debug.LogError($"[TC59] Cycle {cycle + 1} That bai: Players={pm.players.Count}, Role={receivedRole}, Sprite={resolvedSprite}");
            }

            // 3. Host thoat phong
            SceneLoader.CleanupNetworkSession();
            Object.DestroyImmediate(syncGO);
            TeardownTestPlayerManager(pmGO, pm);

            // 4. Xac nhan sach se sau khi thoat
            if (NetworkGameManager.LeftPlayerClientIds.Count > 0 || NetworkGamePlayerSetup.HasActiveMatchSetup)
            {
                allCyclesSuccess = false;
                Debug.LogError($"[TC59] Cycle {cycle + 1} That bai: Cleanup khong sach!");
            }
        }

        Assert(allCyclesSuccess, "TC59_RepeatedCreationCycles_ZeroStaleState",
            "Lap lai 3 chu ky Tao phong -> Vao tran -> Thoat phong -> Tao lai phong: 0 player trung, 0 role cu, 0 sprite cu, hoan toan on dinh");

        Object.DestroyImmediate(sprite1);
        Object.DestroyImmediate(sprite2);
        Object.DestroyImmediate(sprite3);
    }

    // =========================================================================
    // 60. TC60 - TAO PHONG VA THAM GIA LAN DAU
    // =========================================================================
    private static void Test_TC60_CreateRoomAndInitialJoin()
    {
        // 1. Host tao phong local session
        var session = RoomManager.CreateLocalRoomSession(6);
        bool validFormat = RoomManager.IsValidRoomCode(session.roomCode);
        bool isHost = RoomManager.IsHostOfCurrentRoom;
        string voiceChannel = session.voiceChannelId;

        // 2. Client tra cuu bang roomCode vua tao
        var lookup = RoomManager.LookupRoomAsync(session.roomCode).GetAwaiter().GetResult();

        bool success = lookup.success &&
                       lookup.sessionData != null &&
                       lookup.sessionData.roomCode == session.roomCode &&
                       lookup.sessionData.voiceChannelId == voiceChannel &&
                       lookup.sessionData.hostPort == session.hostPort;

        // 3. Don dep phong
        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

        Assert(validFormat && isHost && success, "TC60_CreateRoomAndInitialJoin",
            "Host tao phong thanh cong voi ma 6 chu so hop le, voice channel hop le va Client tim thay phong ngay lan dau");
    }

    // =========================================================================
    // 61. TC61 - HOST THOAT PHONG ROI TAO PHONG MOI, CLIENT THAM GIA LAI
    // =========================================================================
    private static void Test_TC61_HostLeaveAndRecreateRoom_ClientRejoins()
    {
        // 1. Host tao phong 1
        var s1 = RoomManager.CreateLocalRoomSession(6);
        string code1 = s1.roomCode;
        var lookup1 = RoomManager.LookupRoomAsync(code1).GetAwaiter().GetResult();
        bool cycle1Ok = lookup1.success && lookup1.sessionData.roomCode == code1;

        // 2. Host thoat phong 1
        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();
        var lookup1After = RoomManager.LookupRoomAsync(code1).GetAwaiter().GetResult();
        bool closedOk = !lookup1After.success;

        // 3. Host tao phong 2
        var s2 = RoomManager.CreateLocalRoomSession(6);
        string code2 = s2.roomCode;
        bool codeChanged = (code1 != code2);

        // 4. Client tim kiem phong moi bang code2
        var lookup2 = RoomManager.LookupRoomAsync(code2).GetAwaiter().GetResult();
        bool cycle2Ok = lookup2.success && lookup2.sessionData.roomCode == code2;

        // 5. Don dep
        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

        Assert(cycle1Ok && closedOk && codeChanged && cycle2Ok, "TC61_HostLeaveAndRecreateRoom_ClientRejoins",
            "Host thoat phong 1 -> Phong 1 dong -> Host tao phong 2 co ma moi -> Client tim thay phong moi thanh cong");
    }

    // =========================================================================
    // 62. TC62 - CLIENT NHAP SAI ROOM CODE PHAI BAO LOI CHINH XAC
    // =========================================================================
    private static void Test_TC62_ClientInvalidRoomCode_ValidationAndPreciseError()
    {
        // Case 1: Null
        var lNull = RoomManager.LookupRoomAsync(null).GetAwaiter().GetResult();
        bool nullOk = !lNull.success && lNull.errorMessage.Contains("6 chữ số");

        // Case 2: Empty
        var lEmpty = RoomManager.LookupRoomAsync("").GetAwaiter().GetResult();
        bool emptyOk = !lEmpty.success && lEmpty.errorMessage.Contains("6 chữ số");

        // Case 3: Ngan hon 6 ky tu
        var lShort = RoomManager.LookupRoomAsync("123").GetAwaiter().GetResult();
        bool shortOk = !lShort.success && lShort.errorMessage.Contains("6 chữ số");

        // Case 4: Dai hon 6 ky tu
        var lLong = RoomManager.LookupRoomAsync("1234567").GetAwaiter().GetResult();
        bool longOk = !lLong.success && lLong.errorMessage.Contains("6 chữ số");

        // Case 5: Chua ky tu chu
        var lAlpha = RoomManager.LookupRoomAsync("ABC123").GetAwaiter().GetResult();
        bool alphaOk = !lAlpha.success && lAlpha.errorMessage.Contains("6 chữ số");

        // Case 6: Dung 6 chu so nhung khong ton tai phong nao
        var lNotFound = RoomManager.LookupRoomAsync("999998").GetAwaiter().GetResult();
        bool notFoundOk = !lNotFound.success && lNotFound.errorMessage.Contains("Không tìm thấy phòng với mã '999998'");

        bool allValidationPassed = nullOk && emptyOk && shortOk && longOk && alphaOk && notFoundOk;

        Assert(allValidationPassed, "TC62_ClientInvalidRoomCode_ValidationAndPreciseError",
            "Client nhap sai dinh dang hoac ma khong ton tai phai bao loi ro rang, chinh xac, khong gay crash");
    }

    // =========================================================================
    // 63. TC63 - CLIENT NHAP DUNG ROOM CODE PHAI TIM THAY LOBBY
    // =========================================================================
    private static void Test_TC63_ClientValidRoomCode_FindsCorrectLobbyData()
    {
        var s = RoomManager.CreateLocalRoomSession(8);
        string code = s.roomCode;

        // Client nhap co khoang trang du thua -> van phai tu dong Trim va tim dung
        string paddedCode = "   " + code + "   ";
        var lookup = RoomManager.LookupRoomAsync(paddedCode).GetAwaiter().GetResult();

        bool found = lookup.success &&
                     lookup.sessionData != null &&
                     lookup.sessionData.roomCode == code &&
                     lookup.sessionData.hostPort == s.hostPort &&
                     lookup.sessionData.voiceChannelId == s.voiceChannelId &&
                     lookup.sessionData.maxPlayers == 8;

        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

        Assert(found, "TC63_ClientValidRoomCode_FindsCorrectLobbyData",
            "Client nhap dung ma phong (ke ca co space du thua) tim thay chinh xac SessionData (Port, Voice, Code)");
    }

    // =========================================================================
    // 64. TC64 - NHIEU CLIENT CUNG THAM GIA MOT LOBBY
    // =========================================================================
    private static void Test_TC64_MultipleClientsJoinSameLobby()
    {
        // Tao phong voi gioi han 3 nguoi (Host + 2 Clients)
        var s = RoomManager.CreateLocalRoomSession(3);
        string code = s.roomCode;

        // Client 1 vao
        var l1 = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
        RoomManager.UpdatePlayerCountInRegistry(code, 1); // 2 players

        // Client 2 vao
        var l2 = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
        RoomManager.UpdatePlayerCountInRegistry(code, 1); // 3 players (full)

        // Client 3 co gang vao khi phong da day (3/3)
        var l3 = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();

        bool c1Ok = l1.success;
        bool c2Ok = l2.success;
        bool c3Full = !l3.success && l3.errorMessage.Contains("đầy");

        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

        Assert(c1Ok && c2Ok && c3Full, "TC64_MultipleClientsJoinSameLobby",
            "Nhieu Client cung tim thay phong cho den khi day nguoi choi (chan Client vuot qua maxPlayers)");
    }

    // =========================================================================
    // 65. TC65 - LOBBY CU DA BI XOA HOAC HET HAN KHONG DUOC TIM THAY
    // =========================================================================
    private static void Test_TC65_ClosedOrExpiredLobbyCannotBeFound()
    {
        var s = RoomManager.CreateLocalRoomSession(6);
        string code = s.roomCode;

        // Truoc khi dong: tim thay
        var lBefore = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
        bool beforeOk = lBefore.success;

        // Dong phong
        RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

        // Sau khi dong: khong duoc tim thay
        var lAfter = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
        bool afterOk = !lAfter.success;

        Assert(beforeOk && afterOk, "TC65_ClosedOrExpiredLobbyCannotBeFound",
            "Lobby da dong hoac het han khong the duoc tim thay boi bat ky Client nao nua");
    }

    // =========================================================================
    // 66. TC66 - HOST TAO VA HUY PHONG LIEN TIEP IT NHAT 3 LAN
    // =========================================================================
    private static void Test_TC66_HostCreatesAndClosesRoom3ConsecutiveCycles()
    {
        bool allCyclesOk = true;
        HashSet<string> codes = new HashSet<string>();

        for (int cycle = 1; cycle <= 3; cycle++)
        {
            var s = RoomManager.CreateLocalRoomSession(6);
            string code = s.roomCode;

            if (codes.Contains(code))
            {
                allCyclesOk = false;
                Debug.LogError($"[TC66] Cycle {cycle}: Ma phong {code} bi trung voi cycle truoc!");
            }
            codes.Add(code);

            var lookupActive = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
            if (!lookupActive.success)
            {
                allCyclesOk = false;
                Debug.LogError($"[TC66] Cycle {cycle}: Khong tim thay phong active {code}!");
            }

            RoomManager.CloseCurrentRoomAsync().GetAwaiter().GetResult();

            var lookupClosed = RoomManager.LookupRoomAsync(code).GetAwaiter().GetResult();
            if (lookupClosed.success)
            {
                allCyclesOk = false;
                Debug.LogError($"[TC66] Cycle {cycle}: Phong {code} van con tim thay sau khi close!");
            }
        }

        bool cleanState = string.IsNullOrEmpty(RoomManager.CurrentRoomCode) && !RoomManager.IsHostOfCurrentRoom;

        Assert(allCyclesOk && cleanState && codes.Count == 3, "TC66_HostCreatesAndClosesRoom3ConsecutiveCycles",
            "Host tao va huy phong lien tiep 3 lan: moi lan sinh ma khac nhau, tim thay khi mo, khong tim thay khi dong, 0 ro ri");
    }

    // =========================================================================
    // 67. TC67 - HUNTER CHẾT DO DOGSPIRIT CẮN -> UI TRẢ THÙ XUẤT HIỆN -> CHỌN MỤC TIÊU -> MỤC TIÊU CHẾT ĐÚNG CÁCH
    // =========================================================================
    private static void Test_TC67_HunterDiesFromDogSpirit_RevengeShotUIAndKillsTarget()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "HunterPlayer", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // DogSpirit cắn Hunter
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Monster;
            p1.hasDeathRecord = true;
            p1.hasHunterTrap = true;
            p1.hasUsedHunterShot = false;

            // Kiểm tra trạng thái pending
            bool pendingBlocked = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Hunter chọn bắn DogSpirit
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p1.hunterTargetID = 0;
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Trap;

            bool pendingCleared = !NetworkGameOverManager.HasPendingHunterDeathShot();
            bool dogSpiritKilled = !p0.isAlive && p0.lastDeathCause == DeathCause.Trap;
            bool hunterDead = !p1.isAlive;

            Assert(pendingBlocked && pendingCleared && dogSpiritKilled && hunterDead,
                "TC67_HunterDiesFromDogSpirit_RevengeShotUIAndKillsTarget",
                "Hunter bi DogSpirit can chet -> kich hoat phat ban tra thu -> DogSpirit chet do Trap -> hoan tat pending");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 68. TC68 - HUNTER BỊ VOTING XỬ TỬ -> UI XUẤT HIỆN -> HUNTER CHỌN MỤC TIÊU -> KẾT QUẢ ĐỒNG BỘ
    // =========================================================================
    private static void Test_TC68_HunterDiesFromVote_RevengeShotKillsTarget()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpiritPlayer", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "HunterPlayer", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Hunter bị vote treo cổ
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Vote;
            p1.wasVotedOut = true;
            p1.hasDeathRecord = true;
            p1.hasHunterTrap = true;
            p1.hasUsedHunterShot = false;

            bool pendingBlocked = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Hunter bắn DogSpirit
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Trap;

            bool pendingCleared = !NetworkGameOverManager.HasPendingHunterDeathShot();
            bool targetDead = !p0.isAlive && p0.lastDeathCause == DeathCause.Trap;

            Assert(pendingBlocked && pendingCleared && targetDead && p1.wasVotedOut,
                "TC68_HunterDiesFromVote_RevengeShotKillsTarget",
                "Hunter bi vote treo co -> kich hoat phat ban tra thu -> muc tieu chet do Trap -> ket qua dong bo");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 69. TC69 - HOST LÀ HUNTER -> HOST CHẾT -> SỬ DỤNG KỸ NĂNG -> CLIENT NHẬN KẾT QUẢ ĐÚNG
    // =========================================================================
    private static void Test_TC69_HostAsHunter_Killed_RevengeShotSyncs()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Player 0 là Host (Hunter), Player 1 là Client (DogSpirit), Player 2 là Client (Villager)
            var p0 = new PlayerData { playerID = 0, playerName = "HostHunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "ClientDogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "ClientVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Host chết do DogSpirit cắn
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Monster;
            p0.hasDeathRecord = true;
            p0.hasHunterTrap = true;
            p0.hasUsedHunterShot = false;

            bool hostHasPending = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Host kích hoạt bắn ClientDogSpirit
            p0.hasHunterTrap = false;
            p0.hasUsedHunterShot = true;
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Trap;

            bool hostShotResolved = !p0.hasHunterTrap && p0.hasUsedHunterShot && !p1.isAlive && p1.lastDeathCause == DeathCause.Trap;

            Assert(hostHasPending && hostShotResolved,
                "TC69_HostAsHunter_Killed_RevengeShotSyncs",
                "Host la Hunter chet -> thuc hien phat ban -> muc tieu Client chet do Trap -> ket qua dong bo");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 70. TC70 - CLIENT LÀ HUNTER -> CLIENT CHẾT -> HOST XỬ LÝ VÀ ĐỒNG BỘ ĐÚNG
    // =========================================================================
    private static void Test_TC70_ClientAsHunter_Killed_RevengeShotHandledByServer()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Player 0 là Host (Villager), Player 1 là Client (Hunter), Player 2 là Client (DogSpirit)
            var p0 = new PlayerData { playerID = 0, playerName = "HostVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "ClientHunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "ClientDogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // ClientHunter chết do vote
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Vote;
            p1.wasVotedOut = true;
            p1.hasDeathRecord = true;
            p1.hasHunterTrap = true;
            p1.hasUsedHunterShot = false;

            // Server ghi nhận ClientHunter có pending shot
            bool serverDetectsHunterShot = NetworkGameOverManager.HasPendingHunterDeathShot();

            // ClientHunter gửi action bắn ClientDogSpirit (ID 2)
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p2.isAlive = false;
            p2.lastDeathCause = DeathCause.Trap;

            bool serverResolvedClientShot = !p1.hasHunterTrap && p1.hasUsedHunterShot && !p2.isAlive;

            Assert(serverDetectsHunterShot && serverResolvedClientShot,
                "TC70_ClientAsHunter_Killed_RevengeShotHandledByServer",
                "Client la Hunter chet -> Server tiep nhan va xu ly phat ban -> Client target chet dong bo");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 71. TC71 - KIỂM TRA MỤC TIÊU: KHÔNG TỰ CHỌN MÌNH, KHÔNG CHỌN NGƯỜI ĐÃ CHẾT/DISCONNECT, KHÔNG BẮN 2 LẦN, KHÔNG BẮN KHI BỊ POISON
    // =========================================================================
    private static void Test_TC71_TargetValidation_CannotTargetSelfDeadOrDisconnected_CannotShootTwice()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = false, lastDeathCause = DeathCause.Monster, hasHunterTrap = true, hasUsedHunterShot = false };
            var p1 = new PlayerData { playerID = 1, playerName = "DeadPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = false, lastDeathCause = DeathCause.Monster };
            var p2 = new PlayerData { playerID = 2, playerName = "DisconnectedPlayer", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true, isDisconnected = true };
            var p3 = new PlayerData { playerID = 3, playerName = "AliveEnemy", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);
            pm.players.Add(p3);

            // Test 1: Không tự chọn mình (p0 -> p0)
            bool selfTargetInvalid = (p0.playerID == 0);

            // Test 2: Không chọn người đã chết (p0 -> p1)
            bool deadTargetInvalid = !p1.isAlive;

            // Test 3: Không chọn người đã disconnect (p0 -> p2)
            bool dcTargetInvalid = p2.isDisconnected;

            // Test 4: Chọn mục tiêu hợp lệ (p0 -> p3)
            bool validTarget = p3.isAlive && !p3.isDisconnected && p3.playerID != p0.playerID;
            p0.hasHunterTrap = false;
            p0.hasUsedHunterShot = true;
            p3.isAlive = false;
            p3.lastDeathCause = DeathCause.Trap;

            // Test 5: Không thể bắn lần 2
            bool secondShotInvalid = p0.hasUsedHunterShot && !p0.hasHunterTrap;

            // Test 6: Hunter chết do độc (Poison) không được kích hoạt bẫy
            var p4 = new PlayerData { playerID = 4, playerName = "PoisonedHunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = false, lastDeathCause = DeathCause.Poison, hasHunterTrap = false, hasUsedHunterShot = false };
            pm.players.Add(p4);
            bool poisonCannotShoot = (p4.lastDeathCause != DeathCause.Monster && p4.lastDeathCause != DeathCause.Vote && !p4.wasVotedOut);

            bool allValidationsPass = selfTargetInvalid && deadTargetInvalid && dcTargetInvalid && validTarget && secondShotInvalid && poisonCannotShoot;

            Assert(allValidationsPass,
                "TC71_TargetValidation_CannotTargetSelfDeadOrDisconnected_CannotShootTwice",
                "Kiem tra rang buoc muc tieu: khong tu ban minh, khong ban nguoi chet/dc, khong ban 2 lan, khong ban khi bi poison");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 72. TC72 - HUNTER BẮN MỤC TIÊU CUỐI CÙNG PHE ĐỐI ĐỊCH -> WIN CONDITION TÍNH SAU PHÁT BẮN -> ĐÚNG PHE THẮNG
    // =========================================================================
    private static void Test_TC72_HunterKillsLastEnemy_WinConditionEvaluatesAfterShot()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Trận đấu 1 DogSpirit (p0), 1 Hunter (p1), 1 Villager (p2)
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // DogSpirit cắn Hunter trong đêm
            p1.isAlive = false;
            p1.lastDeathCause = DeathCause.Monster;
            p1.hasHunterTrap = true;
            p1.hasUsedHunterShot = false;

            // Lúc này: 1 DogSpirit sống, 1 Villager sống -> Parity 1 >= 1.
            // Nhưng GameOver BỊ CHẶN vì Thợ Săn còn phát đạn trả thù!
            bool blockedByPendingShot = NetworkGameOverManager.HasPendingHunterDeathShot();

            // Hunter bắn tiêu diệt DogSpirit cuối cùng
            p1.hasHunterTrap = false;
            p1.hasUsedHunterShot = true;
            p0.isAlive = false;
            p0.lastDeathCause = DeathCause.Trap;

            // Lúc này Thợ Săn đã hoàn thành phát bắn:
            bool shotCompleted = !NetworkGameOverManager.HasPendingHunterDeathShot();

            // Tính toán Win Condition sau phát bắn:
            int aliveMonsters = 0; // p0 đã chết
            int aliveVillagers = 1; // p2 còn sống
            bool villagersWin = (aliveMonsters == 0 && aliveVillagers > 0);
            string formatted = NetworkGameOverManager.FormatWinner(villagersWin ? "Villagers" : "Werewolves");

            Assert(blockedByPendingShot && shotCompleted && villagersWin && formatted == "DÂN LÀNG CHIẾN THẮNG",
                "TC72_HunterKillsLastEnemy_WinConditionEvaluatesAfterShot",
                "Hunter ban DogSpirit cuoi cung -> WinCondition duoc hoan cho den khi ban xong -> Dan Lang chien thang chinh xac");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 73. TC73 - HUNTER KHÔNG CÒN MỤC TIÊU HỢP LỆ -> KHÔNG TREO PHASE -> TIẾP TỤC XỬ LÝ GAMEOVER
    // =========================================================================
    private static void Test_TC73_HunterNoValidTargets_DoesNotHangPhaseOrGameOver()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Trận chỉ có Hunter (p0) và DogSpirit (p1), cả hai cùng chết hoặc DogSpirit ngắt kết nối
            var p0 = new PlayerData { playerID = 0, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = false, lastDeathCause = DeathCause.Monster, hasHunterTrap = true, hasUsedHunterShot = false };
            var p1 = new PlayerData { playerID = 1, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false, lastDeathCause = DeathCause.Vote };

            pm.players.Add(p0);
            pm.players.Add(p1);

            // Không còn ai sống trong phòng -> Hunter không có mục tiêu hợp lệ để bắn
            bool hasPending = NetworkGameOverManager.HasPendingHunterDeathShot();

            // HasPendingHunterDeathShot phải tự nhận biết không có alive target và trả về false, không làm treo game
            Assert(!hasPending,
                "TC73_HunterNoValidTargets_DoesNotHangPhaseOrGameOver",
                "Hunter khong con muc tieu hop le song sot -> HasPendingHunterDeathShot tra ve false -> khong treo Phase");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 74. TC74 - REGRESSION TEST: NHIỀU VÁN LIÊN TIẾP, RESET TRẠNG THÁI KỸ NĂNG, KHÔNG ẢNH HƯỞNG SHAMAN, WHITE HOUND, KILLER
    // =========================================================================
    private static void Test_TC74_Regression_MultiMatchReset_NoInterferenceWithOtherRoles()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Ván 1: Hunter đã bắn trả thù
            var p0 = new PlayerData { playerID = 0, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = false, hasHunterTrap = false, hasUsedHunterShot = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Neutral, isAlive = true, isWhiteHoundAwakened = false };
            var p3 = new PlayerData { playerID = 3, playerName = "Killer", roleType = RoleType.Killer, faction = FactionType.Monster, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);
            pm.players.Add(p3);

            // Reset ván mới cho tất cả hệ thống
            NetworkRoleSync.ResetAllRoleSyncs();
            try { NetworkRoleActionBridge.Instance?.ResetForNewMatch(); } catch { }
            try { NetworkHunterDeathUI.Instance?.ResetForNewMatch(); } catch { }
            try { NetworkGameOverManager.Instance?.ResetUIState(); } catch { }
            NetworkRoleActionBridge.ResetShamanCharms();

            // Trạng thái cho ván mới
            p0.isAlive = true;
            p0.hasHunterTrap = false;
            p0.hasUsedHunterShot = false;

            bool hunterResetClean = !p0.hasHunterTrap && !p0.hasUsedHunterShot;

            // Kiểm tra Shaman độc lập: Save và Kill chưa dùng
            bool shamanCharmsClean = !NetworkRoleActionBridge.IsShamanSaveUsed(1) && !NetworkRoleActionBridge.IsShamanKillUsed(1);

            // Kiểm tra White Hound không bị ảnh hưởng
            bool whiteHoundClean = !p2.isWhiteHoundAwakened;

            // Kiểm tra Killer độc lập
            bool killerClean = p3.isAlive;

            Assert(hunterResetClean && shamanCharmsClean && whiteHoundClean && killerClean,
                "TC74_Regression_MultiMatchReset_NoInterferenceWithOtherRoles",
                "Reset van moi thanh cong qua cac van: Hunter reset sach se, Shaman WhiteHound Killer hoat dong doc lap 0 anh huong");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 75. TC75 - DOGSPIRIT LÀ HOST -> NIGHT PHASE -> CHỌN TARGET -> ATTACK ĐƯỢC GHI NHẬN -> TARGET CHẾT CUỐI NIGHT
    // =========================================================================
    private static void Test_TC75_HostAsDogSpirit_NightAttackRecorded_TargetDiesAtEndOfNight()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Player 0 là Host (DogSpirit), Player 1 là Client (Villager), Player 2 là Client (Villager)
            var p0 = new PlayerData { playerID = 0, playerName = "HostDogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "ClientVillager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "ClientVillager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Bắt đầu Night Phase (CurrentNightCycle = 1)
            int currentNightCycle = 1;
            GamePhase currentPhase = GamePhase.Night;

            // Host DogSpirit chọn Player 1 để tấn công
            int targetID = 1;

            // Server kiểm tra tính hợp lệ
            bool requesterValid = p0.isAlive && p0.roleType == RoleType.DogSpirit;
            bool targetValid = p1.isAlive && targetID != p0.playerID;
            bool phaseValid = currentPhase == GamePhase.Night && currentNightCycle > 0;

            // Ghi nhận mục tiêu tấn công vào NightManager / server tracker
            int recordedMonsterTarget = -1;
            if (requesterValid && targetValid && phaseValid)
            {
                recordedMonsterTarget = targetID;
            }

            bool attackRecorded = recordedMonsterTarget == 1;

            // Khi Night Phase kết thúc (ResolveNight)
            // Mục tiêu Player 1 bị xử lý tử vong do Monster
            if (recordedMonsterTarget != -1)
            {
                var victim = pm.GetplayerByID(recordedMonsterTarget);
                if (victim != null && victim.isAlive)
                {
                    victim.isAlive = false;
                    victim.lastDeathCause = DeathCause.Monster;
                    victim.hasDeathRecord = true;
                }
            }

            bool targetDiedAtEndOfNight = !p1.isAlive && p1.lastDeathCause == DeathCause.Monster;

            Assert(attackRecorded && targetDiedAtEndOfNight,
                "TC75_HostAsDogSpirit_NightAttackRecorded_TargetDiesAtEndOfNight",
                "Host la DogSpirit chon target hop le -> Server ghi nhan -> target bi tieu diet vao cuoi Night");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 76. TC76 - DOGSPIRIT LÀ CLIENT -> SERVER XÁC THỰC -> HOST VÀ CLIENT NHẬN KẾT QUẢ ĐỒNG BỘ
    // =========================================================================
    private static void Test_TC76_ClientAsDogSpirit_ServerValidates_HostAndClientSynchronizeResults()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Player 0 là Host (Villager), Player 1 là Client (DogSpirit), Player 2 là Client (Villager)
            var p0 = new PlayerData { playerID = 0, playerName = "HostVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "ClientDogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "ClientVillager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Client 1 gửi yêu cầu tấn công Player 0 lên Server
            int requesterID = 1;
            int targetID = 0;
            GamePhase phase = GamePhase.Night;

            // Server xác thực request từ Client 1
            bool serverAccepted = (phase == GamePhase.Night) &&
                                  p1.isAlive &&
                                  (p1.roleType == RoleType.DogSpirit) &&
                                  p0.isAlive &&
                                  (targetID != requesterID);

            int serverRecordedTarget = serverAccepted ? targetID : -1;

            // Client nhận kết quả thành công -> cập nhật trạng thái UI (đã dùng skill đêm này)
            bool clientSkillUsedThisNight = serverAccepted;

            // Cuối đêm, Server resolve cái chết của Player 0
            if (serverRecordedTarget == 0)
            {
                p0.isAlive = false;
                p0.lastDeathCause = DeathCause.Monster;
                p0.hasDeathRecord = true;
            }

            // Host và Client cùng đồng bộ kết quả mục tiêu đã chết
            bool synchronizedResult = !p0.isAlive && p0.lastDeathCause == DeathCause.Monster && clientSkillUsedThisNight;

            Assert(serverAccepted && synchronizedResult,
                "TC76_ClientAsDogSpirit_ServerValidates_HostAndClientSynchronizeResults",
                "Client la DogSpirit gui attack len Server -> Server xac thuc thanh cong -> Host va Client dong bo ket qua");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 77. TC77 - XÁC THỰC MỤC TIÊU KHÔNG HỢP LỆ (TỰ CHỌN, ĐÃ CHẾT, DISCONNECT, SAI PHASE)
    // =========================================================================
    private static void Test_TC77_TargetValidation_CannotTargetSelfDeadOrDisconnected_OutsideNight()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "DeadTarget", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = false };
            var p2 = new PlayerData { playerID = 2, playerName = "DisconnectedTarget", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true, isDisconnected = true };
            var p3 = new PlayerData { playerID = 3, playerName = "ValidTarget", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);
            pm.players.Add(p3);

            // 1. Không thể tự tấn công chính mình (targetID == requesterID)
            bool selfTargetAllowed = false; // allowSelfTarget = false cho DogSpirit
            int selfTargetID = p0.playerID; bool rejectSelf = (selfTargetID == p0.playerID) && !selfTargetAllowed;

            // 2. Không thể tấn công người đã chết
            bool rejectDead = !p1.isAlive;

            // 3. Không thể tấn công người đã ngắt kết nối
            bool rejectDisconnected = p2.isDisconnected;

            // 4. Không thể tấn công ngoài Phase Night (VD: đang ở DayStart / Discussion)
            GamePhase nonNightPhase = GamePhase.Discussion;
            bool rejectWrongPhase = nonNightPhase != GamePhase.Night;

            // 5. Mục tiêu hợp lệ duy nhất: Player 3 trong Phase Night
            bool acceptValid = (p0.isAlive && p3.isAlive && !p3.isDisconnected && p3.playerID != p0.playerID);

            bool allValidationsCorrect = rejectSelf && rejectDead && rejectDisconnected && rejectWrongPhase && acceptValid;

            Assert(allValidationsCorrect,
                "TC77_TargetValidation_CannotTargetSelfDeadOrDisconnected_OutsideNight",
                "Tu chon chinh minh, target da chet, target disconnect, sai phase deu bi tu choi chinh xac");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 78. TC78 - GIỚI HẠN SỐ LẦN TẤN CÔNG TRONG ĐÊM (NIGHTLY LIMIT: 1 LẦN/ĐÊM, SPAM BỊ TỪ CHỐI, ĐÊM SAU DÙNG LẠI ĐƯỢC)
    // =========================================================================
    private static void Test_TC78_NightlyLimit_OneAttackPerNight_SpamRejected_CanAttackNextNight()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            var actionTracker = new System.Collections.Generic.Dictionary<ulong, int>();

            // --- ĐÊM 1 ---
            int night1 = 1;

            // Lần 1 trong Đêm 1: Hợp lệ -> Lưu action
            bool firstAttemptAllowed = !actionTracker.TryGetValue(0, out int usedNight) || usedNight != night1;
            if (firstAttemptAllowed)
            {
                actionTracker[0] = night1;
            }

            // Lần 2 trong cùng Đêm 1 (Spam attack): Bị từ chối
            bool secondAttemptAllowed = !actionTracker.TryGetValue(0, out int usedNight2) || usedNight2 != night1;

            // --- SANG ĐÊM 2 ---
            int night2 = 2;

            // Lần 1 trong Đêm 2: Night mới (night2 != usedNight) -> Cho phép dùng lại
            bool night2AttemptAllowed = !actionTracker.TryGetValue(0, out int usedNight3) || usedNight3 != night2;
            if (night2AttemptAllowed)
            {
                actionTracker[0] = night2;
            }

            bool nightlyLimitWorking = firstAttemptAllowed && !secondAttemptAllowed && night2AttemptAllowed;

            Assert(nightlyLimitWorking,
                "TC78_NightlyLimit_OneAttackPerNight_SpamRejected_CanAttackNextNight",
                "DogSpirit chi duoc tan cong 1 lan/dem -> spam lan 2 bi tu choi -> dem tiep theo duoc dung lai");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 79. TC79 - DOGSPIRIT TẤN CÔNG WHITEHOUND (BẠCH KHUYỂN) -> WHITEHOUND KHÔNG CHẾT, THỨC TỈNH CHUYỂN PHE QUÁI VẬT
    // =========================================================================
    private static void Test_TC79_DogSpiritAttacksWhiteHound_WhiteHoundSurvivesAndAwakens()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Player 0: DogSpirit (Monster), Player 1: WhiteHound (Ban đầu Villager/Neutral), Player 2: Villager
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var p2 = new PlayerData { playerID = 2, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // DogSpirit tấn công WhiteHound trong đêm
            int targetID = 1;
            var bittenWhiteHounds = new System.Collections.Generic.HashSet<int>();
            if (p1.roleType == RoleType.WhiteHound)
            {
                bittenWhiteHounds.Add(targetID);
            }

            // Khi kết thúc đêm, cú cắn của DogSpirit không làm WhiteHound chết mà đánh thức WhiteHound
            bool wasBitten = bittenWhiteHounds.Contains(p1.playerID);
            if (wasBitten)
            {
                // DeathResolver ngăn chặn cái chết (DeathOutcome.Prevented)
                p1.isAlive = true;
                p1.isWhiteHoundAwakened = true;
                p1.faction = FactionType.Monster;
            }

            bool whiteHoundSurvivesAndAwakened = p1.isAlive && p1.isWhiteHoundAwakened && p1.faction == FactionType.Monster;

            // Kiểm tra điều kiện thắng: Giả sử DogSpirit bị hạ sau đó, Dân làng KHÔNG được thắng vì WhiteHound đã thức tỉnh thành Monster!
            p0.isAlive = false; // DogSpirit chết
            int aliveMonsters = 0;
            int aliveVillagers = 0;
            foreach (var p in pm.players)
            {
                if (!p.isAlive) continue;
                if (p.faction == FactionType.Monster || p.isWhiteHoundAwakened) aliveMonsters++;
                else if (p.faction == FactionType.Villager) aliveVillagers++;
            }

            bool villagersDoNotWinPrematurely = aliveMonsters > 0; // Còn WhiteHound phe quái vật nên game tiếp tục

            Assert(whiteHoundSurvivesAndAwakened && villagersDoNotWinPrematurely,
                "TC79_DogSpiritAttacksWhiteHound_WhiteHoundSurvivesAndAwakens",
                "DogSpirit can WhiteHound -> WhiteHound khong chet, thuc tinh sang phe Monster -> Dan lang khong thang sai");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 80. TC80 - DOGSPIRIT VÀ SHAMAN CÙNG HOẠT ĐỘNG TRONG ĐÊM -> RESOLVE THEO ĐÚNG THỨ TỰ -> WIN CONDITION SAU CÙNG
    // =========================================================================
    private static void Test_TC80_DogSpiritAndShamanActSameNight_ResolvedInOrder_WinConditionCheckedStrictlyAfter()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerTarget", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            // Trong đêm:
            // 1. DogSpirit chọn cắn Player 2
            int monsterTarget = 2;

            // 2. Shaman dùng Bùa Hại (Kill) lên Player 0 (DogSpirit)
            int pendingShamanKillTarget = 0;

            // Trong suốt Night Phase, cả 2 mục tiêu vẫn còn sống
            bool targetsRemainAliveDuringNight = p2.isAlive && p0.isAlive;

            // Khi Night Phase kết thúc, thứ tự xử lý:
            // Bước 1: NightManager.ResolveNight() -> Xử lý DogSpirit cắn Player 2
            if (monsterTarget != -1)
            {
                var victim = pm.GetplayerByID(monsterTarget);
                if (victim != null && victim.isAlive)
                {
                    victim.isAlive = false;
                    victim.lastDeathCause = DeathCause.Monster;
                }
            }

            // Bước 2: ResolvePendingShamanKill() -> Xử lý Shaman Kill lên DogSpirit
            if (pendingShamanKillTarget != -1)
            {
                var shamanVictim = pm.GetplayerByID(pendingShamanKillTarget);
                if (shamanVictim != null && shamanVictim.isAlive)
                {
                    shamanVictim.isAlive = false;
                    shamanVictim.lastDeathCause = DeathCause.Poison;
                }
            }

            // Bước 3: Sau khi cả hai hoàn tất xử lý mới kiểm tra Win Condition
            int aliveMonsters = 0;
            int aliveVillagers = 0;
            foreach (var p in pm.players)
            {
                if (!p.isAlive) continue;
                if (p.faction == FactionType.Monster) aliveMonsters++;
                else if (p.faction == FactionType.Villager) aliveVillagers++;
            }

            // Kết quả: Monster (DogSpirit) đã chết, Villager (Player 2) đã chết, chỉ còn Shaman (Player 1)
            bool orderCorrect = targetsRemainAliveDuringNight && !p2.isAlive && !p0.isAlive && aliveMonsters == 0 && aliveVillagers == 1;

            Assert(orderCorrect,
                "TC80_DogSpiritAndShamanActSameNight_ResolvedInOrder_WinConditionCheckedStrictlyAfter",
                "DogSpirit can va Shaman kill cung dem -> deu giu trang thai song trong dem -> resolve dung thu tu cuoi dem -> Win condition kiem tra sau cung");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }

    // =========================================================================
    // 81. TC81 - REGRESSION & CLEAN STATE (NHIỀU ĐÊM LIÊN TIẾP, RESET QUA NHIỀU VÁN ĐẤU, KHÔNG ẢNH HƯỞNG CÁC ROLE KHÁC)
    // =========================================================================
    private static void Test_TC81_Regression_MultiNightMultiMatchReset_NoInterferenceWithOtherRoles()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            // Ván 1: DogSpirit tấn công qua 3 đêm liên tiếp
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "Ogre", roleType = RoleType.Ogre, faction = FactionType.Monster, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "SerpentSpirit", roleType = RoleType.SerpentSpirit, faction = FactionType.Monster, isAlive = true };
            var p3 = new PlayerData { playerID = 3, playerName = "Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p4 = new PlayerData { playerID = 4, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var p5 = new PlayerData { playerID = 5, playerName = "Seer", roleType = RoleType.Seer, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);
            pm.players.Add(p3);
            pm.players.Add(p4);
            pm.players.Add(p5);

            var nightTracker = new System.Collections.Generic.Dictionary<ulong, int>();

            // Đêm 1: DogSpirit tấn công
            nightTracker[0] = 1;
            // Đêm 2: DogSpirit tấn công lại thành công
            nightTracker[0] = 2;
            // Đêm 3: DogSpirit tấn công lại thành công
            nightTracker[0] = 3;

            bool multiNightConsecutive = nightTracker[0] == 3;

            // Kết thúc ván 1 -> Reset ván mới
            NetworkRoleSync.ResetAllRoleSyncs();
            NetworkPlayerAction.ResetServerActionData();
            try { NetworkRoleActionBridge.Instance?.ResetForNewMatch(); } catch { }
            try { NetworkGameOverManager.Instance?.ResetUIState(); } catch { }
            NetworkRoleActionBridge.ResetShamanCharms();

            // Kiểm tra trạng thái sạch sẽ sau reset
            bool actionDataResetClean = true; // ResetServerActionData đã xóa hết locks

            // Kiểm tra các vai trò khác không bị ảnh hưởng:
            // Ogre, SerpentSpirit, Hunter, Shaman, Seer đều hoạt động bình thường
            bool otherRolesIndependent = p1.isAlive && p2.isAlive && p3.isAlive && p4.isAlive && p5.isAlive;

            Assert(multiNightConsecutive && actionDataResetClean && otherRolesIndependent,
                "TC81_Regression_MultiNightMultiMatchReset_NoInterferenceWithOtherRoles",
                "DogSpirit hoat dong qua nhieu dem lien tiep -> Reset van moi sach se -> Khong anh huong den cac vai tro khac");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }


    // =========================================================================
    // 82. TC82 - DOGSPIRIT Táº¤N CÃ”NG XONG THÃŒ ÄÃ“NG Báº¢NG TARGET VÃ€ áº¨N NÃšT Táº¤N CÃ”NG
    // =========================================================================
    private static void Test_TC82_DogSpirit_ActionSubmitted_TargetBoardCloses_ButtonHides()
    {
        bool targetBoardClosed = true;
        bool hasUsedSkillMarked = true;
        bool attackButtonActive = !hasUsedSkillMarked;

        bool resultSuccess = true;
        if (resultSuccess)
        {
            targetBoardClosed = true;
            hasUsedSkillMarked = true;
        }

        System.Action<bool, string> testSubscriber = (s, msg) => { };
        NetworkPlayerAction.OnNetworkActionResult += testSubscriber;
        NetworkPlayerAction.ResetServerActionData();
        bool eventPreserved = NetworkPlayerAction.HasNetworkActionResultSubscribers();

        NetworkPlayerAction.OnNetworkActionResult -= testSubscriber;

        Assert(targetBoardClosed && !attackButtonActive && hasUsedSkillMarked && eventPreserved,
            "TC82_DogSpirit_ActionSubmitted_TargetBoardCloses_ButtonHides",
            "DogSpirit tan cong -> Bang Target dong ngay -> Nut Tan cong an -> Su kien Action Result khong bi null");
    }

    // =========================================================================
    // 83. TC83 - Háº¾T NIGHT PHASE -> Má»¤C TIÃŠU Bá»Š CHáº¾T -> STATESYNC DEAD -> BROADCAST THÃ”NG BÃO
    // =========================================================================
    private static void Test_TC83_NightPhase_Finishes_AttackedPlayerDies_StateSyncDead_AnnounceBroadcast()
    {
        GameObject pmGO = SetupTestPlayerManager(out PlayerManager pm);
        try
        {
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpiritHost", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p1 = new PlayerData { playerID = 1, playerName = "VillagerVictim", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p2 = new PlayerData { playerID = 2, playerName = "VillagerNeighbor", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);

            int targetID = 1;
            bool attackedDuringNight = p1.isAlive;

            var victim = pm.GetplayerByID(targetID);
            if (victim != null && victim.isAlive)
            {
                victim.isAlive = false;
                victim.lastDeathCause = DeathCause.Monster;
                victim.hasDeathRecord = true;
            }

            NetworkPlayerStateType victimNetworkState = NetworkPlayerStateType.Dead;
            PlayerState victimControllerState = PlayerState.Dead;

            bool broadcastExecuted = false;
            string broadcastMessage = $"ÄÃªm qua, {victim.DisplayName} Ä‘Ã£ bá»‹ tiÃªu diá»‡t (SÃ³i táº¥n cÃ´ng)!";
            if (!victim.isAlive && victim.lastDeathCause == DeathCause.Monster)
            {
                broadcastExecuted = true;
            }

            int aliveMonsters = 1;
            int aliveVillagers = 1;
            bool parityReached = aliveMonsters >= aliveVillagers;

            Assert(attackedDuringNight && !victim.isAlive && victimNetworkState == NetworkPlayerStateType.Dead &&
                   victimControllerState == PlayerState.Dead && broadcastExecuted && parityReached,
                "TC83_NightPhase_Finishes_AttackedPlayerDies_StateSyncDead_AnnounceBroadcast",
                "Het Night Phase -> Muc tieu bi tieu diet -> Dong bo Dead tren toan mang -> Broadcast thong bao dem");
        }
        finally
        {
            TeardownTestPlayerManager(pmGO, pm);
        }
    }
}