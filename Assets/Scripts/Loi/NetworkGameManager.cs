using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;

    [Header("Current Game State")]
    public NetworkVariable<GameState> CurrentState =
        new NetworkVariable<GameState>(
            GameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Network Game Timer")]
    [SerializeField] private NetworkGameTimer timer;

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

        // Chỉ Server/Host điều khiển Game Flow
        if (IsServer)
        {
            // Tìm Timer nếu chưa gán trong Inspector
            if (timer == null)
            {
                timer = FindAnyObjectByType<NetworkGameTimer>();
            }

            if (timer == null)
            {
                Debug.LogError(
                    "NETWORK GAME MANAGER: Không tìm thấy NetworkGameTimer!"
                );

                return;
            }

            // Khi vừa vào Game Scene,
            // bắt đầu State NIGHT
            ChangeGameState(GameState.Night);
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnGameStateChanged;
    }

    private void Update()
    {
        // Chỉ Server được điều khiển Game Flow
        if (!IsServer)
            return;

        // Không có Timer thì dừng
        if (timer == null)
            return;

        // Nếu Timer chưa hết thì chưa chuyển State
        if (timer.TimeRemaining.Value > 0f)
            return;

        // Xử lý chuyển State
        switch (CurrentState.Value)
        {
            case GameState.Night:
                ChangeGameState(GameState.Morning);
                break;

            case GameState.Morning:
                ChangeGameState(GameState.Discussion);
                break;

            case GameState.Discussion:
                ChangeGameState(GameState.Voting);
                break;

            case GameState.Voting:
                ChangeGameState(GameState.Resolve);
                break;

            case GameState.Resolve:
                ChangeGameState(GameState.Night);
                break;
        }
    }

    private void OnGameStateChanged(
        GameState oldState,
        GameState newState)
    {
        Debug.Log(
            "GAME STATE: "
            + oldState
            + " → "
            + newState
        );
    }

    // Chỉ Server được quyền đổi GameState
    public void ChangeGameState(GameState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "Chỉ Server mới được phép đổi GameState!"
            );

            return;
        }

        Debug.Log(
            "SERVER: "
            + CurrentState.Value
            + " → "
            + newState
        );

        // Đổi GameState
        CurrentState.Value = newState;

        // Tự động cập nhật PlayerState
        UpdateAllPlayerStates();

        // Reset Timer
        if (timer != null)
        {
            timer.SetTimerForState(newState);
        }
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