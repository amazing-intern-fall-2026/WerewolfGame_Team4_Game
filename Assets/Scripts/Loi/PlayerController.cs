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
        // dựa trên GameState hiện tại
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
        // Không phải Alive → không được di chuyển
        if (State.Value != PlayerState.Alive)
            return false;

        // Không tìm thấy GameManager → không cho di chuyển
        if (NetworkGameManager.Instance == null)
            return false;

        GameState currentGameState =
            NetworkGameManager.Instance.CurrentState.Value;

        switch (currentGameState)
        {
            case GameState.Morning:
                return true;

            case GameState.Night:
                return false;

            case GameState.Discussion:
                return false;

            case GameState.Voting:
                return false;

            case GameState.Resolve:
                return false;

            default:
                return false;
        }
    }

    // =========================================================
    // TỰ ĐỘNG ĐỔI PLAYER STATE THEO GAME STATE
    // =========================================================

    public void ApplyStateFromGameState()
    {
        // Chỉ Server được thay đổi NetworkVariable
        if (!IsServer)
            return;

        if (NetworkGameManager.Instance == null)
            return;

        GameState currentGameState =
            NetworkGameManager.Instance.CurrentState.Value;

        switch (currentGameState)
        {
            case GameState.Night:

                // Người đã chết không trở thành Sleeping
                if (State.Value == PlayerState.Alive)
                {
                    State.Value = PlayerState.Sleeping;
                }

                break;

            case GameState.Morning:

                // Chỉ đánh thức người đang Sleeping
                if (State.Value == PlayerState.Sleeping)
                {
                    State.Value = PlayerState.Alive;
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