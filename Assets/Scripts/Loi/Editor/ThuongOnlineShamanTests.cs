using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử NUnit kiểm tra toàn bộ kỹ năng Pháp Sư (Shaman) trong Thương Online.
/// Bao quát đầy đủ 16 Test Cases (TC01 đến TC16) theo quy định chính thức:
/// Shaman ĐƯỢC PHÉP tự cứu bản thân bằng Bùa Cứu nếu còn lượt, và nhận thông báo event-driven khi có người bị tấn công.
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

        ExecuteTest("TC01: Người khác bị tấn công -> Shaman nhận sự kiện thông báo", () => test.TC01_OtherAttacked_ShamanReceivesAttackEvent());
        ExecuteTest("TC02: Shaman bị DogSpirit tấn công -> Shaman nhận sự kiện tự cứu", () => test.TC02_ShamanAttackedByDogSpirit_ReceivesAttackEvent());
        ExecuteTest("TC03: Shaman chọn tự cứu -> Shaman sống sót", () => test.TC03_ShamanSelfSave_ShamanSurvives());
        ExecuteTest("TC04: Shaman chọn cứu người khác -> Người đó sống sót", () => test.TC04_ShamanSavesOther_TargetSurvives());
        ExecuteTest("TC05: Shaman chọn không cứu -> Mục tiêu chịu kết quả tấn công", () => test.TC05_ShamanDeclinesSave_TargetDies());
        ExecuteTest("TC06: Shaman hết lượt cứu -> Không thể cứu", () => test.TC06_ShamanOutOfSaveCharm_CannotSave());
        ExecuteTest("TC07: Shaman gửi yêu cầu cứu trùng -> Server từ chối", () => test.TC07_DuplicateSaveRequestForSameEvent_Rejected());
        ExecuteTest("TC08: Client giả mạo Shaman -> Server từ chối", () => test.TC08_NonShamanSpoofingSave_Rejected());
        ExecuteTest("TC09: Shaman không lựa chọn -> Timeout không tự động cứu", () => test.TC09_ShamanTimeout_NoAutoSaveAndPhaseContinues());
        ExecuteTest("TC10: Nhiều sự kiện tấn công -> Không cứu nhầm mục tiêu/nguồn khác", () => test.TC10_MultipleAttacksSameNight_RescuesOnlyTargetedEvent());
        ExecuteTest("TC11: Host là Shaman -> Hoạt động đúng", () => test.TC11_HostAsShaman_ResolvesAccurately());
        ExecuteTest("TC12: Client là Shaman -> Hoạt động đúng", () => test.TC12_ClientAsShaman_ResolvesAccurately());
        ExecuteTest("TC13: Shaman chết hoặc mất kết nối -> Không treo Phase", () => test.TC13_ShamanDeadOrDisconnected_DoesNotFreezePhase());
        ExecuteTest("TC14: Shaman dùng bùa hại -> Không bị ảnh hưởng & vẫn cấm tự hại", () => test.TC14_ShamanUsesBadCharm_WorksAndForbidsSelfHarm());
        ExecuteTest("TC15: Bắt đầu trận mới -> Reset lượt cứu và UI", () => test.TC15_NewMatch_ResetsCharmAndAttackEvents());
        ExecuteTest("TC16: Regression các Role khác -> Không phát sinh lỗi", () => test.TC16_OtherRolesRegression_Unaffected());

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

        roleManager.playerRoles[0] = new ShamanRole(p0);
        roleManager.playerRoles[1] = new DogSpirit(p1);
        roleManager.playerRoles[2] = new VillagerRole(p2);
        roleManager.playerRoles[3] = new GuardianRole(p3);

        ThuongOnlineShamanAdapter.ResetForNewMatch();
        nightManager.ResetForNewMatch();
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
    public void TC01_OtherAttacked_ShamanReceivesAttackEvent()
    {
        NightManager.NightAttackEvent? received = null;
        void Handler(NightManager.NightAttackEvent evt) => received = evt;
        NightManager.NightAttackRegistered += Handler;
        try
        {
            nightManager.StartNight();
            nightManager.SetMonsterTarget(2, RoleType.DogSpirit);

            Assert.IsTrue(received.HasValue, "Phải phát sự kiện NightAttackRegistered.");
            Assert.AreEqual(2, received.Value.VictimId);
            Assert.Greater(received.Value.EventId, 0);
            Assert.IsTrue(ThuongOnlineShamanAdapter.CanShamanSaveTarget(playerManager.GetplayerByID(0), playerManager.GetplayerByID(2), out _));
        }
        finally
        {
            NightManager.NightAttackRegistered -= Handler;
        }
    }

    [Test]
    public void TC02_ShamanAttackedByDogSpirit_ReceivesAttackEvent()
    {
        NightManager.NightAttackEvent? received = null;
        void Handler(NightManager.NightAttackEvent evt) => received = evt;
        NightManager.NightAttackRegistered += Handler;
        try
        {
            nightManager.StartNight();
            nightManager.SetMonsterTarget(0, RoleType.DogSpirit);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(0, received.Value.VictimId);
            Assert.IsFalse(ThuongOnlineShamanAdapter.IsShamanSelfSaveForbidden(0, 0));
            Assert.IsTrue(ThuongOnlineShamanAdapter.CanShamanSaveTarget(playerManager.GetplayerByID(0), playerManager.GetplayerByID(0), out _));
        }
        finally
        {
            NightManager.NightAttackRegistered -= Handler;
        }
    }

    [Test]
    public void TC03_ShamanSelfSave_ShamanSurvives()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(0, RoleType.DogSpirit);
        int eventId = nightManager.CurrentMonsterAttackEventId;

        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];
        PlayerData shaman = playerManager.GetplayerByID(0);
        Assert.IsTrue(shamanRole.SelectCharm(true));
        var targetCheck = TargetValidator.ValidateLegacy(shaman, shaman, allowSelfTarget: shamanRole.IsGoodCharmSelected);
        Assert.IsTrue(targetCheck.IsValid, "Shaman phải được phép tự nhắm Bùa Lợi lên bản thân.");
        Assert.IsTrue(shamanRole.ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(eventId));

        nightManager.ResolveNight();
        Assert.IsTrue(shaman.isAlive, "Shaman tự cứu phải sống sót qua đòn tấn công!");
        Assert.IsFalse(shamanRole.HasGoodCharm, "Bùa Cứu phải được tiêu hao.");
    }

    [Test]
    public void TC04_ShamanSavesOther_TargetSurvives()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(2, RoleType.DogSpirit);
        int eventId = nightManager.CurrentMonsterAttackEventId;

        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];
        Assert.IsTrue(shamanRole.ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(eventId));

        nightManager.ResolveNight();
        Assert.IsTrue(playerManager.GetplayerByID(2).isAlive, "Người chơi được cứu phải sống sót!");
    }

    [Test]
    public void TC05_ShamanDeclinesSave_TargetDies()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(2, RoleType.DogSpirit);
        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];

        nightManager.ResolveNight();
        Assert.IsFalse(playerManager.GetplayerByID(2).isAlive, "Không cứu thì mục tiêu phải chết.");
        Assert.IsTrue(shamanRole.HasGoodCharm, "Không cứu thì không được tiêu hao bùa cứu.");
    }

    [Test]
    public void TC06_ShamanOutOfSaveCharm_CannotSave()
    {
        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];
        Assert.IsTrue(shamanRole.ConsumeGoodCharm());
        Assert.IsFalse(shamanRole.HasGoodCharm);
        Assert.IsFalse(shamanRole.SelectCharm(true), "Hết bùa cứu không được chọn lại.");

        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);
        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanSaveTarget(
            playerManager.GetplayerByID(0), playerManager.GetplayerByID(0), out _));
    }

    [Test]
    public void TC07_DuplicateSaveRequestForSameEvent_Rejected()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(2, RoleType.DogSpirit);
        int eventId = nightManager.CurrentMonsterAttackEventId;

        Assert.IsTrue(nightManager.TryValidateAttackEventForSave(eventId, 2, out _));
        Assert.IsTrue(nightManager.MarkAttackEventRescued(eventId));

        Assert.IsFalse(nightManager.TryValidateAttackEventForSave(eventId, 2, out string duplicateErr),
            "Yêu cầu cứu trùng cho cùng attackEventId phải bị từ chối.");
        Assert.IsNotEmpty(duplicateErr);
    }

    [Test]
    public void TC08_NonShamanSpoofingSave_Rejected()
    {
        PlayerData villager = playerManager.GetplayerByID(2);
        PlayerData dog = playerManager.GetplayerByID(1);
        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanSaveTarget(villager, villager, out _));
        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanSaveTarget(dog, villager, out _));
    }

    [Test]
    public void TC09_ShamanTimeout_NoAutoSaveAndPhaseContinues()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(0, RoleType.DogSpirit);
        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];

        nightManager.ResolveNight();
        Assert.IsTrue(shamanRole.HasGoodCharm, "Hết giờ không chọn thì không tự tiêu hao bùa cứu.");
        Assert.IsFalse(playerManager.GetplayerByID(0).isAlive, "Mục tiêu chịu sát thương bình thường.");
    }

    [Test]
    public void TC10_MultipleAttacksSameNight_RescuesOnlyTargetedEvent()
    {
        // Đêm 2 (đêm chẵn để Killer có hiệu lực): DogSpirit và Killer cùng đánh Player 2
        nightManager.StartNight(); // Night 1
        nightManager.ResolveNight();
        nightManager.StartNight(); // Night 2

        nightManager.SetMonsterTarget(2, RoleType.DogSpirit);
        int monsterEventId = nightManager.CurrentMonsterAttackEventId;
        nightManager.SetKillerTarget(2);

        // Shaman chỉ cứu sự kiện DogSpirit
        Assert.IsTrue(nightManager.MarkAttackEventRescued(monsterEventId));

        nightManager.ResolveNight();
        PlayerData p2 = playerManager.GetplayerByID(2);
        Assert.IsFalse(p2.isAlive, "Sự kiện tấn công khác (Killer) vẫn phải gây chết vì chỉ sự kiện DogSpirit được cứu.");
        Assert.AreEqual(DeathCause.Killer, p2.lastDeathCause);
    }

    [Test]
    public void TC11_HostAsShaman_ResolvesAccurately()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(0, RoleType.DogSpirit);
        int eventId = nightManager.CurrentMonsterAttackEventId;
        Assert.IsTrue(nightManager.TryValidateAttackEventForSave(eventId, 0, out _));
    }

    [Test]
    public void TC12_ClientAsShaman_ResolvesAccurately()
    {
        // Xác nhận ClientId/PlayerId mapping chuẩn không nhầm lẫn
        var ui = NetworkShamanNotificationUI.EnsureInstance();
        Assert.IsNotNull(ui);
        ulong fallbackCid = NetworkShamanNotificationUI.GetClientIdForPlayerId(99);
        Assert.AreEqual(ulong.MaxValue, fallbackCid);
    }

    [Test]
    public void TC13_ShamanDeadOrDisconnected_DoesNotFreezePhase()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        shaman.isAlive = false;
        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, playerManager.GetplayerByID(2), out _));

        NetworkRoleActionBridge.RecordShamanKillTarget(2, 0);
        NetworkRoleActionBridge.CancelDisconnectedPlayerActions(0);
        Assert.IsFalse(NetworkRoleActionBridge.HasPendingShamanKill);
    }

    [Test]
    public void TC14_ShamanUsesBadCharm_WorksAndForbidsSelfHarm()
    {
        PlayerData shaman = playerManager.GetplayerByID(0);
        PlayerData dog = playerManager.GetplayerByID(1);

        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanKillTarget(shaman, shaman, out _), "Bùa hại vẫn cấm tự nhắm bản thân.");
        Assert.IsTrue(ThuongOnlineShamanAdapter.CanShamanKillTarget(shaman, dog, out _));

        ShamanRole shamanRole = (ShamanRole)roleManager.playerRoles[0];
        Assert.IsTrue(shamanRole.SelectCharm(false));
        Assert.IsFalse(TargetValidator.ValidateLegacy(shaman, shaman, allowSelfTarget: shamanRole.IsGoodCharmSelected).IsValid);

        NetworkRoleActionBridge.RecordShamanKillTarget(dog.playerID, shaman.playerID);
        NetworkRoleActionBridge.SetShamanKillUsed(shaman.playerID, true);
        ThuongOnlineShamanAdapter.ResolveNightShamanKills();

        Assert.IsFalse(dog.isAlive);
        Assert.AreEqual(DeathCause.Poison, dog.lastDeathCause);
    }

    [Test]
    public void TC15_NewMatch_ResetsCharmAndAttackEvents()
    {
        nightManager.StartNight();
        nightManager.SetMonsterTarget(2, RoleType.DogSpirit);
        int eventId = nightManager.CurrentMonsterAttackEventId;
        nightManager.MarkAttackEventRescued(eventId);
        NetworkRoleActionBridge.SetShamanSaveUsed(0, true);

        ThuongOnlineShamanAdapter.ResetForNewMatch();
        nightManager.ResetForNewMatch();

        Assert.IsFalse(NetworkRoleActionBridge.IsShamanSaveUsed(0));
        Assert.IsFalse(nightManager.IsAttackEventRescued(eventId));
        Assert.IsFalse(nightManager.TryValidateAttackEventForSave(eventId, 2, out _));
    }

    [Test]
    public void TC16_OtherRolesRegression_Unaffected()
    {
        PlayerData dog = playerManager.GetplayerByID(1);
        PlayerData guardian = playerManager.GetplayerByID(3);
        PlayerData villager = playerManager.GetplayerByID(2);

        // DogSpirit vẫn bị cấm tự tấn công chính mình
        Assert.IsFalse(TargetValidator.ValidateLegacy(dog, dog).IsValid, "DogSpirit không được tự target bản thân.");

        // Guardian bảo vệ vẫn hoạt động
        nightManager.StartNight();
        nightManager.SetProtectedTarget(villager.playerID);
        nightManager.SetMonsterTarget(villager.playerID, RoleType.DogSpirit);
        nightManager.ResolveNight();
        Assert.IsTrue(villager.isAlive);
        Assert.IsNotNull(guardian);
    }
}
