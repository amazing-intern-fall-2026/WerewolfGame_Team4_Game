using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Thuong.Online;

/// <summary>
/// Adapter tích hợp từ hệ thống Network của Lợi sang hệ thống Online của Thương.
/// Mục đích:
/// 1. Tích hợp chức năng hiển thị người chơi đang được vote (chuyển sang màu xanh lá #35C759).
/// 2. Hỗ trợ Chuyển Vote (Revote) nhiều lần trong thời gian Voting (khôi phục màu thẻ cũ, tô xanh thẻ mới).
/// 3. Server Authoritative: Chỉ đổi màu khi Server đã xác nhận vote thành công.
/// 4. Bảo mật riêng tư: Mỗi người chỉ nhìn thấy lựa chọn vote của chính mình.
/// 5. Chặn tuyệt đối việc tự bỏ phiếu cho chính bản thân mình (Self-vote).
/// 6. Tự động xóa màu xanh khi kết thúc Voting, hoàn phiếu (disconnect) hoặc bắt đầu trận mới.
/// 
/// ĐIỀU KIỆN TUÂN THỦ:
/// - TUYỆT ĐỐI KHÔNG chỉnh sửa bất kỳ file nào trong Assets/Scripts/Thuong/.
/// - Tự động hook runtime vào scene và UI của Thương.
/// </summary>
[DefaultExecutionOrder(1000)] // Chạy sau các script UI và Voting của Thương
public class ThuongOnlineVoteNetworkAdapter : MonoBehaviour
{
    private static ThuongOnlineVoteNetworkAdapter instance;

    /// <summary>
    /// Màu xanh lá chuẩn #35C759 theo yêu cầu thiết kế
    /// </summary>
    public static readonly Color VoteConfirmedGreen = new Color(53f / 255f, 199f / 255f, 89f / 255f, 1f); // #35C759

    private readonly HashSet<VoteButton> hookedButtons = new HashSet<VoteButton>();
    private readonly Dictionary<Image, Color> defaultImageColors = new Dictionary<Image, Color>();
    private readonly Dictionary<Button, ColorBlock> defaultButtonColors = new Dictionary<Button, ColorBlock>();

    private static System.Reflection.FieldInfo thuongOnlineVotedTargetField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        if (instance != null)
        {
            instance.ResetCaches();
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        var existing = Object.FindAnyObjectByType<ThuongOnlineVoteNetworkAdapter>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("[ThuongOnlineVoteNetworkAdapter]");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<ThuongOnlineVoteNetworkAdapter>();
    }

    private void ResetCaches()
    {
        hookedButtons.Clear();
        defaultImageColors.Clear();
        defaultButtonColors.Clear();
    }

