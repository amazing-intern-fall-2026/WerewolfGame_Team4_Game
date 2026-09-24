using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseSync : NetworkBehaviour
{
    public static NetworkPhaseSync Instance;

    public NetworkVariable<GamePhase> CurrentPhase =
        new NetworkVariable<GamePhase>(
            GamePhase.RoleReveal,
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
        CurrentPhase.OnValueChanged += OnPhaseChanged;

        Debug.Log(
            "NetworkPhaseSync Spawned | Phase = "
            + CurrentPhase.Value
        );

        if (IsServer)
        {
            SyncInitialPhase();
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (GameRoleManager.Instance == null)
            return;

        GamePhase dev2Phase =
            GameRoleManager.Instance.currentPhase;

        if (CurrentPhase.Value == dev2Phase)
            return;

        CurrentPhase.Value = dev2Phase;

        Debug.Log(
            "NETWORK PHASE AUTO SYNC: "
            + CurrentPhase.Value
        );
    }

    private void SyncInitialPhase()
    {
        if (GameRoleManager.Instance == null)
            return;

        GamePhase initialPhase =
            GameRoleManager.Instance.currentPhase;

        CurrentPhase.Value = initialPhase;

        Debug.Log(
            "SERVER: Initial Phase → "
            + initialPhase
        );
    }

    public void SetNetworkPhase(GamePhase phase)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "NETWORK PHASE: Chỉ Server mới được set Phase."
            );
            return;
        }

        CurrentPhase.Value = phase;

        Debug.Log(
            "NETWORK PHASE: SERVER SET → "
            + phase
        );
    }

    private void OnPhaseChanged(
        GamePhase oldPhase,
        GamePhase newPhase)
    {
        Debug.Log(
            "NETWORK PHASE: "
            + oldPhase
            + " → "
            + newPhase
        );
    }
}