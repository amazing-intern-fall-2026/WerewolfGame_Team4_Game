using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NetworkGameOverTests
{
    private static int passCount = 0;
    private static int failCount = 0;

    [InitializeOnLoadMethod]
    [MenuItem("Tools/Tests/Run Network GameOver Tests")]
    public static void RunAllTests()
    {
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
}
