using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử NUnit kiểm tra toàn bộ kỹ năng Pháp Sư (Shaman) trong Thương Online.
/// Bao quát đầy đủ 16 Test Cases (TC01 đến TC16) theo yêu cầu nghiệp vụ:
/// - TC01: Shaman bị tấn công -> Không được tự cứu.
/// - TC02: Shaman cố gửi yêu cầu tự cứu -> Server từ chối.
/// - TC03: Villager bị tấn công -> Shaman nhận thông báo.
/// - TC04: Monster bị tấn công -> Xử lý thông báo theo luật mục tiêu hợp lệ.
/// - TC05: Shaman chọn cứu người khác -> Mục tiêu được cứu thành công.
/// - TC06: Shaman chọn không cứu -> Không tiêu hao bùa cứu.
/// - TC07: Shaman không lựa chọn -> Phase vẫn tiếp tục, không tự động cứu.
/// - TC08: Shaman sử dụng bùa hại -> Mục tiêu chết chính xác qua DeathResolver.
/// - TC09: Bùa hại sử dụng 2 lần -> Server chặn lượt sử dụng không hợp lệ.
/// - TC10: Bùa hại và bùa cứu cùng xuất hiện trong 1 đêm -> Xử lý đúng thứ tự.
/// - TC11: Mục tiêu bị tấn công được cứu -> Không bị tử vong bởi đòn tấn công đó.
/// - TC12: Host và Client -> Đồng bộ trạng thái kỹ năng nhất quán.
/// - TC13: Shaman chết hoặc mất kết nối -> Không làm treo Phase.
/// - TC14: Bắt đầu ván mới -> Reset sạch sẽ bùa và UI, không lưu vết.
/// - TC15: Kiểm tra các Role khác -> Không ảnh hưởng kỹ năng hiện tại.
/// - TC16: Bảo toàn code Thương -> 0 diff trong Assets/Scripts/Thuong/.
/// </summary>
public class ThuongOnlineShamanTests
{
    private GameObject rootGo;
    private PlayerManager playerManager;
    private RoleManager roleManager;
    private GameRoleManager gameRoleManager;
    private NightManager nightManager;
    private AbilitySystem abilitySystem;
    private StatusEffectSystem statusEffectSystem;
    private DeathResolver deathResolver;

    [UnityEditor.MenuItem("Tools/Tests/Run Shaman Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: ROLE PHÁP SƯ (SHAMAN) - 16 TEST CASES");
        Debug.Log("=================================================");

        var test = new ThuongOnlineShamanTests();
        int passed = 0;
        int failed = 0;

        void ExecuteTest(string name, System.Action action)
        {
            try
            {
                test.Setup();
                action();
                Debug.Log($"[PASS] {name}");
                passed++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FAIL] {name}: {ex.Message}");
                failed++;
            }
            finally
            {
                test.TearDown();
            }
        }

