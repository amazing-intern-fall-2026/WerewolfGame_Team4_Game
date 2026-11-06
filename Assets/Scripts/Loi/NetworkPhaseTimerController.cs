using Unity.Netcode;
using UnityEngine;

public class NetworkPhaseTimerController : MonoBehaviour
{
    private NetworkGameTimer gameTimer;
    private NetworkPhaseSync phaseSync;

    private GamePhase lastPhase;
    private bool initialized;
    private bool phaseFinishedHandled;

    private void Start()
    {
        _ = NetworkGameOverManager.Instance;

        gameTimer =
            FindFirstObjectByType<NetworkGameTimer>();

        phaseSync =
            FindFirstObjectByType<NetworkPhaseSync>();
    }

    private void Update()
    {
        // Chỉ Server xử lý chuyển phase
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        // Dừng xử lý timer khi Game Over và kích hoạt UI nếu có winner hợp lệ
        if (NetworkGameOverManager.GameEnded)
        {
            string winner = GameRoleManager.Instance != null ? GameRoleManager.Instance.Winner : null;
            if (string.IsNullOrEmpty(winner) && NetworkGameOverManager.Instance != null)
                winner = NetworkGameOverManager.Instance.currentWinner;
            if (!string.IsNullOrEmpty(winner))
            {
                NetworkGameOverManager.Instance.ShowGameOverUI(winner);
            }
            return;
        }

        if (gameTimer == null)
        {
            gameTimer =
                FindFirstObjectByType<NetworkGameTimer>();

            if (gameTimer == null)
                return;
        }

        if (phaseSync == null)
        {
            phaseSync =
                FindFirstObjectByType<NetworkPhaseSync>();

            if (phaseSync == null)
                return;
        }

        GamePhase currentPhase =
            phaseSync.CurrentPhase.Value;

        // =========================
        // KHỞI TẠO
        // =========================

        if (!initialized)
        {
            initialized = true;
            lastPhase = currentPhase;
            return;
        }

        // =========================
        // PHASE ĐÃ ĐỔI
        // =========================

        if (currentPhase != lastPhase)
        {
            Debug.Log(
                "PHASE TIMER CONTROLLER | "
                + lastPhase
                + " → "
                + currentPhase
            );

            lastPhase = currentPhase;
            phaseFinishedHandled = false;
            return;
        }

        // Bỏ qua nếu phase hiện tại là GameOver
        if (currentPhase == GamePhase.GameOver)
            return;

        // =========================
        // TIMER CHƯA HẾT
        // =========================

        if (gameTimer.TimeRemaining.Value > 0f)
        {
            phaseFinishedHandled = false;
            return;
        }

        // =========================
        // TIMER HẾT
        // =========================

        if (phaseFinishedHandled)
            return;

        phaseFinishedHandled = true;
        HandlePhaseFinished(currentPhase);
    }

    private void HandlePhaseFinished(
        GamePhase phase
    )
    {
        if (GameRoleManager.Instance == null)
            return;

        // Không chuyển phase sau khi Game Over
        if (NetworkGameOverManager.GameEnded || phase == GamePhase.GameOver)
            return;

        switch (phase)
        {
            case GamePhase.RoleReveal:
            case GamePhase.Setup:
                HandleRoleRevealFinished();
                break;

            case GamePhase.DayStart:
                HandleDayStartFinished();
                break;

            case GamePhase.Night:
                HandleNightFinished();
                break;

            case GamePhase.Discussion:
                HandleDiscussionFinished();
                break;

            case GamePhase.Voting:
                HandleVotingFinished();
                break;
        }
    }

    private void HandleRoleRevealFinished()
    {
        Debug.Log(
            "PHASE TIMER CONTROLLER | "
            + "RoleReveal / Setup hết giờ → Bắt đầu Day 1 (StartDay())"
        );

        if (GameRoleManager.Instance != null && !NetworkGameOverManager.GameEnded)
        {
            GameRoleManager.Instance.StartDay();
        }
    }

    private void HandleVotingFinished()
    {
        Debug.Log(
            "PHASE TIMER CONTROLLER | "
            + "Voting hết giờ → TriggerFinishVotingOnServer()"
        );

        NetworkPlayerVote.TriggerFinishVotingOnServer();
    }

