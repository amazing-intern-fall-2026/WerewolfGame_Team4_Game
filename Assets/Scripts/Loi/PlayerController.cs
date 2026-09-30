using Unity.Netcode;
using UnityEngine;

public enum PlayerState
{
    Alive,
    Dead,
    Sleeping,
    Spectating
}

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;

    private NetworkPlayerStateSync networkState;

    public NetworkVariable<PlayerState> State =
        new NetworkVariable<PlayerState>(
            PlayerState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Role")]
    public NetworkVariable<PlayerRole> Role =
        new NetworkVariable<PlayerRole>(
            PlayerRole.Villager,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        networkState = GetComponent<NetworkPlayerStateSync>();
    }

    public override void OnNetworkSpawn()
    {
        State.OnValueChanged += OnStateChanged;

        if (IsServer)
        {
            ApplyStateFromGameState();
        }
    }

    public override void OnNetworkDespawn()
    {
        State.OnValueChanged -= OnStateChanged;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (!CanMove())
        {
            movement = Vector2.zero;
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        movement = movement.normalized;
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        if (!CanMove())
            return;

        rb.MovePosition(
            rb.position +
            movement * moveSpeed * Time.fixedDeltaTime
        );
    }

    private bool CanMove()
    {
        if (networkState == null)
            return false;

        if (networkState.State.Value != NetworkPlayerStateType.Alive)
            return false;

        if (NetworkPhaseSync.Instance == null)
            return false;

        GamePhase phase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        switch (phase)
        {
            case GamePhase.DayStart:
            case GamePhase.Event:
            case GamePhase.Task:
            case GamePhase.Discussion:
                return true;

            case GamePhase.Voting:
            case GamePhase.ResolveVote:
            case GamePhase.Night:
            case GamePhase.ResolveNight:
            case GamePhase.RoleReveal:
            case GamePhase.GameOver:
            default:
                return false;
        }
    }

    public void ApplyStateFromGameState()
    {
        if (!IsServer)
            return;

        if (NetworkGameManager.Instance == null)
            return;

        NetworkGameState currentGameState =
            NetworkGameManager.Instance.CurrentState.Value;

        switch (currentGameState)
        {
            case NetworkGameState.Night:

                if (networkState != null &&
                    networkState.State.Value ==
                    NetworkPlayerStateType.Alive)
                {
                    networkState.SetSleeping();
                }

                break;

            case NetworkGameState.Morning:

                if (networkState != null &&
                    networkState.State.Value ==
                    NetworkPlayerStateType.Sleeping)
                {
                    networkState.SetAlive();
                }

                break;
        }
    }

    private void OnStateChanged(
        PlayerState oldState,
        PlayerState newState)
    {
    }
}