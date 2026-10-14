
using Unity.Netcode;
using UnityEngine;

public class NetworkVoteStateManager : MonoBehaviour
{
    private GamePhase lastPhase;
    private bool initialized;
    private bool waitingForVoteResolution;

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        if (NetworkPhaseSync.Instance == null)
            return;

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        if (!initialized)
        {
            initialized = true;
            lastPhase = currentPhase;
            return;
        }

        if (currentPhase == lastPhase)
            return;

        Debug.Log(
            "NETWORK VOTE STATE | Phase "
            + lastPhase + " → " + currentPhase
        );

        // Bắt đầu lượt vote mới.
        if (currentPhase == GamePhase.Voting)
        {
            NetworkPlayerVote.ResetNetworkVotes();
            waitingForVoteResolution = false;

            Debug.Log(
                "NETWORK VOTE STATE | Bắt đầu Voting mới."
            );
        }

        // Voting vừa kết thúc.
        if (lastPhase == GamePhase.Voting)
        {
            if (currentPhase == GamePhase.ResolveVote)
            {
                // Đợi ResolveVote xử lý xong.
                waitingForVoteResolution = true;
            }
            else
            {
                // Nếu ResolveVote diễn ra quá nhanh và phase
                // trung gian không được đồng bộ, xử lý ở đây.
                PublishVoteResult();
            }
        }

        // Đã thấy ResolveVote, hiện tại đã chuyển sang phase kế tiếp.
        if (waitingForVoteResolution &&
            currentPhase != GamePhase.ResolveVote &&
            currentPhase != GamePhase.Voting)
        {
            waitingForVoteResolution = false;
            PublishVoteResult();
        }

        lastPhase = currentPhase;
    }

    private void PublishVoteResult()
    {
        // Kết quả vote bây giờ được tự động đồng bộ thông qua sự kiện VoteManager.VoteResolved
        Debug.Log("VOTE RESULT STATE | Kết quả vote được xử lý tự động qua Event.");
    }
}
 