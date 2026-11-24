using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử NUnit cho hệ thống Monster Vote – DogSpirit phân định hòa phiếu (TC01–TC14):
/// xác minh không hòa phiếu, hòa 2–3 người dùng phiếu DogSpirit, loại bỏ phiếu Monster khác khi hòa,
/// từ chối mục tiêu ngoài nhóm hòa, đổi lựa chọn trước xác nhận, chống nhiều request,
/// đồng bộ Host/Client, bảo vệ/cứu người, reset qua đêm, và fallback khi DogSpirit mất kết nối/chết/vắng mặt.
/// </summary>
public class MonsterPackNightAttackTests
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

    [MenuItem("Tools/Tests/Run Monster Pack Night Attack Tests")]
    [MenuItem("Tools/Tests/Run Monster Vote Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ MONSTER VOTE – DOGSPIRIT PHÂN ĐỊNH HÒA PHIẾU");
        Debug.Log("=================================================================");

        var tests = new MonsterPackNightAttackTests();
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

        Execute("TC01: Không hòa phiếu -> chọn đúng người nhiều phiếu nhất", () => tests.TC01_NoTie_SelectsHighestVotedTarget());
        Execute("TC02: Hai người hòa phiếu -> dùng phiếu DogSpirit phân định", () => tests.TC02_TwoWayTie_UsesDogSpiritVote());
        Execute("TC03: Ba người hòa phiếu -> DogSpirit chọn một người trong nhóm hòa", () => tests.TC03_ThreeWayTie_DogSpiritChoosesInsideTiedGroup());
        Execute("TC04: Phiếu Monster khác bị bỏ qua khi phân định hòa", () => tests.TC04_OtherMonsterVotesIgnoredDuringTieBreak());
        Execute("TC05: DogSpirit chọn người ngoài nhóm hòa -> Server từ chối", () => tests.TC05_DogSpiritPicksOutsideTiedGroup_Rejected());
        Execute("TC06: DogSpirit đổi lựa chọn trước khi xác nhận -> kết quả cập nhật đúng", () => tests.TC06_DogSpiritChangesVoteBeforeLock_UpdatesResult());
        Execute("TC07: Nhiều request cùng lúc -> chỉ chốt một mục tiêu", () => tests.TC07_MultipleRequests_OnlySingleTargetLocked());
        Execute("TC08: Host và Client đồng bộ chính xác", () => tests.TC08_HostAndClient_VoteTalliesSyncCorrectly());
        Execute("TC09: Mục tiêu được bảo vệ hoặc cứu -> xử lý đúng luật", () => tests.TC09_ProtectedOrRescuedTarget_SurvivesAttack());
        Execute("TC10: Sang đêm mới -> reset toàn bộ phiếu và trạng thái cũ", () => tests.TC10_NextNightAndNewMatch_ResetAllState());
        Execute("TC11: DogSpirit mất kết nối khi hòa -> mặc định chốt phiếu cao nhất trong nhóm hòa", () => tests.TC11_DisconnectedDogSpirit_DefaultsDeterministicallyToHighestTied());
        Execute("TC12: DogSpirit đã chết hoặc không tồn tại khi hòa -> chốt tất định trong nhóm hòa", () => tests.TC12_DeadOrMissingDogSpirit_DefaultsDeterministicallyWithoutSkip());
        Execute("TC13: Bảo toàn luật Xà Tinh 2 đêm đầu và Bạch Khuyển thức tỉnh", () => tests.TC13_SerpentRestrictionAndWhiteHoundAwakeningPreserved());
        Execute("TC14: Tiên Tri vẫn soi bình thường trong lúc DogSpirit đang phân định hòa", () => tests.TC14_SeerRemainsUsableDuringDogSpiritDecision());

        Debug.Log("=================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ MONSTER VOTE: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================================");
    }

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestMonsterVoteEnv]");
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

    private ThuongRoleDefinition CreateKillDefinition(RoleType roleType, bool sharedNightKill)
    {
        var def = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        def.roleID = $"test-{roleType}";
        def.roleType = roleType;
        def.faction = FactionType.Monster;
        def.abilityType = AbilityType.Kill;
        def.sharedNightKill = sharedNightKill;
        def.usablePhase = GamePhase.Night;
        def.targetFactionRule = TargetFactionRule.OtherFaction;
        def.maxUsesPerPhase = 1;
        createdDefs.Add(def);
        return def;
    }

    [Test]
    public void TC01_NoTie_SelectsHighestVotedTarget()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(4, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, v1.playerID, 3, out _));

        Assert.AreEqual(2, nightManager.GetMonsterVoteCountForTarget(v1.playerID));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v2.playerID));
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded(),
            "Không hòa phiếu -> không cần bước DogSpirit quyết định thêm.");
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(v2.isAlive);
        Assert.AreEqual(1, playerManager.players.Count(p => !p.isAlive));
    }

    [Test]
    public void TC02_TwoWayTie_UsesDogSpiritVote()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var p1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var p2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);
        var p3 = AddPlayer(6, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        // P1 = 2 phiếu (Ogre, Serpent), P2 = 2 phiếu (DogSpirit, WhiteHound), P3 = 0 phiếu
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(wh, p2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, p2.playerID, 3, out _));

        // DogSpirit đã bỏ phiếu cho p2 nằm trong nhóm hòa [p1, p2] -> sử dụng ngay lựa chọn của DogSpirit
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsFalse(nightManager.IsDogSpiritDecisionActive);
        Assert.AreEqual(p2.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsTrue(p1.isAlive);
        Assert.IsFalse(p2.isAlive);
        Assert.IsTrue(p3.isAlive);
    }

    [Test]
    public void TC03_ThreeWayTie_DogSpiritChoosesInsideTiedGroup()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var p1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var p2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);
        var p3 = AddPlayer(6, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        // 3 người hòa 1 phiếu: p1 (Ogre), p2 (Serpent), p3 (WhiteHound), còn DogSpirit chưa vote
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, p2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(wh, p3.playerID, 3, out _));

        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsTrue(nightManager.IsDogSpiritDecisionActive);
        CollectionAssert.AreEquivalent(new[] { p1.playerID, p2.playerID, p3.playerID },
            nightManager.DogSpiritTiedCandidateIds.ToArray());

        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, p2.playerID, 3, out _));
        Assert.IsFalse(nightManager.IsDogSpiritDecisionActive);
        Assert.AreEqual(p2.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsTrue(p1.isAlive);
        Assert.IsFalse(p2.isAlive);
        Assert.IsTrue(p3.isAlive);
    }

    [Test]
    public void TC04_OtherMonsterVotesIgnoredDuringTieBreak()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var p1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var p2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);
        var p3 = AddPlayer(6, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        // Ogre vote p1, Serpent vote p2 -> hòa [p1, p2]; DogSpirit chưa vote
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, p2.playerID, 3, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());

        // Trong bước phân định hòa, Ogre / Serpent / WhiteHound đều bị từ chối mọi phiếu/quyết định
        Assert.IsFalse(nightManager.TryCastMonsterVote(ogre, p1.playerID, 3, out _));
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(ogre, p1.playerID, 3, out _));
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(serpent, p2.playerID, 3, out _));
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(wh, p3.playerID, 3, out _));

        // Chỉ DogSpirit chốt trong [p1, p2]
        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, p1.playerID, 3, out _));
        nightManager.ResolveNight();
        Assert.IsFalse(p1.isAlive);
        Assert.IsTrue(p2.isAlive);
        Assert.IsTrue(p3.isAlive);
    }

    [Test]
    public void TC05_DogSpiritPicksOutsideTiedGroup_Rejected()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var p1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var p2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);
        var p3 = AddPlayer(6, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        // Hòa p1 (2 phiếu) & p2 (2 phiếu) khi có 5 Monster? Ở đây: Ogre->p1, Serpent->p2, DogSpirit->p3
        // p1 = 1, p2 = 1, p3 = 1 -> nếu hòa cả 3 thì p3 nằm trong nhóm; nên cho thêm Ogre2/WhiteHound:
        var ogre2 = AddPlayer(7, RoleType.Ogre, FactionType.Monster);
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre2, p1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, p2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(wh, p2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, p3.playerID, 3, out _));

        // Nhóm hòa là [p1, p2] (2 phiếu), còn phiếu của DogSpirit đang là p3 (1 phiếu, ngoài nhóm hòa)
        // -> bắt buộc yêu cầu DogSpirit chọn lại trong nhóm [p1, p2]
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsTrue(nightManager.IsDogSpiritDecisionActive);
        CollectionAssert.AreEquivalent(new[] { p1.playerID, p2.playerID },
            nightManager.DogSpiritTiedCandidateIds.ToArray());

        // Chọn lại đúng p3 (ngoài nhóm hòa) -> Server từ chối
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(dog, p3.playerID, 3, out _));
        Assert.IsTrue(nightManager.IsDogSpiritDecisionActive);

        // Chọn p1 trong nhóm hòa -> thành công
        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, p1.playerID, 3, out _));
        nightManager.ResolveNight();
        Assert.IsFalse(p1.isAlive);
        Assert.IsTrue(p2.isAlive);
        Assert.IsTrue(p3.isAlive);
    }

    [Test]
    public void TC06_DogSpiritChangesVoteBeforeLock_UpdatesResult()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        // DogSpirit vote v1 trước -> rồi đổi sang v2 trước khi kết thúc bỏ phiếu
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 1, out _));
        Assert.AreEqual(2, nightManager.GetMonsterVoteCountForTarget(v1.playerID));

        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v2.playerID, 1, out _));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v1.playerID));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v2.playerID));

        // Đang hòa [v1, v2], phiếu DogSpirit là v2 -> chốt đúng lựa chọn mới nhất của DogSpirit (v2)
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.AreEqual(v2.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsTrue(v1.isAlive);
        Assert.IsFalse(v2.isAlive);
    }

    [Test]
    public void TC07_MultipleRequests_OnlySingleTargetLocked()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        // Spam nhiều request cùng mục tiêu từ 1 Monster -> tổng chỉ tính 1 phiếu
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.IsFalse(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.AreEqual(1, nightManager.MonsterVoteTallies.Values.Sum());

        // Khi DogSpirit chốt hòa, request thứ 2 liên tiếp phải bị từ chối và chỉ có 1 mục tiêu bị khóa
        var dog2 = AddPlayer(4, RoleType.DogSpirit, FactionType.Monster);
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog2, v2.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 1, out _));
        // Đang v1=2, v2=1 -> sửa lại để 2 DogSpirit chia phiếu 1-1 buộc vào bước xác nhận
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v2.playerID, 1, out _));
        // v2=2, v1=1 (DogSpirit1->v1, DogSpirit2->v2, Ogre->v2 -> v2 thắng 2-1); đổi Ogre đi thì bỏ qua, dùng setup sạch:
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 2, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog2, v2.playerID, 2, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());

        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, v1.playerID, 2, out _));
        Assert.IsFalse(nightManager.TrySubmitDogSpiritDecision(dog2, v2.playerID, 2, out _),
            "Request thứ 2 phải bị từ chối sau khi mục tiêu đầu đã chốt.");
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(v2.isAlive);
        Assert.AreEqual(1, playerManager.players.Count(p => !p.isAlive));
    }

    [Test]
    public void TC08_HostAndClient_VoteTalliesSyncCorrectly()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        dog.roleDefinition = CreateKillDefinition(RoleType.DogSpirit, sharedNightKill: true);
        int notifications = 0;
        nightManager.MonsterVoteStateChanged += () => notifications++;

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, v1.playerID, out _));
        Assert.IsTrue(roleManager.UseNightAbility(ogre.playerID, v2.playerID, out _));

        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v1.playerID));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v2.playerID));
        Assert.AreEqual(v1.playerID, nightManager.GetMonsterVotedTarget(dog.playerID));
        Assert.AreEqual(v2.playerID, nightManager.GetMonsterVotedTarget(ogre.playerID));
        Assert.GreaterOrEqual(notifications, 2);

        // Chốt qua DogSpirit vote -> sự kiện sync cập nhật khóa cuối cùng
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsTrue(nightManager.IsMonsterAttackLocked);
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);
    }

    [Test]
    public void TC09_ProtectedOrRescuedTarget_SurvivesAttack()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var guardian = AddPlayer(2, RoleType.VillageGuardian, FactionType.Villager);
        var shaman = AddPlayer(3, RoleType.Shaman, FactionType.Villager);
        var v1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);

        // 1. Mục tiêu hòa phiếu được DogSpirit chọn nhưng được VillageGuardian bảo vệ -> sống sót
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v2.playerID, 1, out _));
        Assert.IsTrue(roleManager.UseNightAbility(guardian.playerID, v1.playerID, out _));
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);
        nightManager.ResolveNight();
        Assert.IsTrue(v1.isAlive, "Mục tiêu được Bảo Vệ phải sống sót.");

        // 2. Mục tiêu không hòa phiếu bị chọn nhưng được Shaman cứu bằng Bùa Lợi -> sống sót
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v2.playerID, 2, out _));
        int evtId = nightManager.CurrentMonsterAttackEventId;
        Assert.Greater(evtId, 0);
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(evtId));
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        nightManager.ResolveNight();
        Assert.IsTrue(v2.isAlive, "Mục tiêu được Pháp Sư cứu phải sống sót.");
    }

    [Test]
    public void TC10_NextNightAndNewMatch_ResetAllState()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var v1 = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(2, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v1.playerID, 1, out _));
        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(nightManager.IsMonsterAttackLocked);

        // Sang đêm mới -> reset toàn bộ phiếu, trạng thái hòa và cờ khóa
        nightManager.StartNight();
        Assert.IsFalse(nightManager.IsMonsterAttackLocked);
        Assert.IsFalse(nightManager.IsMonsterVoteClosed);
        Assert.IsFalse(nightManager.IsDogSpiritDecisionActive);
        Assert.AreEqual(0, nightManager.DogSpiritTiedCandidateIds.Count);
        Assert.AreEqual(-1, nightManager.ActiveMonsterTargetId);
        Assert.AreEqual(-1, nightManager.GetMonsterVotedTarget(dog.playerID));
        Assert.AreEqual(0, nightManager.MonsterVoteTallies.Count);

        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, v2.playerID, 2, out _));
        Assert.AreEqual(1, nightManager.GetMonsterVoteCountForTarget(v2.playerID));

        nightManager.ResetForNewMatch();
        Assert.AreEqual(0, nightManager.MonsterVoteTallies.Count);
        Assert.AreEqual(-1, nightManager.ActiveMonsterTargetId);
        Assert.IsFalse(nightManager.IsMonsterAttackLocked);
    }

    [Test]
    public void TC11_DisconnectedDogSpirit_DefaultsDeterministicallyToHighestTied()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(2, RoleType.SerpentSpirit, FactionType.Monster);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(4, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, v2.playerID, 3, out _));

        // DogSpirit mất kết nối -> mặc định chọn mục tiêu cao phiếu nhất tất định (không bỏ lượt, không random)
        dog.isDisconnected = true;
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsFalse(nightManager.IsDogSpiritDecisionActive);
        Assert.AreEqual(v1.playerID, nightManager.ActiveMonsterTargetId);

        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(v2.isAlive);
    }

    [Test]
    public void TC12_DeadOrMissingDogSpirit_DefaultsDeterministicallyWithoutSkip()
    {
        var ogre = AddPlayer(0, RoleType.Ogre, FactionType.Monster);
        var serpent = AddPlayer(1, RoleType.SerpentSpirit, FactionType.Monster);
        var v1 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        nightManager.StartNight();
        nightManager.StartNight();
        gameRoleManager.currentDay = 3;

        // Trận không có DogSpirit, Ogre & SerpentSpirit hòa phiếu -> vẫn chốt 1 mục tiêu trong nhóm hòa
        Assert.IsTrue(nightManager.TryCastMonsterVote(serpent, v2.playerID, 3, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, v1.playerID, 3, out _));
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.AreEqual(v2.playerID, nightManager.ActiveMonsterTargetId,
            "Không bỏ lượt khi hòa; mặc định chốt ứng viên đạt mốc cao nhất trước.");

        nightManager.ResolveNight();
        Assert.IsFalse(v2.isAlive);
        Assert.IsTrue(v1.isAlive);
    }

    [Test]
    public void TC13_SerpentRestrictionAndWhiteHoundAwakeningPreserved()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var serpent = AddPlayer(1, RoleType.SerpentSpirit, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Villager, awakenedWhiteHound: false);
        var v1 = AddPlayer(3, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        gameRoleManager.currentDay = 1;
        Assert.IsFalse(nightManager.TryCastMonsterVote(serpent, v1.playerID, 1, out _),
            "Đêm 1 Xà Tinh không được tấn công.");

        nightManager.StartNight();
        gameRoleManager.currentDay = 2;
        Assert.IsFalse(nightManager.TryCastMonsterVote(serpent, v1.playerID, 2, out _),
            "Đêm 2 Xà Tinh không được tấn công.");

        // DogSpirit vote WH -> thức tỉnh WhiteHound ở đêm 2
        Assert.IsTrue(nightManager.TryCastMonsterVote(dog, wh.playerID, 2, out _));
        Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        nightManager.ResolveNight();
        Assert.IsTrue(wh.isAlive);
        Assert.IsTrue(wh.isWhiteHoundAwakened);
        Assert.AreEqual(FactionType.Monster, wh.faction);
    }

    [Test]
    public void TC14_SeerRemainsUsableDuringDogSpiritDecision()
    {
        var dog = AddPlayer(0, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(1, RoleType.Ogre, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var seer = AddPlayer(3, RoleType.Seer, FactionType.Villager);
        var v1 = AddPlayer(4, RoleType.Villager, FactionType.Villager);
        var v2 = AddPlayer(5, RoleType.Villager, FactionType.Villager);

        nightManager.StartNight();
        // Ogre vote v1, WhiteHound vote v2, DogSpirit chưa vote -> mở bước DogSpirit phân định hòa
        Assert.IsTrue(roleManager.UseNightAbility(ogre.playerID, v1.playerID, out _));
        Assert.IsTrue(roleManager.UseNightAbility(wh.playerID, v2.playerID, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());

        Assert.IsTrue(roleManager.UseNightAbility(seer.playerID, dog.playerID, out string seerMsg),
            $"Tiên Tri phải soi được khi DogSpirit đang phân định hòa: {seerMsg}");
        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, v1.playerID, 1, out _));
        nightManager.ResolveNight();
        Assert.IsFalse(v1.isAlive);
        Assert.IsTrue(seer.isAlive);
    }
}
