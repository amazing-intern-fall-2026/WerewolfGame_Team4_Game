using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerVote : NetworkBehaviour
{
    // Server lưu:
    // VoterID -> TargetID
    private static readonly Dictionary<int, int> currentChoices =
        new Dictionary<int, int>();

    private void Update()
    {
        if (!IsOwner)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Voting
        )
            return;

        // TEST KEY
        if (Input.GetKeyDown(KeyCode.Alpha6))
            SendVote(0);

        if (Input.GetKeyDown(KeyCode.Alpha7))
            SendVote(1);

        if (Input.GetKeyDown(KeyCode.Alpha8))
            SendVote(2);

        if (Input.GetKeyDown(KeyCode.Alpha9))
            SendVote(3);

        if (Input.GetKeyDown(KeyCode.Alpha0))
            SendVote(4);
    }

    // =========================
    // UI GỌI HÀM NÀY
    // =========================

    public void VoteFromUI(int targetID)
    {
        if (!IsOwner)
            return;

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE UI: Không tìm thấy NetworkPhaseSync!"
            );

            return;
        }

        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Voting
        )
        {
            Debug.LogWarning(
                "NETWORK VOTE UI: Chưa phải Voting!"
            );

            return;
        }

        SendVote(targetID);
    }

    // =========================
    // GỬI VOTE
    // =========================

    private void SendVote(int targetID)
    {
        Debug.Log(
            "VOTE | Client "
            + OwnerClientId
            + " → Player "
            + targetID
        );

        RequestVoteServerRpc(targetID);
    }

    // =========================
    // SERVER NHẬN VOTE
    // =========================

    [ServerRpc]
    private void RequestVoteServerRpc(
        int targetID,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        int voterID =
            (int)senderClientId;

        Debug.Log(
            "NETWORK VOTE | Server nhận:"
            + " Player "
            + voterID
            + " → Player "
            + targetID
        );

        // =========================
        // CHECK NETWORK PHASE
        // =========================

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy NetworkPhaseSync!"
            );

            return;
        }

        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Voting
        )
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không phải Voting!"
            );

            return;
        }

        // =========================
        // CHECK PLAYER
        // =========================

        if (PlayerManager.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy PlayerManager!"
            );

            return;
        }

        PlayerData voter =
            PlayerManager.Instance.GetplayerByID(
                voterID
            );

        PlayerData target =
            PlayerManager.Instance.GetplayerByID(
                targetID
            );

        if (voter == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy Voter "
                + voterID
            );

            return;
        }

        if (target == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy Target "
                + targetID
            );

            return;
        }

        if (!voter.isAlive)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Voter đã chết!"
            );

            return;
        }

        if (!target.isAlive)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Target đã chết!"
            );

            return;
        }

        if (voterID == targetID)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không thể vote chính mình!"
            );

            return;
        }

        // =========================
        // LƯU / ĐỔI LỰA CHỌN
        // =========================

        if (
            currentChoices.TryGetValue(
                voterID,
                out int oldTargetID
            )
        )
        {
            if (oldTargetID == targetID)
            {
                Debug.Log(
                    "NETWORK VOTE: Player "
                    + voterID
                    + " đã vote Player "
                    + targetID
                );

                return;
            }

            Debug.Log(
                "NETWORK VOTE: Player "
                + voterID
                + " ĐỔI VOTE "
                + oldTargetID
                + " → "
                + targetID
            );

            currentChoices[voterID] =
                targetID;
        }
        else
        {
            currentChoices.Add(
                voterID,
                targetID
            );

            Debug.Log(
                "NETWORK VOTE: Player "
                + voterID
                + " vote lần đầu → Player "
                + targetID
            );
        }

        // =========================
        // ĐỒNG BỘ LẠI VOTE DEV2
        // =========================

        RebuildDev2Votes();
    }

    // =========================
    // REPLAY TOÀN BỘ VOTE
    // =========================

    private void RebuildDev2Votes()
    {
        if (VoteManager.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy VoteManager!"
            );

            return;
        }

        if (PlayerManager.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy PlayerManager!"
            );

            return;
        }

        Debug.Log(
            "NETWORK VOTE: Đang rebuild toàn bộ vote..."
        );

        // Reset hệ thống vote của Dev2
        VoteManager.Instance.StartVote();

        // Replay toàn bộ lựa chọn hiện tại
        foreach (
            KeyValuePair<int, int> choice
            in currentChoices
        )
        {
            int voterID =
                choice.Key;

            int targetID =
                choice.Value;

            bool success =
                VoteManager.Instance.TryVote(
                    voterID,
                    targetID
                );

            Debug.Log(
                "NETWORK VOTE REBUILD | Player "
                + voterID
                + " → Player "
                + targetID
                + " | Success = "
                + success
            );
        }

        Debug.Log(
            "NETWORK VOTE: Rebuild vote hoàn tất."
        );
    }

    // =========================
    // RESET KHI VOTING MỚI
    // =========================

    public static void ResetNetworkVotes()
    {
        currentChoices.Clear();

        Debug.Log(
            "NETWORK VOTE: Đã reset lựa chọn Network."
        );
    }
}