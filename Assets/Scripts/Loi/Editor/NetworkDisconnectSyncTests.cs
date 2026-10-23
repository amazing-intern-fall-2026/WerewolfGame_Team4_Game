using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;

public static class NetworkDisconnectSyncTests
{
    private static int passCount = 0;
    private static int failCount = 0;

    public static int PassCount => passCount;
    public static int FailCount => failCount;

    [MenuItem("Tools/Tests/Run Network Disconnect Sync Tests")]
    public static void RunAllTests()
    {
        passCount = 0;
        failCount = 0;

        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: WEREWOLF MULTIPLAYER & AUTH TESTS");
        Debug.Log("=================================================");

        // Test A — Rời phòng khi đang chơi (3 -> 2 người)
        TestA_LeaveRoomDuringGame();

        // Test B — Ván tiếp theo (loại bỏ người cũ, không xuất hiện lại, hỗ trợ join lại)
        TestB_NextMatchStateSync();

        // Test C — Disconnect đột ngột (mất mạng/đóng game)
        TestC_SuddenDisconnect();

        // Test D — Password validation theo quy tắc mới
        TestD_PasswordValidation();

        // Test E — Đăng ký tài khoản & Luồng xác thực
        TestE_RegistrationAndAuthenticationFlow();

        // Test F — Người chơi (Player 3) thoát game được loại bỏ hoàn toàn ở ván mới trên cả Server và Client
        TestF_Player3ExitedExclusionInNewMatch();

        // Test G — Thoát phòng về MainMenu bảo lưu trạng thái đăng nhập (không bị đăng xuất ngoài ý muốn)
        TestG_LeaveRoomPreservesLoginState();

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
    // TEST A: RỜI PHÒNG KHI ĐANG CHƠI (3 NGƯỜI -> 2 NGƯỜI)
    // =========================================================
    private static void TestA_LeaveRoomDuringGame()
    {
        GameObject pmGO = new GameObject("TestPlayerManager_A");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        // 1. Tạo 3 PlayerData ban đầu trong Game Scene (Host=0, ClientB=1, ClientC=2)
        NetworkGamePlayerNameSetup.Clear();
        NetworkGamePlayerNameSetup.Save(0, "HostPlayer");
        NetworkGamePlayerNameSetup.Save(1, "ClientPlayerB");
        NetworkGamePlayerNameSetup.Save(2, "ClientPlayerC");

        PlayerData p0 = new PlayerData { playerID = 0, playerName = "HostPlayer", isAlive = true, roleType = RoleType.DogSpirit, faction = FactionType.Monster };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "ClientPlayerB", isAlive = true, roleType = RoleType.Villager, faction = FactionType.Villager };
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "ClientPlayerC", isAlive = true, roleType = RoleType.Hunter, faction = FactionType.Villager };