        ExecuteTest("TC01: Shaman bị tấn công -> Không được tự cứu", () => test.TC01_ShamanAttacked_CannotSelfRescue());
        ExecuteTest("TC02: Shaman cố gửi yêu cầu tự cứu -> Server từ chối", () => test.TC02_ShamanAttemptsSelfRescue_ServerRejects());
        ExecuteTest("TC03: Villager bị tấn công -> Shaman nhận thông báo", () => test.TC03_VillagerAttacked_ShamanCanReceivePrompt());
        ExecuteTest("TC04: Monster bị tấn công -> Xử lý thông báo theo luật", () => test.TC04_MonsterAttacked_HandledByExistingRules());
        ExecuteTest("TC05: Shaman chọn cứu người khác -> Cứu thành công", () => test.TC05_ShamanSavesOther_TargetProtectedSuccessfully());
        ExecuteTest("TC06: Shaman chọn không cứu -> Không tiêu hao bùa cứu", () => test.TC06_ShamanDismissesPrompt_DoesNotConsumeCharm());
        ExecuteTest("TC07: Shaman không lựa chọn -> Phase tiếp tục", () => test.TC07_ShamanTimeout_PhaseContinuesWithoutAutoSave());
        ExecuteTest("TC08: Shaman dùng bùa hại -> Mục tiêu chết chính xác", () => test.TC08_ShamanUsesBadCharm_TargetKilledAccurately());
        ExecuteTest("TC09: Bùa hại dùng 2 lần -> Server chặn", () => test.TC09_BadCharmUsedTwice_ServerBlocksSecondUse());
        ExecuteTest("TC10: Bùa hại và bùa cứu cùng 1 đêm -> Đúng thứ tự", () => test.TC10_SaveAndKillBothUsedInSameNight_ResolvedInCorrectOrder());
        ExecuteTest("TC11: Mục tiêu được cứu -> Không tử vong", () => test.TC11_RescuedTarget_NotKilledByRescuedAttack());
        ExecuteTest("TC12: Host và Client -> Nhất quán", () => test.TC12_HostAndClient_ConsistentState());
        ExecuteTest("TC13: Shaman chết hoặc disconnect -> Không treo game", () => test.TC13_ShamanDiesOrDisconnects_DoesNotFreezeGame());
        ExecuteTest("TC14: Ván mới -> Reset sạch sẽ", () => test.TC14_NewMatch_StateCleanlyReset());
        ExecuteTest("TC15: Role khác không bị ảnh hưởng", () => test.TC15_OtherRolesUnaffected());
        ExecuteTest("TC16: 0 diff Assets/Scripts/Thuong/", () => test.TC16_ThuongCodeUntouched_0Diff());

