using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerStateTest : NetworkBehaviour
{
    private void Update()
    {
        if (!IsServer)
            return;

        // F1 → Player 0 Dead
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SetPlayerState(0, NetworkPlayerStateType.Dead);
        }

        // F2 → Player 1 Dead
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SetPlayerState(1, NetworkPlayerStateType.Dead);
        }

        // F3 → Player 0 Alive
        if (Input.GetKeyDown(KeyCode.F3))
        {
            SetPlayerState(0, NetworkPlayerStateType.Alive);
        }

        // F4 → Player 1 Alive
        if (Input.GetKeyDown(KeyCode.F4))
        {
            SetPlayerState(1, NetworkPlayerStateType.Alive);
        }

        // F5 → Player 0 Spectating
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SetPlayerState(0, NetworkPlayerStateType.Spectating);
        }

        // F6 → Player 1 Spectating
        if (Input.GetKeyDown(KeyCode.F6))
        {
            SetPlayerState(1, NetworkPlayerStateType.Spectating);
        }
    }

    private void SetPlayerState(
        ulong clientId,
        NetworkPlayerStateType newState)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning(
                "NETWORK TEST: Không tìm thấy ClientId " + clientId
            );
            return;
        }

        NetworkClient client =
            NetworkManager.Singleton.ConnectedClients[clientId];

        if (client.PlayerObject == null)
        {
            Debug.LogWarning(
                "NETWORK TEST: PlayerObject của ClientId "
                + clientId
                + " không tồn tại!"
            );
            return;
        }

        NetworkPlayerStateSync stateSync =
            client.PlayerObject.GetComponent<NetworkPlayerStateSync>();

        if (stateSync == null)
        {
            Debug.LogWarning(
                "NETWORK TEST: Player "
                + clientId
                + " không có NetworkPlayerStateSync!"
            );
            return;
        }

        stateSync.SetState(newState);

        Debug.Log(
            "NETWORK TEST: Player "
            + clientId
            + " → "
            + newState
        );
    }
}