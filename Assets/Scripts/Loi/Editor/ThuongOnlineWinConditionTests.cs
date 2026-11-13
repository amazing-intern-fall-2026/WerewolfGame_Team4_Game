using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ThuongOnlineWinConditionTests
{
    private static int passCount = 0;
    private static int failCount = 0;
    public static int PassCount => passCount;
    public static int FailCount => failCount;

    [InitializeOnLoadMethod]
    [MenuItem("Tools/Tests/Run Thuong Online Win Condition Tests")]
    public static void RunAllTests()
    {
        try { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return; } catch { }

        passCount = 0;
        failCount = 0;

        Debug.Log("=========================================================================");
        Debug.Log("BẮT ĐẦU BỘ KIỂM THỬ: ĐIỀU KIỆN CHIẾN THẮNG BẠCH KHUYỂN – THƯƠNG ONLINE");
        Debug.Log("=========================================================================");

        Test_Case1_UnawakenedWhiteHound_Alive_VillagersWinAccordingToRule();
        Test_Case2_AwakenedWhiteHound_Alive_WithOtherMonsters_VillagersCannotWin();
        Test_Case3_AwakenedWhiteHound_Alive_AllOtherMonstersDead_VillagersCannotWin();
        Test_Case4_AwakenedWhiteHound_Dead_NotCountedAsAliveMonster_VillagersWin();
        Test_Case5_WhiteHoundAwakens_And_DogSpiritKilled_InSameNight_CheckFinalState();
        Test_Case6_HostAndClient_ConsistentOutcome();
        Test_Case7_NewMatch_AwakenedStateReset();

        Debug.Log("=========================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ: {passCount} PASSED, {failCount} FAILED");
        Debug.Log("=========================================================================");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            passCount++;
            Debug.Log($"<color=green>[PASS]</color> {testName} {detail}");
        }
        else
        {
            failCount++;
            Debug.LogError($"<color=red>[FAIL]</color> {testName} {detail}");
        }
    }

    private static GameObject SetupPlayerManager(out PlayerManager pm)
    {
        var go = new GameObject("TestPlayerManager_WinTest");
        go.hideFlags = HideFlags.HideAndDontSave;
        pm = go.AddComponent<PlayerManager>();
        pm.players = new List<PlayerData>();
        PlayerManager.Instance = pm;
        return go;
    }

    private static void Teardown(GameObject pmGO, PlayerManager pm, GameObject gmGO = null, GameRoleManager gm = null, GameObject wcGO = null, WinConditionManager wc = null)
    {
        if (PlayerManager.Instance == pm) PlayerManager.Instance = null;
        if (GameRoleManager.Instance == gm) GameRoleManager.Instance = null;
        if (WinConditionManager.Instance == wc) WinConditionManager.Instance = null;
        if (pmGO != null) UnityEngine.Object.DestroyImmediate(pmGO);
        if (gmGO != null) UnityEngine.Object.DestroyImmediate(gmGO);
        if (wcGO != null) UnityEngine.Object.DestroyImmediate(wcGO);
    }

    // =========================================================================
    // CASE 1: Bạch Khuyển chưa thức tỉnh, còn sống -> Theo luật Dân Làng hiện tại
    // (Khi toàn bộ Quái vật đã bị tiêu diệt, Dân Làng chiến thắng)
    // =========================================================================
    public static void Test_Case1_UnawakenedWhiteHound_Alive_VillagersWinAccordingToRule()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case1"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day; gm.currentPhase = GamePhase.DayResolution;

        var wcGO = new GameObject("WC_Case1"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        try
        {
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var villager = new PlayerData { playerID = 2, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(villager);

            // 1. Kiểm tra qua ThuongOnlineWinAdapter.CanVillagersWin
            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);

            // 2. Kiểm tra qua ThuongMatchOutcome được gọi từ adapter
            string outcomeWinner = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 1, GamePhase.DayResolution, true);

            // 3. Kiểm tra qua WinConditionManager với ThuongOnlineWinAdapter
            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(true);

            bool passed = canVillagersWin &&
                          outcomeWinner == "Villagers" &&
                          gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            Assert(passed, "Case 1: Bạch Khuyển chưa thức tỉnh, còn sống",
                $"(canWin={canVillagersWin}, outcome={outcomeWinner}, gm={gm.Winner})");
        }
        finally
        {
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 2: Bạch Khuyển đã thức tỉnh, còn sống -> Không cho Dân Làng thắng
    // (Còn Khuyển Tinh khác và Bạch Khuyển thức tỉnh cùng tồn tại)
    // =========================================================================
    public static void Test_Case2_AwakenedWhiteHound_Alive_WithOtherMonsters_VillagersCannotWin()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case2"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day; gm.currentPhase = GamePhase.DayResolution;

        var wcGO = new GameObject("WC_Case2"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        try
        {
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };
            var villager1 = new PlayerData { playerID = 2, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var villager2 = new PlayerData { playerID = 3, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var villager3 = new PlayerData { playerID = 4, playerName = "Villager3", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(villager1);
            pm.players.Add(villager2);
            pm.players.Add(villager3);

            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);
            string outcomeWinner = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 1, GamePhase.DayResolution, false);

            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(false);

            bool villagersDidNotWin = !canVillagersWin &&
                                      outcomeWinner != "Villagers" &&
                                      gm.Winner != "Villagers";

            Assert(villagersDidNotWin, "Case 2: Bạch Khuyển đã thức tỉnh, còn sống (cùng Sói khác) -> Chặn Dân Làng thắng",
                $"(canWin={canVillagersWin}, outcome={outcomeWinner}, gm={gm.Winner ?? "None"})");
        }
        finally
        {
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 3: Bạch Khuyển đã thức tỉnh, còn sống và các Ma Sói khác đã chết -> Dân Làng không được thắng
    // (Đây là tình huống lỗi trọng tâm: DogSpirit đã chết, WhiteHound thức tỉnh còn sống)
    // =========================================================================
    public static void Test_Case3_AwakenedWhiteHound_Alive_AllOtherMonstersDead_VillagersCannotWin()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case3"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day; gm.currentPhase = GamePhase.DayResolution;

        var wcGO = new GameObject("WC_Case3"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        try
        {
            // DogSpirit đã chết, Bạch Khuyển thức tỉnh còn sống, 3 Dân Làng còn sống
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };
            var villager1 = new PlayerData { playerID = 2, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var villager2 = new PlayerData { playerID = 3, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var villager3 = new PlayerData { playerID = 4, playerName = "Villager3", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(villager1);
            pm.players.Add(villager2);
            pm.players.Add(villager3);

            // 1. Kiểm tra adapter xác định rõ Bạch Khuyển thuộc phe Quái vật
            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);

            // 2. ThuongMatchOutcome qua adapter
            string outcomeWinner = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 1, GamePhase.DayResolution, false);

            // 3. WinConditionManager evaluator
            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(false);

            bool passed = !canVillagersWin &&
                          outcomeWinner != "Villagers" &&
                          gm.Winner != "Villagers" &&
                          gm.currentState != GameState.GameOver;

            Assert(passed, "Case 3: Bạch Khuyển đã thức tỉnh còn sống và các Ma Sói khác đã chết -> Dân Làng KHÔNG ĐƯỢC THẮNG",
                $"(canWin={canVillagersWin}, outcome={outcomeWinner}, gm={gm.Winner ?? "Playing"})");
        }
        finally
        {
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 4: Bạch Khuyển đã thức tỉnh nhưng đã chết -> Không tính vào số Ma Sói còn sống
    // (Dân Làng chiến thắng khi toàn bộ Ma Sói khác và Bạch Khuyển thức tỉnh đều đã chết)
    // =========================================================================
    public static void Test_Case4_AwakenedWhiteHound_Dead_NotCountedAsAliveMonster_VillagersWin()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case4"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day; gm.currentPhase = GamePhase.DayResolution;

        var wcGO = new GameObject("WC_Case4"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        try
        {
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = false, isWhiteHoundAwakened = true };
            var villager = new PlayerData { playerID = 2, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(villager);

            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);
            string outcomeWinner = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 1, GamePhase.DayResolution, true);

            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(true);

            bool passed = canVillagersWin &&
                          outcomeWinner == "Villagers" &&
                          gm.currentState == GameState.GameOver && gm.Winner == "Villagers";

            Assert(passed, "Case 4: Bạch Khuyển đã thức tỉnh nhưng đã chết -> Không tính vào Ma Sói sống (Dân Làng thắng)",
                $"(canWin={canVillagersWin}, outcome={outcomeWinner}, gm={gm.Winner})");
        }
        finally
        {
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 5: Bạch Khuyển thức tỉnh trong cùng đêm với một sự kiện kết liễu Khuyển Tinh
    // (Kiểm tra đúng trạng thái cuối cùng trước khi xác định thắng: DogSpirit cắn WhiteHound và bị Shaman diệt)
    // =========================================================================
    public static void Test_Case5_WhiteHoundAwakens_And_DogSpiritKilled_InSameNight_CheckFinalState()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case5"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Night; gm.currentPhase = GamePhase.Night;

        var wcGO = new GameObject("WC_Case5"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        var bridgeGO = new GameObject("Bridge_Case5"); bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();

        try
        {
            var dogSpirit = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var whiteHound = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var shaman = new PlayerData { playerID = 2, playerName = "Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
            var villager = new PlayerData { playerID = 3, playerName = "Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(dogSpirit);
            pm.players.Add(whiteHound);
            pm.players.Add(shaman);
            pm.players.Add(villager);

            // Sự kiện 1: DogSpirit cắn WhiteHound trong đêm
            bridge.RecordWhiteHoundBitten(whiteHound.playerID);

            // Sự kiện 2: Shaman kết liễu DogSpirit trong cùng đêm
            dogSpirit.isAlive = false;
            dogSpirit.lastDeathCause = DeathCause.Poison;

            // Kết thúc đêm: giải quyết và đồng bộ trạng thái Bạch Khuyển
            ThuongOnlineWinAdapter.SynchronizeWhiteHoundState();

            // Trạng thái cuối cùng của WhiteHound: còn sống, đã thức tỉnh, phe Monster, roleType giữ WhiteHound
            bool whAwakened = whiteHound.isWhiteHoundAwakened;
            bool whFactionMonster = whiteHound.faction == FactionType.Monster;
            bool whAlive = whiteHound.isAlive;
            bool whRolePreserved = whiteHound.roleType == RoleType.WhiteHound;

            // Kiểm tra điều kiện thắng sau giải quyết đêm:
            // DogSpirit đã chết NHƯNG WhiteHound đã thức tỉnh còn sống -> Dân Làng KHÔNG ĐƯỢC THẮNG!
            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);

            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(false);

            bool villagersDidNotWin = !canVillagersWin && gm.Winner != "Villagers";

            bool passed = whAwakened && whFactionMonster && whAlive && whRolePreserved && villagersDidNotWin;

            Assert(passed, "Case 5: Bạch Khuyển thức tỉnh trong cùng đêm Khuyển Tinh bị diệt -> Kiểm tra đúng trạng thái cuối",
                $"(awakened={whAwakened}, monster={whFactionMonster}, alive={whAlive}, role={whiteHound.roleType}, canWin={canVillagersWin})");
        }
        finally
        {
            if (bridgeGO != null) UnityEngine.Object.DestroyImmediate(bridgeGO);
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 6: Host và Client cùng kiểm tra kết quả -> Kết quả chiến thắng giống nhau
    // =========================================================================
    public static void Test_Case6_HostAndClient_ConsistentOutcome()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);

        try
        {
            // Trận đấu: DogSpirit chết, WhiteHound thức tỉnh còn sống, 2 Villager
            var p0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var p1 = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };
            var p2 = new PlayerData { playerID = 2, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p3 = new PlayerData { playerID = 3, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            pm.players.Add(p0);
            pm.players.Add(p1);
            pm.players.Add(p2);
            pm.players.Add(p3);

            // Server đánh giá:
            bool hostCanVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);
            string hostOutcome = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 2, GamePhase.DayResolution, false);

            // Client nhận dữ liệu đồng bộ (RoleType.WhiteHound, faction=Monster, awakened=true):
            var clientPlayer0 = new PlayerData { playerID = 0, playerName = "DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var clientPlayer1 = new PlayerData { playerID = 1, playerName = "WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };
            var clientPlayer2 = new PlayerData { playerID = 2, playerName = "Villager1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var clientPlayer3 = new PlayerData { playerID = 3, playerName = "Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };

            var clientList = new List<PlayerData> { clientPlayer0, clientPlayer1, clientPlayer2, clientPlayer3 };
            bool clientCanVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(clientList);
            string clientOutcome = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(clientList, 2, GamePhase.DayResolution, false);

            bool consistentCanWin = hostCanVillagersWin == clientCanVillagersWin;
            bool consistentOutcome = hostOutcome == clientOutcome;
            bool bothBlockedVillagers = !hostCanVillagersWin && !clientCanVillagersWin;

            bool passed = consistentCanWin && consistentOutcome && bothBlockedVillagers;

            Assert(passed, "Case 6: Host và Client cùng kiểm tra kết quả -> Đồng bộ kết quả nhất quán",
                $"(hostCanWin={hostCanVillagersWin}, clientCanWin={clientCanVillagersWin}, hostOutcome={hostOutcome}, clientOutcome={clientOutcome})");
        }
        finally
        {
            Teardown(pmGO, pm);
        }
    }

    // =========================================================================
    // CASE 7: Bắt đầu ván mới -> Không giữ trạng thái thức tỉnh của ván trước
    // =========================================================================
    public static void Test_Case7_NewMatch_AwakenedStateReset()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var bridgeGO = new GameObject("Bridge_Case7"); bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();

        try
        {
            // Ván trước: Bạch Khuyển thức tỉnh thành quái vật và bị cắn
            var whiteHound = new PlayerData
            {
                playerID = 1,
                playerName = "WhiteHound",
                roleType = RoleType.WhiteHound,
                faction = FactionType.Monster,
                isAlive = true,
                isWhiteHoundAwakened = true
            };
            pm.players.Add(whiteHound);
            bridge.RecordWhiteHoundBitten(whiteHound.playerID);

            // Bắt đầu ván mới: gọi ResetForNewMatch
            ThuongOnlineWinAdapter.ResetForNewMatch();

            bool awakenedReset = !whiteHound.isWhiteHoundAwakened;
            bool factionReset = whiteHound.faction == FactionType.Villager;
            bool rolePreserved = whiteHound.roleType == RoleType.WhiteHound;
            bool bittenCleared = !bridge.HasWhiteHoundBeenBittenTonight(whiteHound.playerID);

            bool passed = awakenedReset && factionReset && rolePreserved && bittenCleared;

            Assert(passed, "Case 7: Bắt đầu ván mới -> Trạng thái thức tỉnh reset hoàn toàn về ban đầu",
                $"(awakened={whiteHound.isWhiteHoundAwakened}, faction={whiteHound.faction}, role={whiteHound.roleType}, bitten={bittenCleared})");
        }
        finally
        {
            if (bridgeGO != null) UnityEngine.Object.DestroyImmediate(bridgeGO);
            Teardown(pmGO, pm);
        }
    }
}
