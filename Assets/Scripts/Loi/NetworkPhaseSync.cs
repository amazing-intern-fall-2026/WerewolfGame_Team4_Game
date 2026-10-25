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

    // =========================================================
    // NIGHT CYCLE
    // =========================================================

    public NetworkVariable<int> CurrentNightCycle =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private int lastNetworkNightDay = -1;

    // =========================================================
    // WINNER
    // =========================================================

    public NetworkVariable<Unity.Collections.FixedString64Bytes> Winner =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        _ = NetworkGameOverManager.Instance;

        CurrentPhase.OnValueChanged += OnPhaseChanged;
        CurrentNightCycle.OnValueChanged += OnNightCycleChanged;
        Winner.OnValueChanged += OnWinnerChanged;

        Debug.Log(
            "NetworkPhaseSync Spawned | Phase = "
            + CurrentPhase.Value
            + " | NightCycle = "
            + CurrentNightCycle.Value
        );

        if (IsServer)
        {
            SyncInitialPhase();
        }
    }

    // =========================================================
    // NETWORK DESPAWN
    // =========================================================

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
        CurrentNightCycle.OnValueChanged -= OnNightCycleChanged;
        Winner.OnValueChanged -= OnWinnerChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsServer)
            return;

        if (GameRoleManager.Instance == null)
            return;

        GameRoleManager roleManager =
            GameRoleManager.Instance;

        GamePhase dev2Phase =
            roleManager.currentPhase;

        // =====================================================
        // SYNC PHASE
        // =====================================================

        if (CurrentPhase.Value != dev2Phase)
        {
            if (dev2Phase == GamePhase.GameOver)
            {
                string winnerName = roleManager.Winner;
                if (string.IsNullOrEmpty(winnerName) && NetworkGameOverManager.Instance != null)
                    winnerName = NetworkGameOverManager.Instance.currentWinner;
                if (string.IsNullOrEmpty(winnerName))
                    winnerName = "Villagers";

                Winner.Value = new Unity.Collections.FixedString64Bytes(winnerName);
                CurrentPhase.Value = dev2Phase;

                Debug.Log(
                    "NETWORK PHASE AUTO SYNC: GameOver | Winner = "
                    + winnerName
                );

                NotifyGameOverClientRpc(winnerName);
                NetworkGameOverManager.Instance.ShowGameOverUI(winnerName);
            }
            else
            {
                CurrentPhase.Value = dev2Phase;
                Debug.Log(
                    "NETWORK PHASE AUTO SYNC: "
                    + CurrentPhase.Value
                );
            }
        }

        // Luôn đảm bảo GameOver UI được hiển thị nếu phase hiện tại là GameOver
        if (CurrentPhase.Value == GamePhase.GameOver || (roleManager != null && roleManager.currentState == GameState.GameOver))
        {
            string winner = Winner.Value.ToString();
            if (string.IsNullOrEmpty(winner) && roleManager != null)
                winner = roleManager.Winner;
            if (string.IsNullOrEmpty(winner) && NetworkGameOverManager.Instance != null)
                winner = NetworkGameOverManager.Instance.currentWinner;

            if (!string.IsNullOrEmpty(winner))
            {
                NetworkGameOverManager.Instance.ShowGameOverUI(winner);
            }
        }

        // =====================================================
        // SYNC NIGHT CYCLE
        // =====================================================

        if (dev2Phase == GamePhase.Night)
        {
            int currentDay =
                roleManager.currentDay;

            if (currentDay != lastNetworkNightDay)
            {
                lastNetworkNightDay =
                    currentDay;

                CurrentNightCycle.Value =
                    currentDay;

                Debug.Log(
                    "NETWORK NIGHT CYCLE → "
                    + CurrentNightCycle.Value
                    + " | Day="
                    + currentDay
                );
            }
        }
    }

    // =========================================================
    // INITIAL PHASE
    // =========================================================

    private void SyncInitialPhase()
    {
        if (GameRoleManager.Instance == null)
            return;

        GameRoleManager roleManager =
            GameRoleManager.Instance;

        GamePhase initialPhase =
            roleManager.currentPhase;

        CurrentPhase.Value =
            initialPhase;

        if (initialPhase == GamePhase.GameOver)
        {
            string winnerName = roleManager.Winner;
            if (string.IsNullOrEmpty(winnerName) && NetworkGameOverManager.Instance != null)
                winnerName = NetworkGameOverManager.Instance.currentWinner;
            if (string.IsNullOrEmpty(winnerName))
                winnerName = "Villagers";

            Winner.Value = new Unity.Collections.FixedString64Bytes(winnerName);
        }

        Debug.Log(
            "SERVER: Initial Phase → "
            + initialPhase
        );

        // Nếu game bắt đầu trực tiếp ở Night
        if (initialPhase == GamePhase.Night)
        {
            lastNetworkNightDay =
                roleManager.currentDay;

            CurrentNightCycle.Value =
                roleManager.currentDay;

            Debug.Log(
                "SERVER: Initial Night Cycle → "
                + CurrentNightCycle.Value
            );
        }
    }

    // =========================================================
    // MANUAL SET PHASE
    // =========================================================

    public void SetNetworkPhase(GamePhase phase)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "NETWORK PHASE: Chỉ Server mới được set Phase."
            );

            return;
        }

        CurrentPhase.Value =
            phase;

        Debug.Log(
            "NETWORK PHASE: SERVER SET → "
            + phase
        );
    }

    // =========================================================
    // PHASE CHANGED
    // =========================================================

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

        if (newPhase == GamePhase.Voting)
        {
            if (IsServer)
            {
                NetworkPlayerVote.ResetNetworkVotes();
            }
            else
            {
                NetworkPlayerVote.ResetLocalStaticVotes();
            }
        }
        else if (oldPhase == GamePhase.Voting)
        {
            NetworkPlayerVote.ResetLocalStaticVotes();
        }

        if (newPhase == GamePhase.GameOver)
        {
            string winnerName = Winner.Value.ToString();
            if (string.IsNullOrEmpty(winnerName) && GameRoleManager.Instance != null)
                winnerName = GameRoleManager.Instance.Winner;

            if (!string.IsNullOrEmpty(winnerName))
            {
                NetworkGameOverManager.Instance.ShowGameOverUI(winnerName);
            }
            else
            {
                Debug.Log("NETWORK PHASE: Đã chuyển phase sang GameOver, đang đợi Winner từ RPC hoặc Winner.OnValueChanged.");
            }
        }
        else if (oldPhase == GamePhase.GameOver && newPhase != GamePhase.GameOver)
        {
            NetworkGameOverManager.Instance?.ResetUIState();
        }
    }

    private void OnWinnerChanged(
        Unity.Collections.FixedString64Bytes oldWinner,
        Unity.Collections.FixedString64Bytes newWinner)
    {
        string winnerName = newWinner.ToString();
        Debug.Log($"NETWORK WINNER CHANGED: {oldWinner} → {winnerName}");

        if (!string.IsNullOrEmpty(winnerName))
        {
            NetworkGameOverManager.Instance?.ShowGameOverUI(winnerName);
        }
    }

    // =========================================================
    // CLIENT RPC: GAME OVER NOTIFICATION
    // =========================================================

    [ClientRpc]
    public void NotifyGameOverClientRpc(string winner)
    {
        Debug.Log($"[NetworkPhaseSync] NotifyGameOverClientRpc: Winner = {winner}");
        if (string.IsNullOrEmpty(winner))
            winner = "Villagers";

        NetworkGameOverManager.Instance?.ShowGameOverUI(winner);
    }

    // =========================================================
    // CLIENT RPC: SYNC ACTIVE SESSION PLAYERS
    // =========================================================

    [ClientRpc]
    public void SyncActiveSessionClientRpc(ulong[] activeClientIds, ulong[] leftClientIds)
    {
        Debug.Log("[NetworkPhaseSync] SyncActiveSessionClientRpc nhận danh sách active và left players từ Server.");

        if (leftClientIds != null)
        {
            foreach (ulong leftId in leftClientIds)
            {
                NetworkGameManager.LeftPlayerClientIds.Add(leftId);
            }
        }

        if (activeClientIds != null && activeClientIds.Length > 0)
        {
            NetworkGamePlayerSetup.SetActiveMatchClientIds(activeClientIds);
        }

        var activeSet = new System.Collections.Generic.HashSet<ulong>(activeClientIds ?? System.Array.Empty<ulong>());

        // 1. Tiêu hủy PlayerController GameObject mồ côi (không nằm trong activeSet hoặc nằm trong LeftPlayerClientIds)
        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc != null && (!activeSet.Contains(pc.OwnerClientId) || NetworkGameManager.LeftPlayerClientIds.Contains(pc.OwnerClientId)))
            {
                Debug.Log($"[NetworkPhaseSync] SyncActiveSession: Client tiêu hủy PlayerController mồ côi ClientId={pc.OwnerClientId}");
                Destroy(pc.gameObject);
            }
        }

        // 2. Dọn dẹp PlayerManager trên client
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            PlayerManager.Instance.players.RemoveAll(p => p == null 
                || !activeSet.Contains((ulong)p.playerID) 
                || NetworkGameManager.LeftPlayerClientIds.Contains((ulong)p.playerID));
        }

        NetworkGameManager.RefreshAllGameUI(-1);
    }

    // =========================================================
    // CLIENT RPC: RESET CLIENT MATCH STATE
    // =========================================================

    [ClientRpc]
    public void ResetClientMatchStateClientRpc(ulong[] activeClientIds)
    {
        Debug.Log("[NetworkPhaseSync] ResetClientMatchStateClientRpc: Đặt lại UI, Skill và dọn dẹp player mồ côi trên Client.");
        NetworkGameOverManager.Instance?.ResetUIState();
        NetworkSkillUI.Instance?.ResetForNewMatch();
        NetworkHunterDeathUI.Instance?.ResetForNewMatch();
        NetworkDeathNotification.Instance?.HideDeathNotification();

        if (activeClientIds != null && activeClientIds.Length > 0)
        {
            NetworkGamePlayerSetup.SetActiveMatchClientIds(activeClientIds);
            var activeSet = new System.Collections.Generic.HashSet<ulong>(activeClientIds);

            // 1. Dọn dẹp PlayerController mồ côi trên client
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var pc in controllers)
            {
                if (pc != null && (!activeSet.Contains(pc.OwnerClientId) || NetworkGameManager.LeftPlayerClientIds.Contains(pc.OwnerClientId)))
                {
                    Debug.Log($"[NetworkPhaseSync] Client tiêu hủy PlayerController mồ côi ClientId={pc.OwnerClientId}");
                    Destroy(pc.gameObject);
                }
            }

            // 2. Dọn dẹp PlayerManager trên client
            if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
            {
                PlayerManager.Instance.players.RemoveAll(p => p == null 
                    || !activeSet.Contains((ulong)p.playerID)
                    || NetworkGameManager.LeftPlayerClientIds.Contains((ulong)p.playerID));
                foreach (var p in PlayerManager.Instance.players)
                {
                    p?.ResetForNewMatch();
                }
            }
        }

        NetworkGameManager.RefreshAllGameUI(-1);
    }

    // =========================================================
    // NIGHT CYCLE CHANGED
    // =========================================================

    private void OnNightCycleChanged(
        int oldNight,
        int newNight)
    {
        Debug.Log(
            "NETWORK NIGHT CYCLE: "
            + oldNight
            + " → "
            + newNight
        );
    }
}