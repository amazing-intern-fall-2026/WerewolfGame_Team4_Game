using System;
using System.Collections.Generic;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adapter tích hợp dành riêng cho vai trò Bảo Vệ (VillageGuardian) trong Thương Online.
/// 
/// Đảm bảo:
/// 1. VillageGuardian được phép tự chọn bản thân làm mục tiêu bảo vệ (allowSelfTarget = true).
/// 2. Sử dụng hệ thống configured ability (useLegacyAbility = false) để TargetValidator và AbilitySystem
///    tính toán mục tiêu hợp lệ đầy đủ (AllowedTargets bao gồm chính mình).
/// 3. Server xử lý và ghi nhận mục tiêu bảo vệ bản thân hợp lệ, chuyển thành trạng thái Protected khi giải quyết đêm.
/// 4. Không ảnh hưởng đến bất kỳ vai trò nào khác (DogSpirit, Seer, v.v. giữ nguyên quy tắc không tự chọn bản thân).
/// 5. Bảo toàn quy tắc: không được bảo vệ cùng một người (kể cả bản thân) trong hai đêm liên tiếp.
/// 6. TUYỆT ĐỐI KHÔNG CHỈNH SỬA BẤT KỲ FILE NÀO TRONG Assets/Scripts/Thuong/.
/// </summary>
public static class ThuongOnlineGuardianAdapter
{
    private static bool isListenerRegistered;
    private static ThuongRoleDefinition cachedRuntimeGuardianDefinition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        EnsureGuardianSelfTargetConfigured();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        EnsureGuardianSelfTargetConfigured();

        if (!isListenerRegistered)
        {
            isListenerRegistered = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureGuardianSelfTargetConfigured();
    }

    /// <summary>
    /// Điểm kích hoạt chính để đảm bảo vai trò Bảo Vệ (VillageGuardian) có thể tự bảo vệ bản thân.
    /// Có thể gọi an toàn tại bất kỳ thời điểm nào (khi khởi động, đổi scene, phân vai, v.v.).
    /// </summary>
    public static void EnsureGuardianSelfTargetConfigured()
    {
        // 1. Cấu hình trên RoleManager.Instance catalog (nếu đã có trong scene)
        if (RoleManager.Instance != null)
        {
            ConfigureCatalog(RoleManager.Instance.roleDefinitions);

            // Nếu roleDefinitions của RoleManager chưa được gán, gán fallback catalog
            if (RoleManager.Instance.roleDefinitions == null)
            {
                var fallbackCatalog = CreateFallbackCatalogWithGuardian();
                RoleManager.Instance.roleDefinitions = fallbackCatalog;
            }
        }

        // 2. Cấu hình cho mọi PlayerData thuộc PlayerManager có vai trò VillageGuardian
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            foreach (PlayerData player in PlayerManager.Instance.players)
            {
                if (player == null) continue;

                if (player.roleType == RoleType.VillageGuardian)
                {
                    if (player.roleDefinition == null)
                    {
                        player.roleDefinition = FindOrGetGuardianDefinition();
                    }

                    if (player.roleDefinition != null)
                    {
                        ConfigureDefinition(player.roleDefinition);
                    }
                }
            }
        }