    private void HandleDayStartFinished()
    {
        Debug.Log(
            "PHASE TIMER CONTROLLER | "
            + "DayStart hết giờ → EndDay()"
        );

        GameRoleManager gameRoleManager =
            GameRoleManager.Instance;

        if (
            gameRoleManager.currentState
            != GameState.Day
        )
        {
            return;
        }

        gameRoleManager.EndDay();
    }

    private void HandleNightFinished()
    {
        Debug.Log("PHASE TIMER CONTROLLER | Night hết giờ -> Kết thúc Night Phase và xử lý tử vong.");

        var aliveBefore = new System.Collections.Generic.Dictionary<int, string>();
        if (PlayerManager.Instance != null && PlayerManager.Instance.players != null)
        {
            foreach (var p in PlayerManager.Instance.players)
            {
                if (p != null && p.isAlive)
                {
                    aliveBefore[p.playerID] = p.DisplayName;
                }
            }
        }

        if (GameRoleManager.Instance != null)
        {
            if (GameRoleManager.Instance.currentState == GameState.Night)
            {
                GameRoleManager.Instance.FinishNight();
            }
            else
            {
                GameRoleManager.Instance.SetPhase(GamePhase.Discussion);
            }
        }

        if (NightManager.Instance != null && NightManager.Instance.HasUnresolvedNightResolution)
        {
            NightManager.Instance.ResolveNight();
        }

        NetworkRoleActionBridge.ResolvePendingShamanKill();
        NetworkGameOverManager.SynchronizeWhiteHoundState();

        // Kiểm tra xem ai đã chết trong đêm nay
        var newlyDead = new System.Collections.Generic.List<int>();
        if (PlayerManager.Instance != null)
        {
            foreach (var kvp in aliveBefore)
            {
                var p = PlayerManager.Instance.GetplayerByID(kvp.Key);
                if (p != null && !p.isAlive)
                {
                    newlyDead.Add(kvp.Key);
                    string causeStr = GetDeathCauseDisplayName(p.lastDeathCause);
                    Debug.Log($"PHASE TIMER CONTROLLER | Nạn nhân chết đêm nay: Player {kvp.Key + 1} ({kvp.Value}) - Nguyên nhân: {causeStr}");
                    NetworkPhaseSync.Instance?.AnnounceNightDeath(kvp.Key, kvp.Value, causeStr);
                }
            }
        }

        if (newlyDead.Count == 0)
        {
            Debug.Log("PHASE TIMER CONTROLLER | Đêm qua là một đêm bình yên, không ai chết.");
            NetworkPhaseSync.Instance?.AnnounceNightPeaceful();
        }

        NetworkGameOverManager.Instance?.CheckServerWinCondition(forceCheck: true);
    }

    private static string GetDeathCauseDisplayName(DeathCause cause)
    {
        return cause switch
        {
            DeathCause.Monster => "Sói tấn công",
            DeathCause.Poison => "Trúng độc",
            DeathCause.Trap => "Bẫy Thợ Săn",
            DeathCause.Killer => "Sát nhân",
            DeathCause.Curse => "Lời nguyền",
            DeathCause.Vote => "Bỏ phiếu",
            _ => "Bị tiêu diệt"
        };
    }

    private void HandleDiscussionFinished()
    {
        Debug.Log("PHASE TIMER CONTROLLER | Discussion hết giờ -> Bắt đầu Voting");
        NetworkGameOverManager.Instance?.CheckServerWinCondition(forceCheck: true);
        if (GameRoleManager.Instance != null && !NetworkGameOverManager.GameEnded)
        {
            if (GameRoleManager.Instance.currentState == GameState.Discussion)
            {
                GameRoleManager.Instance.StartVoting();
            }
            else
            {
                GameRoleManager.Instance.SetPhase(GamePhase.Voting);
            }
        }
    }

    public void ResetForNewMatch()
    {
        lastPhase = GamePhase.Setup;
        phaseFinishedHandled = false;
        initialized = false;
        Debug.Log("PHASE TIMER CONTROLLER: Reset For New Match hoàn tất.");
    }
}