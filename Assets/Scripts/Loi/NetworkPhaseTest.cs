using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseTest : NetworkBehaviour
{
    private void Update()
    {
        if (!IsServer)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetPhase(GamePhase.DayStart);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetPhase(GamePhase.Night);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetPhase(GamePhase.Discussion);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetPhase(GamePhase.Voting);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SetPhase(GamePhase.ResolveVote);
        }
    }

    private void SetPhase(GamePhase phase)
    {
        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogError(
                "NETWORK PHASE TEST: Không tìm thấy NetworkPhaseSync!"
            );
            return;
        }

        NetworkPhaseSync.Instance.CurrentPhase.Value = phase;

        Debug.Log(
            "NETWORK TEST: Server → "
            + phase
        );
    }
}