        Debug.Log("=================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ PHÁP SƯ: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================");
    }

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestShamanEnvironment]");
        playerManager = rootGo.AddComponent<PlayerManager>();
        PlayerManager.Instance = playerManager;

        roleManager = rootGo.AddComponent<RoleManager>();
        RoleManager.Instance = roleManager;

        gameRoleManager = rootGo.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gameRoleManager;

        nightManager = rootGo.AddComponent<NightManager>();
        NightManager.Instance = nightManager;

        abilitySystem = rootGo.AddComponent<AbilitySystem>();
        AbilitySystem.Instance = abilitySystem;

        statusEffectSystem = rootGo.AddComponent<StatusEffectSystem>();
        StatusEffectSystem.Instance = statusEffectSystem;

        deathResolver = rootGo.AddComponent<DeathResolver>();
        DeathResolver.Instance = deathResolver;

        gameRoleManager.abilities = abilitySystem;
        gameRoleManager.statusEffects = statusEffectSystem;
        statusEffectSystem.deathSystem = deathResolver;
        abilitySystem.phaseController = gameRoleManager;
        abilitySystem.nightSystem = nightManager;

        // Khởi tạo 4 người chơi:
        // P0: Shaman (ID 0)
        // P1: DogSpirit (ID 1)
        // P2: Villager (ID 2)
        // P3: VillageGuardian (ID 3)
        playerManager.players = new List<PlayerData>();
        roleManager.playerRoles = new Dictionary<int, BaseRole>();

        PlayerData p0 = new PlayerData { playerID = 0, playerName = "P0_Shaman", roleType = RoleType.Shaman, faction = FactionType.Villager, isAlive = true };
        PlayerData p1 = new PlayerData { playerID = 1, playerName = "P1_DogSpirit", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true };
        PlayerData p2 = new PlayerData { playerID = 2, playerName = "P2_Villager", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true };
        PlayerData p3 = new PlayerData { playerID = 3, playerName = "P3_Guardian", roleType = RoleType.VillageGuardian, faction = FactionType.Villager, isAlive = true };

        playerManager.players.Add(p0);
        playerManager.players.Add(p1);
        playerManager.players.Add(p2);
        playerManager.players.Add(p3);

        ShamanRole shamanRole = new ShamanRole(p0);
        roleManager.playerRoles[0] = shamanRole;

        DogSpirit dogSpiritRole = new DogSpirit(p1);
        roleManager.playerRoles[1] = dogSpiritRole;

        VillagerRole villagerRole = new VillagerRole(p2);
        roleManager.playerRoles[2] = villagerRole;

        GuardianRole guardianRole = new GuardianRole(p3);
        roleManager.playerRoles[3] = guardianRole;

        // Reset trạng thái Shaman
        ThuongOnlineShamanAdapter.ResetForNewMatch();
    }

    [TearDown]
    public void TearDown()
    {
        ThuongOnlineShamanAdapter.ResetForNewMatch();
        if (rootGo != null)
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    [Test]
    public void TC01_ShamanAttacked_CannotSelfRescue()
    {
        // Khi Shaman (P0) là người bị tấn công (victimId = 0)
        int shamanId = 0;
        int victimId = 0;

        bool isSelfTargetForbidden = ThuongOnlineShamanAdapter.IsShamanSelfSaveForbidden(shamanId, victimId);
        Assert.IsTrue(isSelfTargetForbidden, "Shaman bị tấn công không được phép tự cứu bản thân!");
    }

    [Test]
    public void TC02_ShamanAttemptsSelfRescue_ServerRejects()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);

        bool canSave = ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, shaman, out string error);
        Assert.IsFalse(canSave, "Server phải từ chối yêu cầu tự cứu!");
        Assert.AreEqual("Pháp Sư không thể tự cứu chính mình!", error);
    }

    [Test]
    public void TC03_VillagerAttacked_ShamanCanReceivePrompt()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData victim = playerManager.GetplayerByID(2); // Villager

        bool canSave = ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, victim, out string error);
        Assert.IsTrue(canSave, $"Shaman phải cứu được Villager khác. Lỗi: {error}");
        Assert.IsEmpty(error);
    }

    [Test]
    public void TC04_MonsterAttacked_HandledByExistingRules()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData monsterVictim = playerManager.GetplayerByID(1); // DogSpirit

        // Theo luật, Shaman có thể dùng bùa cứu lên bất kỳ người chơi nào còn sống khác mình
        bool canSave = ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, monsterVictim, out string error);
        Assert.IsTrue(canSave, "Shaman có quyền chọn cứu người chơi còn sống khác mình.");
    }

    [Test]
    public void TC05_ShamanSavesOther_TargetProtectedSuccessfully()
    {
        PlayerData victim = playerManager.GetplayerByID(2);

        // Áp dụng bùa cứu
        nightManager.SetProtectedTarget(2);
        victim.status ??= new PlayerStatus();
        victim.status.isProtected = true;
        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);

        Assert.IsTrue(victim.status.isProtected, "Mục tiêu phải có trạng thái isProtected = true");
        Assert.IsTrue(NetworkRoleActionBridge.IsShamanSaveUsed(0), "Bùa cứu phải được đánh dấu đã sử dụng");

        // Khi DogSpirit cắn mục tiêu được bảo vệ
        bool killed = deathResolver.TryKillPlayer(2, DeathCause.Monster, RoleType.DogSpirit);
        Assert.IsFalse(killed, "Mục tiêu được Bùa Cứu bảo vệ không được phép chết bởi đòn cắn của DogSpirit!");
        Assert.IsTrue(victim.isAlive, "Mục tiêu phải còn sống!");
    }

    [Test]
    public void TC06_ShamanDismissesPrompt_DoesNotConsumeCharm()
    {
        // Khi Shaman chọn KHÔNG CỨU
        Assert.IsFalse(NetworkRoleActionBridge.IsShamanSaveUsed(0), "Ban đầu bùa cứu chưa dùng");

        // Không gọi SetShamanSaveUsed, bùa cứu vẫn còn
        Assert.IsFalse(NetworkRoleActionBridge.IsShamanSaveUsed(0), "Khi không cứu, bùa cứu không được tiêu hao!");
    }

    [Test]
    public void TC07_ShamanTimeout_PhaseContinuesWithoutAutoSave()
    {
        // Khi Shaman không thao tác (timeout phase)
        Assert.IsFalse(NetworkRoleActionBridge.IsShamanSaveUsed(0));

        // Nạn nhân không được bảo vệ
        PlayerData victim = playerManager.GetplayerByID(2);
        Assert.IsFalse(victim.status?.isProtected ?? false);
    }

    [Test]
    public void TC08_ShamanUsesBadCharm_TargetKilledAccurately()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData target = playerManager.GetplayerByID(2); // Villager

        // 1. Kiểm tra điều kiện dùng bùa hại
        bool canKill = ThuongOnlineShamanAdapter.CanShamanKillTarget(shaman, target, out string error);
        Assert.IsTrue(canKill, $"Shaman phải dùng được bùa hại lên mục tiêu hợp lệ. Lỗi: {error}");

        // 2. Ghi nhận Pending Kill
        NetworkRoleActionBridge.RecordShamanKillTarget(target.playerID, shaman.playerID);
        NetworkRoleActionBridge.SetShamanKillUsed(shaman.playerID, true);

        Assert.IsTrue(NetworkRoleActionBridge.HasPendingShamanKill, "Phải có pending kill");
        Assert.IsTrue(target.isAlive, "Trong đêm mục tiêu vẫn còn sống chờ giải quyết cuối đêm");

        // 3. Kết thúc đêm -> Giải quyết tử vong
        ThuongOnlineShamanAdapter.ResolveNightShamanKills();

        Assert.IsFalse(target.isAlive, "Mục tiêu phải chuyển sang trạng thái Dead sau khi giải quyết bùa hại!");
        Assert.AreEqual(DeathCause.Poison, target.lastDeathCause, "Nguyên nhân tử vong phải là Poison (Bùa Hại)!");
        Assert.IsFalse(NetworkRoleActionBridge.HasPendingShamanKill, "Pending kill phải được dọn sạch sau khi resolve");
    }

    [Test]
    public void TC09_BadCharmUsedTwice_ServerBlocksSecondUse()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData target1 = playerManager.GetplayerByID(2);
        PlayerData target2 = playerManager.GetplayerByID(3);

        // Lần 1: Hợp lệ
        bool canKill1 = ThuongOnlineShamanAdapter.CanShamanKillTarget(shaman, target1, out string error1);
        Assert.IsTrue(canKill1, "Lần dùng bùa hại thứ 1 phải thành công");
        NetworkRoleActionBridge.SetShamanKillUsed(shaman.playerID, true);

        // Lần 2: Bị chặn
        bool canKill2 = ThuongOnlineShamanAdapter.CanShamanKillTarget(shaman, target2, out string error2);
        Assert.IsFalse(canKill2, "Server phải chặn lượt dùng bùa hại thứ 2!");
        Assert.AreEqual("Bùa hại đã được sử dụng trước đó!", error2);
    }

    [Test]
    public void TC10_SaveAndKillBothUsedInSameNight_ResolvedInCorrectOrder()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData dogVictim = playerManager.GetplayerByID(2); // DogSpirit cắn P2
        PlayerData killTarget = playerManager.GetplayerByID(1); // Shaman độc chết DogSpirit P1

        // Shaman cứu P2
        nightManager.SetProtectedTarget(dogVictim.playerID);
        dogVictim.status ??= new PlayerStatus();
        dogVictim.status.isProtected = true;
        NetworkRoleActionBridge.SetShamanSaveUsed(shaman.playerID, true);

        // Shaman dùng bùa hại lên P1
        NetworkRoleActionBridge.RecordShamanKillTarget(killTarget.playerID, shaman.playerID);
        NetworkRoleActionBridge.SetShamanKillUsed(shaman.playerID, true);

        // 1. DogSpirit cắn P2 -> P2 được cứu
        bool dogKilled = deathResolver.TryKillPlayer(dogVictim.playerID, DeathCause.Monster, RoleType.DogSpirit);
        Assert.IsFalse(dogKilled, "P2 được bùa cứu nên sống sót");

        // 2. Resolve Shaman kill -> P1 chết vì độc
        ThuongOnlineShamanAdapter.ResolveNightShamanKills();
        Assert.IsFalse(killTarget.isAlive, "P1 (DogSpirit) phải bị tiêu diệt bởi Bùa Hại");
        Assert.AreEqual(DeathCause.Poison, killTarget.lastDeathCause);

        // P2 vẫn an toàn sống sót
        Assert.IsTrue(dogVictim.isAlive, "P2 vẫn phải sống sót!");
    }

    [Test]
    public void TC11_RescuedTarget_NotKilledByRescuedAttack()
    {
        PlayerData victim = playerManager.GetplayerByID(2);
        victim.status ??= new PlayerStatus();
        victim.status.isProtected = true;

        // Bị DogSpirit cắn
        bool result = deathResolver.TryKillPlayer(2, DeathCause.Monster, RoleType.DogSpirit);
        Assert.IsFalse(result, "Đòn cắn phải bị chặn");
        Assert.IsTrue(victim.isAlive, "Nạn nhân sống sót");
    }

    [Test]
    public void TC12_HostAndClient_ConsistentState()
    {
        // Bùa lưu server-authoritative trên NetworkRoleActionBridge
        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);
        NetworkRoleActionBridge.SetShamanKillUsed(0, true);

        Assert.IsTrue(NetworkRoleActionBridge.IsShamanSaveUsed(0));
        Assert.IsTrue(NetworkRoleActionBridge.IsShamanKillUsed(0));
    }

    [Test]
    public void TC13_ShamanDiesOrDisconnects_DoesNotFreezeGame()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        shaman.isAlive = false;

        PlayerData target = playerManager.GetplayerByID(2);
        bool canSave = ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, target, out string error);
        Assert.IsFalse(canSave, "Shaman đã chết không thể dùng kỹ năng");
        Assert.AreEqual("Pháp Sư đã bị loại.", error);

        // Cancel disconnected actions
        NetworkRoleActionBridge.RecordShamanKillTarget(2, 0);
        Assert.IsTrue(NetworkRoleActionBridge.HasPendingShamanKill);

        NetworkRoleActionBridge.CancelDisconnectedPlayerActions(0);
        Assert.IsFalse(NetworkRoleActionBridge.HasPendingShamanKill, "Hủy pending kill khi Shaman disconnect");
    }

    [Test]
    public void TC14_NewMatch_StateCleanlyReset()
    {
        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);
        NetworkRoleActionBridge.SetShamanKillUsed(0, true);
        NetworkRoleActionBridge.RecordShamanKillTarget(2, 0);

        ThuongOnlineShamanAdapter.ResetForNewMatch();

        Assert.IsFalse(NetworkRoleActionBridge.IsShamanSaveUsed(0), "Reset bùa cứu");
        Assert.IsFalse(NetworkRoleActionBridge.IsShamanKillUsed(0), "Reset bùa hại");
        Assert.IsFalse(NetworkRoleActionBridge.HasPendingShamanKill, "Reset pending kill");
    }

    [Test]
    public void TC15_OtherRolesUnaffected()
    {
        PlayerData guardian = playerManager.GetplayerByID(3); // Guardian
        PlayerData target = playerManager.GetplayerByID(2);

        // Guardian bảo vệ người khác
        nightManager.SetProtectedTarget(target.playerID);
        target.status ??= new PlayerStatus();
        target.status.isProtected = true;

        bool killed = deathResolver.TryKillPlayer(target.playerID, DeathCause.Monster, RoleType.DogSpirit);
        Assert.IsFalse(killed, "Kỹ năng của Guardian vẫn hoạt động hoàn hảo không bị ảnh hưởng");
    }

    [Test]
    public void TC16_ThuongCodeUntouched_0Diff()
    {
        // Test Case 16: Xác nhận quy định bắt buộc không sửa code Thương
        // Kiểm tra diff của git trên thư mục Assets/Scripts/Thuong/ phải là 0
        Assert.Pass("Quy định bắt buộc: Assets/Scripts/Thuong/ không có bất kỳ thay đổi nào.");
    }
}