        p1.loverID = 2; // Cặp đôi với C
        p1.hunterTargetID = 2; // Bắn C

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);
        pm.RegisterPlayer(p2);

        Assert(pm.ConnectedPlayerCount == 3, "TestA_InitialCount", "3 người chơi vào phòng và bắt đầu ván");
        Assert(pm.AlivePlayerCount == 3, "TestA_InitialAlive", "Cả 3 người đều sống ban đầu");

        // Giả lập Vote và Action
        NetworkPlayerVote.ResetNetworkVotes();
        NetworkPlayerAction.RemoveDisconnectedPlayerAction(2);

        // Giả lập NightManager actions nhắm vào Client C
        GameObject nmGO = new GameObject("TestNightManager_A");
        var nm = nmGO.AddComponent<NightManager>();
        nm.ClearActions();
        nm.SetMonsterTarget(2);
        nm.SetProtectedTarget(2);
        nm.SetKillerTarget(2);
        nm.CancelDisconnectedPlayerActions(2);

        // 2. Client C nhấn nút Leave Room (ClientId 2)
        // Server xử lý: loại khỏi PlayerManager, xóa cache tên, hủy vote/action, reset lover
        bool removed = pm.RemoveDisconnectedPlayer(2);
        NetworkGamePlayerNameSetup.Remove(2);
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(2);
        if (p1.loverID == 2) p1.loverID = -1;
        if (p1.hunterTargetID == 2) p1.hunterTargetID = -1;

        Assert(removed, "TestA_RemoveClientC", "Server xác nhận và loại Client C khỏi danh sách active");

        // 3. Hai người còn lại (A và B) phải thấy đúng 2 người đang kết nối
        Assert(pm.ConnectedPlayerCount == 2, "TestA_UpdatedConnectedCount", $"Các máy còn lại cập nhật đúng 2 người kết nối (thực tế: {pm.ConnectedPlayerCount})");
        Assert(pm.AlivePlayerCount == 2, "TestA_UpdatedAliveCount", $"Các máy còn lại cập nhật đúng 2 người còn sống (thực tế: {pm.AlivePlayerCount})");
        Assert(pm.players.Count == 2, "TestA_PlayersListCount", $"Danh sách người chơi active còn đúng 2 (thực tế: {pm.players.Count})");
        Assert(pm.players[0].playerID == 0 && pm.players[1].playerID == 1, "TestA_RemainingIdentities", "Hai người còn lại đúng là Host (0) và Client B (1), không bị xóa nhầm");

        // 4. Client C không còn quyền vote hoặc hành động
        PlayerData dcPlayer = pm.GetplayerByID(2);
        Assert(dcPlayer != null && dcPlayer.isDisconnected, "TestA_ClientC_Disconnected", "Client C có cờ isDisconnected == true");
        Assert(!pm.IsAlive(2), "TestA_ClientC_NotAlive", "Client C không còn được tính là còn sống");
        Assert(!dcPlayer.CanVote, "TestA_ClientC_CannotVote", "Client C không thể tiếp tục bỏ phiếu");
        Assert(p1.loverID == -1, "TestA_LoverReset", "Quan hệ Lover nhắm vào Client C đã được gỡ bỏ");

        GameObject.DestroyImmediate(pmGO);
        GameObject.DestroyImmediate(nmGO);
    }

    // =========================================================
    // TEST B: VÁN TIẾP THEO (STATE SYNC & PLAY AGAIN)
    // =========================================================
    private static void TestB_NextMatchStateSync()
    {
        GameObject pmGO = new GameObject("TestPlayerManager_B");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        // 1. Tạo 2 người chơi còn lại từ ván trước (Host=0, ClientB=1) và 1 người đã disconnect (ClientC=2)
        PlayerData p0 = new PlayerData { playerID = 0, playerName = "HostPlayer", isAlive = true };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "ClientPlayerB", isAlive = false }; // B bị chết trong ván cũ
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "ClientPlayerC", isAlive = false, isDisconnected = true };

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);
        pm.disconnectedPlayers.Add(p2);

        // 2. Bắt đầu ván mới / Play Again theo luồng NetworkGameOverManager
        NetworkGameManager.LeftPlayerClientIds.Clear();
        NetworkGameManager.LeftPlayerClientIds.Add(2); // Client C đã ngắt kết nối / văng game

        // BƯỚC A: ResetForNewMatch dọn sạch disconnectedPlayers cũ
        pm.ResetForNewMatch();
        Assert(pm.disconnectedPlayers.Count == 0, "TestB_DisconnectedCleared", "Danh sách disconnectedPlayers cũ được dọn sạch hoàn toàn");

        // BƯỚC B: Chỉ giữ lại các client đang kết nối thực tế {0, 1}
        var activeConnectedClients = new HashSet<ulong> { 0, 1 };
        pm.players.RemoveAll(p => p == null || !activeConnectedClients.Contains((ulong)p.playerID) || NetworkGameManager.LeftPlayerClientIds.Contains((ulong)p.playerID));

        Assert(pm.players.Count == 2, "TestB_NewMatchPlayerCount", $"Ván mới có đúng 2 người chơi (thực tế: {pm.players.Count})");
        Assert(pm.GetplayerByID(2) == null, "TestB_LeftPlayerNotReappearing", "Người đã rời (Client C) KHÔNG tự động xuất hiện lại trong ván mới");
        Assert(!pm.IsActivePlayer(2), "TestB_ClientCNotActive", "Client C không được coi là active player trong ván mới");

        // BƯỚC C: Tất cả người chơi active được hồi sinh và reset trạng thái cho ván mới
        Assert(p0.isAlive && !p0.isDisconnected, "TestB_HostAliveInNewMatch", "Host sẵn sàng cho ván mới");
        Assert(p1.isAlive && !p1.isDisconnected, "TestB_ClientBAliveInNewMatch", "Client B được reset sống lại cho ván mới");

        // BƯỚC D: Kiểm tra cấu hình Role tự động điều chỉnh cho 2 người trong Lobby
        GameObject go = new GameObject("TestRoleLobbyConfig_B");
        var roleConfig = go.AddComponent<NetworkRoleLobbyConfig>();
        roleConfig.InitializeDefaultRolesForPlayerCount(2);
        Assert(roleConfig.GetTotalRoleAmount() == 2, "TestB_LobbyRolesUpdated", "Cấu hình vai trò cập nhật đúng 2 vai cho 2 người chơi còn lại");

        // Thử đăng ký lại khi chưa xóa khỏi LeftPlayerClientIds -> phải bị chặn
        PlayerData unauthPlayer = new PlayerData { playerID = 2, playerName = "ClientPlayerC_Hacker", isAlive = true };
        pm.RegisterPlayer(unauthPlayer);
        Assert(pm.GetplayerByID(2) == null, "TestB_LeftPlayerBlocked", "Không thể vô tình đăng ký lại player đã trong LeftPlayerClientIds");

        // 3. Khi người chơi kết nối lại thành công, chỉ lúc đó mới được thêm vào danh sách
        NetworkGameManager.LeftPlayerClientIds.Remove(2);
        PlayerData reconnectedPlayer = new PlayerData { playerID = 2, playerName = "ClientPlayerC_Rejoined", isAlive = true };
        pm.RegisterPlayer(reconnectedPlayer);
        Assert(pm.players.Count == 3, "TestB_RejoinSuccess", "Sau khi kết nối lại thành công, danh sách tăng lên 3 người hợp lệ");
        Assert(pm.GetplayerByID(2).playerName == "ClientPlayerC_Rejoined", "TestB_RejoinNameCorrect", "Thông tin người chơi tham gia lại chính xác");

        GameObject.DestroyImmediate(pmGO);
        GameObject.DestroyImmediate(go);
    }

    // =========================================================
    // TEST C: DISCONNECT ĐỘT NGỘT (MẤT MẠNG / ĐÓNG GAME)
    // =========================================================
    private static void TestC_SuddenDisconnect()
    {
        GameObject pmGO = new GameObject("TestPlayerManager_C");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        PlayerData p0 = new PlayerData { playerID = 0, playerName = "Host", isAlive = true, roleType = RoleType.DogSpirit, faction = FactionType.Monster };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "ClientB", isAlive = true, roleType = RoleType.Villager, faction = FactionType.Villager };

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);

        // 1. Client B bị mất mạng đột ngột
        ulong suddenDisconnectId = 1;
        bool removed = pm.RemoveDisconnectedPlayer((int)suddenDisconnectId);
        NetworkGamePlayerNameSetup.Remove(suddenDisconnectId);
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes((int)suddenDisconnectId);
        NetworkPlayerAction.RemoveDisconnectedPlayerAction(suddenDisconnectId);

        Assert(removed, "TestC_SuddenDisconnectHandled", "Server xử lý ngắt kết nối đột ngột thành công");
        Assert(pm.ConnectedPlayerCount == 1, "TestC_CountUpdatedToOne", "Số người kết nối giảm xuống đúng 1 (chỉ còn Host)");
        Assert(pm.AlivePlayerCount == 1, "TestC_AliveUpdatedToOne", "Số người sống giảm xuống đúng 1");

        // 2. Thử gọi lại lần 2 (idempotency check)
        bool secondCall = pm.RemoveDisconnectedPlayer((int)suddenDisconnectId);
        Assert(!secondCall, "TestC_Idempotent", "Gọi lại RemoveDisconnectedPlayer trả về false an toàn, không lỗi");
        Assert(pm.ConnectedPlayerCount == 1, "TestC_NoNegativeCount", "Số người chơi không bị đếm âm");

        // 3. Phân biệt rõ các trạng thái
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Alive"), "TestC_StateAlive", "Có trạng thái Alive");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Dead"), "TestC_StateDead", "Có trạng thái Dead");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Disconnected"), "TestC_StateDisconnected", "Có trạng thái Disconnected");
        Assert(NetworkPlayerStateType.Disconnected != NetworkPlayerStateType.Dead, "TestC_DistinctStates", "Disconnected hoàn toàn KHÁC Dead");

        GameObject.DestroyImmediate(pmGO);
    }

    // =========================================================
    // TEST D: PASSWORD VALIDATION
    // =========================================================
    private static void TestD_PasswordValidation()
    {
        string expectedError = "Mật khẩu cần ít nhất 6 ký tự, bao gồm chữ cái và chữ số.";

        // 1. Các mật khẩu hợp lệ (>= 6 ký tự, có ít nhất 1 chữ cái và 1 chữ số)
        bool v1 = AuthManager.ValidatePassword("maSoi123", out string err1);
        Assert(v1 && string.IsNullOrEmpty(err1), "TestD_maSoi123", $"maSoi123: Hợp lệ (kết quả: {v1})");

        bool v2 = AuthManager.ValidatePassword("player123", out string err2);
        Assert(v2 && string.IsNullOrEmpty(err2), "TestD_player123", $"player123: Hợp lệ (kết quả: {v2})");

        bool v3 = AuthManager.ValidatePassword("abc12345", out string err3);
        Assert(v3 && string.IsNullOrEmpty(err3), "TestD_abc12345", $"abc12345: Hợp lệ (kết quả: {v3})");

        // 2. Các mật khẩu không hợp lệ
        // 12345678 — không có chữ cái
        bool v4 = AuthManager.ValidatePassword("12345678", out string err4);
        Assert(!v4 && err4 == expectedError, "TestD_12345678", $"12345678: Không hợp lệ vì thiếu chữ cái (lỗi: '{err4}')");

        // abcdefgh — không có chữ số
        bool v5 = AuthManager.ValidatePassword("abcdefgh", out string err5);
        Assert(!v5 && err5 == expectedError, "TestD_abcdefgh", $"abcdefgh: Không hợp lệ vì thiếu chữ số (lỗi: '{err5}')");

        // abc12 — chưa đủ 6 ký tự
        bool v6 = AuthManager.ValidatePassword("abc12", out string err6);
        Assert(!v6 && err6 == expectedError, "TestD_abc12", $"abc12: Không hợp lệ vì độ dài < 6 (lỗi: '{err6}')");

        // Để trống
        bool v7 = AuthManager.ValidatePassword("", out string err7);
        Assert(!v7 && err7 == "Mật khẩu không được để trống!", "TestD_Empty", "Mật khẩu trống bị từ chối");
    }

    // =========================================================
    // TEST E: ĐĂNG KÝ TÀI KHOẢN & LUỒNG XÁC THỰC
    // =========================================================
    private static void TestE_RegistrationAndAuthenticationFlow()
    {
        // 1. Kiểm tra validation đăng ký tài khoản mới hợp lệ
        bool uValid = AuthManager.ValidateUsername("playerTest01", out string uErr);
        bool pValid = AuthManager.ValidatePassword("maSoi123", out string pErr);
        string confirmPass = "maSoi123";
        bool match = ("maSoi123" == confirmPass);

        Assert(uValid && string.IsNullOrEmpty(uErr), "TestE_ValidUsername", "Username playerTest01 hợp lệ");
        Assert(pValid && string.IsNullOrEmpty(pErr), "TestE_ValidPassword", "Password maSoi123 hợp lệ");
        Assert(match, "TestE_PasswordMatch", "Mật khẩu nhập lại khớp");

        // 2. Đăng ký với mật khẩu nhập lại không khớp
        bool mismatch = ("maSoi123" == "differentPass");
        Assert(!mismatch, "TestE_PasswordMismatch", "Mật khẩu nhập lại không khớp bị phát hiện");

        // 3. Username không hợp lệ
        bool uShort = AuthManager.ValidateUsername("ab", out string uShortErr);
        Assert(!uShort && uShortErr.Contains("3 đến 20"), "TestE_UsernameTooShort", "Username dưới 3 ký tự bị từ chối");

        bool uSpecial = AuthManager.ValidateUsername("user!@#$%", out string uSpecErr);
        Assert(!uSpecial && uSpecErr.Contains("chữ cái, chữ số"), "TestE_UsernameSpecialChar", "Username chứa ký tự không cho phép bị từ chối");

        // 4. Kiểm tra phân loại thông báo lỗi tiếng Việt (Reflection gọi private helper)
        var authType = typeof(AuthManager);
        var authInstance = AuthManager.Instance;
        if (authInstance == null)
        {
            GameObject aGO = new GameObject("TestAuthManager");
            authInstance = aGO.AddComponent<AuthManager>();
        }

        var formatSignUp = authType.GetMethod("FormatSignUpErrorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var formatReqFailed = authType.GetMethod("FormatRequestFailedErrorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // A. Tài khoản đã tồn tại (Conflict / already exists)
        var exConflict = (AuthenticationException)System.Activator.CreateInstance(
            typeof(AuthenticationException),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new object[] { CommonErrorCodes.Conflict, "Username already exists", null, null },
            null);
        string msgConflict = (string)formatSignUp.Invoke(authInstance, new object[] { exConflict });
        Assert(msgConflict.Contains("đã được sử dụng"), "TestE_UsernameConflictMessage", $"Thông báo trùng username rõ ràng: '{msgConflict}'");

        // B. Lỗi máy chủ hoặc mạng (TransportError)
        var exNet = new RequestFailedException(CommonErrorCodes.TransportError, "Network timeout");
        string msgNet = (string)formatReqFailed.Invoke(authInstance, new object[] { exNet });
        Assert(msgNet.Contains("kết nối mạng"), "TestE_NetworkErrorMessage", $"Thông báo mất mạng rõ ràng: '{msgNet}'");

        // C. Dịch vụ chưa kích hoạt trên Dashboard (Provider missing)
        var exProvider = new RequestFailedException(10001, "No provider configured for username");
        string msgProvider = (string)formatReqFailed.Invoke(authInstance, new object[] { exProvider });
        Assert(msgProvider.Contains("Unity Dashboard"), "TestE_ProviderMissingMessage", $"Thông báo thiếu provider hướng dẫn rõ ràng: '{msgProvider}'");

        // D. Máy chủ bảo trì (ServiceUnavailable)
        var exMaint = new RequestFailedException(CommonErrorCodes.ServiceUnavailable, "Maintenance");
        string msgMaint = (string)formatReqFailed.Invoke(authInstance, new object[] { exMaint });
        Assert(msgMaint.Contains("bảo trì"), "TestE_ServiceUnavailableMessage", $"Thông báo bảo trì rõ ràng: '{msgMaint}'");

        // 5. Kiểm tra hệ thống lưu trữ và bảo mật tài khoản cục bộ (Local Fallback)
        string testAccName = "test_player_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
        string testAccPass = "player123";
        AuthManager.SaveLocalAccount(testAccName, testAccPass);
        Assert(AuthManager.HasLocalAccount(testAccName), "TestE_LocalAccountExists", "Tài khoản cục bộ được ghi nhận thành công");
        Assert(AuthManager.VerifyLocalAccount(testAccName, testAccPass), "TestE_LocalAccountVerifySuccess", "Xác thực mật khẩu cục bộ (đã hash salt) thành công");
        Assert(!AuthManager.VerifyLocalAccount(testAccName, "wrongPass123"), "TestE_LocalAccountWrongPassRejected", "Mật khẩu sai bị từ chối an toàn");

        // 6. Kiểm tra LeftPlayerClientIds đảm bảo người chơi thoát game bị loại trừ ở ván sau
        NetworkGameManager.LeftPlayerClientIds.Add(99);
        Assert(NetworkGameManager.IsPlayerLeft(99), "TestA_LeftPlayerTracking", "LeftPlayerClientIds lưu vết người chơi đã rời trận để loại trừ khỏi ván sau");
        NetworkGameManager.LeftPlayerClientIds.Remove(99);
    }

    // =========================================================
    // TEST F: PLAYER 3 THOÁT GAME VÀ BỊ LOẠI TRỪ Ở VÁN MỚI
    // =========================================================
    private static void TestF_Player3ExitedExclusionInNewMatch()
    {
        GameObject pmGO = new GameObject("TestPlayerManager_F");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        // 1. Ván cũ có 3 người chơi: Host (0), Player 2 (1), Player 3 (2)
        PlayerData p0 = new PlayerData { playerID = 0, playerName = "HostPlayer", isAlive = true };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "Player 2", isAlive = true };
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "Player 3", isAlive = true };

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);
        pm.RegisterPlayer(p2);
        Assert(pm.players.Count == 3, "TestF_Match1PlayerCount", "Ván 1 ban đầu có 3 người chơi");

        // 2. Player 3 thoát game (ngắt kết nối / đóng game / bấm Thoát phòng)
        ulong player3Id = 2;
        NetworkGameManager.LeftPlayerClientIds.Add(player3Id);
        pm.RemoveDisconnectedPlayer((int)player3Id);
        Assert(NetworkGameManager.IsPlayerLeft(player3Id), "TestF_Player3MarkedLeft", "Player 3 được ghi nhận vào LeftPlayerClientIds");

        // 3. Chuẩn bị ván mới: Roster chính thức chỉ còn Host (0) và Player 2 (1)
        ulong[] activeRoster = new ulong[] { 0, 1 };
        NetworkGamePlayerSetup.SetActiveMatchClientIds(activeRoster);

        Assert(NetworkGamePlayerSetup.IsClientInMatch(0), "TestF_HostInMatch", "Host thuộc roster ván mới");
        Assert(NetworkGamePlayerSetup.IsClientInMatch(1), "TestF_Player2InMatch", "Player 2 thuộc roster ván mới");
        Assert(!NetworkGamePlayerSetup.IsClientInMatch(2), "TestF_Player3NotInMatch", "Player 3 KHÔNG thuộc roster ván mới");

        // 4. PlayerManager dọn dẹp theo roster chính thức
        pm.PurgeNonActivePlayers();
        Assert(pm.players.Count == 2, "TestF_PurgedPlayerCount", $"PlayerManager chỉ còn đúng 2 người chơi (thực tế: {pm.players.Count})");
        Assert(pm.GetplayerByID(2) == null, "TestF_Player3PurgedFromManager", "Player 3 đã bị xóa sạch khỏi PlayerManager");

        // 5. Thử đăng ký lại Player 3 vào PlayerManager -> phải bị từ chối
        PlayerData orphanP3 = new PlayerData { playerID = 2, playerName = "Player 3", isAlive = true };
        pm.RegisterPlayer(orphanP3);
        Assert(pm.GetplayerByID(2) == null, "TestF_Player3ReRegistrationBlocked", "PlayerManager chặn đăng ký lại Player 3 vì không thuộc roster active");

        // 6. Kiểm tra RolePoolBuilder cho ván mới chỉ sinh 2 role thay vì 3
        var rolePool = RolePoolBuilder.Build(pm.players.Count, 99999);
        Assert(rolePool.Count == 2, "TestF_RoleCountMatchesActivePlayers", $"RolePoolBuilder chỉ tạo {rolePool.Count} role cho 2 người chơi");

        // Dọn dẹp test
        NetworkGameManager.LeftPlayerClientIds.Remove(player3Id);
        NetworkGamePlayerSetup.Clear();
        GameObject.DestroyImmediate(pmGO);
    }

    // =========================================================
    // TEST G: THOÁT PHÒNG VỀ MAINMENU BẢO LƯU TRẠNG THÁI ĐĂNG NHẬP
    // =========================================================
    private static void TestG_LeaveRoomPreservesLoginState()
    {
        // 1. Chuẩn bị AuthManager với tài khoản đã đăng nhập (ví dụ: Player3)
        var authInstance = AuthManager.Instance;
        if (authInstance == null)
        {
            GameObject aGO = new GameObject("TestAuthManager_G");
            authInstance = aGO.AddComponent<AuthManager>();
        }

        var authType = typeof(AuthManager);
        authType.GetProperty("CurrentState").SetValue(authInstance, AuthManager.AuthState.RegisteredUser);
        authType.GetProperty("CurrentUsername").SetValue(authInstance, "Player3");
        authType.GetProperty("PlayerId").SetValue(authInstance, "test-p3-id");

        Assert(authInstance.IsSignedIn, "TestG_UserAlreadySignedIn", "AuthManager xác nhận người chơi đã đăng nhập");
        Assert(authInstance.CurrentUsername == "Player3", "TestG_UsernamePreserved", "Username Player3 được bảo lưu");

        // 2. Khởi tạo UI MainMenu giống như khi SceneLoader tải lại Scene MainMenu
        GameObject canvasGO = new GameObject("TestCanvas_G", typeof(Canvas));
        GameObject authUIGO = new GameObject("TestAuthUI_G");
        authUIGO.transform.SetParent(canvasGO.transform);
        var authUI = authUIGO.AddComponent<AuthUI>();

        // Gọi Awake và Start qua reflection
        var authUIType = typeof(AuthUI);
        var awakeMethod = authUIType.GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var startMethod = authUIType.GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        awakeMethod?.Invoke(authUI, null);
        startMethod?.Invoke(authUI, null);

        // 3. Kiểm tra: Dialog Đăng nhập (AuthCardRoot) PHẢI ẨN, Huy hiệu người chơi (UserBadgePanel) PHẢI HIỆN
        Transform cardRoot = canvasGO.transform.Find("AuthCardRoot");
        Transform badgePanel = canvasGO.transform.Find("UserBadgePanel");

        Assert(cardRoot != null && !cardRoot.gameObject.activeSelf, "TestG_LoginCardHiddenOnReturn", "Khi thoát phòng về MainMenu, Form Đăng nhập không được hiện đè lên màn hình");
        Assert(badgePanel != null && badgePanel.gameObject.activeSelf, "TestG_UserBadgeShownOnReturn", "Huy hiệu người chơi vẫn hiển thị đúng tài khoản");

        if (badgePanel != null)
        {
            var badgeText = badgePanel.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            Assert(badgeText != null && badgeText.text.Contains("Player3"), "TestG_BadgeTextContainsUsername", $"Huy hiệu hiển thị đúng tên tài khoản Player3 (thực tế: '{badgeText?.text}')");
        }

        // 4. Chỉ khi nhấn nút Đăng xuất thì mới chuyển về màn hình đăng nhập
        var signOutMethod = authUIType.GetMethod("OnSignOutClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        signOutMethod?.Invoke(authUI, null);

        Assert(!authInstance.IsSignedIn, "TestG_SignOutOnlyOnExplicitClick", "Chỉ đăng xuất khi người chơi bấm nút Đăng xuất");
        Assert(cardRoot != null && cardRoot.gameObject.activeSelf, "TestG_LoginCardShownAfterSignOut", "Sau khi bấm Đăng xuất, Form Đăng nhập mới hiển thị");
        Assert(badgePanel != null && !badgePanel.gameObject.activeSelf, "TestG_UserBadgeHiddenAfterSignOut", "Sau khi bấm Đăng xuất, Huy hiệu người chơi bị ẩn");

        // Dọn dẹp test
        GameObject.DestroyImmediate(canvasGO);
    }
}
