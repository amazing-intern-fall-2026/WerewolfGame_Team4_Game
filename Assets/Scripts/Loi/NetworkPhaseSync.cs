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

    private GamePhase lastPhase;

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

        if (GameManager.Instance == null)
            return;

        GamePhase currentPhase =
            GameManager.Instance.currentPhase;

        if (currentPhase == lastPhase)
            return;

        lastPhase = currentPhase;

        CurrentPhase.Value = currentPhase;

        Debug.Log(
            "SERVER: Phase changed → "
            + currentPhase
        );
    }

    private void SyncInitialPhase()
    {
        if (GameManager.Instance == null)
            return;

        lastPhase =
            GameManager.Instance.currentPhase;

        CurrentPhase.Value =
            lastPhase;

        Debug.Log(
            "SERVER: Initial Phase → "
            + lastPhase
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