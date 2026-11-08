using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Collections;
using Thuong.Online;

/// <summary>
/// Adapter tích hợp từ hệ thống Network của Lợi sang hệ thống Online của Thương.
/// Mục đích:
/// 1. Tích hợp chức năng hiển thị người chơi đang được vote (chuyển sang màu xanh lá #35C759).
/// 2. ĐỒNG BỘ MÀU VOTE CHO TẤT CẢ (HOST LẪN CLIENT ĐỀU THẤY ĐỔI MÀU REAL-TIME).
/// 3. Hỗ trợ Chuyển Vote (Revote) nhiều lần trong thời gian Voting (khôi phục màu thẻ cũ, tô xanh thẻ mới).
/// 4. Server Authoritative: Chỉ đổi màu khi Server đã xác nhận vote thành công.
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

    private const string VoteSyncMessage = "loi.online.vote_sync.v1";
    private const string VoteRequestMessage = "loi.online.vote_request.v1";

    private readonly HashSet<VoteButton> hookedButtons = new HashSet<VoteButton>();
    private readonly Dictionary<Image, Color> defaultImageColors = new Dictionary<Image, Color>();
    private readonly Dictionary<Button, ColorBlock> defaultButtonColors = new Dictionary<Button, ColorBlock>();

    // Bộ nhớ vote đồng bộ giữa Server và tất cả Client (voterId -> targetId)
    private static readonly Dictionary<int, int> serverVoteChoices = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> syncedVoteChoices = new Dictionary<int, int>();

    private static System.Reflection.FieldInfo thuongOnlineVotedTargetField;
    private bool isNetworkRegistered = false;
    private int lastBroadcastHash = 0;

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
        syncedVoteChoices.Clear();
        serverVoteChoices.Clear();
        lastBroadcastHash = 0;
    }

    private void OnDestroy()
    {
        UnregisterNetworkHandlers();
    }

    private void EnsureNetworkHandlers()
    {
        if (isNetworkRegistered) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            var messaging = NetworkManager.Singleton.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(VoteSyncMessage, OnReceiveVoteSync);
            messaging.RegisterNamedMessageHandler(VoteRequestMessage, OnReceiveVoteRequest);
            isNetworkRegistered = true;
        }
    }

    private void UnregisterNetworkHandlers()
    {
        if (!isNetworkRegistered) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            var messaging = NetworkManager.Singleton.CustomMessagingManager;
            messaging.UnregisterNamedMessageHandler(VoteSyncMessage);
            messaging.UnregisterNamedMessageHandler(VoteRequestMessage);
        }
        isNetworkRegistered = false;
    }

    private void Update()
    {
        EnsureNetworkHandlers();

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
        var online = ThuongOnlineSession.Active;
        var game = GameRoleManager.Instance;
        var hud = Object.FindAnyObjectByType<GameHUD>();

        bool isVotingPhase = CheckIsVotingPhase(online, game, hud);

        if (!isVotingPhase)
        {
            // Nếu hết giờ hoặc chuyển phase: Xóa bảng vote và gửi thông báo dọn dẹp màu cho tất cả Client
            if (serverVoteChoices.Count > 0)
            {
                serverVoteChoices.Clear();
                BroadcastVoteSyncToAllClients();
            }
            return;
        }

        var pm = PlayerManager.Instance;
        if (pm == null || pm.players == null) return;

        bool choicesChanged = false;

        // Trong thời gian Voting, mở cờ hasVoted = false trên Server để:
        // 1. Cho phép VoteManager.ValidateVote và TryVote ghi nhận mục tiêu mới khi người chơi chuyển phiếu (Revote).
        // 2. Không cho phép cờ privateFlags |= 2 khóa các nút vote ở Client.
        foreach (var player in pm.players)
        {
            if (player == null || !player.isAlive) continue;

            // Kiểm tra lựa chọn hiện tại trong VoteManager của Thương
            if (VoteManager.Instance != null)
            {
                int currentChoice = VoteManager.Instance.GetVotedTarget(player.playerID);
                if (currentChoice >= 0)
                {
                    if (currentChoice == player.playerID && pm.players.Count > 1)
                    {
                        // Phát hiện tự vote bản thân -> Loại bỏ lựa chọn này khỏi VoteManager
                        Debug.LogWarning($"[NETWORK VOTE ADAPTER] Chặn tự vote bản thân: Player {player.playerID + 1} vote chính mình!");
                        player.hasVoted = false;
                        if (serverVoteChoices.Remove(player.playerID))
                        {
                            choicesChanged = true;
                        }
                    }
                    else
                    {
                        if (!serverVoteChoices.TryGetValue(player.playerID, out int prev) || prev != currentChoice)
                        {
                            serverVoteChoices[player.playerID] = currentChoice;
                            choicesChanged = true;
                        }
                    }
                }
            }

            // Giữ hasVoted = false trong suốt Phase Voting để hỗ trợ đổi phiếu liên tục
            player.hasVoted = false;
        }

        if (choicesChanged)
        {
            BroadcastVoteSyncToAllClients();
        }
    }

    private void BroadcastVoteSyncToAllClients()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.CustomMessagingManager == null) return;

        // Cập nhật local syncedVoteChoices trên Host
        syncedVoteChoices.Clear();
        foreach (var kvp in serverVoteChoices)
        {
            syncedVoteChoices[kvp.Key] = kvp.Value;
        }

        using var writer = new FastBufferWriter(1024, Allocator.Temp);
        writer.WriteValueSafe((ushort)serverVoteChoices.Count);
        foreach (var kvp in serverVoteChoices)
        {
            writer.WriteValueSafe(kvp.Key);
            writer.WriteValueSafe(kvp.Value);
        }

        var clientIds = NetworkManager.Singleton.ConnectedClientsIds;
        if (clientIds != null && clientIds.Count > 0)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                VoteSyncMessage,
                clientIds,
                writer,
                NetworkDelivery.ReliableSequenced
            );
        }
    }

    private void OnReceiveVoteSync(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ushort count);
        syncedVoteChoices.Clear();
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out int voterId);
            reader.ReadValueSafe(out int targetId);
            syncedVoteChoices[voterId] = targetId;
        }
    }

    private void OnReceiveVoteRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        reader.ReadValueSafe(out int voterId);
        reader.ReadValueSafe(out int targetId);

        Debug.Log($"[NETWORK VOTE ADAPTER] Server nhận VoteRequest từ Client {senderClientId}: Voter {voterId} -> Target {targetId}");

        if (voterId == targetId)
        {
            Debug.LogWarning("[NETWORK VOTE ADAPTER] Từ chối: Voter tự vote bản thân.");
            return;
        }

        RecordAndBroadcastVoteOnServer(voterId, targetId);
    }

    private void RecordAndBroadcastVoteOnServer(int voterId, int targetId)
    {
        // 1. Ghi nhận vào VoteManager của Thương
        if (VoteManager.Instance != null)
        {
            var pm = PlayerManager.Instance;
            var voter = pm != null ? pm.GetplayerByID(voterId) : null;
            if (voter != null) voter.hasVoted = false;
            VoteManager.Instance.TryVote(voterId, targetId);
        }

        // 2. Ghi nhận vào serverVoteChoices
        serverVoteChoices[voterId] = targetId;

        // 3. Đồng bộ vào NetworkPlayerVote nếu có
        NetworkPlayerVote.RecordVote(voterId, targetId);

        // 4. Phát tán bảng vote mới nhất cho TẤT CẢ Client
        BroadcastVoteSyncToAllClients();
    }

    private void LateUpdate()
    {
        // =========================================================================
        // CLIENT UI: ĐỒNG BỘ MÀU XANH LÁ #35C759 CHO CẢ HOST VÀ CLIENT (TẤT CẢ ĐỀU THẤY)
        // =========================================================================
        var online = ThuongOnlineSession.Active;
        var game = GameRoleManager.Instance;
        var hud = Object.FindAnyObjectByType<GameHUD>();

        // Kiểm tra Phase Voting toàn diện (hoạt động chính xác trên cả Host và Client)
        bool isVotingPhase = CheckIsVotingPhase(online, game, hud);

        // Tìm tất cả VoteButton đang có trên UI của Thương (kể cả trong ThuongMeetingRosterUI)
        var voteButtons = Object.FindObjectsByType<VoteButton>(FindObjectsSortMode.None);
        if (voteButtons == null || voteButtons.Length == 0)
        {
            return;
        }

        // Nếu KHÔNG phải phase Voting: Khôi phục tất cả nút về màu mặc định
        if (!isVotingPhase)
        {
            RestoreAllDefaultColors(voteButtons);
            return;
        }

        int localId = online != null ? online.LocalPlayerId : -1;
        if (localId < 0 && hud != null)
        {
            localId = hud.localVoterID;
        }
        if (localId < 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            localId = (int)NetworkManager.Singleton.LocalClientId;
        }

        var pm = PlayerManager.Instance;
        var localData = localId >= 0 && pm != null ? pm.GetplayerByID(localId) : null;
        bool localCanVote = localData != null && localData.isAlive && localData.CanVote;

        // Lấy mục tiêu mà chính Local Player đã vote (đảm bảo tính riêng tư: chỉ người chơi đó thấy)
        int myVotedTarget = -1;
        if (localId >= 0 && syncedVoteChoices.TryGetValue(localId, out int choiceTarget))
        {
            myVotedTarget = choiceTarget;
        }
        if (myVotedTarget < 0)
        {
            myVotedTarget = GetConfirmedVotedTarget(localId);
        }

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
            else if (isVotingPhase && (localCanVote || localData == null))
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

            // BẢO MẬT RIÊNG TƯ: Màu xanh #35C759 CHỈ hiển thị ở phía người chơi đang vote,
            // TUYỆT ĐỐI KHÔNG hiển thị lựa chọn của người chơi khác!
            bool shouldHighlight = isVotingPhase && myVotedTarget >= 0 && btn.targetID == myVotedTarget;

            if (shouldHighlight)
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

    private void RestoreAllDefaultColors(VoteButton[] voteButtons)
    {
        if (voteButtons == null) return;
        foreach (var btn in voteButtons)
        {
            if (btn == null) continue;
            var buttonComp = btn.GetComponent<Button>();
            var img = btn.GetComponent<Image>() ?? buttonComp?.targetGraphic as Image;
            if (img == null && buttonComp != null) img = btn.GetComponentInChildren<Image>();

            if (img != null && defaultImageColors.TryGetValue(img, out Color defCol))
            {
                img.color = defCol;
            }
            if (buttonComp != null && defaultButtonColors.TryGetValue(buttonComp, out ColorBlock defCb))
            {
                buttonComp.colors = defCb;
            }
        }
    }

    private void OnVoteButtonClicked(int targetID)
    {
        var online = ThuongOnlineSession.Active;
        var hud = Object.FindAnyObjectByType<GameHUD>();

        int localId = online != null ? online.LocalPlayerId : -1;
        if (localId < 0 && hud != null) localId = hud.localVoterID;
        if (localId < 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            localId = (int)NetworkManager.Singleton.LocalClientId;
        }

        // Chặn tuyệt đối nếu click vào bản thân
        if (localId >= 0 && targetID == localId)
        {
            Debug.LogWarning("[NETWORK VOTE ADAPTER] Bạn không thể tự bỏ phiếu cho chính bản thân mình!");
            return;
        }

        Debug.Log($"[NETWORK VOTE ADAPTER] Player {localId + 1} bấm vote cho Player {targetID + 1}");

        // 1. Gửi qua hệ thống ThuongOnlineSession
        if (online != null)
        {
            online.RequestVote(targetID);
        }

        // 2. Gửi qua kênh Network đồng bộ riêng của Adapter tới Server để đảm bảo chắc chắn Server nhận được
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                using var writer = new FastBufferWriter(32, Allocator.Temp);
                writer.WriteValueSafe(localId);
                writer.WriteValueSafe(targetID);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    VoteRequestMessage,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced
                );
            }
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            RecordAndBroadcastVoteOnServer(localId, targetID);
        }

        // 3. Nếu chạy qua hệ thống NetworkPlayerVote của Lợi
        var allVotes = FindObjectsByType<NetworkPlayerVote>(FindObjectsSortMode.None);
        foreach (var v in allVotes)
        {
            if (v != null && (v.IsOwner || (int)v.OwnerClientId == localId))
            {
                v.VoteFromUI(targetID);
                break;
            }
        }

        // 4. Nếu chạy chế độ offline hoặc tích hợp nội bộ
        if (VoteManager.Instance != null && localId >= 0)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(localId);
            if (voter != null) voter.hasVoted = false;
            VoteManager.Instance.TryVote(localId, targetID);
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái Voting Phase trên cả Host và Client một cách toàn diện
    /// </summary>
    private static bool CheckIsVotingPhase(ThuongOnlineSession online, GameRoleManager game, GameHUD hud)
    {
        // 1. Kiểm tra qua ThuongOnlineSession (chính xác trên cả Host và Client)
        if (online != null && online.VisibleState == GameState.Voting)
            return true;

        // 2. Kiểm tra qua GameHUD của Thương (meetingPanel hiển thị tiêu đề "BỎ PHIẾU")
        if (hud != null && hud.meetingPanel != null && hud.meetingPanel.activeSelf)
        {
            if (hud.meetingTitle != null && hud.meetingTitle.text == "BỎ PHIẾU")
                return true;
        }

        // 3. Kiểm tra qua GameRoleManager
        if (game != null && (game.currentState == GameState.Voting ||
            (ThuongLogicSceneScope.IsActive && game.currentPhase == GamePhase.Voting)))
            return true;

        // 4. Kiểm tra qua NetworkPhaseSync (Lợi network system)
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentPhase.Value == GamePhase.Voting)
            return true;

        return false;
    }

    /// <summary>
    /// Lấy mục tiêu bỏ phiếu đã được Server xác nhận cho Local Player.
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

        // 2. Kiểm tra qua GameHUD hint text
        var hud = Object.FindAnyObjectByType<GameHUD>();
        if (hud != null && hud.votingStatusText != null && !string.IsNullOrEmpty(hud.votingStatusText.text))
        {
            string txt = hud.votingStatusText.text;
            int idx = txt.IndexOf("Đã chọn P");
            if (idx >= 0 && idx + 11 <= txt.Length)
            {
                string numStr = txt.Substring(idx + 9, 2);
                if (int.TryParse(numStr, out int pNum) && pNum > 0)
                {
                    return pNum - 1;
                }
            }
        }

        // 3. Kiểm tra qua NetworkPlayerVote (dành cho chế độ Network của Lợi)
        if (localId >= 0)
        {
            int netTarget = NetworkPlayerVote.GetCurrentChoice(localId);
            if (netTarget >= 0) return netTarget;
        }

        // 4. Kiểm tra qua VoteManager (dành cho Host/Server hoặc local)
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
