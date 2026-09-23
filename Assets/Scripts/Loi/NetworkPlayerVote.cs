using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerVote : NetworkBehaviour
{
    // 6 = Vote Player 0
    // 7 = Vote Player 1
    // 8 = Vote Player 2
    // 9 = Vote Player 3
    // 0 = Vote Player 4

    private void Update()
    {
        if (!IsOwner)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        if (NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Voting)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha6))
            RequestVote(0);

        if (Input.GetKeyDown(KeyCode.Alpha7))
            RequestVote(1);

        if (Input.GetKeyDown(KeyCode.Alpha8))
            RequestVote(2);

        if (Input.GetKeyDown(KeyCode.Alpha9))
            RequestVote(3);

        if (Input.GetKeyDown(KeyCode.Alpha0))
            RequestVote(4);
    }

    private void RequestVote(int targetID)
    {
        Debug.Log(
            "VOTE TEST | Player "
            + OwnerClientId
            + " → Vote Player "
            + targetID
        );

        RequestVoteServerRpc(targetID);
    }

    [ServerRpc]
    private void RequestVoteServerRpc(
        int targetID,
        ServerRpcParams rpcParams = default)
    {
        int voterID = (int)OwnerClientId;

        if (GameRoleManager.Instance == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy GameRoleManager!"
            );
            return;
        }

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
                "NETWORK VOTE: Không tìm thấy PlayerManger!"
            );
            return;
        }

        PlayerData voter =
            PlayerManager.Instance.GetplayerByID(voterID);

        PlayerData target =
            PlayerManager.Instance.GetplayerByID(targetID);

        if (voter == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy Voter Player "
                + voterID
            );
            return;
        }

        if (target == null)
        {
            Debug.LogWarning(
                "NETWORK VOTE: Không tìm thấy Target Player "
                + targetID
            );
            return;
        }

        Debug.Log(
            "VOTER CHECK | Player "
            + voterID
            + " | Alive = "
            + voter.isAlive
            + " | HasVoted = "
            + voter.hasVoted
            + " | VotePower = "
            + voter.votePower
        );

        Debug.Log(
            "TARGET CHECK | Player "
            + targetID
            + " | Alive = "
            + target.isAlive
        );

        bool success = VoteManager.Instance.TryVote(
            voterID,
            targetID
        );

        if (!success)
        {
            Debug.LogWarning(
                "NETWORK VOTE: ❌ Vote không hợp lệ | Player "
                + voterID
                + " → "
                + targetID
            );

            return;
        }

        Debug.Log(
            "NETWORK VOTE: ✅ Vote hợp lệ | Player "
            + voterID
            + " → "
            + targetID
        );
    }
}