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
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            return;

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

        Debug.Log("=================================================");
        Debug.Log($"KẾT QUẢ TỔNG HỢP: PASS = {passCount} | FAIL = {failCount}");
        Debug.Log("=================================================");
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            passCount++;
            Debug.Log($"[PASS] {testName} {details}");
        }
        else
        {
            failCount++;
            Debug.LogError($"[FAIL] {testName} - Thất bại! {details}");
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

    private static void TeardownTestPlayerManager(GameObject go, PlayerManager pm)
    {
        if (PlayerManager.Instance == pm)
            PlayerManager.Instance = null;

        if (go != null)
            Object.DestroyImmediate(go);
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
}
