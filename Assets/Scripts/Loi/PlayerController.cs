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
    [SerializeField] private float moveSpeed = 5f;

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

        Debug.Log(
            "Player " +
            OwnerClientId +
            " Spawned | State = " +
            State.Value
        );

        // Server tự thiết lập trạng thái cho Player
        // dựa trên NetworkGameState hiện tại
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

        // TEST:
        // Nhấn K để yêu cầu Server đổi trạng thái
        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleStateServerRpc();
        }

        // Kiểm tra Player có được di chuyển hay không
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
        // Không tìm thấy NetworkPlayerStateSync
        if (networkState == null)
            return false;

        // Chỉ Alive mới được di chuyển
        if (networkState.State.Value != NetworkPlayerStateType.Alive)
            return false;

        // Không có NetworkPhaseSync
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

    // =========================================================
    // TỰ ĐỘNG ĐỔI PLAYER STATE THEO NETWORK GAME STATE
    // =========================================================

    public void ApplyStateFromGameState()
    {
        // Chỉ Server được thay đổi NetworkVariable
        if (!IsServer)
            return;

        if (NetworkGameManager.Instance == null)
            return;

        NetworkGameState currentGameState =
            NetworkGameManager.Instance.CurrentState.Value;

        switch (currentGameState)
        {
            case NetworkGameState.Night:

                // Người đã chết không trở thành Sleeping
                if (networkState != null &&
                    networkState.State.Value == NetworkPlayerStateType.Alive)
                {
                    networkState.SetSleeping();
                }

                break;

            case NetworkGameState.Morning:

                // Chỉ đánh thức người đang Sleeping
                if (networkState != null &&
                    networkState.State.Value == NetworkPlayerStateType.Sleeping)
                {
                    networkState.SetAlive();
                }

                break;
        }
    }

    // =========================================================
    // TEST ĐỔI STATE BẰNG PHÍM K
    // =========================================================

    [ServerRpc]
    private void ToggleStateServerRpc()
    {
        Debug.Log(
            "SERVER: Player " +
            OwnerClientId +
            " yêu cầu đổi State"
        );

        if (State.Value == PlayerState.Alive)
        {
            State.Value = PlayerState.Dead;
        }
        else
        {
            State.Value = PlayerState.Alive;
        }
    }

    // =========================================================
    // STATE CHANGED
    // =========================================================

    private void OnStateChanged(
        PlayerState oldState,
        PlayerState newState)
    {
        Debug.Log(
            "Player " +
            OwnerClientId +
            " | State: " +
            oldState +
            " → " +
            newState
        );
    }
}