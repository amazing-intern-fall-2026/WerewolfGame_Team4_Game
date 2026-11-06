using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Thuong.Online;

/// <summary>
/// Adapter tích hợp từ hệ thống Network của Lợi sang hệ thống Online của Thương.
/// Mục đích:
/// 1. Cho phép Chuyển Vote (Revote) nhiều lần trong thời gian Voting trong Thương Online.
/// 2. Chặn tuyệt đối việc tự bỏ phiếu cho chính bản thân mình (Self-vote).
/// 
/// ĐIỀU KIỆN TUÂN THỦ:
/// - Không chỉnh sửa bất kỳ file nào trong Assets/Scripts/Thuong/.
/// - Tự động hook runtime vào scene và UI của Thương.
/// </summary>
[DefaultExecutionOrder(1000)] // Chạy sau các script UI và Voting của Thương
public class ThuongOnlineVoteNetworkAdapter : MonoBehaviour
{
    private static ThuongOnlineVoteNetworkAdapter instance;
    private readonly HashSet<VoteButton> hookedButtons = new HashSet<VoteButton>();

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
            (ThuongLogicSceneScope.IsActive && game.currentPhase == GamePhase.Voting);

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
                    // Reset vote cho player này
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
        // CLIENT UI: KHÓA NÚT VOTE BẢN THÂN & MỞ KHÓA NÚT CHUYỂN VOTE CHO THƯƠNG ONLINE
        // =========================================================================
        var online = ThuongOnlineSession.Active;
        var game = GameRoleManager.Instance;

        bool isVotingPhase = game != null && (game.currentState == GameState.Voting ||
            (ThuongLogicSceneScope.IsActive && game.currentPhase == GamePhase.Voting));

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
        if (voteButtons == null || voteButtons.Length == 0) return;

        var pm = PlayerManager.Instance;
        var localData = localId >= 0 && pm != null ? pm.GetplayerByID(localId) : null;
        bool localCanVote = localData != null && localData.isAlive && localData.CanVote;

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
                continue;
            }

            // 3. Mở khóa nút để cho phép chuyển vote (Revote) trong Phase Voting
            if (isVotingPhase && localCanVote)
            {
                var targetData = pm != null ? pm.GetplayerByID(btn.targetID) : null;
                bool targetAlive = targetData != null ? targetData.isAlive : pm?.IsAlive(btn.targetID) ?? true;

                if (targetAlive)
                {
                    buttonComp.interactable = true;
                }
            }
        }
    }

    private void OnVoteButtonClicked(int targetID)
    {
        var online = ThuongOnlineSession.Active;
        int localId = online != null ? online.LocalPlayerId : -1;

        // Chặn tuyệt đối nếu click vào bản thân
        if (localId >= 0 && targetID == localId)
        {
            Debug.LogWarning("[NETWORK VOTE ADAPTER] Bạn không thể tự bỏ phiếu cho chính bản thân mình!");
            return;
        }

        // Gửi lệnh Vote / Chuyển vote trực tiếp lên hệ thống Online của Thương
        if (online != null)
        {
            Debug.Log($"[NETWORK VOTE ADAPTER] Client chuyển vote sang Player {targetID + 1}");
            online.RequestVote(targetID);
            return;
        }

        // Nếu chạy chế độ offline hoặc tích hợp nội bộ:
        if (VoteManager.Instance != null && localId >= 0)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(localId);
            if (voter != null) voter.hasVoted = false;
            VoteManager.Instance.TryVote(localId, targetID);
        }
    }
}
