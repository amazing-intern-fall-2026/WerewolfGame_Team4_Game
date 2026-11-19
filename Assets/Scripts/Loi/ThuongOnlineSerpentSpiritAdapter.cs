using System;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adapter tích hợp dành riêng cho vai trò Xà Tinh (SerpentSpirit) trong Thương Online.
/// 
/// Đảm bảo:
/// 1. Xà Tinh chỉ được tấn công từ Đêm 3 (Night 3) trở đi.
/// 2. Đêm 1 & Đêm 2: Ẩn nút tấn công và các thành phần chọn mục tiêu (không chỉ disable mà UI thực sự không xuất hiện).
/// 3. Server xác thực số đêm thực tế và từ chối mọi yêu cầu tấn công trong Đêm 1 và Đêm 2.
/// 4. Đêm 3 trở đi: Cho phép sử dụng kỹ năng bình thường theo luật.
/// 5. Khi chuyển sang Ngày hoặc phase khác: Ẩn UI kỹ năng.
/// 6. Hoàn toàn Event-driven, KHÔNG dùng Update/LateUpdate polling, KHÔNG Reflection, KHÔNG sửa code Thương.
/// </summary>
public static class ThuongOnlineSerpentSpiritAdapter
{
    private static bool isListenerRegistered = false;

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

        EnforceSessionRestriction();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceSessionRestriction();
    }

    private static void HandleGlobalPhaseChanged(GamePhase phase)
    {
        EnforceSessionRestriction();

        // Cập nhật lại UI kỹ năng nếu đang hiển thị
        if (NetworkSkillUI.Instance != null)
        {
            NetworkSkillUI.Instance.UpdateSkillUI();
        }
    }

    /// <summary>
    /// Đồng bộ ràng buộc số đêm cho Xà Tinh trong ThuongOnlineSession nếu đang hoạt động.
    /// </summary>
    public static void EnforceSessionRestriction()
    {
        var session = ThuongOnlineSession.Active;
        if (session != null)
        {
            session.EnforceSerpentSpiritNightRestriction();
        }
    }

    /// <summary>
    /// Kiểm tra xem số đêm hiện tại có hợp lệ để Xà Tinh tấn công hay không (từ Đêm 3 trở đi).
    /// </summary>
    public static bool IsSerpentSpiritNightValid(int nightNumber)
    {
        return nightNumber >= 3;
    }

    /// <summary>
    /// Kiểm tra người chơi Xà Tinh có đủ điều kiện tấn công ở đêm hiện tại hay không.
    /// </summary>
    public static bool CanSerpentSpiritAttack(PlayerData player, int nightNumber)
    {
        if (player == null || !player.isAlive) return false;
        if (player.roleType != RoleType.SerpentSpirit) return false;
        return IsSerpentSpiritNightValid(nightNumber);
    }
}

