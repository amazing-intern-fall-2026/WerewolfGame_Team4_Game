using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkVoteResultSync : NetworkBehaviour
{
    [SerializeField]
    private NetworkVoteResultNotification resultUI;

    private void Start()
    {
        if (!IsServer)
            return;

        if (VoteManager.Instance == null)
        {
            Debug.LogWarning(
                "VOTE RESULT SYNC | Không tìm thấy VoteManager!"
            );

            return;
        }

        VoteManager.Instance.VoteResolved +=
            OnVoteResolved;

        Debug.Log(
            "VOTE RESULT SYNC | Đã đăng ký VoteResolved."
        );
    }

    private void OnDestroy()
    {
        if (VoteManager.Instance != null)
        {
            VoteManager.Instance.VoteResolved -=
                OnVoteResolved;
        }
    }

    private void OnVoteResolved(VoteResolution resolution)
    {
        if (!IsServer)
            return;

        if (resolution == null)
            return;

        int eliminatedPlayerID = -1;
        int voteCount = 0;

        if (resolution.Eliminated != null)
        {
            eliminatedPlayerID =
                resolution.Eliminated.playerID;

            if (resolution.Tallies != null &&
                resolution.Tallies.TryGetValue(
                    eliminatedPlayerID,
                    out int count))
            {
                voteCount = count;
            }
        }

        SendVoteResultClientRpc(
            resolution.Message,
            eliminatedPlayerID,
            voteCount,
            resolution.IsTie
        );
    }

    [ClientRpc]
    private void SendVoteResultClientRpc(
        string message,
        int eliminatedPlayerID,
        int voteCount,
        bool isTie)
    {
        Debug.Log(
            "VOTE RESULT SYNC | Client nhận kết quả."
            + " | Message = "
            + message
        );

        if (resultUI == null)
        {
            Debug.LogWarning(
                "VOTE RESULT SYNC | resultUI = NULL!"
            );

            return;
        }

        string displayMessage;

        if (eliminatedPlayerID >= 0)
        {
            displayMessage =
                "Player "
                + (eliminatedPlayerID + 1)
                + " đã bị loại\n"
                + "Số phiếu: "
                + voteCount;
        }
        else
        {
            displayMessage = message;
        }

        resultUI.ShowResult(
            "VOTE RESULT",
            displayMessage
        );
    }
}