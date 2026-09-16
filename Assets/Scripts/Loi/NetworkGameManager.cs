using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;

    [Header("Current Network Game State")]
    public NetworkVariable<NetworkGameState> CurrentState =
        new NetworkVariable<NetworkGameState>(
            NetworkGameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentState.OnValueChanged += OnGameStateChanged;

        Debug.Log(
            "NetworkGameManager Spawned | Current State = "
            + CurrentState.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(
        NetworkGameState oldState,
        NetworkGameState newState)
    {
        Debug.Log(
            "NETWORK GAME STATE: "
            + oldState
            + " → "
            + newState
        );
    }

    // Chỉ Server được quyền đổi NetworkGameState
    public void ChangeGameState(NetworkGameState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Server mới được phép đổi NetworkGameState!"
            );

            return;
        }

        Debug.Log(
            "SERVER NETWORK STATE: "
            + CurrentState.Value
            + " → "
            + newState
        );

        CurrentState.Value = newState;

        UpdateAllPlayerStates();
    }

    private void UpdateAllPlayerStates()
    {
        if (!IsServer)
            return;

        PlayerController[] players =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );

        foreach (PlayerController player in players)
        {
            player.ApplyStateFromGameState();
        }
    }
}