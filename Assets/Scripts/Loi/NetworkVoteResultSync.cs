
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkVoteResultSync : NetworkBehaviour
{
    public static NetworkVoteResultSync Instance { get; private set; }

    [SerializeField]
    private NetworkVoteResultNotification resultUI;

    private readonly Dictionary<int, int> capturedTallies =
        new Dictionary<int, int>();

    private int capturedTargetID = -1;
    private int capturedVoteCount;
    private bool capturedTie;
    private bool hasCapturedResult;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Đã có NetworkVoteResultSync khác trong scene."
            );
            return;
        }

        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Chụp kết quả vote từ dữ liệu VoteManager hiện có.
    /// Không sửa code của Dev2.
    /// </summary>
    public void CaptureVoteResult()
    {
        if (!IsServer)
            return;

        capturedTallies.Clear();
        capturedTargetID = -1;
        capturedVoteCount = 0;
        capturedTie = false;
        hasCapturedResult = false;

        if (PlayerManager.Instance == null ||
            VoteManager.Instance == null)
        {
            Debug.LogWarning(
                "VOTE SYNC | Thiếu PlayerManager hoặc VoteManager."
            );
            return;
        }

        foreach (PlayerData voter in PlayerManager.Instance.players)
        {
            // Không lọc isAlive ở đây vì người vote có thể đã bị loại
            // sau khi VoteManager.ResolveVote() được thực hiện.
            if (voter == null || !voter.hasVoted)
                continue;

            int targetID =
                VoteManager.Instance.GetVotedTarget(voter.playerID);

            if (targetID < 0)
                continue;

            if (!capturedTallies.ContainsKey(targetID))
                capturedTallies[targetID] = 0;

            capturedTallies[targetID] +=
                Mathf.Max(1, voter.votePower);
        }

        int highest = 0;

        foreach (KeyValuePair<int, int> vote in capturedTallies)
        {
            if (vote.Value > highest)
            {
                highest = vote.Value;
                capturedTargetID = vote.Key;
                capturedTie = false;
            }
            else if (vote.Value == highest)
            {
                capturedTie = true;
            }
        }

        capturedVoteCount = highest;

        if (capturedTallies.Count == 0 || highest == 0)
        {
            capturedTargetID = -1;
            capturedVoteCount = 0;
            capturedTie = false;
        }

        hasCapturedResult = true;

        Debug.Log(
            $"VOTE SYNC | Captured target={capturedTargetID}, " +
            $"votes={capturedVoteCount}, tie={capturedTie}"
        );
    }

    /// <summary>
    /// Gửi kết quả đến tất cả Client sau khi vote đã được xử lý.
    /// </summary>
    public void PublishVoteResult()
    {
        if (!IsServer || !hasCapturedResult)
            return;

        hasCapturedResult = false;

        int eliminatedPlayerID = -1;
        string message;

        if (capturedTie)
        {
            message = "Phiếu bầu hòa!\nKhông có người bị loại.";
        }
        else if (capturedTargetID < 0)
        {
            message =
                "Không có phiếu bầu hợp lệ.\nKhông có người bị loại.";
        }
        else
        {
            PlayerData target =
                PlayerManager.Instance != null
                    ? PlayerManager.Instance.GetplayerByID(
                        capturedTargetID)
                    : null;

            if (target != null && !target.isAlive)
            {
                eliminatedPlayerID = capturedTargetID;
                message = "Người chơi đã bị loại.";
            }
            else
            {
                message = "Không có người bị loại.";
            }
        }

        SendVoteResultClientRpc(
            message,
            eliminatedPlayerID,
            capturedVoteCount,
            capturedTie
        );

        Debug.Log(
            $"VOTE SYNC | Published eliminated={eliminatedPlayerID}, " +
            $"votes={capturedVoteCount}, tie={capturedTie}"
        );
    }

    [ClientRpc]
    private void SendVoteResultClientRpc(
        string message,
        int eliminatedPlayerID,
        int voteCount,
        bool isTie)
    {
        string displayMessage;

        if (isTie)
        {
            displayMessage =
                "Phiếu bầu hòa!\nKhông có người bị loại.";
        }
        else if (eliminatedPlayerID >= 0)
        {
            displayMessage =
                $"Player {eliminatedPlayerID + 1} đã bị loại\n" +
                $"Số phiếu: {voteCount}";
        }
        else
        {
            displayMessage = message;
        }

        Debug.Log("VOTE SYNC | " + displayMessage);

        if (resultUI == null)
        {
            Debug.LogWarning(
                "VOTE SYNC | Chưa gán Result UI trong Inspector."
            );
            return;
        }

        resultUI.ShowResult("VOTE RESULT", displayMessage);
    }
}
