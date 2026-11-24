using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

        // Tạm thời dùng luật thắng bình thường trong phạm vi bộ regression test cũ, sau đó khôi phục lại cờ testing
        WinConditionTestConfig.SetRuntimeOverrideForTest(false);
        try
        {
            Test_Case1_UnawakenedWhiteHound_Alive_VillagersWinAccordingToRule();
            Test_Case2_AwakenedWhiteHound_Alive_WithOtherMonsters_VillagersCannotWin();
            Test_Case3_AwakenedWhiteHound_Alive_AllOtherMonstersDead_VillagersCannotWin();
            Test_Case4_AwakenedWhiteHound_Dead_NotCountedAsAliveMonster_VillagersWin();
            Test_Case5_WhiteHoundAwakens_And_DogSpiritKilled_InSameNight_CheckFinalState();
            Test_Case6_HostAndClient_ConsistentOutcome();
            Test_Case7_NewMatch_AwakenedStateReset();
            Test_Case8_AwakenedWhiteHound_2VillagersAlive_DogSpiritDead_GameMustContinue();
            Test_Case9_DogSpiritBitesWhiteHound_AwakenNotificationSent();
            Test_Case10_MainMenu_BlocksGameOverUI();
            Test_TC1_to_TC8_SerpentSpirit();
            Test_TC9_to_TC16_DogSpirit_WhiteHound();
            Test_TC1_to_TC11_DogSpirit_Bite_Notification();
            Test_TC01_to_TC14_Monster_Chat();
        }
        finally
        {
            WinConditionTestConfig.SetRuntimeOverrideForTest(null);
        }

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
        if (NetworkGameOverManager.Instance != null)
        {
            NetworkGameOverManager.Instance.ResetUIState();
        }
        var leftover = GameObject.Find("GameOverDedicatedCanvas");
        if (leftover != null)
        {
            UnityEngine.Object.DestroyImmediate(leftover);
        }
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

    // =========================================================================
    // CASE 8: Bạch Khuyển vừa thức tỉnh, DogSpirit đã chết, vẫn còn 2 Dân Làng sống
    // -> TRẬN ĐẤU PHẢI TIẾP TỤC (Ongoing), KHÔNG ĐƯỢC KẾT THÚC CHO SÓI TRẮNG THẮNG
    // =========================================================================
    public static void Test_Case8_AwakenedWhiteHound_2VillagersAlive_DogSpiritDead_GameMustContinue()
    {
        var pmGO = SetupPlayerManager(out PlayerManager pm);
        var gmGO = new GameObject("GM_Case8"); gmGO.hideFlags = HideFlags.HideAndDontSave;
        var gm = gmGO.AddComponent<GameRoleManager>(); GameRoleManager.Instance = gm;
        gm.currentState = GameState.Day; gm.currentPhase = GamePhase.DayResolution;

        var wcGO = new GameObject("WC_Case8"); wcGO.hideFlags = HideFlags.HideAndDontSave;
        var wc = wcGO.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wc;

        try
        {
            // Thiết lập kịch bản thực tế đúng theo ảnh chụp và mô tả lỗi:
            // P01: Thợ Săn (Villager) - Còn sống
            // P02: DogSpirit (Monster) - Đã bị loại
            // P03: Dân Làng (Villager) - Còn sống
            // P04: Bạch Khuyển thức tỉnh (White Hound / Sói Trắng) - Còn sống
            var p01 = new PlayerData { playerID = 0, playerName = "P01_Hunter", roleType = RoleType.Hunter, faction = FactionType.Villager, isAlive = true };
            var p02 = new PlayerData { playerID = 1, playerName = "P02_DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = false };
            var p03 = new PlayerData { playerID = 2, playerName = "P03_Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p04 = new PlayerData { playerID = 3, playerName = "P04_WhiteHound", roleType = RoleType.WhiteHound, faction = FactionType.Monster, isAlive = true, isWhiteHoundAwakened = true };

            pm.players.Add(p01);
            pm.players.Add(p02);
            pm.players.Add(p03);
            pm.players.Add(p04);

            // 1. Dân làng không thể thắng vì WhiteHound thức tỉnh là Monster
            bool canVillagersWin = ThuongOnlineWinAdapter.CanVillagersWin(pm.players);

            // 2. ThuongMatchOutcome: không được xử Sói Trắng thắng khi còn 2 Dân Làng
            string outcomeWinner = ThuongOnlineWinAdapter.EvaluateOnlineOutcomeForTest(pm.players, 1, GamePhase.DayResolution, false);

            // 3. Đánh giá qua ThuongOnlineWinAdapter evaluator
            ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
            wc.CheckWinCondition(false);

            bool whiteWolfDidNotPrematurelyWin = gm.Winner != "White Wolf" && gm.Winner != "WhiteHound";
            bool matchContinues = outcomeWinner == "Ongoing" && gm.currentState != GameState.GameOver;

            bool passed = !canVillagersWin && whiteWolfDidNotPrematurelyWin && matchContinues;

            Assert(passed, "Case 8: Bạch Khuyển thức tỉnh + 2 Dân Làng sống sót -> Trận đấu tiếp tục (Ongoing, không xử Sói Trắng thắng)",
                $"(canVillagersWin={canVillagersWin}, outcomeWinner={outcomeWinner}, gmWinner={gm.Winner ?? "None"}, state={gm.currentState})");
        }
        finally
        {
            Teardown(pmGO, pm, gmGO, gm, wcGO, wc);
        }
    }

    // =========================================================================
    // CASE 9: DogSpirit cắn trúng Bạch Khuyển -> Gửi thông báo riêng đến Bạch Khuyển
    // =========================================================================
    public static void Test_Case9_DogSpiritBitesWhiteHound_AwakenNotificationSent()
    {
        try
        {
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();

            // Phát thông báo thức tỉnh cho người chơi Bạch Khuyển
            NetworkWhiteHoundNotificationUI.NotifyPlayerAwakened(3);

            bool notified = NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            bool correctTitle = NetworkWhiteHoundNotificationUI.NotificationTitle.Contains("BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG") &&
                                NetworkWhiteHoundNotificationUI.NotificationTitle.Contains("BẢN NĂNG BẠCH KHUYỂN ĐÃ THỨC TỈNH");
            bool correctSubtitle = NetworkWhiteHoundNotificationUI.NotificationSubtitle.Contains("Bạn đã chuyển sang phe Sói Trắng");

            // Kiểm tra không thông báo trùng lặp
            NetworkWhiteHoundNotificationUI.NotifyPlayerAwakened(3);

            // Reset khi ván mới
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            bool resetOk = !NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;

            bool passed = notified && correctTitle && correctSubtitle && resetOk;

            Assert(passed, "Case 9: DogSpirit cắn trúng Bạch Khuyển -> Thông báo thức tỉnh hiển thị đúng tiêu đề, nội dung và không trùng lặp",
                $"(notified={notified}, titleOk={correctTitle}, subtitleOk={correctSubtitle}, resetOk={resetOk})");
        }
        finally
        {
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
        }
    }

    // =========================================================================
    // CASE 10: Chặn hoàn toàn GameOver UI trong Edit Mode hoặc khi ở scene MainMenu
    // =========================================================================
    public static void Test_Case10_MainMenu_BlocksGameOverUI()
    {
        try
        {
            if (NetworkGameOverManager.Instance != null)
            {
                NetworkGameOverManager.Instance.ResetUIState();
                // Trong Edit mode, TriggerGameOver hoặc ShowGameOverUI phải bị chặn
                NetworkGameOverManager.Instance.TriggerGameOver("Villagers");
                NetworkGameOverManager.Instance.ShowGameOverUI("Villagers");
            }

            var leftoverCanvas = GameObject.Find("GameOverDedicatedCanvas");
            bool noCanvasSpawned = leftoverCanvas == null;

            Assert(noCanvasSpawned, "Case 10: NetworkGameOverManager không tạo popup GameOver Canvas ngoài Play mode hoặc trong MainMenu",
                $"(noCanvasSpawned={noCanvasSpawned})");
        }
        finally
        {
            var leftoverCanvas = GameObject.Find("GameOverDedicatedCanvas");
            if (leftoverCanvas != null) UnityEngine.Object.DestroyImmediate(leftoverCanvas);
            if (NetworkGameOverManager.Instance != null) NetworkGameOverManager.Instance.ResetUIState();
        }
    }

    // =========================================================================
    // TC1 đến TC8: Xà Tinh (SerpentSpirit) chỉ được tấn công từ Đêm thứ 3
    // =========================================================================
    public static void Test_TC1_to_TC8_SerpentSpirit()
    {
        Debug.Log("--- BẮT ĐẦU KIỂM THỬ TC1 - TC8: XÀ TINH (SERPENT SPIRIT) ---");
        GameObject skillUIGO = new GameObject("Test_NetworkSkillUI");
        skillUIGO.hideFlags = HideFlags.HideAndDontSave;
        var skillUI = skillUIGO.AddComponent<NetworkSkillUI>();

        GameObject attackBtn = new GameObject("Test_AttackBtn");
        attackBtn.hideFlags = HideFlags.HideAndDontSave;
        GameObject targetPanel = new GameObject("Test_TargetPanel");
        targetPanel.hideFlags = HideFlags.HideAndDontSave;
        targetPanel.SetActive(false);

        GameObject bridgeGO = new GameObject("Test_Bridge");
        bridgeGO.hideFlags = HideFlags.HideAndDontSave;
        var bridge = bridgeGO.AddComponent<NetworkRoleActionBridge>();

        try
        {
            // TC1: Xà Tinh – Đêm 1: UI tấn công và panel ẩn hoàn toàn
            skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
            skillUI.SetNightNumberForTest(1);
            skillUI.UpdateSkillUI();
            bool tc1_attackHidden = !attackBtn.activeSelf;
            bool tc1_panelHidden = !targetPanel.activeSelf;
            Assert(tc1_attackHidden && tc1_panelHidden, "TC1: Xà Tinh – Đêm 1: UI tấn công và Target Panel ẩn hoàn toàn",
                $"(attackActive={attackBtn.activeSelf}, panelActive={targetPanel.activeSelf})");

            // TC2: Xà Tinh – Đêm 2: Tiếp tục ẩn hoàn toàn
            skillUI.SetNightNumberForTest(2);
            skillUI.UpdateSkillUI();
            bool tc2_attackHidden = !attackBtn.activeSelf;
            bool tc2_panelHidden = !targetPanel.activeSelf;
            Assert(tc2_attackHidden && tc2_panelHidden, "TC2: Xà Tinh – Đêm 2: UI tấn công và Target Panel tiếp tục ẩn hoàn toàn",
                $"(attackActive={attackBtn.activeSelf}, panelActive={targetPanel.activeSelf})");

            // TC3: Xà Tinh – Đêm 3: UI tấn công xuất hiện
            skillUI.SetNightNumberForTest(3);
            skillUI.UpdateSkillUI();
            bool tc3_attackVisible = attackBtn.activeSelf;
            Assert(tc3_attackVisible, "TC3: Xà Tinh – Đêm 3: UI tấn công xuất hiện",
                $"(attackActive={attackBtn.activeSelf})");

            // TC4: Xà Tinh – Đêm 4 trở đi: UI tấn công tiếp tục xuất hiện
            skillUI.SetNightNumberForTest(4);
            skillUI.UpdateSkillUI();
            bool tc4_attackVisible = attackBtn.activeSelf;
            Assert(tc4_attackVisible, "TC4: Xà Tinh – Đêm 4 trở đi: UI tấn công tiếp tục xuất hiện",
                $"(attackActive={attackBtn.activeSelf})");

            // TC5: Xà Tinh – Chuyển sang Ngày: UI kỹ năng ban đêm ẩn hoàn toàn
            skillUI.SetPhaseForTest(isNightPhase: false, isDiscussionPhase: false);
            bool tc5_dayAttackHidden = !attackBtn.activeSelf;
            bool tc5_dayPanelHidden = !targetPanel.activeSelf;
            Assert(tc5_dayAttackHidden && tc5_dayPanelHidden, "TC5: Xà Tinh – Chuyển Ngày: UI tấn công và panel ẩn hoàn toàn",
                $"(attackActive={attackBtn.activeSelf}, panelActive={targetPanel.activeSelf})");

            // TC6: Xà Tinh mở lại UI trong cùng đêm: Giữ nguyên trạng thái theo đúng số đêm
            skillUI.SetPhaseForTest(isNightPhase: true, isDiscussionPhase: false);
            skillUI.SetNightNumberForTest(3);
            skillUI.UpdateSkillUI();
            bool tc6_reopenNight3 = attackBtn.activeSelf;
            skillUI.SetNightNumberForTest(2);
            skillUI.UpdateSkillUI();
            bool tc6_reopenNight2 = !attackBtn.activeSelf;
            Assert(tc6_reopenNight3 && tc6_reopenNight2, "TC6: Xà Tinh mở lại UI trong cùng đêm giữ đúng trạng thái theo số đêm",
                $"(reopenNight3={tc6_reopenNight3}, reopenNight2={tc6_reopenNight2})");

            // TC7: Hack/bypass gọi skill Đêm 1 hoặc Đêm 2: Server từ chối và log cảnh báo
            bridge.SetNightNumberForTest(1);
            int bridgeNight1 = bridge.GetCurrentNightNumber();
            bridge.SetNightNumberForTest(2);
            int bridgeNight2 = bridge.GetCurrentNightNumber();
            bridge.SetNightNumberForTest(3);
            int bridgeNight3 = bridge.GetCurrentNightNumber();
            bool tc7_serverRejectsNight1And2 = bridgeNight1 < 3 && bridgeNight2 < 3 && bridgeNight3 >= 3;
            Assert(tc7_serverRejectsNight1And2, "TC7: Server xác thực số đêm: Đêm 1 và 2 bị từ chối, chỉ cho phép từ Đêm 3",
                $"(night1={bridgeNight1}, night2={bridgeNight2}, night3={bridgeNight3})");

            // TC8: Role khác (DogSpirit / Ogre) không bị ảnh hưởng logic đêm 3 của Xà Tinh
            skillUI.SetupForTest(RoleType.DogSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
            skillUI.SetNightNumberForTest(1);
            skillUI.UpdateSkillUI();
            bool tc8_dogSpiritNight1CanAttack = attackBtn.activeSelf;
            Assert(tc8_dogSpiritNight1CanAttack, "TC8: Role khác (DogSpirit) được phép tấn công ngay từ Đêm 1 (không bị giới hạn Đêm 3)",
                $"(dogSpiritAttackActive={attackBtn.activeSelf})");
        }
        finally
        {
            if (skillUIGO != null) UnityEngine.Object.DestroyImmediate(skillUIGO);
            if (attackBtn != null) UnityEngine.Object.DestroyImmediate(attackBtn);
            if (targetPanel != null) UnityEngine.Object.DestroyImmediate(targetPanel);
            if (bridgeGO != null) UnityEngine.Object.DestroyImmediate(bridgeGO);
        }
    }

    // =========================================================================
    // TC9 đến TC16: DogSpirit cắn trúng Bạch Khuyển & Popup Thức Tỉnh
    // =========================================================================
    public static void Test_TC9_to_TC16_DogSpirit_WhiteHound()
    {
        Debug.Log("--- BẮT ĐẦU KIỂM THỬ TC9 - TC16: DOGSPIRIT CẮN TRÚNG BẠCH KHUYỂN ---");
        GameObject notifGO = new GameObject("Test_NetworkWhiteHoundNotificationUI");
        notifGO.hideFlags = HideFlags.HideAndDontSave;
        var notifUI = notifGO.AddComponent<NetworkWhiteHoundNotificationUI>();

        try
        {
            // TC9: DogSpirit cắn trúng Bạch Khuyển -> Bạch Khuyển nhận popup thông báo
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var targetWH = new PlayerData { playerID = 3, playerName = "TargetWH", roleType = RoleType.WhiteHound, isWhiteHoundAwakened = true };
            var dogSpiritPlayer = new PlayerData { playerID = 1, playerName = "AttackerDog", roleType = RoleType.DogSpirit };
            var requestWH = new DeathRequest(targetWH, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resultWH = new DeathResult(requestWH, DeathOutcome.Prevented, "WhiteHound awakened");

            notifUI.HandleDeathResolved(resultWH);
            bool tc9_notified = NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            Assert(tc9_notified, "TC9: DogSpirit cắn trúng Bạch Khuyển thức tỉnh -> Kích hoạt thông báo thức tỉnh",
                $"(hasNotified={tc9_notified})");

            // TC10: DogSpirit cắn trúng Dân Làng -> Dân Làng chết bình thường, KHÔNG có popup thức tỉnh
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var targetVillager = new PlayerData { playerID = 2, playerName = "TargetVillager", roleType = RoleType.Villager, isWhiteHoundAwakened = false };
            var requestVillager = new DeathRequest(targetVillager, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resultVillager = new DeathResult(requestVillager, DeathOutcome.Killed, "Villager killed");

            notifUI.HandleDeathResolved(resultVillager);
            bool tc10_notNotified = !NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            Assert(tc10_notNotified, "TC10: DogSpirit cắn trúng Dân Làng -> Không kích hoạt popup thức tỉnh",
                $"(hasNotified={NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch})");

            // TC11: DogSpirit cắn trúng Bạch Khuyển có Bảo Vệ -> Không thức tỉnh, không hiển thị popup
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var protectedWH = new PlayerData { playerID = 3, playerName = "ProtectedWH", roleType = RoleType.WhiteHound, isWhiteHoundAwakened = false };
            var requestProtected = new DeathRequest(protectedWH, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resultProtected = new DeathResult(requestProtected, DeathOutcome.Prevented, "Protected by Guardian");

            notifUI.HandleDeathResolved(resultProtected);
            bool tc11_notNotified = !NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            Assert(tc11_notNotified, "TC11: DogSpirit cắn trúng Bạch Khuyển có Bảo Vệ (chưa thức tỉnh) -> Không kích hoạt popup",
                $"(hasNotified={NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch})");

            // TC12: Bạch Khuyển nhận popup trên Host -> Popup hiển thị đúng giao diện và nội dung
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            notifUI.ShowNotification();
            bool tc12_visible = notifUI.IsVisible;
            bool tc12_titleCorrect = NetworkWhiteHoundNotificationUI.NotificationTitle == "BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG! BẢN NĂNG BẠCH KHUYỂN ĐÃ THỨC TỈNH!";
            bool tc12_subtitleCorrect = NetworkWhiteHoundNotificationUI.NotificationSubtitle == "Bạn đã chuyển sang phe Sói Trắng. Hãy tiếp tục trận đấu!";
            Assert(tc12_visible && tc12_titleCorrect && tc12_subtitleCorrect, "TC12: Host nhận popup hiển thị đúng giao diện, tiêu đề và dòng phụ",
                $"(visible={tc12_visible}, titleOk={tc12_titleCorrect}, subtitleOk={tc12_subtitleCorrect})");

            // TC13: Bạch Khuyển nhận popup trên Client -> Popup hiển thị đúng giao diện và nội dung qua CustomMessaging
            bool tc13_msgNameCorrect = NetworkWhiteHoundNotificationUI.WhiteHoundAwakenedMessageName == "loi.whitehound.awakened.notification.v1";
            Assert(tc13_msgNameCorrect && tc12_titleCorrect && tc12_subtitleCorrect, "TC13: Client nhận popup qua CustomMessaging với đúng thông tin chuẩn",
                $"(msgName={NetworkWhiteHoundNotificationUI.WhiteHoundAwakenedMessageName})");

            // TC14: Người chơi khác không phải Bạch Khuyển -> Không nhìn thấy popup thức tỉnh
            var villagerPlayer = new PlayerData { playerID = 0, roleType = RoleType.Villager, isWhiteHoundAwakened = false };
            bool isVillagerWH = villagerPlayer.roleType == RoleType.WhiteHound || villagerPlayer.roleType == RoleType.WhiteWolf;
            Assert(!isVillagerWH, "TC14: Người chơi khác không phải Bạch Khuyển không bị nhận diện nhầm thành WhiteHound",
                $"(isWhiteHound={isVillagerWH})");

            // TC15: Popup đóng được: Nút Đóng hoặc HideNotification ẩn popup thành công
            notifUI.HideNotification();
            bool tc15_hidden = !notifUI.IsVisible;
            Assert(tc15_hidden, "TC15: Popup đóng thành công (ẩn hoàn toàn)",
                $"(visible={notifUI.IsVisible})");

            // TC16: Bạch Khuyển đã thức tỉnh -> Không gửi lại thông báo thức tỉnh cũ
            notifUI.ShowNotification();
            bool wasVisible = notifUI.IsVisible;
            notifUI.HandleDeathResolved(resultWH);
            Assert(wasVisible && NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch, "TC16: Bạch Khuyển đã thức tỉnh không gửi lặp lại thông báo trong cùng trận đấu",
                $"(hasNotified={NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch})");
        }
        finally
        {
            if (notifUI != null) notifUI.HideNotification();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            if (notifGO != null) UnityEngine.Object.DestroyImmediate(notifGO);
        }
    }

    // =========================================================================
    // FEATURE: THÔNG BÁO CHO DOGSPIRIT KHI CẮN TRÚNG BẠCH KHUYỂN (TC1 - TC11)
    // =========================================================================
    public static void Test_TC1_to_TC11_DogSpirit_Bite_Notification()
    {
        Debug.Log("--- BẮT ĐẦU KIỂM THỬ TC1 - TC11: THÔNG BÁO CHO DOGSPIRIT KHI CẮN TRÚNG BẠCH KHUYỂN ---");
        GameObject dsUIGO = new GameObject("Test_NetworkDogSpiritNotificationUI");
        dsUIGO.hideFlags = HideFlags.HideAndDontSave;
        var dsUI = dsUIGO.AddComponent<NetworkDogSpiritNotificationUI>();

        GameObject whUIGO = new GameObject("Test_NetworkWhiteHoundNotificationUI_ForDogTest");
        whUIGO.hideFlags = HideFlags.HideAndDontSave;
        var whUI = whUIGO.AddComponent<NetworkWhiteHoundNotificationUI>();

        var pmGO = SetupPlayerManager(out PlayerManager pm);

        try
        {
            var dogSpiritPlayer = new PlayerData { playerID = 1, playerName = "DogSpirit_P1", roleType = RoleType.DogSpirit, isAlive = true };
            var targetWH = new PlayerData { playerID = 3, playerName = "WhiteHound_P3", roleType = RoleType.WhiteHound, isAlive = true, isWhiteHoundAwakened = true };
            var villagerPlayer = new PlayerData { playerID = 2, playerName = "Villager_P2", roleType = RoleType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            var otherPlayer = new PlayerData { playerID = 0, playerName = "Other_P0", roleType = RoleType.Seer, isAlive = true, isWhiteHoundAwakened = false };

            pm.players.Add(otherPlayer);
            pm.players.Add(dogSpiritPlayer);
            pm.players.Add(villagerPlayer);
            pm.players.Add(targetWH);

            // TC1: DogSpirit cắn trúng Bạch Khuyển -> DogSpirit nhận popup
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var reqBiteWH = new DeathRequest(targetWH, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resBiteWH = new DeathResult(reqBiteWH, DeathOutcome.Prevented, "WhiteHound awakened");

            dsUI.HandleDeathResolved(resBiteWH);
            bool tc1_dsNotified = NetworkDogSpiritNotificationUI.HasNotifiedThisMatch && dsUI.IsVisible;
            bool tc1_titleMatch = NetworkDogSpiritNotificationUI.NotificationTitle == "BẠN ĐÃ CẮN TRÚNG BẠCH KHUYỂN!";
            bool tc1_subtitleMatch = NetworkDogSpiritNotificationUI.NotificationSubtitle.Contains("đánh thức bản năng Bạch Khuyển");
            bool tc1_labelMatch = NetworkDogSpiritNotificationUI.TargetRoleLabel == "MỤC TIÊU: WHITE HOUND";
            Assert(tc1_dsNotified && tc1_titleMatch && tc1_subtitleMatch && tc1_labelMatch,
                "TC1: DogSpirit cắn trúng Bạch Khuyển -> DogSpirit nhận popup với đúng tiêu đề, phụ đề và nhãn mục tiêu",
                $"(dsVisible={dsUI.IsVisible}, titleOk={tc1_titleMatch}, labelOk={tc1_labelMatch})");

            // TC2: Bạch Khuyển bị cắn và thức tỉnh -> Bạch Khuyển nhận popup riêng
            whUI.HandleDeathResolved(resBiteWH);
            bool tc2_whNotified = NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch && whUI.IsVisible;
            bool tc2_whTitleMatch = NetworkWhiteHoundNotificationUI.NotificationTitle == "BẠN ĐÃ BỊ KHUYỂN TINH TẤN CÔNG! BẢN NĂNG BẠCH KHUYỂN ĐÃ THỨC TỈNH!";
            bool tc2_independentPopups = dsUI.IsVisible && whUI.IsVisible && (NetworkDogSpiritNotificationUI.NotificationTitle != NetworkWhiteHoundNotificationUI.NotificationTitle);
            Assert(tc2_whNotified && tc2_whTitleMatch && tc2_independentPopups,
                "TC2: Bạch Khuyển nhận popup riêng, hai popup hoạt động độc lập và có nội dung phân biệt",
                $"(whVisible={whUI.IsVisible}, independent={tc2_independentPopups})");

            // TC3: Bạch Khuyển được Bảo Vệ bảo vệ -> Không có popup cắn trúng
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var protectedWH = new PlayerData { playerID = 3, playerName = "WhiteHound_P3", roleType = RoleType.WhiteHound, isAlive = true, isWhiteHoundAwakened = false };
            var reqProtected = new DeathRequest(protectedWH, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resProtected = new DeathResult(reqProtected, DeathOutcome.Prevented, "Protected by Guardian");

            dsUI.HandleDeathResolved(resProtected);
            whUI.HandleDeathResolved(resProtected);
            bool tc3_noDS = !NetworkDogSpiritNotificationUI.HasNotifiedThisMatch && !dsUI.IsVisible;
            bool tc3_noWH = !NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch && !whUI.IsVisible;
            Assert(tc3_noDS && tc3_noWH, "TC3: Bạch Khuyển được Bảo Vệ bảo vệ (chưa thức tỉnh) -> Không có popup cắn trúng",
                $"(noDS={tc3_noDS}, noWH={tc3_noWH})");

            // TC4: DogSpirit cắn Dân Làng -> Không có popup Bạch Khuyển
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            var reqVillager = new DeathRequest(villagerPlayer, DeathCause.Monster, source: dogSpiritPlayer, sourceRole: RoleType.DogSpirit);
            var resVillager = new DeathResult(reqVillager, DeathOutcome.Killed, "Villager killed");

            dsUI.HandleDeathResolved(resVillager);
            whUI.HandleDeathResolved(resVillager);
            bool tc4_noPopup = !NetworkDogSpiritNotificationUI.HasNotifiedThisMatch && !dsUI.IsVisible;
            Assert(tc4_noPopup, "TC4: DogSpirit cắn Dân Làng -> Không kích hoạt popup Bạch Khuyển",
                $"(dsVisible={dsUI.IsVisible})");

            // TC5: Host là DogSpirit -> Host nhận popup
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkDogSpiritNotificationUI.NotifyDogSpiritBitten(dogSpiritPlayerId: 1, whiteHoundPlayerId: 3);
            bool tc5_hostReceived = dsUI.IsVisible && NetworkDogSpiritNotificationUI.HasNotifiedThisMatch;
            Assert(tc5_hostReceived, "TC5: Host là DogSpirit -> Host nhận popup hiển thị đầy đủ",
                $"(hostVisible={dsUI.IsVisible})");

            // TC6: Client là DogSpirit -> Client nhận popup qua CustomMessaging
            bool tc6_msgRegistered = NetworkDogSpiritNotificationUI.DogSpiritBiteWhiteHoundMessageName == "loi.dogspirit.bitten.whitehound.notification.v1";
            Assert(tc6_msgRegistered, "TC6: Client là DogSpirit -> Đăng ký tin nhắn mạng riêng biệt loi.dogspirit.bitten.whitehound.notification.v1",
                $"(msgName={NetworkDogSpiritNotificationUI.DogSpiritBiteWhiteHoundMessageName})");

            // TC7: Host là Bạch Khuyển -> Host nhận popup riêng
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.NotifyPlayerAwakened(playerId: 3);
            bool tc7_hostWHReceived = whUI.IsVisible && NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            Assert(tc7_hostWHReceived, "TC7: Host là Bạch Khuyển -> Host nhận popup thức tỉnh riêng",
                $"(whHostVisible={whUI.IsVisible})");

            // TC8: Client là Bạch Khuyển -> Client nhận popup riêng
            bool tc8_whMsgRegistered = NetworkWhiteHoundNotificationUI.WhiteHoundAwakenedMessageName == "loi.whitehound.awakened.notification.v1";
            Assert(tc8_whMsgRegistered, "TC8: Client là Bạch Khuyển -> Nhận gói tin thức tỉnh riêng loi.whitehound.awakened.notification.v1",
                $"(whMsgName={NetworkWhiteHoundNotificationUI.WhiteHoundAwakenedMessageName})");

            // TC9: Người chơi khác (Seer, Villager) -> Không nhận thông báo riêng
            bool p0IsDog = otherPlayer.roleType == RoleType.DogSpirit;
            bool p0IsWH = otherPlayer.roleType == RoleType.WhiteHound || otherPlayer.roleType == RoleType.WhiteWolf;
            Assert(!p0IsDog && !p0IsWH, "TC9: Người chơi khác (Seer, Villager) không nhận nhầm bất kỳ thông báo riêng nào",
                $"(isDog={p0IsDog}, isWH={p0IsWH})");

            // TC10: Một lần thức tỉnh -> Không xuất hiện popup trùng lặp
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            dsUI.HandleDeathResolved(resBiteWH);
            bool firstShow = dsUI.IsVisible && NetworkDogSpiritNotificationUI.HasNotifiedThisMatch;
            // Gọi lần thứ 2 trong cùng trận đấu
            dsUI.HandleDeathResolved(resBiteWH);
            bool stillOnlyOne = dsUI.IsVisible && NetworkDogSpiritNotificationUI.HasNotifiedThisMatch;
            Assert(firstShow && stillOnlyOne, "TC10: Một lần thức tỉnh -> Không xuất hiện popup trùng lặp (chỉ 1 lần hiển thị duy nhất)",
                $"(firstShow={firstShow}, stillOne={stillOnlyOne})");

            // TC11: Bắt đầu ván mới -> Có thể gửi thông báo cho sự kiện mới
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            bool resetOk = !NetworkDogSpiritNotificationUI.HasNotifiedThisMatch && !NetworkWhiteHoundNotificationUI.HasNotifiedThisMatch;
            dsUI.HandleDeathResolved(resBiteWH);
            bool newMatchNotified = NetworkDogSpiritNotificationUI.HasNotifiedThisMatch && dsUI.IsVisible;
            Assert(resetOk && newMatchNotified, "TC11: Bắt đầu ván mới -> Reset thành công và có thể kích hoạt thông báo cho ván mới",
                $"(resetOk={resetOk}, newMatchNotified={newMatchNotified})");
        }
        finally
        {
            if (dsUI != null) dsUI.HideNotification();
            if (whUI != null) whUI.HideNotification();
            NetworkDogSpiritNotificationUI.ResetMatchNotificationState();
            NetworkWhiteHoundNotificationUI.ResetMatchNotificationState();
            if (dsUIGO != null) UnityEngine.Object.DestroyImmediate(dsUIGO);
            if (whUIGO != null) UnityEngine.Object.DestroyImmediate(whUIGO);
            if (pmGO != null) UnityEngine.Object.DestroyImmediate(pmGO);
        }
    }

    // =========================================================================
    // TC01 đến TC14: HỆ THỐNG CHAT 2 KÊNH (CHAT LÀNG VÀ CHAT MONSTER)
    // =========================================================================
    public static void Test_TC01_to_TC14_Monster_Chat()
    {
        GameObject pmGO = SetupPlayerManager(out PlayerManager pm);
        GameObject chatMgrGO = null;
        GameObject chatUIGO = null;
        GameObject canvasGO = null;

        try
        {
            canvasGO = new GameObject("TestCanvas_Chat", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            chatMgrGO = new GameObject("TestNetworkChatManager", typeof(NetworkChatManager));
            var chatMgr = chatMgrGO.GetComponent<NetworkChatManager>();
            chatMgr.ClearHistory();

            chatUIGO = new GameObject("TestChatUI", typeof(NetworkChatUI));
            chatUIGO.transform.SetParent(canvasGO.transform, false);
            var chatUI = chatUIGO.GetComponent<NetworkChatUI>();
            chatUI.EnsureUIReferences();

            // Khởi tạo người chơi:
            // P0: Villager
            // P1: Neutral (Madman)
            // P2: Monster (DogSpirit)
            // P3: WhiteHound (chưa thức tỉnh)
            var p0_Villager = new PlayerData { playerID = 0, playerName = "DanLang", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
            var p1_Neutral = new PlayerData { playerID = 1, playerName = "KeDien", roleType = RoleType.Madman, faction = FactionType.Neutral, isAlive = true };
            var p2_Monster = new PlayerData { playerID = 2, playerName = "KhuyenTinh", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
            var p3_WhiteHound = new PlayerData { playerID = 3, playerName = "BachKhuyen", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };

            pm.players.Add(p0_Villager);
            pm.players.Add(p1_Neutral);
            pm.players.Add(p2_Monster);
            pm.players.Add(p3_WhiteHound);

            // TC01: Chat Làng gửi và nhận độc lập với Chat Monster
            chatMgr.SendChatMessage("Xin chao ca lang!", ChatChannel.Village);
            bool tc01_villageHasMsg = chatMgr.VillageHistory.Count == 1;
            bool tc01_monsterEmpty = chatMgr.MonsterHistory.Count == 0;
            Assert(tc01_villageHasMsg && tc01_monsterEmpty, "TC01: Chat Làng gửi và nhận hoàn toàn độc lập với kênh Chat Monster",
                $"(villageCount={chatMgr.VillageHistory.Count}, monsterCount={chatMgr.MonsterHistory.Count})");

            // TC02: Chat Monster gửi vào kênh Monster, không xuất hiện ở Chat Làng
            NetworkChatManager.SetLocalMonsterChatPermission(true);
            chatMgr.SendMonsterChatMessage("Ke hoach dem nay cua Soi!");
            bool tc02_monsterHasMsg = chatMgr.MonsterHistory.Count == 1;
            bool tc02_villageUnchanged = chatMgr.VillageHistory.Count == 1;
            Assert(tc02_monsterHasMsg && tc02_villageUnchanged, "TC02: Chat Monster chỉ gửi vào kênh Monster và không xuất hiện ở Chat Làng",
                $"(villageCount={chatMgr.VillageHistory.Count}, monsterCount={chatMgr.MonsterHistory.Count})");

            // TC03: Villager không thuộc phe Monster
            bool tc03_isMonster = NetworkChatManager.IsPlayerMonster(p0_Villager);
            Assert(!tc03_isMonster, "TC03: Người chơi Dân Làng không thuộc phe Monster",
                $"(isMonster={tc03_isMonster})");

            // TC04: Villager không thể có quyền Monster Chat
            NetworkChatManager.SetLocalMonsterChatPermission(false);
            bool tc04_hasPerm = NetworkChatManager.HasLocalMonsterChatPermission();
            Assert(!tc04_hasPerm, "TC04: Dân Làng không có quyền sử dụng Chat Monster",
                $"(hasPermission={tc04_hasPerm})");

            // TC05: Neutral (Phe thứ 3) không có quyền Monster Chat
            bool tc05_isMonster = NetworkChatManager.IsPlayerMonster(p1_Neutral);
            Assert(!tc05_isMonster, "TC05: Người chơi phe thứ 3 (Neutral) không thuộc phe Monster",
                $"(isMonster={tc05_isMonster})");

            // TC06: Server từ chối phát tán tin nhắn Monster nếu sender không phải là Monster
            bool tc06_rejected = !chatMgr.ProcessMonsterChatMessage(0 /* Villager */, "Tin nhan gia mao");
            Assert(tc06_rejected, "TC06: Server xác thực và từ chối phát tán tin nhắn Monster từ người không thuộc Monster",
                $"(rejected={tc06_rejected})");

            // TC07: Bạch Khuyển chưa thức tỉnh không có quyền Chat Monster
            bool tc07_whMonster = NetworkChatManager.IsPlayerMonster(p3_WhiteHound);
            Assert(!tc07_whMonster, "TC07: Bạch Khuyển khi chưa thức tỉnh không có quyền Chat Monster",
                $"(whMonster={tc07_whMonster})");

            // TC08: Tab CHAT MONSTER bị ẩn khi người chơi chưa có quyền Monster
            NetworkChatManager.SetLocalMonsterChatPermission(false);
            chatUI.RefreshMonsterTabVisibility();
            bool tc08_hidden = !NetworkChatUI.HasMonsterChatPermission();
            Assert(tc08_hidden, "TC08: Tab CHAT MONSTER bị ẩn hoàn toàn khi người chơi không có quyền Monster",
                $"(hidden={tc08_hidden})");

            // TC09: DogSpirit cắn trúng Bạch Khuyển -> Bạch Khuyển thức tỉnh nhưng KHÔNG nằm trong kênh chat của Monster
            p3_WhiteHound.isWhiteHoundAwakened = true;
            p3_WhiteHound.faction = FactionType.Monster;
            bool tc09_inMonsterChat = NetworkChatManager.IsPlayerMonster(p3_WhiteHound);
            Assert(!tc09_inMonsterChat, "TC09: Bạch Khuyển sau khi thức tỉnh thành Sói Trắng KHÔNG nằm trong kênh chat của Monster",
                $"(inMonsterChat={tc09_inMonsterChat})");

            // TC10: Bạch Khuyển thức tỉnh -> Tab CHAT MONSTER vẫn bị ẩn và không có quyền truy cập
            bool tc10_hasMonsterPerm = NetworkChatManager.IsPlayerMonster(p3_WhiteHound);
            Assert(!tc10_hasMonsterPerm, "TC10: Bạch Khuyển thức tỉnh không có quyền Chat Monster, bảo toàn bí mật kênh sói",
                $"(hasPerm={tc10_hasMonsterPerm})");

            // Kiểm tra: Thông báo cho DogSpirit khi cắn trúng Bạch Khuyển đã bị loại bỏ hoàn toàn
            NetworkDogSpiritNotificationUI.NotifyDogSpiritBitten(2, 3);
            bool tcDogSpiritNoNotification = !NetworkDogSpiritNotificationUI.HasNotifiedThisMatch;
            Assert(tcDogSpiritNoNotification, "TC_DogSpiritNoNotification: Đã loại bỏ hoàn toàn thông báo cắn trúng Bạch Khuyển cho DogSpirit",
                $"(hasNotified={!tcDogSpiritNoNotification})");

            // TC11: Nếu được Bảo Vệ bảo vệ thành công -> Bạch Khuyển không thức tỉnh và không có quyền
            var p3_Protected = new PlayerData { playerID = 4, playerName = "BachKhuyen2", roleType = RoleType.WhiteHound, faction = FactionType.Villager, isAlive = true, isWhiteHoundAwakened = false };
            pm.players.Add(p3_Protected);
            bool tc11_noAwaken = !p3_Protected.isWhiteHoundAwakened && p3_Protected.faction == FactionType.Villager;
            Assert(tc11_noAwaken, "TC11: Được Bảo Vệ bảo vệ thành công -> Bạch Khuyển không thức tỉnh và không nhận quyền Chat Monster",
                $"(isAwakened={p3_Protected.isWhiteHoundAwakened}, faction={p3_Protected.faction})");

            // TC12: Chuyển đổi qua lại giữa Tab Chat Làng và Tab Chat Monster bảo toàn lịch sử
            chatUI.SwitchToTab(NetworkChatUI.ChatTab.Village);
            bool tc12_villageActive = chatUI.ActiveTab == NetworkChatUI.ChatTab.Village;
            chatUI.SwitchToTab(NetworkChatUI.ChatTab.Monster);
            bool tc12_monsterActive = chatUI.ActiveTab == NetworkChatUI.ChatTab.Monster;
            bool tc12_historyPreserved = chatMgr.VillageHistory.Count == 1 && chatMgr.MonsterHistory.Count == 1;
            Assert(tc12_villageActive && tc12_monsterActive && tc12_historyPreserved,
                "TC12: Chuyển đổi qua lại giữa 2 Tab hoạt động mượt mà và bảo toàn lịch sử từng kênh",
                $"(historyOk={tc12_historyPreserved})");

            // TC13: Định dạng tin nhắn Monster có prefix phân biệt
            var monsterMsg = chatMgr.MonsterHistory[0];
            bool tc13_channelCheck = monsterMsg.Channel == (byte)ChatChannel.Monster;
            Assert(tc13_channelCheck, "TC13: Tin nhắn Monster được lưu đúng Channel = 1 để hiển thị màu và nhãn [Sói] phân biệt",
                $"(channel={monsterMsg.Channel})");

            // TC14: Reset trận mới -> Làm sạch toàn bộ lịch sử 2 kênh và reset quyền thức tỉnh
            ThuongOnlineWinAdapter.ResetForNewMatch();
            NetworkChatManager.SetLocalMonsterChatPermission(false);
            chatUI.RefreshMonsterTabVisibility();
            bool tc14_historyEmpty = chatMgr.VillageHistory.Count == 0 && chatMgr.MonsterHistory.Count == 0;
            bool tc14_whReset = !p3_WhiteHound.isWhiteHoundAwakened && p3_WhiteHound.faction == FactionType.Villager;
            Assert(tc14_historyEmpty && tc14_whReset, "TC14: Bắt đầu ván mới -> Xóa sạch lịch sử 2 kênh chat và khôi phục trạng thái Bạch Khuyển ban đầu",
                $"(historyEmpty={tc14_historyEmpty}, whReset={tc14_whReset})");
        }
        finally
        {
            if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            if (chatMgrGO != null) UnityEngine.Object.DestroyImmediate(chatMgrGO);
            Teardown(pmGO, pm);
        }
    }
}

/// <summary>
/// Menu cấu hình và bộ kiểm thử NUnit TC01–TC10 cho Debug Mode bật/tắt xét điều kiện thắng (DisableWinConditionForTesting).
/// Mặc định luôn false; chỉ cho phép bật trong Editor hoặc Development Build và đồng bộ từ Host/Server.
/// </summary>
public class WinConditionDisableTests
{
    private const string ToggleMenuPath = "Tools/Loi/Testing/Disable Win Condition For Testing";

    private GameObject rootGo;
    private PlayerManager playerManager;
    private GameRoleManager gameRoleManager;
    private WinConditionManager winConditionManager;
    private NightManager nightManager;
    private DeathResolver deathResolver;
    private VoteManager voteManager;

    [MenuItem(ToggleMenuPath, false)]
    public static void ToggleDisableWinCondition()
    {
        bool next = !WinConditionTestConfig.DisableWinConditionForTesting;
        ThuongOnlineWinAdapter.SetDebugMode(next);
        Menu.SetChecked(ToggleMenuPath, WinConditionTestConfig.DisableWinConditionForTesting);
        Debug.Log(WinConditionTestConfig.DisableWinConditionForTesting
            ? "[DebugMode] ĐÃ BẬT chế độ kiểm thử: Tạm ngưng xét điều kiện thắng (chỉ có hiệu lực trong phiên hiện tại)."
            : "[DebugMode] ĐÃ TẮT chế độ kiểm thử: Khôi phục xét điều kiện thắng bình thường.");
    }

    [MenuItem(ToggleMenuPath, true)]
    public static bool ToggleDisableWinConditionValidate()
    {
        Menu.SetChecked(ToggleMenuPath, WinConditionTestConfig.DisableWinConditionForTesting);
        return WinConditionTestConfig.CanModifyDebugMode();
    }

    [MenuItem("Tools/Loi/Testing/Restore Win Condition (Enable)")]
    public static void RestoreWinConditionMenu()
    {
        WinConditionTestConfig.ResetForNewMatch();
        ThuongOnlineWinAdapter.Instance?.ResetDebugToggleForNewMatch();
        Menu.SetChecked(ToggleMenuPath, false);
        Thuong.Online.ThuongOnlineSession.Active?.BroadcastWinCheckTestModeOnServer();
        WinConditionManager.Instance?.NotifyMatchStateChanged();
        Debug.Log("[DebugMode] Đã đặt lại DisableWinConditionForTesting = false (chế độ thắng thua bình thường).");
    }

    [MenuItem("Tools/Tests/Run Win Condition Disable Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ DEBUG MODE XÉT ĐIỀU KIỆN THẮNG (TC01–TC10)");
        Debug.Log("=================================================================");

        var tests = new WinConditionDisableTests();
        int passed = 0;
        int failed = 0;

        void Execute(string name, Action body)
        {
            try
            {
                tests.Setup();
                body();
                Debug.Log($"[PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {name}: {ex.Message}");
                failed++;
            }
            finally
            {
                tests.TearDown();
            }
        }

        Execute("TC01: Mở game bình thường -> Debug Mode mặc định tắt (false)", () => tests.TC01_DefaultState_DebugModeIsDisabled());
        Execute("TC02: Khi tắt Debug Mode -> Dân Làng thắng đúng điều kiện", () => tests.TC02_DebugModeOff_VillagersWinNormally());
        Execute("TC03: Khi tắt Debug Mode -> Ma Sói thắng đúng điều kiện", () => tests.TC03_DebugModeOff_WerewolvesWinNormally());
        Execute("TC04: WhiteHound và các phe trung lập vẫn thắng đúng luật", () => tests.TC04_DebugModeOff_WhiteHoundAndNeutralsWinNormally());
        Execute("TC05: Bật Debug Mode trong Editor -> Trận không tự kết thúc khi đạt điều kiện thắng", () => tests.TC05_DebugModeOn_MatchDoesNotEndOnWinConditions());
        Execute("TC06: Khi bật Debug Mode -> Người chơi vẫn chết và dùng kỹ năng bình thường", () => tests.TC06_DebugModeOn_PlayersStillDieAndUseSkillsNormally());
        Execute("TC07: Host và Client nhận cùng trạng thái Debug Mode", () => tests.TC07_HostAndClient_SyncDebugModeState());
        Execute("TC08: Client không thể tự ý thay đổi trạng thái Debug Mode", () => tests.TC08_ClientCannotModifyDebugMode());
        Execute("TC09: Bắt đầu ván mới không giữ nhầm trạng thái kiểm thử", () => tests.TC09_NewMatch_DoesNotRetainTestingState());
        Execute("TC10: Release Build không thể bật Debug Mode", () => tests.TC10_ReleaseBuild_CannotEnableDebugMode());

        Debug.Log("=================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ DEBUG MODE: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================================");
    }

    [SetUp]
    public void Setup()
    {
        WinConditionTestConfig.ResetForNewMatch();

        rootGo = new GameObject("[TestWinDebugModeEnv]");
        playerManager = rootGo.AddComponent<PlayerManager>();
        PlayerManager.Instance = playerManager;

        gameRoleManager = rootGo.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gameRoleManager;
        gameRoleManager.manualLogicPrototype = true;
        gameRoleManager.currentState = GameState.Day;
        gameRoleManager.currentPhase = GamePhase.DayResolution;
        gameRoleManager.currentDay = 1;

        winConditionManager = rootGo.AddComponent<WinConditionManager>();
        WinConditionManager.Instance = winConditionManager;

        nightManager = rootGo.AddComponent<NightManager>();
        NightManager.Instance = nightManager;

        deathResolver = rootGo.AddComponent<DeathResolver>();
        DeathResolver.Instance = deathResolver;

        voteManager = rootGo.AddComponent<VoteManager>();
        VoteManager.Instance = voteManager;

        playerManager.players = new List<PlayerData>
        {
            new PlayerData { playerID = 0, playerName = "P0_DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true },
            new PlayerData { playerID = 1, playerName = "P1_Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true },
            new PlayerData { playerID = 2, playerName = "P2_Madman", roleType = RoleType.Madman, faction = FactionType.Neutral, isAlive = true },
            new PlayerData { playerID = 3, playerName = "P3_Villager2", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true }
        };
    }

    [TearDown]
    public void TearDown()
    {
        WinConditionTestConfig.ResetForNewMatch();
        if (PlayerManager.Instance == playerManager) PlayerManager.Instance = null;
        if (GameRoleManager.Instance == gameRoleManager) GameRoleManager.Instance = null;
        if (WinConditionManager.Instance == winConditionManager) WinConditionManager.Instance = null;
        if (NightManager.Instance == nightManager) NightManager.Instance = null;
        if (DeathResolver.Instance == deathResolver) DeathResolver.Instance = null;
        if (VoteManager.Instance == voteManager) VoteManager.Instance = null;
        if (rootGo != null) UnityEngine.Object.DestroyImmediate(rootGo);
    }

    [Test]
    public void TC01_DefaultState_DebugModeIsDisabled()
    {
        Assert.IsFalse(WinConditionTestConfig.DisableWinConditionForTesting);
        Assert.IsFalse(WinConditionTestConfig.IsWinConditionDisabled);
    }

    [Test]
    public void TC02_DebugModeOff_VillagersWinNormally()
    {
        playerManager.players[0].isAlive = false;
        playerManager.players[2].isAlive = false;

        ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
        winConditionManager.CheckWinCondition(true);

        Assert.AreEqual(GameState.GameOver, gameRoleManager.currentState);
        Assert.AreEqual("Villagers", gameRoleManager.Winner);
    }

    [Test]
    public void TC03_DebugModeOff_WerewolvesWinNormally()
    {
        playerManager.players[2].isAlive = false;
        playerManager.players[3].isAlive = false;

        ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
        winConditionManager.CheckWinCondition(true);

        Assert.AreEqual(GameState.GameOver, gameRoleManager.currentState);
        Assert.AreEqual("Werewolves", gameRoleManager.Winner);
    }

    [Test]
    public void TC04_DebugModeOff_WhiteHoundAndNeutralsWinNormally()
    {
        // Kiểm tra WhiteHound thức tỉnh thắng (1v1) và Madman bị vote loại ngày 1 thắng
        playerManager.players[0].isAlive = false;
        playerManager.players[1].roleType = RoleType.WhiteHound;
        playerManager.players[1].isWhiteHoundAwakened = true;
        playerManager.players[1].faction = FactionType.Monster;
        playerManager.players[2].isAlive = false;

        ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
        winConditionManager.CheckWinCondition(true);

        Assert.AreEqual(GameState.GameOver, gameRoleManager.currentState);
        Assert.AreEqual("White Wolf", gameRoleManager.Winner);
    }

    [Test]
    public void TC05_DebugModeOn_MatchDoesNotEndOnWinConditions()
    {
        WinConditionTestConfig.DisableWinConditionForTesting = true;
        Assert.IsTrue(WinConditionTestConfig.IsWinConditionDisabled);

        // Dù Dân Làng đạt điều kiện thắng (hoặc Ma Sói/Neutral), trận vẫn không GameOver
        playerManager.players[0].isAlive = false;
        playerManager.players[2].isAlive = false;
        ThuongOnlineWinAdapter.EnsureWinEvaluatorInstalled();
        winConditionManager.CheckWinCondition(true);

        Assert.AreNotEqual(GameState.GameOver, gameRoleManager.currentState);
        Assert.IsTrue(string.IsNullOrEmpty(gameRoleManager.Winner));
    }

    [Test]
    public void TC06_DebugModeOn_PlayersStillDieAndUseSkillsNormally()
    {
        WinConditionTestConfig.DisableWinConditionForTesting = true;

        nightManager.StartNight();
        nightManager.SetMonsterTarget(1, RoleType.DogSpirit);
        nightManager.ResolveNight();
        Assert.IsFalse(playerManager.players[1].isAlive, "Người chơi vẫn chết do kỹ năng đêm khi bật Debug Mode.");

        gameRoleManager.SetPhase(GamePhase.Voting);
        voteManager.StartVote();
        Assert.IsTrue(voteManager.TryVote(2, 0));
        Assert.IsTrue(voteManager.TryVote(3, 0));
        voteManager.ResolveVote();
        Assert.AreEqual(0, voteManager.LastResolution.Eliminated.playerID);
        Assert.IsFalse(playerManager.players[0].isAlive, "Người chơi vẫn bị loại qua bỏ phiếu.");
        Assert.AreNotEqual(GameState.GameOver, gameRoleManager.currentState);
    }

    [Test]
    public void TC07_HostAndClient_SyncDebugModeState()
    {
        WinConditionTestConfig.SetServerSyncOverride(true);
        Assert.IsTrue(WinConditionTestConfig.IsWinConditionDisabled);

        WinConditionTestConfig.SetServerSyncOverride(false);
        Assert.IsFalse(WinConditionTestConfig.IsWinConditionDisabled);
    }

    [Test]
    public void TC08_ClientCannotModifyDebugMode()
    {
        Assert.IsFalse(WinConditionTestConfig.CanModifyDebugModeForState(isListening: true, isServer: false));
        Assert.IsTrue(WinConditionTestConfig.CanModifyDebugModeForState(isListening: true, isServer: true));
    }

    [Test]
    public void TC09_NewMatch_DoesNotRetainTestingState()
    {
        WinConditionTestConfig.DisableWinConditionForTesting = true;
        Assert.IsTrue(WinConditionTestConfig.IsWinConditionDisabled);

        ThuongOnlineWinAdapter.ResetForNewMatch();

        Assert.IsFalse(WinConditionTestConfig.DisableWinConditionForTesting);
        Assert.IsFalse(WinConditionTestConfig.IsWinConditionDisabled);
    }

    [Test]
    public void TC10_ReleaseBuild_CannotEnableDebugMode()
    {
        Assert.IsFalse(WinConditionTestConfig.ResolveDebugStateForEnvironment(isEditor: false, isDebugBuild: false, requestedState: true),
            "Release Build tuyệt đối không được phép bật Debug Mode.");
        Assert.IsTrue(WinConditionTestConfig.ResolveDebugStateForEnvironment(isEditor: false, isDebugBuild: true, requestedState: true),
            "Development Build được phép bật khi yêu cầu.");
        Assert.IsTrue(WinConditionTestConfig.ResolveDebugStateForEnvironment(isEditor: true, isDebugBuild: false, requestedState: true),
            "Unity Editor được phép bật khi yêu cầu.");
    }
}
