using System;
using System.Collections.Generic;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adapter tích hợp kỹ năng Pháp Sư (Shaman) trong Thương Online.
/// 
/// Đảm bảo:
/// 1. Shaman KHÔNG ĐƯỢC PHÉP tự cứu bản thân (cả ở UI, Target Selection và Server RPC validation).
/// 2. Bùa hại (Poison / Kill Charm) của Shaman gây sát thương chí mạng chính xác lên mục tiêu hợp lệ thông qua DeathResolver.
/// 3. Không bỏ qua cơ chế delay xử lý bùa hại hiện có (5.0s delay trong NetworkGameOverManager).
/// 4. Quản lý việc thông báo UI riêng cho Shaman khi có người bị tấn công vào ban đêm để lựa chọn cứu hoặc không cứu.
/// 5. Độc lập, Event-driven, KHÔNG Reflection, KHÔNG Update polling.
/// 6. TUYỆT ĐỐI KHÔNG CHỈNH SỬA BẤT KỲ FILE NÀO TRONG Assets/Scripts/Thuong/.
/// </summary>
public static class ThuongOnlineShamanAdapter
{
    private static bool isListenerRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        EnsureInstalled();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        EnsureInstalled();
    }

    public static void EnsureInstalled()
    {
        if (!isListenerRegistered)
        {
            isListenerRegistered = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            NetworkPhaseSync.OnPhaseChangedGlobal -= HandleGlobalPhaseChanged;
            NetworkPhaseSync.OnPhaseChangedGlobal += HandleGlobalPhaseChanged;
        }

        HookSceneManagers();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookSceneManagers();
    }

    private static void HookSceneManagers()
    {
        if (GameRoleManager.Instance != null)
        {
            GameRoleManager.Instance.PhaseChanged -= HandleGameRolePhaseChanged;
            GameRoleManager.Instance.PhaseChanged += HandleGameRolePhaseChanged;
        }

        if (StatusEffectSystem.Instance != null)
        {
            StatusEffectSystem.Instance.EffectApplied -= HandleStatusEffectApplied;
            StatusEffectSystem.Instance.EffectApplied += HandleStatusEffectApplied;
        }
    }

    private static void HandleGlobalPhaseChanged(GamePhase phase)
    {
        if (phase != GamePhase.Night)
        {
            ResolveNightShamanKills();
            NetworkShamanNotificationUI.Instance?.HidePrompt();
        }
    }

    private static void HandleGameRolePhaseChanged(GamePhase prev, GamePhase next)
    {
        // Khi kết thúc Night Phase chuyển sang ResolveNight, Discussion, hoặc DayStart
        if (prev == GamePhase.Night || next == GamePhase.ResolveNight || next == GamePhase.Discussion || next == GamePhase.DayStart)
        {
            ResolveNightShamanKills();
            NetworkShamanNotificationUI.Instance?.HidePrompt();
        }
    }

    /// <summary>
    /// Xử lý sự kiện áp dụng hiệu ứng trong Thương Online.
    /// Nếu Shaman dùng Bùa Hại (Dev2 legacy áp dụng Silenced), adapter sẽ chuyển đổi thành
    /// ghi nhận mục tiêu trúng độc (Poison Kill) và hủy hiệu ứng Silenced sai.
    /// </summary>
    private static void HandleStatusEffectApplied(PlayerData target, StatusEffectInstance effect)
    {
        if (target == null || effect == null) return;

        // Kiểm tra xem hiệu ứng có đến từ Shaman không
        if (effect.SourcePlayerID >= 0 && PlayerManager.Instance != null)
        {
            PlayerData source = PlayerManager.Instance.GetplayerByID(effect.SourcePlayerID);
            if (source != null && source.roleType == RoleType.Shaman)
            {
                if (effect.Type == StatusEffectType.Silenced)
                {
                    // Đây là Bùa Hại của Shaman!
                    // 1. Gỡ bỏ trạng thái Silenced không chính xác
                    target.effects?.Remove(effect);
                    if (target.status != null)
                    {
                        target.status.isSilenced = false;
                    }

                    // 2. Ghi nhận mục tiêu tử vong do Bùa Hại
                    NetworkRoleActionBridge.RecordShamanKillTarget(target.playerID, source.playerID);
                    NetworkRoleActionBridge.SetShamanKillUsed(source.playerID, true);

                    // 3. Kích hoạt cơ chế delay xử lý bùa hại (5 giây)
                    NetworkGameOverManager.TriggerShamanKillDelay(5.0f);

                    Debug.Log($"[ThuongOnlineShamanAdapter] Chuyển đổi Bùa Hại của Shaman (Player {source.playerID + 1}) thành Poison Kill lên Player {target.playerID + 1}.");
                }
                else if (effect.Type == StatusEffectType.Protected)
                {
                    // Shaman dùng Bùa Cứu trong Thương Online
                    // Đảm bảo không cho tự cứu bản thân
                    if (target.playerID == source.playerID)
                    {
                        Debug.LogWarning($"[ThuongOnlineShamanAdapter] CHẶN TỰ CỨU: Shaman (Player {source.playerID + 1}) cố dùng Bùa Lợi lên chính mình!");
                        target.effects?.Remove(effect);
                        if (target.status != null) target.status.isProtected = false;
                        // Phục hồi lại bùa lợi cho Shaman nếu bị trừ nhầm
                        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(source.playerID, out var role) && role is ShamanRole sr)
                        {
                            sr.SelectCharm(true);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Giải quyết tử vong cho các mục tiêu bị Shaman dùng Bùa Hại.
    /// </summary>
    public static void ResolveNightShamanKills()
    {
        if (NetworkRoleActionBridge.HasPendingShamanKill)
        {
            NetworkRoleActionBridge.ResolvePendingShamanKill();
        }
    }

    /// <summary>
    /// Kiểm tra xem Shaman có đang cố tự cứu bản thân hay không.
    /// </summary>
    public static bool IsShamanSelfSaveForbidden(int shamanPlayerId, int targetPlayerId)
    {
        return shamanPlayerId == targetPlayerId;
    }

    /// <summary>
    /// Kiểm tra xem Shaman có đủ điều kiện sử dụng Bùa Cứu hay không.
    /// </summary>
    public static bool CanShamanSaveTarget(PlayerData shaman, PlayerData target, out string error)
    {
        if (shaman == null || !shaman.isAlive)
        {
            error = "Pháp Sư đã bị loại.";
            return false;
        }

        if (shaman.roleType != RoleType.Shaman)
        {
            error = "Người chơi không phải Pháp Sư.";
            return false;
        }

        if (NetworkRoleActionBridge.IsShamanSaveUsed(shaman.playerID))
        {
            error = "Bùa cứu đã được sử dụng trước đó!";
            return false;
        }

        if (target == null || !target.isAlive)
        {
            error = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        if (shaman.playerID == target.playerID)
        {
            error = "Pháp Sư không được phép tự cứu chính mình!";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Kiểm tra xem Shaman có đủ điều kiện sử dụng Bùa Hại hay không.
    /// </summary>
    public static bool CanShamanKillTarget(PlayerData shaman, PlayerData target, out string error)
    {
        if (shaman == null || !shaman.isAlive)
        {
            error = "Pháp Sư đã bị loại.";
            return false;
        }

        if (shaman.roleType != RoleType.Shaman)
        {
            error = "Người chơi không phải Pháp Sư.";
            return false;
        }

        if (NetworkRoleActionBridge.IsShamanKillUsed(shaman.playerID))
        {
            error = "Bùa hại đã được sử dụng trước đó!";
            return false;
        }

        if (target == null || !target.isAlive)
        {
            error = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        if (shaman.playerID == target.playerID)
        {
            error = "Không thể dùng bùa hại lên chính mình!";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reset toàn bộ trạng thái Shaman khi bắt đầu ván mới.
    /// </summary>
    public static void ResetForNewMatch()
    {
        NetworkRoleActionBridge.ResetShamanCharms();
        NetworkRoleActionBridge.ClearPendingShamanKill();
        NetworkShamanNotificationUI.Instance?.HidePrompt();
    }
}
