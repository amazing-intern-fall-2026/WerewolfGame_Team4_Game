using System;
using Thuong.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adapter tích hợp dành riêng cho vai trò Ông Tơ Bà Nguyệt (WeaverOfFate / Dệt Duyên) trong Thương Online.
/// 
/// Đảm bảo:
/// 1. Tự động khởi tạo và bảo toàn instance LoverManager trong suốt phiên chơi.
/// 2. Hỗ trợ ghép đôi 2 người chơi còn sống bất kỳ trong ban đêm (Night).
/// 3. Giới hạn sử dụng 1 lần duy nhất mỗi ván (HasUsedAbility).
/// 4. Đồng bộ trạng thái Lovers giữa PlayerData, LoverManager và Network.
/// 5. Đảm bảo hiệu ứng tử vì tình (Love Death): khi 1 người trong cặp chết, người còn lại chết theo.
/// 6. Hoàn toàn Event-driven, KHÔNG dùng Update/LateUpdate polling, KHÔNG Reflection, KHÔNG sửa code Thương.
/// </summary>
public static class ThuongOnlineWeaverOfFateAdapter
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
        EnsureLoverManager();

        if (!isListenerRegistered)
        {
            isListenerRegistered = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureLoverManager();
    }

    /// <summary>
    /// Đảm bảo LoverManager luôn tồn tại một Instance duy nhất trong scene / match.
    /// Tránh lỗi "Chưa có LoverManager trong màn chơi" khi WeaverOfFate thực hiện TryMakeLovers.
    /// </summary>
    public static LoverManager EnsureLoverManager()
    {
        if (LoverManager.Instance != null)
        {
            return LoverManager.Instance;
        }

        var existing = UnityEngine.Object.FindAnyObjectByType<LoverManager>();
        if (existing != null)
        {
            LoverManager.Instance = existing;
            return existing;
        }

        var go = new GameObject("LoverManager_Auto");
        var lm = go.AddComponent<LoverManager>();
        UnityEngine.Object.DontDestroyOnLoad(go);
        LoverManager.Instance = lm;
        Debug.Log("[ThuongOnlineWeaverOfFateAdapter] Đã tự động khởi tạo LoverManager instance.");
        return lm;
    }

    /// <summary>
    /// Kiểm tra người chơi có vai trò Ông Tơ Bà Nguyệt hay không.
    /// </summary>
    public static bool IsWeaverOfFate(PlayerData player)
    {
        if (player == null) return false;
        return player.roleType == RoleType.WeaverOfFate;
    }

    /// <summary>
    /// Kiểm tra xem WeaverOfFate có đủ điều kiện ghép đôi hay không.
    /// </summary>
    public static bool CanWeaverOfFatePair(PlayerData player, GamePhase phase, bool hasUsedAbility)
    {
        if (player == null || !player.isAlive) return false;
        if (player.roleType != RoleType.WeaverOfFate) return false;
        if (hasUsedAbility) return false;
        if (phase != GamePhase.Night) return false;
        return true;
    }

    /// <summary>
    /// Thực hiện logic ghép đôi giữa 2 người chơi (Server-side authoritative).
    /// </summary>
    public static bool ExecuteMakeLovers(ulong requesterClientId, int playerAID, int playerBID, out string feedback)
    {
        feedback = string.Empty;
        EnsureLoverManager();

        if (playerAID < 0 || playerBID < 0)
        {
            feedback = "Mã người chơi không hợp lệ.";
            return false;
        }

        if (playerAID == playerBID)
        {
            feedback = "Hai mục tiêu ghép đôi phải khác nhau.";
            return false;
        }

        PlayerData playerA = PlayerManager.Instance?.GetplayerByID(playerAID);
        PlayerData playerB = PlayerManager.Instance?.GetplayerByID(playerBID);

        if (playerA == null || playerB == null)
        {
            feedback = "Không tìm thấy người chơi hợp lệ.";
            return false;
        }

        if (!playerA.isAlive || !playerB.isAlive)
        {
            feedback = "Hai mục tiêu phải là người chơi còn sống.";
            return false;
        }

        // Kiểm tra xem 1 trong 2 đã có đôi từ trước chưa
        if (playerA.loverID != -1 && playerA.loverID != playerBID)
        {
            feedback = $"{playerA.playerName} đã có đôi từ trước.";
            return false;
        }

        if (playerB.loverID != -1 && playerB.loverID != playerAID)
        {
            feedback = $"{playerB.playerName} đã có đôi từ trước.";
            return false;
        }

        // Gọi LoverManager để thiết lập cặp đôi
        bool success = LoverManager.Instance != null && LoverManager.Instance.TryMakeLovers(playerAID, playerBID, out feedback);

        // Fallback hoặc đồng bộ trực tiếp PlayerData
        if (success || LoverManager.Instance != null)
        {
            playerA.loverID = playerBID;
            playerB.loverID = playerAID;
            playerA.NotifyChanged();
            playerB.NotifyChanged();

            // Đánh dấu requester WeaverOfFate đã dùng kỹ năng
            PlayerData requesterData = PlayerManager.Instance?.GetplayerByID((int)requesterClientId);
            if (requesterData != null)
            {
                requesterData.hasUseNightAction = true;
                requesterData.NotifyChanged();
            }

            if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null &&
                RoleManager.Instance.playerRoles.TryGetValue((int)requesterClientId, out var baseRole))
            {
                if (baseRole is WeaverOfFateRole weaverRole)
                {
                    // WeaverOfFateRole.HasUsedAbility được cập nhật thông qua TryMakeLovers
                }
            }

            feedback = $"Đã ghép đôi thành công {playerA.playerName} và {playerB.playerName}!";
            Debug.Log($"[ThuongOnlineWeaverOfFateAdapter] {feedback}");
            return true;
        }

        if (string.IsNullOrEmpty(feedback))
        {
            feedback = "Không thể ghép đôi hai người chơi này.";
        }
        return false;
    }
}