    private void Update()
    {
        // =========================================================================
        // SERVER AUTHORITY: XỬ LÝ CHUYỂN VOTE & CHẶN VOTE BẢN THÂN TRONG THƯƠNG ONLINE
        // =========================================================================
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            ProcessServerVotingLogic();
        }
    }

    private void ProcessServerVotingLogic()
    {
        var game = GameRoleManager.Instance;
        if (game == null) return;

        bool isVotingPhase = game.currentState == GameState.Voting ||
            (ThuongLogicSceneScope.IsActive && game.currentPhase == GamePhase.Voting) ||
            (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.Voting);

        if (!isVotingPhase) return;

        var pm = PlayerManager.Instance;
        if (pm == null || pm.players == null) return;

        // Trong thời gian Voting, mở cờ hasVoted = false trên Server để:
        // 1. Cho phép VoteManager.ValidateVote và TryVote ghi nhận mục tiêu mới khi người chơi chuyển phiếu (Revote).
        // 2. Không cho phép cờ privateFlags |= 2 khóa các nút vote ở Client.
        foreach (var player in pm.players)
        {
            if (player == null || !player.isAlive) continue;

            // Kiểm tra nếu có người chơi nào vô tình/cố ý vote chính mình trong VoteManager
            if (VoteManager.Instance != null)
            {
                int currentChoice = VoteManager.Instance.GetVotedTarget(player.playerID);
                if (currentChoice == player.playerID && pm.players.Count > 1)
                {
                    // Phát hiện tự vote bản thân -> Loại bỏ lựa chọn này khỏi VoteManager
                    Debug.LogWarning($"[NETWORK VOTE ADAPTER] Chặn tự vote bản thân: Player {player.playerID + 1} vote chính mình!");
                    player.hasVoted = false;
                }
            }

            // Giữ hasVoted = false trong suốt Phase Voting để hỗ trợ đổi phiếu liên tục
            player.hasVoted = false;
        }
    }

    private void LateUpdate()
    {
        // =========================================================================
        // CLIENT UI: ĐỒNG BỘ MÀU XANH LÁ #35C759 CHO MỤC TIÊU ĐƯỢC CHỌN & MỞ KHÓA REVOTE
        // =========================================================================
        var online = ThuongOnlineSession.Active;
        var game = GameRoleManager.Instance;

        bool isVotingPhase = (game != null && (game.currentState == GameState.Voting ||
            (ThuongLogicSceneScope.IsActive && game.currentPhase == GamePhase.Voting))) ||
            (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.Voting);

        int localId = online != null ? online.LocalPlayerId : -1;
        if (localId < 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            localId = (int)NetworkManager.Singleton.LocalClientId;
        }
        if (localId < 0)
        {
            var hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud != null)
            {
                localId = hud.localVoterID;
            }
        }

        // Tìm tất cả VoteButton đang có trên UI của Thương (kể cả trong ThuongMeetingRosterUI)
        var voteButtons = Object.FindObjectsByType<VoteButton>(FindObjectsSortMode.None);
        if (voteButtons == null || voteButtons.Length == 0)
        {
            return;
        }

        var pm = PlayerManager.Instance;
        var localData = localId >= 0 && pm != null ? pm.GetplayerByID(localId) : null;
        bool localCanVote = localData != null && localData.isAlive && localData.CanVote;

        // Lấy mục tiêu mà chính Local Player đã được Server xác nhận vote
        int confirmedTarget = isVotingPhase ? GetConfirmedVotedTarget(localId) : -1;

        foreach (var btn in voteButtons)
        {
            if (btn == null) continue;

            var buttonComp = btn.GetComponent<Button>();
            if (buttonComp == null) continue;

            // 1. Hook sự kiện OnClick để gửi RequestVote ngay cả khi CanVoteFor gốc bị khóa cờ hasVoted
            if (!hookedButtons.Contains(btn))
            {
                hookedButtons.Add(btn);
                int target = btn.targetID;
                buttonComp.onClick.AddListener(() =>
                {
                    OnVoteButtonClicked(target);
                });
            }

            // 2. Chặn vote chính bản thân mình (Self-vote)
            if (localId >= 0 && btn.targetID == localId)
            {
                buttonComp.interactable = false;
            }
            else if (isVotingPhase && localCanVote)
            {
                // 3. Mở khóa nút để cho phép chuyển vote (Revote) trong Phase Voting
                var targetData = pm != null ? pm.GetplayerByID(btn.targetID) : null;
                bool targetAlive = targetData != null ? targetData.isAlive : pm?.IsAlive(btn.targetID) ?? true;

                if (targetAlive)
                {
                    buttonComp.interactable = true;
                }
            }

            // 4. Quản lý màu nút theo chuẩn #35C759
            var img = btn.GetComponent<Image>() ?? buttonComp.targetGraphic as Image;
            if (img == null)
            {
                img = btn.GetComponentInChildren<Image>();
            }

            if (img != null && !defaultImageColors.ContainsKey(img))
            {
                defaultImageColors[img] = img.color;
            }

            if (!defaultButtonColors.ContainsKey(buttonComp))
            {
                defaultButtonColors[buttonComp] = buttonComp.colors;
            }

            bool isThisTargetConfirmed = isVotingPhase && confirmedTarget >= 0 && btn.targetID == confirmedTarget;

            if (isThisTargetConfirmed)
            {
                // Mục tiêu được chọn: Chuyển sang màu xanh lá #35C759
                if (img != null)
                {
                    img.color = VoteConfirmedGreen;
                }

                var cb = buttonComp.colors;
                cb.normalColor = VoteConfirmedGreen;
                cb.highlightedColor = new Color(70f / 255f, 215f / 255f, 105f / 255f, 1f);
                cb.selectedColor = VoteConfirmedGreen;
                buttonComp.colors = cb;
            }
            else
            {
                // Các mục tiêu còn lại hoặc khi chuyển phiếu (Revote): Trở về màu mặc định
                if (img != null && defaultImageColors.TryGetValue(img, out Color defImgCol))
                {
                    img.color = defImgCol;
                }

                if (defaultButtonColors.TryGetValue(buttonComp, out ColorBlock defCb))
                {
                    buttonComp.colors = defCb;
                }
            }
        }
    }

    private void OnVoteButtonClicked(int targetID)
    {
        var online = ThuongOnlineSession.Active;
        int localId = online != null ? online.LocalPlayerId : -1;
        if (localId < 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            localId = (int)NetworkManager.Singleton.LocalClientId;
        }
        if (localId < 0)
        {
            var hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud != null) localId = hud.localVoterID;
        }

        // Chặn tuyệt đối nếu click vào bản thân
        if (localId >= 0 && targetID == localId)
        {
            Debug.LogWarning("[NETWORK VOTE ADAPTER] Bạn không thể tự bỏ phiếu cho chính bản thân mình!");
            return;
        }

        // Kiểm tra xem đã vote người này chưa (tránh spam request trùng)
        int currentChoice = GetConfirmedVotedTarget(localId);
        if (currentChoice == targetID)
        {
            Debug.Log($"[NETWORK VOTE ADAPTER] Bạn đã bỏ phiếu cho Player {targetID + 1} rồi.");
            return;
        }

        // Gửi lệnh Vote / Chuyển vote trực tiếp lên Server:
        // Lưu ý: Không tự ý đổi màu ở đây, chỉ khi Server xác nhận thành công thì LateUpdate mới tô màu xanh lá #35C759
        if (online != null)
        {
            Debug.Log($"[NETWORK VOTE ADAPTER] Gửi yêu cầu vote sang Player {targetID + 1} qua ThuongOnlineSession");
            online.RequestVote(targetID);
            return;
        }

        // Nếu chạy qua hệ thống NetworkPlayerVote của Lợi
        var allVotes = FindObjectsByType<NetworkPlayerVote>(FindObjectsSortMode.None);
        foreach (var v in allVotes)
        {
            if (v != null && (v.IsOwner || (int)v.OwnerClientId == localId))
            {
                Debug.Log($"[NETWORK VOTE ADAPTER] Gửi yêu cầu vote sang Player {targetID} qua NetworkPlayerVote");
                v.VoteFromUI(targetID);
                return;
            }
        }

        // Nếu chạy chế độ offline hoặc tích hợp nội bộ:
        if (VoteManager.Instance != null && localId >= 0)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(localId);
            if (voter != null) voter.hasVoted = false;
            VoteManager.Instance.TryVote(localId, targetID);
        }
    }

    /// <summary>
    /// Lấy mục tiêu bỏ phiếu đã được Server xác nhận cho Local Player (đảm bảo tính riêng tư).
    /// </summary>
    public static int GetConfirmedVotedTarget(int localId)
    {
        // 1. Kiểm tra qua ThuongOnlineSession (dành cho chế độ Online của Thương)
        var online = ThuongOnlineSession.Active;
        if (online != null)
        {
            int target = GetThuongOnlineVotedTarget(online);
            if (target >= 0) return target;
        }

        // 2. Kiểm tra qua NetworkPlayerVote (dành cho chế độ Network của Lợi)
        if (localId >= 0)
        {
            int netTarget = NetworkPlayerVote.GetCurrentChoice(localId);
            if (netTarget >= 0) return netTarget;
        }

        // 3. Kiểm tra qua VoteManager (dành cho Host/Server hoặc local)
        if (localId >= 0 && VoteManager.Instance != null)
        {
            int vmTarget = VoteManager.Instance.GetVotedTarget(localId);
            if (vmTarget >= 0) return vmTarget;
        }

        return -1;
    }

    private static int GetThuongOnlineVotedTarget(ThuongOnlineSession session)
    {
        if (session == null) return -1;
        try
        {
            if (thuongOnlineVotedTargetField == null)
            {
                thuongOnlineVotedTargetField = typeof(ThuongOnlineSession).GetField("localVotedTarget",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            if (thuongOnlineVotedTargetField != null)
            {
                return (int)thuongOnlineVotedTargetField.GetValue(session);
            }
        }
        catch
        {
            // Bỏ qua lỗi nếu phiên bản không có field này
        }
        return -1;
    }
}