        // 3. Cấu hình cho local player trong ThuongOnlineSession nếu đang có vai trò VillageGuardian
        var session = ThuongOnlineSession.Active;
        if (session != null && session.HasLocalRole)
        {
            var localPlayer = PlayerManager.Instance?.GetplayerByID(session.LocalPlayerId);
            if (localPlayer != null && localPlayer.roleType == RoleType.VillageGuardian)
            {
                if (localPlayer.roleDefinition == null)
                {
                    localPlayer.roleDefinition = FindOrGetGuardianDefinition();
                }

                if (localPlayer.roleDefinition != null)
                {
                    ConfigureDefinition(localPlayer.roleDefinition);
                }
            }
        }
    }

    /// <summary>
    /// Cấu hình catalog vai trò để định nghĩa VillageGuardian cho phép tự chọn bản thân.
    /// </summary>
    public static void ConfigureCatalog(ThuongRoleDefinitionCatalog catalog)
    {
        if (catalog == null) return;

        var guardianDef = catalog.Find(RoleType.VillageGuardian);
        if (guardianDef != null)
        {
            ConfigureDefinition(guardianDef);
        }
        else
        {
            var created = FindOrGetGuardianDefinition();
            if (!catalog.definitions.Contains(created))
            {
                catalog.definitions.Add(created);
            }
        }
    }

    /// <summary>
    /// Thiết lập các thuộc tính bắt buộc trên ThuongRoleDefinition của VillageGuardian.
    /// </summary>
    public static void ConfigureDefinition(ThuongRoleDefinition def)
    {
        if (def == null) return;
        if (def.roleType != RoleType.VillageGuardian) return;

        // BẬT tính năng tự chọn bản thân cho Bảo Vệ
        def.allowSelfTarget = true;

        // TẮT useLegacyAbility để hệ thống chuyển sang dùng configured ability (AbilitySystem & TargetValidator)
        def.useLegacyAbility = false;

        def.abilityType = AbilityType.Protect;
        def.usablePhase = GamePhase.Night;
        def.targetFactionRule = TargetFactionRule.Any;
        def.canTargetDead = false;

        // Không cho phép bảo vệ cùng một người trong hai đêm liên tiếp
        def.forbidConsecutiveNightTarget = true;

        if (def.maxUsesPerPhase < 1) def.maxUsesPerPhase = 1;
        if (def.effectStrength < 1) def.effectStrength = 1;
        if (def.effectDurationTicks < 1) def.effectDurationTicks = 1;
        def.effectTicksOnPhase = GamePhase.Discussion;

        if (string.IsNullOrEmpty(def.displayName)) def.displayName = "BẢO VỆ";
        if (string.IsNullOrEmpty(def.abilityName)) def.abilityName = "BẢO VỆ";
        def.description = "Bảo vệ một người chơi (có thể chọn chính mình) trong đêm khỏi đòn tấn công. Không thể bảo vệ cùng một người trong hai đêm liên tiếp.";
    }

    /// <summary>
    /// Tìm hoặc khởi tạo định nghĩa VillageGuardian hợp lệ.
    /// </summary>
    public static ThuongRoleDefinition FindOrGetGuardianDefinition()
    {
        if (RoleManager.Instance != null && RoleManager.Instance.roleDefinitions != null)
        {
            var def = RoleManager.Instance.roleDefinitions.Find(RoleType.VillageGuardian);
            if (def != null)
            {
                ConfigureDefinition(def);
                return def;
            }
        }

        if (cachedRuntimeGuardianDefinition != null)
        {
            ConfigureDefinition(cachedRuntimeGuardianDefinition);
            return cachedRuntimeGuardianDefinition;
        }

        cachedRuntimeGuardianDefinition = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        cachedRuntimeGuardianDefinition.roleID = "gameplay-guardian";
        cachedRuntimeGuardianDefinition.displayName = "BẢO VỆ";
        cachedRuntimeGuardianDefinition.abilityName = "BẢO VỆ";
        cachedRuntimeGuardianDefinition.roleType = RoleType.VillageGuardian;
        cachedRuntimeGuardianDefinition.faction = FactionType.Villager;
        ConfigureDefinition(cachedRuntimeGuardianDefinition);

        if (RoleManager.Instance != null && RoleManager.Instance.roleDefinitions != null)
        {
            RoleManager.Instance.roleDefinitions.definitions.Add(cachedRuntimeGuardianDefinition);
        }

        return cachedRuntimeGuardianDefinition;
    }

    private static ThuongRoleDefinitionCatalog CreateFallbackCatalogWithGuardian()
    {
        var catalog = ScriptableObject.CreateInstance<ThuongRoleDefinitionCatalog>();
        var guardian = FindOrGetGuardianDefinition();
        catalog.definitions.Add(guardian);
        return catalog;
    }
}

