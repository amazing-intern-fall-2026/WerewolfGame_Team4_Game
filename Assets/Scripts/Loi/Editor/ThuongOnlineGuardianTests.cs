using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử NUnit kiểm tra chức năng Bảo Vệ (VillageGuardian) tự chọn bản thân trong Thương Online.
/// Kiểm thử các trường hợp bắt buộc theo yêu cầu:
/// - Host dùng Bảo Vệ, chọn bản thân: Thành công
/// - Client dùng Bảo Vệ, chọn bản thân: Thành công
/// - Host/Client chọn người chơi khác: Hoạt động như cũ
/// - Bảo Vệ chọn mục tiêu hợp lệ: Server xử lý đúng, được bảo vệ khi bị Sói cắn
/// - Role khác (Sói, Tiên tri): Không được tự chọn bản thân
/// - Chơi nhiều đêm / nhiều trận liên tiếp: Quy tắc chống lặp 2 đêm liên tiếp hoạt động đúng, reset sạch trạng thái
/// </summary>
public class ThuongOnlineGuardianTests
{
    private GameObject rootGo;
    private PlayerManager playerManager;
    private RoleManager roleManager;
    private GameRoleManager gameRoleManager;
    private NightManager nightManager;
    private AbilitySystem abilitySystem;
    private StatusEffectSystem statusEffectSystem;
    private DeathResolver deathResolver;

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestGuardianEnvironment]");
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

        // Tạo 4 người chơi
        playerManager.players = new List<PlayerData>();
        for (int i = 0; i < 4; i++)
        {
            var p = new PlayerData
            {
                playerID = i,
                playerName = $"Player {i + 1}",
                isAlive = true,
                status = new PlayerStatus(),
                effects = new List<StatusEffectInstance>()
            };
            playerManager.players.Add(p);
        }

        // Đảm bảo cấu hình VillageGuardian
        ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
    }

    [TearDown]
    public void TearDown()
    {
        if (abilitySystem != null) abilitySystem.SetMatchRunning(false);
        if (nightManager != null) nightManager.ClearActions();

        if (PlayerManager.Instance == playerManager) PlayerManager.Instance = null;
        if (RoleManager.Instance == roleManager) RoleManager.Instance = null;
        if (GameRoleManager.Instance == gameRoleManager) GameRoleManager.Instance = null;
        if (NightManager.Instance == nightManager) NightManager.Instance = null;
        if (AbilitySystem.Instance == abilitySystem) AbilitySystem.Instance = null;
        if (StatusEffectSystem.Instance == statusEffectSystem) StatusEffectSystem.Instance = null;
        if (DeathResolver.Instance == deathResolver) DeathResolver.Instance = null;

        if (rootGo != null) Object.DestroyImmediate(rootGo);
    }

    [Test]
    public void Test_01_GuardianDefinition_AllowsSelfTarget()
    {
        var guardianDef = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();
        Assert.IsNotNull(guardianDef, "Định nghĩa VillageGuardian phải tồn tại.");
        Assert.IsTrue(guardianDef.allowSelfTarget, "allowSelfTarget của Bảo Vệ phải là true.");
        Assert.IsFalse(guardianDef.useLegacyAbility, "useLegacyAbility phải là false để sử dụng AbilitySystem.");
        Assert.AreEqual(AbilityType.Protect, guardianDef.abilityType, "Kỹ năng phải là Protect.");
        Assert.IsTrue(guardianDef.forbidConsecutiveNightTarget, "Phải cấm bảo vệ cùng một người 2 đêm liên tiếp.");
    }

    [Test]
    public void Test_02_TargetValidator_GuardianCanTargetSelf()
    {
        var guardianPlayer = playerManager.GetplayerByID(0);
        guardianPlayer.roleType = RoleType.VillageGuardian;
        guardianPlayer.faction = FactionType.Villager;
        guardianPlayer.roleDefinition = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();

        var result = TargetValidator.Validate(guardianPlayer, guardianPlayer, guardianPlayer.roleDefinition);
        Assert.IsTrue(result.IsValid, "Bảo Vệ tự chọn chính mình phải hợp lệ.");
        Assert.AreEqual(ActionFailureReason.None, result.FailureReason);
    }

    [Test]
    public void Test_03_TargetValidator_GuardianCanTargetOthers()
    {
        var guardianPlayer = playerManager.GetplayerByID(0);
        guardianPlayer.roleType = RoleType.VillageGuardian;
        guardianPlayer.faction = FactionType.Villager;
        guardianPlayer.roleDefinition = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();

        var otherPlayer = playerManager.GetplayerByID(1);
        otherPlayer.roleType = RoleType.Villager;
        otherPlayer.faction = FactionType.Villager;

        var result = TargetValidator.Validate(guardianPlayer, otherPlayer, guardianPlayer.roleDefinition);
        Assert.IsTrue(result.IsValid, "Bảo Vệ chọn người chơi khác phải hợp lệ như cũ.");
        Assert.AreEqual(ActionFailureReason.None, result.FailureReason);
    }

    [Test]
    public void Test_04_GuardianSelfProtect_QueuesAndResolvesSuccessfully()
    {
        abilitySystem.SetMatchRunning(true);
        nightManager.StartNight();
        gameRoleManager.currentPhase = GamePhase.Night;

        var guardian = playerManager.GetplayerByID(0);
        guardian.roleType = RoleType.VillageGuardian;
        guardian.faction = FactionType.Villager;
        guardian.roleDefinition = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();

        roleManager.playerRoles[0] = new GuardianRole(guardian);

        // Bảo vệ tự chọn bản thân
        bool success = roleManager.UseNightAbility(0, 0, out string feedback);
        Assert.IsTrue(success, $"Server phải chấp nhận Bảo Vệ tự bảo vệ bản thân. Phản hồi: {feedback}");
        Assert.AreEqual(1, nightManager.PendingActionCount, "Hành động bảo vệ phải được đưa vào hàng đợi đêm.");

        // Giải quyết đêm
        nightManager.ResolveNight();

        // Kiểm tra kết quả: mục tiêu (chính Bảo Vệ) phải nhận trạng thái Protected
        Assert.IsTrue(guardian.HasEffect(StatusEffectType.Protected), "Bảo Vệ phải có hiệu ứng Protected sau khi giải quyết đêm.");
    }

    [Test]
    public void Test_05_ProtectedGuardian_BlocksMonsterKill()
    {
        abilitySystem.SetMatchRunning(true);
        nightManager.StartNight();
        gameRoleManager.currentPhase = GamePhase.Night;

        var guardian = playerManager.GetplayerByID(0);
        guardian.roleType = RoleType.VillageGuardian;
        guardian.faction = FactionType.Villager;
        guardian.roleDefinition = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();
        roleManager.playerRoles[0] = new GuardianRole(guardian);

        var wolf = playerManager.GetplayerByID(1);
        wolf.roleType = RoleType.DogSpirit;
        wolf.faction = FactionType.Monster;

        // Bảo Vệ tự bảo vệ bản thân
        roleManager.UseNightAbility(0, 0, out _);

        // Sói chọn cắn Bảo Vệ
        nightManager.SetMonsterTarget(0);

        // Giải quyết đêm
        nightManager.ResolveNight();

        // Đòn cắn phải bị chặn, Bảo Vệ vẫn sống
        Assert.IsTrue(guardian.isAlive, "Bảo Vệ tự bảo vệ bản thân phải sống sót khi bị Sói cắn.");
    }

    [Test]
    public void Test_06_ConsecutiveNightProtection_SelfForbiddenOnNight2_AllowedOnNight3()
    {
        abilitySystem.SetMatchRunning(true);
        nightManager.StartNight(); // Đêm 1
        gameRoleManager.currentPhase = GamePhase.Night;

        var guardian = playerManager.GetplayerByID(0);
        guardian.roleType = RoleType.VillageGuardian;
        guardian.faction = FactionType.Villager;
        guardian.roleDefinition = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();
        roleManager.playerRoles[0] = new GuardianRole(guardian);

        // Đêm 1: Bảo vệ bản thân -> Thành công
        bool night1Success = roleManager.UseNightAbility(0, 0, out string fb1);
        Assert.IsTrue(night1Success, "Đêm 1: Tự bảo vệ phải thành công.");

        nightManager.ResolveNight();

        // Bắt đầu Đêm 2
        nightManager.StartNight(); // Đêm 2
        abilitySystem.BeginPhase(GamePhase.Night);

        // Đêm 2: Thử bảo vệ lại bản thân -> Phải bị từ chối theo luật không bảo vệ cùng người 2 đêm liên tiếp
        bool night2Self = roleManager.UseNightAbility(0, 0, out string fb2);
        Assert.IsFalse(night2Self, "Đêm 2: Không được phép bảo vệ cùng một người hai đêm liên tiếp.");
        Assert.IsTrue(fb2.Contains("hai đêm liên tiếp"), $"Thông báo lỗi phải nêu rõ luật: {fb2}");

        // Đêm 2: Bảo vệ người khác (Player 1) -> Thành công
        bool night2Other = roleManager.UseNightAbility(0, 1, out string fb2Other);
        Assert.IsTrue(night2Other, "Đêm 2: Bảo vệ người khác phải thành công.");

        nightManager.ResolveNight();

        // Bắt đầu Đêm 3
        nightManager.StartNight(); // Đêm 3
        abilitySystem.BeginPhase(GamePhase.Night);

        // Đêm 3: Tự bảo vệ lại bản thân -> Thành công
        bool night3Self = roleManager.UseNightAbility(0, 0, out string fb3);
        Assert.IsTrue(night3Self, "Đêm 3: Tự bảo vệ lại bản thân phải thành công.");
    }

    [Test]
    public void Test_07_OtherRolesCannotSelfTarget()
    {
        // 1. Sói (DogSpirit)
        var wolfDef = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        wolfDef.roleType = RoleType.DogSpirit;
        wolfDef.faction = FactionType.Monster;
        wolfDef.abilityType = AbilityType.Kill;
        wolfDef.allowSelfTarget = false;
        wolfDef.targetFactionRule = TargetFactionRule.OtherFaction;

        var wolf = playerManager.GetplayerByID(1);
        wolf.roleType = RoleType.DogSpirit;
        wolf.faction = FactionType.Monster;
        wolf.roleDefinition = wolfDef;

        var wolfResult = TargetValidator.Validate(wolf, wolf, wolfDef);
        Assert.IsFalse(wolfResult.IsValid, "Sói tuyệt đối không được tự cắn bản thân.");

        // 2. Tiên tri (Seer) theo luật legacy
        var seer = playerManager.GetplayerByID(2);
        seer.roleType = RoleType.Seer;
        seer.faction = FactionType.Villager;

        var seerResult = TargetValidator.ValidateLegacy(seer, seer);
        Assert.IsFalse(seerResult.IsValid, "Tiên tri tuyệt đối không được tự soi bản thân.");
        Assert.AreEqual(ActionFailureReason.SelfTargetForbidden, seerResult.FailureReason);
    }
}

