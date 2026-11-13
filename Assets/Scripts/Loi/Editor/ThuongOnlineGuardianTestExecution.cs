using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Thuong.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Trình thực thi kiểm thử độc lập cho tính năng Bảo Vệ (VillageGuardian) tự chọn bản thân trong Thương Online.
/// Chạy kiểm tra cách ly trên preview scene mà không làm ảnh hưởng đến dữ liệu đang chạy.
/// </summary>
[InitializeOnLoad]
public static class ThuongOnlineGuardianTestExecution
{
    public static int PassCount { get; private set; }
    public static int FailCount { get; private set; }
    public static List<string> Results { get; } = new();

    static ThuongOnlineGuardianTestExecution()
    {
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (!Application.isPlaying && ThuongOnlineSession.Active == null &&
                    (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening))
                {
                    Run();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GUARDIAN TESTS] Auto-run skipped: " + ex.Message);
            }
        };
    }

    [MenuItem("Tools/Loi/Run Thuong Online Guardian Self Target Tests")]
    public static void Run()
    {
        if (Application.isPlaying || ThuongOnlineSession.Active != null ||
            (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening))
            throw new InvalidOperationException("Hãy chạy kiểm thử ngoài chế độ Play Mode / Active Network Session.");

        PassCount = FailCount = 0;
        Results.Clear();

        var previousPlayers = PlayerManager.Instance;
        var previousRoles = RoleManager.Instance;
        var previousGame = GameRoleManager.Instance;
        var previousNight = NightManager.Instance;
        var previousAbilities = AbilitySystem.Instance;
        var previousStatus = StatusEffectSystem.Instance;
        var previousDeath = DeathResolver.Instance;

        var scene = EditorSceneManager.NewPreviewScene();

        try
        {
            PlayerManager.Instance = null;
            RoleManager.Instance = null;
            GameRoleManager.Instance = null;
            NightManager.Instance = null;
            AbilitySystem.Instance = null;
            StatusEffectSystem.Instance = null;
            DeathResolver.Instance = null;

            T Add<T>() where T : Component
            {
                var root = new GameObject(typeof(T).Name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                return root.AddComponent<T>();
            }

            var players = Add<PlayerManager>();
            var roles = Add<RoleManager>();
            var game = Add<GameRoleManager>();
            var night = Add<NightManager>();
            var abilities = Add<AbilitySystem>();
            var status = Add<StatusEffectSystem>();
            var death = Add<DeathResolver>();

            PlayerManager.Instance = players;
            RoleManager.Instance = roles;
            GameRoleManager.Instance = game;
            NightManager.Instance = night;
            AbilitySystem.Instance = abilities;
            StatusEffectSystem.Instance = status;
            DeathResolver.Instance = death;

            game.abilities = abilities;
            game.statusEffects = status;
            status.deathSystem = death;
            abilities.phaseController = game;
            abilities.nightSystem = night;

            // 4 người chơi
            players.players = Enumerable.Range(0, 4).Select(i => new PlayerData
            {
                playerID = i,
                playerName = "Player " + (i + 1),
                isAlive = true,
                status = new PlayerStatus(),
                effects = new List<StatusEffectInstance>()
            }).ToList();

            // 1. Kiểm tra cấu hình Adapter
            ThuongOnlineGuardianAdapter.EnsureGuardianSelfTargetConfigured();
            var guardianDef = ThuongOnlineGuardianAdapter.FindOrGetGuardianDefinition();
            Require(guardianDef != null && guardianDef.allowSelfTarget && !guardianDef.useLegacyAbility &&
                    guardianDef.forbidConsecutiveNightTarget && guardianDef.abilityType == AbilityType.Protect,
                "TC01: ThuongOnlineGuardianAdapter cấu hình đúng ThuongRoleDefinition cho VillageGuardian (allowSelfTarget = true)");

            // 2. Gán vai trò cho Player 0 là VillageGuardian
            var p0 = players.GetplayerByID(0);
            p0.roleType = RoleType.VillageGuardian;
            p0.faction = FactionType.Villager;
            p0.roleDefinition = guardianDef;
            roles.playerRoles[0] = new GuardianRole(p0);

            // Gán vai trò cho Player 1 là Sói (DogSpirit)
            var p1 = players.GetplayerByID(1);
            p1.roleType = RoleType.DogSpirit;
            p1.faction = FactionType.Monster;

            // Gán vai trò cho Player 2 là Dân làng
            var p2 = players.GetplayerByID(2);
            p2.roleType = RoleType.Villager;
            p2.faction = FactionType.Villager;

            // Gán vai trò cho Player 3 là Tiên tri
            var p3 = players.GetplayerByID(3);
            p3.roleType = RoleType.Seer;
            p3.faction = FactionType.Villager;

            // 3. Kiểm tra TargetValidator chấp nhận Bảo Vệ tự chọn bản thân
            var valSelf = TargetValidator.Validate(p0, p0, guardianDef);
            Require(valSelf.IsValid, "TC02: TargetValidator.Validate cho phép Bảo Vệ tự chọn chính mình");

            // 4. Kiểm tra TargetValidator chấp nhận Bảo Vệ chọn người khác
            var valOther = TargetValidator.Validate(p0, p2, guardianDef);
            Require(valOther.IsValid, "TC03: TargetValidator.Validate cho phép Bảo Vệ chọn người chơi khác");

            // 5. Kiểm tra Server xử lý kỹ năng tự bảo vệ (Đêm 1)
            abilities.SetMatchRunning(true);
            night.StartNight();
            game.currentPhase = GamePhase.Night;

            bool useNight1 = roles.UseNightAbility(0, 0, out string fb1);
            Require(useNight1 && night.PendingActionCount == 1,
                $"TC04: Server xử lý yêu cầu Bảo Vệ tự bảo vệ thành công (Night 1). Phản hồi: {fb1}");

            // 6. Kiểm tra giải quyết đêm và chặn đòn cắn của Ma sói
            night.SetMonsterTarget(0); // Sói cắn Bảo Vệ
            night.ResolveNight();

            Require(p0.isAlive && p0.HasEffect(StatusEffectType.Protected),
                "TC05: Bảo Vệ tự bảo vệ bản thân và sống sót khi bị Ma sói cắn trong đêm");

            // 7. Kiểm tra quy tắc cấm bảo vệ cùng một người 2 đêm liên tiếp (Đêm 2)
            night.StartNight();
            abilities.BeginPhase(GamePhase.Night);

            bool useNight2Self = roles.UseNightAbility(0, 0, out string fb2Self);
            Require(!useNight2Self, "TC06: Cấm Bảo Vệ tự bảo vệ bản thân 2 đêm liên tiếp");

            bool useNight2Other = roles.UseNightAbility(0, 2, out string fb2Other);
            Require(useNight2Other, "TC07: Đêm 2 Bảo Vệ chọn người chơi khác thành công");

            night.ResolveNight();

            // 8. Đêm 3: Có thể tự bảo vệ lại
            night.StartNight();
            abilities.BeginPhase(GamePhase.Night);

            bool useNight3Self = roles.UseNightAbility(0, 0, out string fb3Self);
            Require(useNight3Self, "TC08: Đêm 3 Bảo Vệ được phép tự bảo vệ bản thân trở lại");

            // 9. Kiểm tra các vai trò khác KHÔNG được phép tự chọn bản thân
            var wolfDef = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
            wolfDef.roleType = RoleType.DogSpirit;
            wolfDef.allowSelfTarget = false;
            wolfDef.targetFactionRule = TargetFactionRule.OtherFaction;
            var valWolfSelf = TargetValidator.Validate(p1, p1, wolfDef);
            Require(!valWolfSelf.IsValid, "TC09: Ma sói (DogSpirit) tuyệt đối không được tự cắn bản thân");

            var valSeerSelf = TargetValidator.ValidateLegacy(p3, p3);
            Require(!valSeerSelf.IsValid, "TC10: Tiên tri (Seer) tuyệt đối không được tự soi bản thân");

            Debug.Log($"<color=green>[GUARDIAN TESTS] HOÀN TẤT: {PassCount} PASS, {FailCount} FAIL.</color>");
        }
        catch (Exception ex)
        {
            FailCount++;
            Results.Add($"[EXCEPTION] {ex.Message}\n{ex.StackTrace}");
            Debug.LogError($"[GUARDIAN TESTS] Lỗi kiểm thử: {ex.Message}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerManager.Instance = previousPlayers;
            RoleManager.Instance = previousRoles;
            GameRoleManager.Instance = previousGame;
            NightManager.Instance = previousNight;
            AbilitySystem.Instance = previousAbilities;
            StatusEffectSystem.Instance = previousStatus;
            DeathResolver.Instance = previousDeath;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (condition)
        {
            PassCount++;
            Results.Add("[PASS] " + message);
        }
        else
        {
            FailCount++;
            Results.Add("[FAIL] " + message);
            Debug.LogError("[GUARDIAN TESTS FAIL] " + message);
        }
    }
}

