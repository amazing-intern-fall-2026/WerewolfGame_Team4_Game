using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerVote : NetworkBehaviour
{
    public static event System.Action<int, int> OnVoteCountChanged;
    public static event System.Action<int, int> OnLocalVoteConfirmed;
    public static event System.Action OnVotesReset;

    private static readonly Dictionary<int, int> currentChoices = new Dictionary<int, int>();
    private static readonly HashSet<int> votedVoters = new HashSet<int>();
    private static bool isEndingVoting = false;

    public static bool HasLocalPlayerVoted(int localId)
    {
        return votedVoters.Contains(localId) || currentChoices.ContainsKey(localId);
    }

    public static int GetCurrentChoice(int localId)
    {
        return currentChoices.TryGetValue(localId, out int target) ? target : -1;
    }

    private void Awake()
    {
        ResetLocalStaticVotes();
    }

    public override void OnNetworkSpawn()
    {
        ResetLocalStaticVotes();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        if (NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Voting)
            return;

        // Không cho Player chết điều khiển Vote
        if (!IsLocalPlayerAlive())
            return;
    }

    public void VoteFromUI(int targetID)
    {
        if (!IsOwner)
            return;

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning("NETWORK VOTE UI: Không tìm thấy NetworkPhaseSync!");
            return;
        }

        if (NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Voting)
        {
            Debug.LogWarning("NETWORK VOTE UI: Chưa phải Voting!");
            return;
        }

        if (!IsLocalPlayerAlive())
        {
            Debug.LogWarning("NETWORK VOTE UI: Player đã chết, không thể Vote!");
            return;
        }

        SendVote(targetID);
    }

    private bool IsLocalPlayerAlive()
    {
        NetworkPlayerStateSync stateSync = GetComponent<NetworkPlayerStateSync>();
        if (stateSync == null)
        {
            Debug.LogWarning("NETWORK VOTE: Không tìm thấy NetworkPlayerStateSync!");
            return false;
        }

        return stateSync.State.Value == NetworkPlayerStateType.Alive;
    }

    private void SendVote(int targetID)
    {
        if (!IsLocalPlayerAlive())
        {
            Debug.LogWarning("NETWORK VOTE: Player đã chết, không gửi Vote.");
            return;
        }

        Debug.Log($"VOTE | Client {OwnerClientId} → Player {targetID}");
        RequestVoteServerRpc(targetID);
    }

    [ServerRpc]
    private void RequestVoteServerRpc(int targetID, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int voterID = (int)senderClientId;

        Debug.Log($"NETWORK VOTE | Server nhận: Player {voterID} → Player {targetID}");

        // =========================
        // 1. CHECK PHASE
        // =========================
        if (NetworkPhaseSync.Instance == null || NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Voting)
        {
            Debug.LogWarning($"NETWORK VOTE: Từ chối phiếu vì phase hiện tại không phải Voting ({NetworkPhaseSync.Instance?.CurrentPhase.Value})");
            return;
        }

        // =========================
        // 2. CHECK ĐỔI PHIẾU HOẶC BỎ PHIẾU MỚI
        // =========================
        bool isChangeVote = currentChoices.TryGetValue(voterID, out int oldTarget);
        if (isChangeVote && oldTarget == targetID)
        {
            Debug.Log($"NETWORK VOTE: Player {voterID} đã chọn Target {targetID} rồi, giữ nguyên.");
            return;
        }

        // =========================
        // 3. CHECK VOTER TRẠNG THÁI MẠNG
        // =========================
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out NetworkClient voterClient))
        {
            Debug.LogWarning("NETWORK VOTE: Không tìm thấy Client của voter!");
            return;
        }

        if (voterClient.PlayerObject == null)
        {
            Debug.LogWarning("NETWORK VOTE: PlayerObject của voter NULL!");
            return;
        }

        NetworkPlayerStateSync voterState = voterClient.PlayerObject.GetComponent<NetworkPlayerStateSync>();
        if (voterState == null || voterState.State.Value != NetworkPlayerStateType.Alive)
        {
            Debug.LogWarning($"NETWORK VOTE: Player {voterID} đã chết hoặc không có trạng thái Alive → TỪ CHỐI VOTE!");
            return;
        }

        // =========================
        // 4. CHECK KHÔNG VOTE CHÍNH MÌNH (Cho phép khi test 1 người chơi)
        // =========================
        int eligibleCount = GetEligibleVoterCount();
        if (voterID == targetID && eligibleCount > 1)
        {
            Debug.LogWarning("NETWORK VOTE: Không thể vote chính mình trong phòng nhiều người!");
            return;
        }

        // =========================
        // 5. CHECK TARGET TRẠNG THÁI MẠNG (DISCONNECT & ALIVE)
        // =========================
        if (NetworkGameManager.IsPlayerLeft((ulong)targetID))
        {
            Debug.LogWarning($"NETWORK VOTE: Target {targetID} đã disconnect khỏi phòng → TỪ CHỐI VOTE!");
            return;
        }

        bool targetConnected = NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)targetID);
        if (!targetConnected)
        {
            Debug.LogWarning($"NETWORK VOTE: Target {targetID} không còn trong ConnectedClients → TỪ CHỐI VOTE!");
            return;
        }

        // Tìm PlayerController của target để check NetworkPlayerStateSync
        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController targetController = null;
        foreach (var pc in controllers)
        {
            if (pc != null && (int)pc.OwnerClientId == targetID)
            {
                targetController = pc;
                break;
            }
        }

        if (targetController != null)
        {
            var targetStateSync = targetController.GetComponent<NetworkPlayerStateSync>();
            if (targetStateSync != null && targetStateSync.State.Value != NetworkPlayerStateType.Alive)
            {
                Debug.LogWarning($"NETWORK VOTE: Target {targetID} đã chết trên mạng → TỪ CHỐI VOTE!");
                return;
            }
        }

        // =========================
        // 6. CHECK PLAYER DATA DEV2 (NẾU CÓ)
        // =========================
        if (PlayerManager.Instance != null)
        {
            PlayerData voter = PlayerManager.Instance.GetplayerByID(voterID);
            PlayerData target = PlayerManager.Instance.GetplayerByID(targetID);

            if (voter != null && (!voter.isAlive || !voter.CanVote))
            {
                Debug.LogWarning($"NETWORK VOTE: PlayerData xác nhận Voter {voterID} không thể vote → TỪ CHỐI VOTE!");
                return;
            }

            if (target != null && !target.isAlive)
            {
                Debug.LogWarning($"NETWORK VOTE: PlayerData xác nhận Target {targetID} đã chết → TỪ CHỐI VOTE!");
                return;
            }
        }

        // =========================
        // 7. GHI NHẬN PHIẾU
        // =========================
        if (!RecordVote(voterID, targetID))
            return;

        // Gửi xác nhận về cho Voter
        SendVoteConfirmedClientRpc(voterID, targetID);

        // Kiểm tra xem tất cả người đủ điều kiện đã bỏ phiếu chưa
        CheckAndResolveIfAllVoted();
    }

    public static bool RecordVote(int voterID, int targetID)
    {
        int eligibleCount = GetEligibleVoterCount();
        if (voterID == targetID && eligibleCount > 1)
        {
            Debug.LogWarning("NETWORK VOTE: Không thể vote chính mình trong phòng nhiều người!");
            return false;
        }

        bool isChanging = currentChoices.TryGetValue(voterID, out int previousTarget);
        if (isChanging && previousTarget == targetID)
        {
            Debug.Log($"NETWORK VOTE: Player {voterID} đã chọn Target {targetID} rồi, giữ nguyên.");
            return true;
        }

        currentChoices[voterID] = targetID;
        votedVoters.Add(voterID);

        if (isChanging)
        {
            Debug.Log($"NETWORK VOTE: Server ghi nhận ĐỔI PHIẾU: Voter {voterID} ({previousTarget} -> {targetID})");
        }
        else
        {
            Debug.Log($"NETWORK VOTE: Server ghi nhận phiếu thành công: Voter {voterID} -> Target {targetID}");
        }

        if (VoteManager.Instance != null && PlayerManager.Instance != null)
        {
            var voter = PlayerManager.Instance.GetplayerByID(voterID);
            if (voter != null)
            {
                voter.hasVoted = false; // Reset cờ để VoteManager.TryVote ghi nhận mục tiêu mới khi đổi vote
            }
            VoteManager.Instance.TryVote(voterID, targetID);
        }
        else if (VoteManager.Instance != null)
        {
            VoteManager.Instance.TryVote(voterID, targetID);
        }

        SyncVoteCounts();
        return true;
    }

    public static int GetVoteCountFor(int targetID)
    {
        int count = 0;
        foreach (var choice in currentChoices)
        {
            if (choice.Value == targetID) count++;
        }
        return count;
    }

    [ClientRpc]
    private void SendVoteConfirmedClientRpc(int voterId, int targetId)
    {
        if (NetworkManager.Singleton != null && (int)NetworkManager.Singleton.LocalClientId == voterId)
        {
            Debug.Log($"NETWORK VOTE: Client nhận xác nhận phiếu bầu thành công cho Target {targetId}");
            votedVoters.Add(voterId);
            currentChoices[voterId] = targetId;
            OnLocalVoteConfirmed?.Invoke(voterId, targetId);
        }
    }

    public static void CheckAndResolveIfAllVoted()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (NetworkPhaseSync.Instance == null || NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Voting)
            return;

        if (isEndingVoting)
            return;

        // Khi test 1 người chơi, không kết thúc ngay để cho phép người chơi test đổi vote
        if (GetEligibleVoterCount() <= 1)
            return;

        if (HasAllEligiblePlayersVoted())
        {
            Debug.Log("NETWORK VOTE: Tất cả người chơi đủ điều kiện đã bỏ phiếu -> Kết thúc Voting sớm.");
            TriggerFinishVotingOnServer();
        }
    }

    public static int GetEligibleVoterCount()
    {
        int eligibleVotersCount = 0;
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null && PlayerManager.Instance.players.Count > 0)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null && p.isAlive && !p.isDisconnected && !NetworkGameManager.IsPlayerLeft((ulong)p.playerID))
                    eligibleVotersCount++;
            }
        }
        else
        {
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p == null) continue;
                int pId = (int)p.OwnerClientId;
                if (NetworkGameManager.IsPlayerLeft((ulong)pId)) continue;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients != null && !NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)pId)) continue;

                var state = p.GetComponent<NetworkPlayerStateSync>();
                if (state != null && state.State.Value == NetworkPlayerStateType.Alive)
                {
                    eligibleVotersCount++;
                }
            }
        }
        return eligibleVotersCount;
    }

    public static bool HasAllEligiblePlayersVoted()
    {
        int eligibleVotersCount = GetEligibleVoterCount();
        int votesCount = currentChoices.Count;
        Debug.Log($"NETWORK VOTE CHECK | Đã vote: {votesCount}/{eligibleVotersCount} người đủ điều kiện.");

        return eligibleVotersCount > 0 && votesCount >= eligibleVotersCount;
    }

    public static bool TriggerFinishVotingOnServer()
    {
        if (isEndingVoting)
            return false;

        isEndingVoting = true;

        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentState == GameState.Voting)
        {
            Debug.Log("NETWORK VOTE: GameRoleManager.FinishVoting() được gọi từ hệ thống Voting.");
            GameRoleManager.Instance.FinishVoting();
        }
        else
        {
            Debug.LogWarning("NETWORK VOTE: Không thể gọi FinishVoting vì GameRoleManager không ở phase Voting.");
        }
        return true;
    }

    private static void SyncVoteCounts()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
            return;

        // Đếm vote cho TẤT CẢ player
        Dictionary<int, int> voteCounts = new Dictionary<int, int>();
        foreach (PlayerController player in players)
        {
            if (player == null) continue;
            int playerID = (int)player.OwnerClientId;
            voteCounts[playerID] = 0;
        }

        // Cộng vote hiện tại
        foreach (KeyValuePair<int, int> choice in currentChoices)
        {
            int targetID = choice.Value;
            if (!voteCounts.ContainsKey(targetID))
                voteCounts[targetID] = 0;

            voteCounts[targetID]++;
        }

        int[] targetIDs = new int[voteCounts.Count];
        int[] counts = new int[voteCounts.Count];
        int index = 0;

        foreach (KeyValuePair<int, int> vote in voteCounts)
        {
            targetIDs[index] = vote.Key;
            counts[index] = vote.Value;
            index++;
        }

        NetworkPlayerVote[] allVotes = FindObjectsByType<NetworkPlayerVote>(FindObjectsSortMode.None);
        if (allVotes != null && allVotes.Length > 0)
        {
            NetworkPlayerVote sender = allVotes[0];
            if (sender != null)
            {
                sender.SendVoteCountsClientRpc(targetIDs, counts);
            }
        }
    }

    [ClientRpc]
    private void SendVoteCountsClientRpc(int[] targetIDs, int[] counts)
    {
        if (targetIDs == null || counts == null)
            return;

        if (targetIDs.Length != counts.Length)
            return;

        for (int i = 0; i < targetIDs.Length; i++)
        {
            OnVoteCountChanged?.Invoke(targetIDs[i], counts[i]);
        }
    }

    public static void ResetLocalStaticVotes()
    {
        currentChoices.Clear();
        votedVoters.Clear();
        isEndingVoting = false;
        Debug.Log("NETWORK VOTE: Đã reset local static votes.");
    }

    public static void ResetNetworkVotes()
    {
        ResetLocalStaticVotes();

        Debug.Log("NETWORK VOTE: Đã reset toàn bộ lựa chọn vote cho phase mới.");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkPlayerVote[] allVotes = FindObjectsByType<NetworkPlayerVote>(FindObjectsSortMode.None);
            if (allVotes != null && allVotes.Length > 0)
            {
                allVotes[0].NotifyVotesResetClientRpc();
            }
        }
    }

    [ClientRpc]
    private void NotifyVotesResetClientRpc()
    {
        ResetLocalStaticVotes();
        OnVotesReset?.Invoke();
        Debug.Log("NETWORK VOTE: Client nhận thông báo Reset lượt vote.");
    }

    public static void RemoveDisconnectedPlayerVotes(int disconnectedPlayerId)
    {
        // 1. Xóa lựa chọn vote của voter đã disconnect
        if (currentChoices.ContainsKey(disconnectedPlayerId))
        {
            currentChoices.Remove(disconnectedPlayerId);
        }
        votedVoters.Remove(disconnectedPlayerId);

        // 2. Xóa vote của người khác bầu cho người đã disconnect (để họ có thể bầu lại người sống)
        List<int> votersToReset = new List<int>();
        foreach (var kvp in currentChoices)
        {
            if (kvp.Value == disconnectedPlayerId)
            {
                votersToReset.Add(kvp.Key);
            }
        }
        foreach (int voter in votersToReset)
        {
            currentChoices.Remove(voter);
            votedVoters.Remove(voter);
        }

        // 3. Nếu là Server, đồng bộ lại danh sách vote và kiểm tra điều kiện kết thúc
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SyncVoteCounts();
            CheckAndResolveIfAllVoted();
        }

        OnVoteCountChanged?.Invoke(disconnectedPlayerId, 0);
        Debug.Log($"NETWORK VOTE: Đã xóa toàn bộ vote liên quan đến Player {disconnectedPlayerId}");
    }
}