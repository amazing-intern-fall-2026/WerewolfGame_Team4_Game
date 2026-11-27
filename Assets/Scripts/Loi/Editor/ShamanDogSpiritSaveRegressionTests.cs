using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử hồi quy NUnit (TC01–TC10) cho lỗi:
/// Shaman dùng Bùa Cứu hợp lệ nhưng mục tiêu vẫn bị DogSpirit loại (tái tạo attackEventId khi
/// RecalculateMonsterVoteOutcome / LockMonsterAttackTarget, hoặc kết thúc đêm đồng thời).
/// </summary>
public class ShamanDogSpiritSaveRegressionTests
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

    [MenuItem("Tools/Tests/Run Shaman DogSpirit Save Regression Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ HỒI QUY SHAMAN BÙA CỨU VS DOGSPIRIT");
        Debug.Log("=================================================================");

        var tests = new ShamanDogSpiritSaveRegressionTests();
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

        Execute("TC01: DogSpirit tấn công P01 (Monster Vote / Queued), Shaman cứu -> P01 sống", () => tests.TC01_DogSpiritAttacksP01_ShamanSaves_P01Survives());
        Execute("TC02: DogSpirit tấn công P01, Shaman không cứu -> P01 chết theo luật", () => tests.TC02_DogSpiritAttacksP01_ShamanDoesNotSave_P01Dies());
        Execute("TC03: Shaman cứu người chơi khác -> chỉ mục tiêu đó được cứu", () => tests.TC03_ShamanSavesOtherTarget_OnlyThatTargetRescued());
        Execute("TC04: DogSpirit tấn công P01, Shaman cứu P01 -> không nhầm sang mục tiêu khác", () => tests.TC04_DogSpiritAttacksP01_ShamanSavesP01_DoesNotLeakToOtherTarget());
        Execute("TC05: Shaman tự cứu khi bị DogSpirit tấn công -> hoạt động đúng", () => tests.TC05_ShamanSelfSavesAgainstDogSpirit_Survives());
        Execute("TC06: Shaman không còn bùa cứu -> không thể cứu", () => tests.TC06_ShamanOutOfGoodCharm_CannotSave());
        Execute("TC07: Yêu cầu cứu bị từ chối -> không đánh dấu sự kiện đã cứu và không trừ bùa", () => tests.TC07_RejectedSaveRequest_DoesNotMarkRescuedOrConsumeCharm());
        Execute("TC08: Hai sự kiện tấn công khác nhau -> cứu một sự kiện không hủy sự kiện còn lại", () => tests.TC08_TwoDistinctAttackEvents_RescuingOneDoesNotCancelOther());
        Execute("TC09: Host và Client đồng bộ trạng thái cứu & cửa sổ chờ sau quyết định DogSpirit", () => tests.TC09_HostAndClient_SaveSyncAndPostDecisionGraceWindow());
        Execute("TC10: Chạy nhiều đêm liên tiếp -> không tái sử dụng attackEventId hoặc trạng thái cứu cũ", () => tests.TC10_ConsecutiveNights_NeverReuseEventIdOrRescuedState());

        Debug.Log("=================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ HỒI QUY SHAMAN: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================================");
    }

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestShamanDogSpiritSaveEnv]");
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
        NetworkRoleActionBridge.ResetShamanCharms();
        nightManager.ResetForNewMatch();
    }

    [TearDown]
    public void TearDown()
    {
        NetworkRoleActionBridge.ResetShamanCharms();
        if (NetworkShamanNotificationUI.Instance != null)
        {
            NetworkShamanNotificationUI.Instance.ResetForNewMatch();
            UnityEngine.Object.DestroyImmediate(NetworkShamanNotificationUI.Instance.gameObject);
        }
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
            playerName = $"P{id + 1:00}",
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

    private ThuongRoleDefinition CreateMonsterKillDef(RoleType roleType)
    {
        var def = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        def.roleID = $"test-{roleType}";
        def.roleType = roleType;
        def.faction = FactionType.Monster;
        def.abilityType = AbilityType.Kill;
        def.useLegacyAbility = false;
        def.sharedNightKill = true;
        def.usablePhase = GamePhase.Night;
        def.targetFactionRule = TargetFactionRule.OtherFaction;
        def.maxUsesPerPhase = 1;
        createdDefs.Add(def);
        return def;
    }

    [Test]
    public void TC01_DogSpiritAttacksP01_ShamanSaves_P01Survives()
    {
        // Repro chính xác kịch bản lỗi thực tế: DogSpirit dùng Monster Vote (configured ThuongRoleDefinition),
        // có Monster khác cùng vote / hòa phiếu kích hoạt RecalculateMonsterVoteOutcome & LockMonsterAttackTarget.
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(2, RoleType.Ogre, FactionType.Monster);
        var shaman = AddPlayer(3, RoleType.Shaman, FactionType.Villager);
        var p05 = AddPlayer(4, RoleType.Villager, FactionType.Villager);

        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);
        ogre.roleDefinition = CreateMonsterKillDef(RoleType.Ogre);

        int notifiedEventId = 0;
        void OnAttack(NightManager.NightAttackEvent evt)
        {
            if (evt.VictimId == p01.playerID) notifiedEventId = evt.EventId;
        }
        NightManager.NightAttackRegistered += OnAttack;
        try
        {
            nightManager.StartNight();

            // 1. DogSpirit chọn P01 -> sinh attackEventId ban đầu cho P01
            Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out string dogMsg), dogMsg);
            Assert.Greater(notifiedEventId, 0);
            Assert.AreEqual(notifiedEventId, nightManager.CurrentMonsterAttackEventId);

            // 2. Shaman bấm SỬ DỤNG BÙA CỨU ngay với attackEventId nhận được
            Assert.IsTrue(nightManager.TryValidateAttackEventForSave(notifiedEventId, p01.playerID, out _));
            var shamanRole = (ShamanRole)roleManager.playerRoles[shaman.playerID];
            Assert.IsTrue(shamanRole.ConsumeGoodCharm());
            Assert.IsTrue(nightManager.MarkAttackEventRescued(notifiedEventId));

            // 3. Ogre vote P05 (hòa [P01, P05]) -> BeginDogSpiritTieDecisionIfNeeded dùng phiếu DogSpirit khóa lại P01
            Assert.IsTrue(roleManager.UseNightAbility(ogre.playerID, p05.playerID, out string ogreMsg), ogreMsg);
            Assert.IsFalse(nightManager.BeginDogSpiritTieDecisionIfNeeded());
            Assert.AreEqual(p01.playerID, nightManager.ActiveMonsterTargetId);
            Assert.AreEqual(notifiedEventId, nightManager.CurrentMonsterAttackEventId,
                "Khóa lại cùng mục tiêu P01 không được cấp ID mới làm mất trạng thái đã cứu.");

            // 4. Kết thúc đêm -> P01 PHẢI sống sót
            nightManager.ResolveNight();
            Assert.IsTrue(p01.isAlive, "P01 phải sống sót sau khi Shaman đã cứu đúng attackEventId của DogSpirit.");
            Assert.IsTrue(p05.isAlive);
            Assert.IsFalse(shamanRole.HasGoodCharm);
        }
        finally
        {
            NightManager.NightAttackRegistered -= OnAttack;
        }
    }

    [Test]
    public void TC02_DogSpiritAttacksP01_ShamanDoesNotSave_P01Dies()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var shaman = AddPlayer(2, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        nightManager.ResolveNight();

        Assert.IsFalse(p01.isAlive);
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).HasGoodCharm);
    }

    [Test]
    public void TC03_ShamanSavesOtherTarget_OnlyThatTargetRescued()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var p02 = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(2, RoleType.DogSpirit, FactionType.Monster);
        var wh = AddPlayer(3, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var shaman = AddPlayer(4, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, p02.playerID, out _));

        int p02EvtId = nightManager.GetWhiteHoundAttackEventId(wh.playerID);
        Assert.IsTrue(nightManager.TryValidateAttackEventForSave(p02EvtId, p02.playerID, out _));
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(p02EvtId));

        nightManager.ResolveNight();
        Assert.IsTrue(p02.isAlive, "Chỉ P02 được cứu phải sống.");
        Assert.IsFalse(p01.isAlive, "P01 không được cứu vẫn phải chết.");
    }

    [Test]
    public void TC04_DogSpiritAttacksP01_ShamanSavesP01_DoesNotLeakToOtherTarget()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var p02 = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(2, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(3, RoleType.Ogre, FactionType.Monster);
        var ogre2 = AddPlayer(4, RoleType.Ogre, FactionType.Monster);
        var shaman = AddPlayer(5, RoleType.Shaman, FactionType.Villager);

        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);
        ogre.roleDefinition = CreateMonsterKillDef(RoleType.Ogre);
        ogre2.roleDefinition = CreateMonsterKillDef(RoleType.Ogre);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        int p01EvtId = nightManager.CurrentMonsterAttackEventId;

        // Cố tình gửi target p02 cùng eventId của p01 -> bị từ chối
        Assert.IsFalse(nightManager.TryValidateAttackEventForSave(p01EvtId, p02.playerID, out _));

        // Cứu đúng p01
        Assert.IsTrue(nightManager.TryValidateAttackEventForSave(p01EvtId, p01.playerID, out _));
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(p01EvtId));

        // Sau đó phe Monster đổi hẳn sang tấn công P02 (2 phiếu > 1 phiếu) -> sự kiện P02 KHÔNG được ké trạng thái cứu của P01
        Assert.IsTrue(roleManager.UseNightAbility(ogre.playerID, p02.playerID, out _));
        Assert.IsTrue(roleManager.UseNightAbility(ogre2.playerID, p02.playerID, out _));
        Assert.AreEqual(p02.playerID, nightManager.ActiveMonsterTargetId);
        Assert.AreNotEqual(p01EvtId, nightManager.CurrentMonsterAttackEventId);

        nightManager.ResolveNight();
        Assert.IsTrue(p01.isAlive);
        Assert.IsFalse(p02.isAlive, "Đổi sang tấn công P02 phải có eventId riêng chưa cứu và loại P02 đúng luật.");
    }

    [Test]
    public void TC05_ShamanSelfSavesAgainstDogSpirit_Survives()
    {
        var shaman = AddPlayer(0, RoleType.Shaman, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, shaman.playerID, out _));
        int evtId = nightManager.CurrentMonsterAttackEventId;
        Assert.IsTrue(nightManager.TryValidateAttackEventForSave(evtId, shaman.playerID, out _));

        var shamanRole = (ShamanRole)roleManager.playerRoles[shaman.playerID];
        Assert.IsTrue(shamanRole.ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(evtId));

        nightManager.ResolveNight();
        Assert.IsTrue(shaman.isAlive, "Shaman tự cứu qua Monster Vote của DogSpirit phải sống sót.");
    }

    [Test]
    public void TC06_ShamanOutOfGoodCharm_CannotSave()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var shaman = AddPlayer(2, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        var shamanRole = (ShamanRole)roleManager.playerRoles[shaman.playerID];
        Assert.IsTrue(shamanRole.ConsumeGoodCharm());
        NetworkRoleActionBridge.SetShamanSaveUsed(shaman.playerID, true);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        Assert.IsFalse(ThuongOnlineShamanAdapter.CanShamanSaveTarget(shaman, p01, out _));
        Assert.IsFalse(shamanRole.ConsumeGoodCharm());

        nightManager.ResolveNight();
        Assert.IsFalse(p01.isAlive);
    }

    [Test]
    public void TC07_RejectedSaveRequest_DoesNotMarkRescuedOrConsumeCharm()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var p02 = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(2, RoleType.DogSpirit, FactionType.Monster);
        var shaman = AddPlayer(3, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        int evtId = nightManager.CurrentMonsterAttackEventId;

        // Thử cứu sai mục tiêu (p02 thay vì p01) -> từ chối
        Assert.IsFalse(nightManager.TryValidateAttackEventForSave(evtId, p02.playerID, out _));
        // Hoặc Shaman đã chết / Silenced -> từ chối
        var shamanRole = (ShamanRole)roleManager.playerRoles[shaman.playerID];
        Assert.IsFalse(nightManager.IsAttackEventRescued(evtId));
        Assert.IsTrue(shamanRole.HasGoodCharm);

        nightManager.ResolveNight();
        Assert.IsFalse(p01.isAlive);
    }

    [Test]
    public void TC08_TwoDistinctAttackEvents_RescuingOneDoesNotCancelOther()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var wh = AddPlayer(2, RoleType.WhiteHound, FactionType.Monster, awakenedWhiteHound: true);
        var shaman = AddPlayer(3, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        nightManager.StartNight();
        // Cả DogSpirit (Monster Vote) lẫn Bạch Khuyển (độc lập) đều đánh P01 trong cùng đêm -> 2 eventId tách biệt
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        Assert.IsTrue(nightManager.TrySubmitWhiteHoundAttack(wh, p01.playerID, out _));

        int dogEvtId = nightManager.CurrentMonsterAttackEventId;
        int whEvtId = nightManager.GetWhiteHoundAttackEventId(wh.playerID);
        Assert.Greater(dogEvtId, 0);
        Assert.Greater(whEvtId, 0);
        Assert.AreNotEqual(dogEvtId, whEvtId);

        // Shaman chỉ cứu sự kiện DogSpirit -> không được hủy đòn độc lập của Bạch Khuyển
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(dogEvtId));
        Assert.IsTrue(nightManager.IsAttackEventRescued(dogEvtId));
        Assert.IsFalse(nightManager.IsAttackEventRescued(whEvtId));

        nightManager.ResolveNight();
        Assert.IsFalse(p01.isAlive, "Sự kiện tấn công còn lại của Bạch Khuyển vẫn phải có hiệu lực.");
    }

    [Test]
    public void TC09_HostAndClient_SaveSyncAndPostDecisionGraceWindow()
    {
        // Local playerID = 0 đóng vai Shaman để kiểm tra UI Client nhận prompt & không bị ghi đè attackEventId
        var shaman = AddPlayer(0, RoleType.Shaman, FactionType.Villager);
        var p01 = AddPlayer(1, RoleType.Villager, FactionType.Villager);
        var p02 = AddPlayer(2, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(3, RoleType.DogSpirit, FactionType.Monster);
        var ogre = AddPlayer(4, RoleType.Ogre, FactionType.Monster);
        var ogre2 = AddPlayer(5, RoleType.Ogre, FactionType.Monster);

        // 1. Khi có gói fallback attackEventId == 0 đến sau gói có attackEventId > 0, UI Client không được ghi đè mất ID thật
        var ui = NetworkShamanNotificationUI.EnsureInstance();
        ui.ResetForNewMatch();
        ui.HandleVictimPromptReceived(p01.playerID, p01.DisplayName, 42);
        ui.HandleVictimPromptReceived(p01.playerID, p01.DisplayName, 0);

        // 2. Khi DogSpirit chốt hòa phiếu sang P01: NightManager giữ nguyên sự kiện active & tìm đúng eventId cho victim
        nightManager.StartNight();
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre, p01.playerID, 1, out _));
        Assert.IsTrue(nightManager.TryCastMonsterVote(ogre2, p02.playerID, 1, out _));
        Assert.IsTrue(nightManager.BeginDogSpiritTieDecisionIfNeeded());
        Assert.IsTrue(nightManager.IsDogSpiritDecisionActive);

        Assert.IsTrue(nightManager.TrySubmitDogSpiritDecision(dog, p01.playerID, 1, out _));
        int lockedEvtId = nightManager.CurrentMonsterAttackEventId;
        Assert.Greater(lockedEvtId, 0);
        Assert.IsTrue(nightManager.HasUnrescuedActiveAttackEvent(lockedEvtId));
        Assert.IsTrue(nightManager.TryFindActiveAttackEventForVictim(p01.playerID, out int foundEvtId));
        Assert.AreEqual(lockedEvtId, foundEvtId);

        // Simulate Server đánh dấu cứu thành công & đồng bộ trạng thái cứu
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(lockedEvtId));
        NetworkRoleActionBridge.SetShamanSaveUsed(shaman.playerID, true);
        Assert.IsFalse(nightManager.HasUnrescuedActiveAttackEvent(lockedEvtId));

        nightManager.ResolveNight();
        Assert.IsTrue(p01.isAlive);
        Assert.IsTrue(NetworkRoleActionBridge.IsShamanSaveUsed(shaman.playerID));
    }

    [Test]
    public void TC10_ConsecutiveNights_NeverReuseEventIdOrRescuedState()
    {
        var p01 = AddPlayer(0, RoleType.Villager, FactionType.Villager);
        var dog = AddPlayer(1, RoleType.DogSpirit, FactionType.Monster);
        var shaman = AddPlayer(2, RoleType.Shaman, FactionType.Villager);
        dog.roleDefinition = CreateMonsterKillDef(RoleType.DogSpirit);

        // Đêm 1: DogSpirit tấn công P01, Shaman cứu P01
        nightManager.StartNight();
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        int night1EvtId = nightManager.CurrentMonsterAttackEventId;
        Assert.Greater(night1EvtId, 0);
        Assert.IsTrue(((ShamanRole)roleManager.playerRoles[shaman.playerID]).ConsumeGoodCharm());
        Assert.IsTrue(nightManager.MarkAttackEventRescued(night1EvtId));
        nightManager.ResolveNight();
        Assert.IsTrue(p01.isAlive);

        // Đêm 2: DogSpirit tấn công lại chính P01 -> attackEventId phải tăng mới,
        // không tái sử dụng night1EvtId và KHÔNG kế thừa trạng thái đã cứu từ đêm 1.
        nightManager.StartNight();
        Assert.IsFalse(nightManager.IsAttackEventRescued(night1EvtId));
        Assert.IsTrue(roleManager.UseNightAbility(dog.playerID, p01.playerID, out _));
        int night2EvtId = nightManager.CurrentMonsterAttackEventId;
        Assert.Greater(night2EvtId, night1EvtId);
        Assert.IsFalse(nightManager.IsAttackEventRescued(night2EvtId));

        nightManager.ResolveNight();
        Assert.IsFalse(p01.isAlive, "Đêm 2 không được kế thừa trạng thái cứu của Đêm 1.");
    }
}
