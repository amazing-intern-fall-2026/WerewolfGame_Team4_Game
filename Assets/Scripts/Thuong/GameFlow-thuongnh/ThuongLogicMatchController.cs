using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ThuongLogicMatchController : MonoBehaviour
{
    public PlayerManager players;
    public GameRoleManager phaseController;
    public RoleManager roles;
    public DeathResolver deathSystem;
    public StatusEffectSystem statusSystem;
    public AbilitySystem abilitySystem;
    public NightManager nightSystem;
    public VoteManager voteSystem;
    public WinConditionManager winSystem;
    public bool IsMatchRunning { get; private set; }
    public event Action MatchStarted;
    public event Action MatchEnded;
    private bool resolving;
    private bool subscribed;
    private void OnEnable()
    {
        if (!Application.isPlaying || deathSystem == null) return;
        BindSystems();
        Subscribe();
    }
    private void OnDisable()
    {
        if (subscribed && deathSystem != null) deathSystem.DeathResolved -= HandleDeath;
        subscribed = false;
    }
    private void Subscribe()
    {
        if (subscribed) return;
        deathSystem.DeathResolved += HandleDeath;
        subscribed = true;
    }
    private void Start() => BindSystems();
    private void BindSystems()
    {
        // Fast Play Mode can retain stale statics from editor previews. The offline scene
        // owns these existing managers, so bind the serialized references before use.
        if (phaseController == null || !phaseController.manualLogicPrototype) return;
        PlayerManager.Instance = players;
        GameRoleManager.Instance = phaseController;
        RoleManager.Instance = roles;
        DeathResolver.Instance = deathSystem;
        StatusEffectSystem.Instance = statusSystem;
        AbilitySystem.Instance = abilitySystem;
        NightManager.Instance = nightSystem;
        VoteManager.Instance = voteSystem;
        WinConditionManager.Instance = winSystem;
    }
    private void OnDestroy() { if (subscribed && deathSystem != null) deathSystem.DeathResolved -= HandleDeath; }

    public bool ValidateRoster(out string message)
    {
        var ids = new HashSet<int>();
        if (players == null || players.players == null || players.players.Count == 0)
        { message = "Chưa có người chơi."; return false; }
        foreach (var player in players.players)
        {
            if (player == null || !ids.Add(player.playerID))
            { message = "Player ID trùng hoặc dữ liệu người chơi bị thiếu."; return false; }
            if (player.roleDefinition == null)
            { message = $"{player.DisplayName} chưa có role asset."; return false; }
        }
        message = "Hợp lệ.";
        return true;
    }

    public void StartMatch()
    {
        BindSystems();
        if (!ValidateRoster(out string message)) { Debug.LogError(message); return; }
        Subscribe();
        IsMatchRunning = false;
        abilitySystem.SetMatchRunning(false);
        nightSystem.ResetForNewMatch();
        voteSystem.StartVote();
        foreach (var player in players.players) player.ResetForNewMatch();
        roles.AssignConfiguredRoles();
        phaseController.ResetLogicMatch();
        IsMatchRunning = true;
        abilitySystem.SetMatchRunning(true);
        MatchStarted?.Invoke();
        EnterPhase(GamePhase.Night);
    }

    public void AdvanceMatch()
    {
        if (!IsMatchRunning || resolving) return;
        var phase = phaseController.currentPhase;
        // Vote uses the existing VoteManager; resolve before DayResolution expiry.
        var next = phase switch
        {
            GamePhase.Night => GamePhase.ResolveNight,
            GamePhase.ResolveNight => GamePhase.Discussion,
            GamePhase.Discussion => GamePhase.Voting,
            GamePhase.Voting => GamePhase.DayResolution,
            GamePhase.DayResolution => GamePhase.Night,
            _ => phase
        };
        if (phase == GamePhase.DayResolution) phaseController.currentDay++;
        EnterPhase(next);
    }

    private void EnterPhase(GamePhase phase)
    {
        resolving = true;
        try
        {
            if (phaseController.currentPhase == GamePhase.Voting && phase == GamePhase.DayResolution)
                voteSystem.ResolveVote();
            // Tick only old effects before new night actions. Curses applied tonight start next resolution.
            statusSystem.ProcessPhaseStart(phase, players.players);
            abilitySystem.BeginPhase(phase);
            if (phase == GamePhase.Night) nightSystem.StartNight();
            phaseController.SetPhase(phase);
            if (phase == GamePhase.ResolveNight) nightSystem.ResolveNight();
            if (phase == GamePhase.Voting) voteSystem.StartVote();
            // Evaluate after the entire batch so simultaneous deaths cannot declare an early winner.
            EvaluateWin();
        }
        finally { resolving = false; }
    }

    private void HandleDeath(DeathResult result)
    {
        if (IsMatchRunning && !resolving && result.Outcome == DeathOutcome.Killed) EvaluateWin();
    }
    private void EvaluateWin()
    {
        winSystem.CheckWinCondition();
        if (!IsMatchRunning || phaseController.currentState != GameState.GameOver) return;
        IsMatchRunning = false;
        abilitySystem.SetMatchRunning(false);
        nightSystem.ClearActions();
        MatchEnded?.Invoke();
    }
}
