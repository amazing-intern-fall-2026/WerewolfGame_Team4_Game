using Unity.Netcode;
using UnityEngine;

public enum NetworkPlayerStateType
{
    Alive,
    Dead,
    Sleeping,
    Spectating
}

public class NetworkPlayerStateSync : NetworkBehaviour
{
    public NetworkVariable<NetworkPlayerStateType> State =
        new NetworkVariable<NetworkPlayerStateType>(
            NetworkPlayerStateType.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        State.OnValueChanged += OnStateChanged;

        Debug.Log(
            "NETWORK PLAYER STATE | Player "
            + OwnerClientId
            + " | State = "
            + State.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        State.OnValueChanged -= OnStateChanged;
    }

    private void OnStateChanged(
        NetworkPlayerStateType oldState,
        NetworkPlayerStateType newState)
    {
        Debug.Log(
            "NETWORK PLAYER STATE | Player "
            + OwnerClientId
            + " : "
            + oldState
            + " → "
            + newState
        );
    }

    // Server đổi State
    public void SetState(NetworkPlayerStateType newState)
    {
        if (!IsServer)
            return;

        State.Value = newState;
    }

    public void SetSleeping()
    {
        if (State.Value == NetworkPlayerStateType.Alive)
            SetState(NetworkPlayerStateType.Sleeping);
    }

    public void SetAlive()
    {
        if (State.Value == NetworkPlayerStateType.Sleeping)
            SetState(NetworkPlayerStateType.Alive);
    }

    public static void SyncGameplayAliveState(
        int playerID,
        bool isAlive)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                (ulong)playerID,
                out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        NetworkPlayerStateSync stateSync =
            client.PlayerObject.GetComponent<NetworkPlayerStateSync>();

        if (stateSync == null)
            return;

        if (isAlive)
            stateSync.SetState(
                NetworkPlayerStateType.Alive
            );
        else
            stateSync.KillPlayer();
    }

    // Server cho Player chết
    public void KillPlayer()
    {
        if (!IsServer)
            return;

        if (State.Value == NetworkPlayerStateType.Dead)
            return;

        State.Value = NetworkPlayerStateType.Dead;

        Debug.Log(
            "SERVER: Player "
            + OwnerClientId
            + " đã chết."
        );
    }

    // Server hồi sinh Player
    public void RevivePlayer()
    {
        if (!IsServer)
            return;

        if (State.Value != NetworkPlayerStateType.Dead)
            return;

        State.Value = NetworkPlayerStateType.Alive;

        Debug.Log(
            "SERVER: Player "
            + OwnerClientId
            + " đã hồi sinh."
        );
    }

    // Server chuyển sang Spectating
    public void SetSpectating()
    {
        if (!IsServer)
            return;

        if (State.Value != NetworkPlayerStateType.Dead)
            return;

        State.Value =
            NetworkPlayerStateType.Spectating;

        Debug.Log(
            "SERVER: Player "
            + OwnerClientId
            + " → Spectating."
        );
    }
}