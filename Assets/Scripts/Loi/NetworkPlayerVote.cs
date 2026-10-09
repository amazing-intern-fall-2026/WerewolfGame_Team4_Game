using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerVote : NetworkBehaviour
{
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

        // Không cho Player chết điều khiển Vote
        if (!IsLocalPlayerAlive())
            return;

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

        // =========================
        // KIỂM TRA PLAYER LOCAL
        // =========================

        if (!IsLocalPlayerAlive())
        {
            Debug.LogWarning(
                "NETWORK VOTE UI: Player đã chết, không thể Vote!"
            );

            return;
        }

        SendVote(targetID);
    }

    private bool IsLocalPlayerAlive()
    {
        NetworkPlayerStateSync stateSync =
            GetComponent<NetworkPlayerStateSync>();

        if (stateSync == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy NetworkPlayerStateSync!"
            );

            return false;
        }

        return stateSync.State.Value ==
               NetworkPlayerStateType.Alive;
    }

    private void SendVote(int targetID)
    {
        if (!IsLocalPlayerAlive())
        {
            Debug.LogWarning(
                "NETWORK VOTE: Player đã chết, không gửi Vote."
            );

            return;
        }

        Debug.Log(
            "VOTE | Client "
            + OwnerClientId
            + " → Player "
            + targetID
        );

        RequestVoteServerRpc(targetID);
    }

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
        // CHECK PHASE
        // =========================

        if (NetworkPhaseSync.Instance == null)
            return;

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
        // CHECK NETWORK STATE
        // =========================

        if (
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                senderClientId,
                out NetworkClient voterClient
            )
        )
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy Client của voter!"
            );

            return;
        }

        if (voterClient.PlayerObject == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: PlayerObject của voter NULL!"
            );

            return;
        }

        NetworkPlayerStateSync voterState =
            voterClient.PlayerObject.GetComponent<
                NetworkPlayerStateSync
            >();

        if (voterState == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy NetworkPlayerStateSync!"
            );

            return;
        }

        if (
            voterState.State.Value !=
            NetworkPlayerStateType.Alive
        )
        {
            Debug.LogWarning(
                "NETWORK VOTE: Player "
                + voterID
                + " đã chết → TỪ CHỐI VOTE!"
            );

            return;
        }

        // =========================
        // CHECK PLAYER DATA
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
                "NETWORK VOTE: PlayerData xác nhận Player "
                + voterID
                + " đã chết → TỪ CHỐI VOTE!"
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
        // VOTE / ĐỔI VOTE
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

        RebuildDev2Votes();
    }

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

        VoteManager.Instance.StartVote();

        foreach (
            KeyValuePair<int, int> choice
            in currentChoices
        )
        {
            int voterID =
                choice.Key;

            int targetID =
                choice.Value;

            PlayerData voter =
                PlayerManager.Instance.GetplayerByID(
                    voterID
                );

            // Không rebuild vote của Player đã chết
            if (voter == null || !voter.isAlive)
            {
                Debug.Log(
                    "NETWORK VOTE REBUILD | "
                    + "Bỏ qua Player "
                    + voterID
                    + " vì đã chết."
                );

                continue;
            }

            PlayerData target =
                PlayerManager.Instance.GetplayerByID(
                    targetID
                );

            // Không rebuild vote vào Player đã chết
            if (target == null || !target.isAlive)
            {
                Debug.Log(
                    "NETWORK VOTE REBUILD | "
                    + "Bỏ qua Target "
                    + targetID
                    + " vì đã chết."
                );

                continue;
            }

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

    public static void ResetNetworkVotes()
    {
        currentChoices.Clear();

        Debug.Log(
            "NETWORK VOTE: Đã reset lựa chọn Network."
        );
    }
}