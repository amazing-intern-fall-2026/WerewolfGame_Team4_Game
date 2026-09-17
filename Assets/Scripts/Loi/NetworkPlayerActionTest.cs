using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerActionTest : NetworkBehaviour
{
    private NetworkPlayerAction action;

    private void Start()
    {
        action = GetComponent<NetworkPlayerAction>();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Q = Player 0 → Player 1
        if (Input.GetKeyDown(KeyCode.Q))
        {
            action.RequestActionServerRpc(1);
        }

        // E = Player 1 → Player 0
        if (Input.GetKeyDown(KeyCode.E))
        {
            action.RequestActionServerRpc(0);
        }

        // R = tự Action chính mình
        if (Input.GetKeyDown(KeyCode.R))
        {
            action.RequestActionServerRpc(OwnerClientId);
        }
    }
}