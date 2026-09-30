using Unity.Netcode;
using UnityEngine;

public class NetworkVoteStateManager : MonoBehaviour
{
    private GamePhase lastPhase;
    private bool initialized;

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
            + lastPhase
            + " → "
            + currentPhase
        );

        if (currentPhase == GamePhase.Voting)
        {
            NetworkPlayerVote.ResetNetworkVotes();

            Debug.Log(
                "NETWORK VOTE STATE | Bắt đầu Voting mới."
            );
        }

        lastPhase = currentPhase;
    }
}