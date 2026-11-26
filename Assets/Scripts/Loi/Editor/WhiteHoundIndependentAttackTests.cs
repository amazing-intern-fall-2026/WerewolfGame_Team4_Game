using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử NUnit cho cơ chế tấn công độc lập của Bạch Khuyển (TC01–TC10):
/// xác minh Bạch Khuyển có thể tấn công cả Dân lẫn Monster, tách biệt hoàn toàn khỏi Monster Vote,
/// không bị ảnh hưởng bởi đóng vote / phân định hòa của DogSpirit, tạo sự kiện tấn công riêng biệt,
/// tương thích Bảo Vệ / Pháp Sư cứu, đồng bộ state độc lập, và bảo toàn kênh chat / thức tỉnh.
/// </summary>
public class WhiteHoundIndependentAttackTests
{
    private GameObject rootGo;
    private PlayerManager playerManager;
    private RoleManager roleManager;
    private GameRoleManager gameRoleManager;
    private NightManager nightManager;
    private AbilitySystem abilitySystem;
    private StatusEffectSystem statusEffectSystem;
    private DeathResolver deathResolver;
    private readonly List<ThuongRoleDefinition> createdDefs = new();

    [MenuItem("Tools/Tests/Run WhiteHound Independent Attack Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ BẠCH KHUYỂN – TẤN CÔNG ĐỘC LẬP");
        Debug.Log("=================================================================");

        var tests = new WhiteHoundIndependentAttackTests();
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
                Debug.LogError($"[FAIL] {name}: {ex.Message}\n{ex.StackTrace}");
                failed++;
            }
            finally
            {
                tests.TearDown();
            }
        }

        Execute("TC01: Bạch Khuyển tấn công mục tiêu phe Dân -> thành công", () => tests.TC01_WhiteHoundAttacksVillager_Succeeds());
        Execute("TC02: Bạch Khuyển tấn công mục tiêu phe Monster -> thành công", () => tests.TC02_WhiteHoundAttacksMonster_Succeeds());
        Execute("TC03: Bạch Khuyển không thuộc danh sách vote phe Monster", () => tests.TC03_WhiteHoundExcludedFromMonsterVoteAndMembership());
        Execute("TC04: Bạch Khuyển tấn công trước/trong khi đóng Monster Vote & DogSpirit hòa phiếu", () => tests.TC04_WhiteHoundActsIndependentlyOfMonsterVoteTiming());
        Execute("TC05: DogSpirit không thể quyết định hộ hoặc ghi đè đòn tấn công Bạch Khuyển", () => tests.TC05_DogSpiritCannotOverrideOrActForWhiteHound());
        Execute("TC06: Phe Monster vẫn vote bình thường (kể cả vote vào Bạch Khuyển) khi có Bạch Khuyển", () => tests.TC06_MonsterVoteWorksNormallyAlongsideWhiteHound());
        Execute("TC07: Monster Vote & Bạch Khuyển cùng tấn công trong một đêm -> hai sự kiện độc lập", () => tests.TC07_SimultaneousMonsterVoteAndWhiteHoundAttacks_BothResolveCleanly());
        Execute("TC08: Mục tiêu Bạch Khuyển được Bảo Vệ hoặc Pháp Sư cứu -> sống sót đúng luật", () => tests.TC08_WhiteHoundTargetProtectedOrRescued_Survives());
        Execute("TC09: Trạng thái đồng bộ Host/Client của Bạch Khuyển tách biệt hoàn toàn với Monster Vote", () => tests.TC09_WhiteHoundSyncStateIsolatedFromMonsterVote());
        Execute("TC10: Bảo toàn quy tắc thức tỉnh, vai trò WhiteHound và loại trừ khỏi kênh Monster", () => tests.TC10_PreservesAwakeningRoleAndMonsterChannelExclusion());

        Debug.Log("=================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ BẠCH KHUYỂN ĐỘC LẬP: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================================");
    }

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestWhiteHoundIndependentEnv]");
        playerManager = rootGo.AddComponent<PlayerManager>();
        PlayerManager.Instance = playerManager;

        roleManager = rootGo.AddComponent<RoleManager>();
        RoleManager.Instance = roleManager;

        gameRoleManager = rootGo.AddComponent<GameRoleManager>();
        GameRoleManager.Instance = gameRoleManager;
        gameRoleManager.manualLogicPrototype = true;
        gameRoleManager.currentState = GameState.Night;
        gameRoleManager.currentPhase = GamePhase.Night;
        gameRoleManager.currentDay = 1;

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
        abilitySystem.SetMatchRunning(true);

        playerManager.players = new List<PlayerData>();
        roleManager.playerRoles = new Dictionary<int, BaseRole>();
        nightManager.ResetForNewMatch();
    }

    [TearDown]
    public void TearDown()
    {
        if (abilitySystem != null) abilitySystem.SetMatchRunning(false);
        if (nightManager != null) nightManager.ClearActions();
        foreach (var def in createdDefs)
        {
            if (def != null) UnityEngine.Object.DestroyImmediate(def);
        }
        createdDefs.Clear();

        if (PlayerManager.Instance == playerManager) PlayerManager.Instance = null;
        if (RoleManager.Instance == roleManager) RoleManager.Instance = null;
        if (GameRoleManager.Instance == gameRoleManager) GameRoleManager.Instance = null;
        if (NightManager.Instance == nightManager) NightManager.Instance = null;
        if (AbilitySystem.Instance == abilitySystem) AbilitySystem.Instance = null;
        if (StatusEffectSystem.Instance == statusEffectSystem) StatusEffectSystem.Instance = null;
        if (DeathResolver.Instance == deathResolver) DeathResolver.Instance = null;

        if (rootGo != null) UnityEngine.Object.DestroyImmediate(rootGo);
    }

    private PlayerData AddPlayer(int id, RoleType roleType, FactionType faction, bool awakenedWhiteHound = false)
    {
        var p = new PlayerData
        {
            playerID = id,
            playerName = $"Player {id + 1}",
            roleType = roleType,
            faction = faction,
            isAlive = true,
            isWhiteHoundAwakened = awakenedWhiteHound,
            status = new PlayerStatus(),
            effects = new List<StatusEffectInstance>()
        };
        playerManager.players.Add(p);
        roleManager.playerRoles[id] = RoleCatalog.Create(roleType, p);
        if (awakenedWhiteHound && roleManager.playerRoles[id] is WhiteHound wh)
        {
            wh.BecomeMonster();
        }
        return p;
    }

    private ThuongRoleDefinition CreateWhiteHoundKillDef(bool sharedNightKill = false)
    {
        var def = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        def.roleID = "test-WhiteHound";
        def.roleType = RoleType.WhiteHound;
        def.faction = FactionType.Villager;
        def.abilityType = AbilityType.Kill;
        def.sharedNightKill = sharedNightKill;
        def.usablePhase = GamePhase.Night;
        def.targetFactionRule = TargetFactionRule.Any;
        def.maxUsesPerPhase = 1;
        createdDefs.Add(def);
        return def;
    }

    [Test]
    public void TC01_WhiteHoundAttacksVillager_Succeeds()
    {
        var wh = AddPlayer(0, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var villager = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        wh.roleDefinition = CreateWhiteHoundKillDef();

        nightManager.StartNight();
        Assert.IsTrue(NightManager.IsValidWhiteHoundTarget(wh, villager, out _));
        Assert.IsTrue(roleManager.UseNightAbility(wh.playerID, villager.playerID, out string msg), msg);
        Assert.AreEqual(villager.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));
        Assert.AreEqual(-1, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsFalse(villager.isAlive);
        Assert.IsTrue(wh.isAlive);
    }

    [Test]
    public void TC02_WhiteHoundAttacksMonster_Succeeds()
    {
        var wh = AddPlayer(0, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(2, RoleType.Ogre, FactionType.Monster);
        wh.roleDefinition = CreateWhiteHoundKillDef();

        nightManager.StartNight();
        Assert.IsFalse(NightManager.IsValidWhiteHoundTarget(wh, wh, out _), "Không được tự đánh chính mình.");
        Assert.IsTrue(NightManager.IsValidWhiteHoundTarget(wh, dog, out _));
        Assert.IsTrue(NightManager.IsValidWhiteHoundTarget(wh, ogre, out _));

        // Cho phép tấn công trực tiếp DogSpirit
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, dog.playerID, out string dogMsg), dogMsg);
        Assert.AreEqual(dog.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));

        nightManager.ResolveNight();
        Assert.IsFalse(dog.isAlive, "DogSpirit bị Bạch Khuyển tấn công phải chết.");
        Assert.IsTrue(ogre.isAlive);

        // Sang đêm sau có thể chọn tiếp một Monster khác (Ogre) qua RoleManager
        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(wh.playerID, ogre.playerID, out string ogreMsg), ogreMsg);
        nightManager.ResolveNight();
        Assert.IsFalse(ogre.isAlive, "Ogre bị Bạch Khuyển tấn công phải chết.");
    }

    [Test]
    public void TC03_WhiteHoundExcludedFromMonsterVoteAndMembership()
    {
        var wh = AddPlayer(0, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var villager = AddPlayer(2, RoleType.Villager, FactionType.Villager);

        Assert.IsFalse(NightManager.IsMonsterPackMember(wh));
        Assert.IsFalse(NightManager.IsMonsterPackRoleType(RoleType.WhiteHound, true, FactionType.Monster));
        Assert.IsFalse(NightManager.IsMonsterPackRoleType(RoleType.WhiteWolf, true, FactionType.Monster));
        Assert.IsFalse(NightManager.CanParticipateInMonsterPackAttack(wh, 1, out _));
        Assert.IsTrue(NightManager.IsMonsterPackMember(dog));

        nightManager.StartNight();
        Assert.IsFalse(nightManager.TryCastMonsterVote(wh, villager.playerID, 1, out _));
        Assert.AreEqual(0, nightManager.GetMonsterVoteCountForTarget(villager.playerID));
        Assert.AreEqual(-1, nightManager.GetMonsterVotedTarget(wh.playerID));
    }

    [Test]
    public void TC04_WhiteHoundActsIndependentlyOfMonsterVoteTiming()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var v3 = AddPlayer(5, RoleType.Villager, FactionType.Villager);

        // 1. Bạch Khuyển tấn công trước khi Monster Vote kết thúc
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, ogre.playerID, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.AreEqual(ogre.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);

        // 2. Bạch Khuyển tấn công ngay cả khi Monster Vote đã đóng và DogSpirit đang phân định hòa
        var ogre2 = AddPlayer(6, RoleType.Ogre, FactionType.Monster);
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 2, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre2, v2.playerID, 2, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsTrue(nightManager.IsDogSpiritDecisionActive);
        Assert.IsTrue(nightManager.IsMonsterVoteClosed);

        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, v3.playerID, out string lateMsg),
            $"Bạch Khuyển vẫn phải tấn công được khi Monster Vote đã đóng: {lateMsg}");
        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, v1.playerID, 2, out _));

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(v2.isAlive);
        Assert.IsFalse(v3.isAlive);
    }

    [Test]
    public void TC05_DogSpiritCannotOverrideOrActForWhiteHound()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var ogre2 = AddPlayer(2, RoleType.Ogre, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var v1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);
        var v3 = AddPlayer(6, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, v3.playerID, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre2, v2.playerID, 1, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());

        // Bạch Khuyển không có quyền quyết định thay DogSpirit
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(wh, v1.playerID, 1, out _));
        // DogSpirit chốt v1 không được ghi đè hay xóa mục tiêu v3 của Bạch Khuyển
        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, v1.playerID, 1, out _));
        Assert.AreEqual(v3.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsFalse(v3.isAlive);
    }

    [Test]
    public void TC06_MonsterVoteWorksNormallyAlongsideWhiteHound()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        // Monster có thể vote tấn công chính Bạch Khuyển (vì Bạch Khuyển không thuộc Monster Pack)
        Assert.IsTrue(NightManager.IsValidMonsterPackTarget(dog, wh, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, wh.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 1, out _));

        // Hòa [wh, v1], DogSpirit đang chọn wh nên dùng luôn lựa chọn của DogSpirit
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.AreEqual(wh.playerID, nightManager.ActiveMonsterTargetId);

        // Trong cùng đêm đó Bạch Khuyển vẫn chọn giết Ogre độc lập
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, ogre.playerID, out _));

        nightManager.ResolveNight();
        Assert.IsFalse(wh.isAlive, "Monster Vote vẫn giết được Bạch Khuyển đã thức tỉnh.");
        Assert.IsFalse(ogre.isAlive, "Bạch Khuyển vẫn giết được Ogre trước khi bị Resolve.");
        Assert.IsTrue(v1.isAlive);
    }

    [Test]
    public void TC07_SimultaneousMonsterVoteAndWhiteHoundAttacks_BothResolveCleanly()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var wh = AddPlayer(1, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        // Trường hợp A: Hai mục tiêu khác nhau -> 2 attackEventId riêng biệt, cả 2 đều chết
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, v2.playerID, out _));

        int monsterEvtId = nightManager.CurrentMonsterAttackEventId;
        int whEvtId = nightManager.GetWhiteHoundAttackEventId(wh.playerID);
        Assert.Greater(monsterEvtId, 0);
        Assert.Greater(whEvtId, 0);
        Assert.AreNotEqual(monsterEvtId, whEvtId);

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsFalse(v2.isAlive);

        // Trường hợp B: Trùng cùng một mục tiêu -> không lỗi chết trùng lặp bất thường
        var v3 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v3.playerID, 2, out _));
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, v3.playerID, out _));
        Assert.AreNotEqual(nightManager.CurrentMonsterAttackEventId, nightManager.GetWhiteHoundAttackEventId(wh.playerID));

        int diedEvents = 0;
        Action<DeathResult> onDied = result =>
        {
            if (result.Outcome == DeathOutcome.Killed)
                diedEvents++;
        };
        deathResolver.DeathResolved += onDied;
        try
        {
            nightManager.ResolveNight();
        }
        finally
        {
            deathResolver.DeathResolved -= onDied;
        }

        Assert.IsFalse(v3.isAlive);
        Assert.AreEqual(1, diedEvents, "Cùng bị hai nguồn đánh trong một đêm chỉ được chết một lần.");
    }

    [Test]
    public void TC08_WhiteHoundTargetProtectedOrRescued_Survives()
    {
        var wh = AddPlayer(0, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var guardian = AddPlayer(1, RoleType.VillageGuardian, FactionType.Villager);
        var shaman = AddPlayer(2, RoleType.Shaman, FactionType.Villager);
        var dog = AddPlayer(3, RoleType.DogSpirit, FactionType.Monster);
        var v1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);

        // 1. Bảo Vệ bảo vệ mục tiêu (kể cả Monster) khỏi Bạch Khuyển -> sống sót
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, dog.playerID, out _));
        Assert.IsTrue(roleManager.UseNightAbility(guardian.playerID, dog.playerID, out _));
        nightManager.ResolveNight();
        Assert.IsTrue(dog.isAlive, "Mục tiêu được Bảo Vệ bảo vệ phải sống sót trước đòn Bạch Khuyển.");

        // 2. Pháp Sư cứu đúng AttackEventId của Bạch Khuyển -> sống sót
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, v1.playerID, out _));
        int whEvtId = nightManager.GetWhiteHoundAttackEventId(wh.playerID);
        Assert.Greater(whEvtId, 0);
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(whEvtId));
        nightManager.ResolveNight();
        Assert.IsTrue(v1.isAlive, "Mục tiêu được Pháp Sư cứu phải sống sót trước đòn Bạch Khuyển.");
    }

    [Test]
    public void TC09_WhiteHoundSyncStateIsolatedFromMonsterVote()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        int monsterSyncCount = 0;
        nightManager.MonsterVoteStateChanged += () => monsterSyncCount++;

        nightManager.StartNight();
        // Hành động của Bạch Khuyển không được đẩy vào MonsterVoteTallies hay phát tín hiệu MonsterVoteStateChanged
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, ogre.playerID, out _));
        Assert.AreEqual(0, monsterSyncCount);
        Assert.AreEqual(0, nightManager.MonsterVoteTallies.Count);
        Assert.AreEqual(-1, nightManager.ActiveMonsterTargetId);
        Assert.IsTrue(nightManager.HasPendingWhiteHoundAttack);
        Assert.IsTrue(nightManager.HasWhiteHoundSubmittedAttack(wh.playerID));
        Assert.AreEqual(ogre.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));

        // Phiếu của Monster không xóa/ghi đè state của Bạch Khuyển
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v1.playerID));
        Assert.AreEqual(0, nightManager.GetMonsterVoteCountForTarget(ogre.playerID));
        Assert.AreEqual(ogre.playerID, nightManager.GetWhiteHoundTarget(wh.playerID));

        // Sang đêm mới reset độc lập cả 2 hệ thống
        nightManager.StartNight();
        Assert.IsFalse(nightManager.HasPendingWhiteHoundAttack);
        Assert.IsFalse(nightManager.HasWhiteHoundSubmittedAttack(wh.playerID));
        Assert.AreEqual(-1, nightManager.GetWhiteHoundTarget(wh.playerID));
    }

    [Test]
    public void TC10_PreservesAwakeningRoleAndMonsterChannelExclusion()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var wh = AddPlayer(1, RoleType.WhiteHound, FactionType.Villager, awakenedWhiteHound: false);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        // Chưa thức tỉnh thì Bạch Khuyển không được tấn công
        Assert.IsFalse(NightManager.CanParticipateInWhiteHoundAttack(wh, out _));
        Assert.IsFalse(nightManager.TrySubmitWhiteHoundAttack(wh, v1.playerID, out _));

        // Khi phe Monster tấn công Bạch Khuyển chưa thức tỉnh -> thức tỉnh, sống sót, giữ nguyên RoleType.WhiteHound
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, wh.playerID, 1, out _));
        nightManager.ResolveNight();
        Assert.IsTrue(wh.isAlive);
        Assert.IsTrue(wh.isWhiteHoundAwakened);
        Assert.AreEqual(RoleType.WhiteHound, wh.roleType);
        Assert.AreEqual(FactionType.Monster, wh.faction);

        // Sau khi thức tỉnh: được tự tấn công độc lập nhưng vẫn KHÔNG thuộc kênh Monster Pack / Monster Vote
        Assert.IsFalse(NightManager.IsMonsterPackMember(wh));
        Assert.IsFalse(NightManager.CanParticipateInMonsterPackAttack(wh, 2, out _));
        nightManager.StartNight();
        Assert.IsTrue(NightManager.CanParticipateInWhiteHoundAttack(wh, out _));
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, dog.playerID, out _));
    }
}
