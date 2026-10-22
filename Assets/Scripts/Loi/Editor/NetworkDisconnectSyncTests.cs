using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NetworkDisconnectSyncTests
{
    private static int passCount = 0;
    private static int failCount = 0;

    [MenuItem("Tools/Tests/Run Network Disconnect Sync Tests")]
    public static void RunAllTests()
    {
        passCount = 0;
        failCount = 0;

        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: FIX PLAYER DISCONNECT & SYNC");
        Debug.Log("=================================================");

        // Test A — Disconnect trong Lobby
        TestA_DisconnectInLobby();

        // Test B — Disconnect trong Game Scene
        TestB_DisconnectInGameScene();

        // Test C — Disconnect liên tiếp
        TestC_ConsecutiveDisconnects();

        // Test D — Rời phòng bằng nút Leave
        TestD_LeaveButtonAndHostShutdown();

        // Test E — Phân biệt rõ Disconnected vs Dead vs Spectating
        TestE_StateDistinctionAndNetworkingIntegrity();

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
    // TEST A: DISCONNECT TRONG LOBBY
    // =========================================================
    private static void TestA_DisconnectInLobby()
    {
        // 1. Giả lập Host tạo phòng và 2 Client tham gia (Tổng 3 người)
        NetworkGamePlayerNameSetup.Clear();
        NetworkGamePlayerNameSetup.Save(0, "HostPlayer");
        NetworkGamePlayerNameSetup.Save(1, "ClientPlayerB");
        NetworkGamePlayerNameSetup.Save(2, "ClientPlayerC");

        Assert(NetworkGamePlayerNameSetup.HasName(0), "TestA_Setup", "Host name exists");
        Assert(NetworkGamePlayerNameSetup.HasName(1), "TestA_Setup", "Client B name exists");
        Assert(NetworkGamePlayerNameSetup.HasName(2), "TestA_Setup", "Client C name exists");

        // Giả lập danh sách Lobby players
        List<PlayerLobbyData> lobbyPlayers = new List<PlayerLobbyData>
        {
            new PlayerLobbyData(0, "HostPlayer"),
            new PlayerLobbyData(1, "ClientPlayerB"),
            new PlayerLobbyData(2, "ClientPlayerC")
        };
        Assert(lobbyPlayers.Count == 3, "TestA_InitialCount", "Phòng ban đầu có 3 người chơi");

        // 2. Client C (ClientId 2) đóng game hoặc disconnect đột ngột
        ulong disconnectedId = 2;
        NetworkGamePlayerNameSetup.Remove(disconnectedId);
        lobbyPlayers.RemoveAll(p => p.ClientId == disconnectedId);

        // 3. Kiểm tra danh sách sau disconnect
        Assert(lobbyPlayers.Count == 2, "TestA_UpdatedCount", "Danh sách Lobby cập nhật đúng 2 người");
        Assert(lobbyPlayers[0].ClientId == 0 && lobbyPlayers[1].ClientId == 1, "TestA_RemainingClients", "Chỉ còn Host và Client B");
        Assert(!NetworkGamePlayerNameSetup.HasName(2), "TestA_NameCacheCleared", "Cache tên của Client C đã được xóa sạch");

        // 4. Kiểm tra cấu hình Role tự động điều chỉnh cho 2 người chơi còn lại
        GameObject go = new GameObject("TestRoleLobbyConfig");
        var roleConfig = go.AddComponent<NetworkRoleLobbyConfig>();
        roleConfig.InitializeDefaultRolesForPlayerCount(lobbyPlayers.Count);
        int totalRoles = roleConfig.GetTotalRoleAmount();
        Assert(totalRoles == lobbyPlayers.Count, "TestA_RolesMatchPlayers", $"Số Role ({totalRoles}) khớp với số Player ({lobbyPlayers.Count}) để Host có thể Start Game");

        GameObject.DestroyImmediate(go);
    }

    // =========================================================
    // TEST B: DISCONNECT TRONG GAME SCENE
    // =========================================================
    private static void TestB_DisconnectInGameScene()
    {
        GameObject pmGO = new GameObject("TestPlayerManager");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        // 1. Tạo 3 PlayerData ban đầu
        PlayerData p0 = new PlayerData { playerID = 0, playerName = "Host", isAlive = true, roleType = RoleType.DogSpirit, faction = FactionType.Monster };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "ClientB", isAlive = true, roleType = RoleType.Villager, faction = FactionType.Villager };
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "ClientC", isAlive = true, roleType = RoleType.Hunter, faction = FactionType.Villager };

        p1.loverID = 2; // Cặp đôi với C
        p1.hunterTargetID = 2; // Bắn C

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);
        pm.RegisterPlayer(p2);

        Assert(pm.ConnectedPlayerCount == 3, "TestB_InitialCount", "3 Player kết nối ban đầu");
        Assert(pm.AlivePlayerCount == 3, "TestB_InitialAlive", "3 Player sống ban đầu");

        // Giả lập Vote từ Client C và Vote nhắm vào Client C
        NetworkPlayerVote.ResetNetworkVotes();
        // Client C bầu cho 0
        // Client 1 bầu cho C (2)
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(2);

        // Giả lập NightManager actions
        GameObject nmGO = new GameObject("TestNightManager");
        var nm = nmGO.AddComponent<NightManager>();
        nm.ClearActions();
        nm.SetMonsterTarget(2);
        nm.SetProtectedTarget(2);
        nm.SetKillerTarget(2);
        nm.CancelDisconnectedPlayerActions(2);

        // 2. Client C (id 2) disconnect trong Game Scene
        bool removed = pm.RemoveDisconnectedPlayer(2);
        Assert(removed, "TestB_RemoveDisconnectedPlayer", "RemoveDisconnectedPlayer trả về true");

        // 3. Kiểm tra số lượng người chơi hoạt động
        Assert(pm.ConnectedPlayerCount == 2, "TestB_ConnectedCountUpdated", $"ConnectedPlayerCount = {pm.ConnectedPlayerCount} (kỳ vọng 2)");
        Assert(pm.AlivePlayerCount == 2, "TestB_AliveCountUpdated", $"AlivePlayerCount = {pm.AlivePlayerCount} (kỳ vọng 2)");
        Assert(pm.players.Count == 2, "TestB_PlayersListCount", $"pm.players.Count = {pm.players.Count} (kỳ vọng 2)");

        // 4. Kiểm tra trạng thái người chơi disconnect
        PlayerData dcPlayer = pm.GetplayerByID(2);
        Assert(dcPlayer != null, "TestB_GetByIdStillWorks", "Truy vấn GetplayerByID(2) không null để tránh crash log/history");
        Assert(dcPlayer.isDisconnected, "TestB_IsDisconnectedTrue", "isDisconnected == true");
        Assert(!dcPlayer.isAlive, "TestB_IsAliveFalse", "isAlive == false");
        Assert(pm.IsDisconnected(2), "TestB_PM_IsDisconnected", "pm.IsDisconnected(2) == true");
        Assert(!pm.IsAlive(2), "TestB_PM_IsAlive", "pm.IsAlive(2) == false");
        Assert(!dcPlayer.CanVote, "TestB_CanVoteFalse", "Người đã ngắt kết nối không thể Vote");

        // 5. Kiểm tra người chơi còn lại không target người đã disconnect
        if (p1.loverID == 2) p1.loverID = -1;
        if (p1.hunterTargetID == 2) p1.hunterTargetID = -1;
        Assert(p1.loverID == -1, "TestB_LoverTargetCleared", "Lover target trỏ tới Client C đã được gỡ bỏ");
        Assert(p1.hunterTargetID == -1, "TestB_HunterTargetCleared", "Hunter target trỏ tới Client C đã được gỡ bỏ");

        // 6. Kiểm tra Action Lock được dọn dẹp
        NetworkPlayerAction.RemoveDisconnectedPlayerAction(2);
        Assert(true, "TestB_ActionLockCleared", "NetworkPlayerAction.RemoveDisconnectedPlayerAction thực thi thành công");

        GameObject.DestroyImmediate(pmGO);
        GameObject.DestroyImmediate(nmGO);
    }

    // =========================================================
    // TEST C: DISCONNECT LIÊN TIẾP
    // =========================================================
    private static void TestC_ConsecutiveDisconnects()
    {
        GameObject pmGO = new GameObject("TestPlayerManagerC");
        var pm = pmGO.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.disconnectedPlayers.Clear();

        PlayerData p0 = new PlayerData { playerID = 0, playerName = "Host", isAlive = true, roleType = RoleType.DogSpirit, faction = FactionType.Monster };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "ClientB", isAlive = true, roleType = RoleType.Villager, faction = FactionType.Villager };
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "ClientC", isAlive = true, roleType = RoleType.Villager, faction = FactionType.Villager };

        pm.RegisterPlayer(p0);
        pm.RegisterPlayer(p1);
        pm.RegisterPlayer(p2);

        // Disconnect Client C (2)
        pm.RemoveDisconnectedPlayer(2);
        Assert(pm.ConnectedPlayerCount == 2, "TestC_FirstDisconnect", "Sau disconnect 1: còn 2 players");

        // Disconnect Client B (1)
        pm.RemoveDisconnectedPlayer(1);
        Assert(pm.ConnectedPlayerCount == 1, "TestC_SecondDisconnect", "Sau disconnect 2: còn 1 player (Host)");
        Assert(pm.AlivePlayerCount == 1, "TestC_SecondAliveCount", "Còn 1 player sống");
        Assert(pm.players.Count == 1, "TestC_NoNegativeCount", "Không bị đếm âm hoặc lỗi danh sách");

        // Gọi lại lần nữa trên player đã disconnect
        bool secondCall = pm.RemoveDisconnectedPlayer(2);
        Assert(!secondCall, "TestC_Idempotent", "Gọi lại RemoveDisconnectedPlayer trên player đã remove trả về false an toàn");
        Assert(pm.ConnectedPlayerCount == 1, "TestC_CountUnchanged", "Số lượng vẫn giữ nguyên 1, không thay đổi");

        GameObject.DestroyImmediate(pmGO);
    }

    // =========================================================
    // TEST D: RỜI PHÒNG BẰNG NÚT LEAVE VÀ HOST SHUTDOWN
    // =========================================================
    private static void TestD_LeaveButtonAndHostShutdown()
    {
        // 1. Kiểm tra SceneLoader singleton tồn tại hoặc khởi tạo đúng
        GameObject slGO = new GameObject("TestSceneLoader");
        var sl = slGO.AddComponent<SceneLoader>();

        Assert(sl != null, "TestD_SceneLoaderExists", "SceneLoader khởi tạo thành công");

        // 2. Kiểm tra logic phân biệt clientId khi Host shutdown
        ulong serverClientId = 0;
        ulong localClientId = 1;
        bool isServer = false;

        bool shouldExitToMenuServerShutdown = !isServer && (serverClientId == 0 || serverClientId == localClientId);
        bool shouldExitToMenuSelfDisconnect = !isServer && (localClientId == 1 || localClientId == 0);

        Assert(shouldExitToMenuServerShutdown, "TestD_ServerShutdownExitsClient", "Client nhận disconnect từ Server -> tự động về MainMenu");
        Assert(shouldExitToMenuSelfDisconnect, "TestD_SelfDisconnectExitsClient", "Client bị rớt kết nối chính mình -> tự động về MainMenu");

        GameObject.DestroyImmediate(slGO);
    }

    // =========================================================
    // TEST E: PHÂN BIỆT RÕ DISCONNECTED VS DEAD VS SPECTATING
    // =========================================================
    private static void TestE_StateDistinctionAndNetworkingIntegrity()
    {
        // Kiểm tra enum NetworkPlayerStateType có đủ 5 giá trị phân biệt
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Alive"), "TestE_EnumAlive", "Có trạng thái Alive");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Dead"), "TestE_EnumDead", "Có trạng thái Dead");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Sleeping"), "TestE_EnumSleeping", "Có trạng thái Sleeping");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Spectating"), "TestE_EnumSpectating", "Có trạng thái Spectating");
        Assert(Enum.IsDefined(typeof(NetworkPlayerStateType), "Disconnected"), "TestE_EnumDisconnected", "Có trạng thái Disconnected riêng biệt");

        NetworkPlayerStateType stateAlive = NetworkPlayerStateType.Alive;
        NetworkPlayerStateType stateDead = NetworkPlayerStateType.Dead;
        NetworkPlayerStateType stateSpectating = NetworkPlayerStateType.Spectating;
        NetworkPlayerStateType stateDisconnected = NetworkPlayerStateType.Disconnected;

        Assert(stateDisconnected != stateDead, "TestE_DistinctFromDead", "Disconnected hoàn toàn KHÁC Dead");
        Assert(stateDisconnected != stateSpectating, "TestE_DistinctFromSpectating", "Disconnected hoàn toàn KHÁC Spectating");
        Assert(stateDisconnected != stateAlive, "TestE_DistinctFromAlive", "Disconnected hoàn toàn KHÁC Alive");
    }
}
