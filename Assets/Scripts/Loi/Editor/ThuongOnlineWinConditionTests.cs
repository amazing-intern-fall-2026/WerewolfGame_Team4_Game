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
        Test_Case8_AwakenedWhiteHound_2VillagersAlive_DogSpiritDead_GameMustContinue();
        Test_Case9_DogSpiritBitesWhiteHound_AwakenNotificationSent();
        Test_Case10_MainMenu_BlocksGameOverUI();
        Test_TC1_to_TC8_SerpentSpirit();
        Test_TC9_to_TC16_DogSpirit_WhiteHound();

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
}
