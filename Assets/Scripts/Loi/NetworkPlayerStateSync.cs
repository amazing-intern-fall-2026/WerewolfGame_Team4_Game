using Unity.Netcode;
using UnityEngine;
using System;

public enum NetworkPlayerStateType
{
    Alive,
    Dead,
    Sleeping,
    Spectating,
    Disconnected
}

public class NetworkPlayerStateSync : NetworkBehaviour
{
    public static event Action OnLocalPlayerDeath;

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
            + (OwnerClientId + 1)
            + " | State = "
            + State.Value
        );

        UpdateVisualForState(State.Value);

        // Trường hợp Player đã Dead trước khi UI subscribe
        if (IsOwner &&
            State.Value == NetworkPlayerStateType.Dead)
        {
            OnLocalPlayerDeath?.Invoke();
        }
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
            + (OwnerClientId + 1)
            + " : "
            + oldState
            + " → "
            + newState
        );

        UpdateVisualForState(newState);

        // Chỉ thông báo cho chính Player bị chết
        if (IsOwner &&
            oldState != NetworkPlayerStateType.Dead &&
            newState == NetworkPlayerStateType.Dead)
        {
            OnLocalPlayerDeath?.Invoke();
        }
    }

    public void UpdateVisualForState(NetworkPlayerStateType state)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (state == NetworkPlayerStateType.Dead)
        {
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    sr.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                }
            }
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
        else if (state == NetworkPlayerStateType.Alive || state == NetworkPlayerStateType.Sleeping)
        {
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    sr.color = Color.white;
                }
            }
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
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

        NetworkPlayerStateSync stateSync = null;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue((ulong)playerID, out NetworkClient client) && client.PlayerObject != null)
        {
            stateSync = client.PlayerObject.GetComponent<NetworkPlayerStateSync>();
        }

        if (stateSync == null)
        {
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc != null && (int)pc.OwnerClientId == playerID)
                {
                    stateSync = pc.GetComponent<NetworkPlayerStateSync>();
                    break;
                }
            }
        }

        if (stateSync == null)
        {
            Debug.LogWarning($"[NetworkPlayerStateSync] KhÃ´ng tÃ¬m tháº¥y stateSync cho playerID {playerID}");
            return;
        }

        if (isAlive)
        {
            stateSync.SetState(NetworkPlayerStateType.Alive);
        }
        else
        {
            stateSync.KillPlayer();
        }

        PlayerController controller = stateSync.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.State.Value = isAlive ? PlayerState.Alive : PlayerState.Dead;
        }

        if (PlayerManager.Instance != null)
        {
            PlayerData p = PlayerManager.Instance.GetplayerByID(playerID);
            if (p != null)
            {
                p.isAlive = isAlive;
                if (!isAlive && p.lastDeathCause == default)
                {
                    p.lastDeathCause = DeathCause.Monster;
                }

                if (p.roleType == RoleType.Hunter && !p.hasUsedHunterShot)
                {
                    bool validCause = (p.lastDeathCause == DeathCause.Monster ||
                                       p.lastDeathCause == DeathCause.Vote ||
                                       p.wasVotedOut);
                    p.hasHunterTrap = validCause;
                    NetworkRoleSync.SyncHunterRevengeState((ulong)playerID, validCause, p.hasUsedHunterShot);
                    Debug.Log($"HUNTER SYNC: Player {playerID + 1} Hunter death resolved (Cause={p.lastDeathCause}, ValidCause={validCause}) -> Sync canShoot={validCause}");
                }
            }
        }
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
            + (OwnerClientId + 1)
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
            + (OwnerClientId + 1)
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
            + (OwnerClientId + 1)
            + " → Spectating."
        );
    }

    // Server đánh dấu Player Disconnected
    public void SetDisconnected()
    {
        if (!IsServer)
            return;

        State.Value = NetworkPlayerStateType.Disconnected;

        Debug.Log(
            "SERVER: Player "
            + (OwnerClientId + 1)
            + " → Disconnected."
        );
    }

    /// <summary>
    /// Server gọi khi bắt đầu ván mới: Đặt tất cả Player đang kết nối về Alive và đồng bộ qua mạng.
    /// </summary>
    public static void ResetAllPlayersAlive()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        Debug.Log("SERVER: Reset toàn bộ người chơi về Alive cho ván mới.");

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject != null)
            {
                var stateSync = client.PlayerObject.GetComponent<NetworkPlayerStateSync>();
                if (stateSync != null)
                {
                    stateSync.SetState(NetworkPlayerStateType.Alive);
                    stateSync.UpdateVisualForState(NetworkPlayerStateType.Alive);
                }

                var controller = client.PlayerObject.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.State.Value = PlayerState.Alive;
                }
            }
        }
    }
}