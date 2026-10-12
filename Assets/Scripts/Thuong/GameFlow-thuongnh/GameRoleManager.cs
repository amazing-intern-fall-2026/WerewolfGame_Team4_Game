using UnityEngine;

public class GameRoleManager : MonoBehaviour
{
    public static GameRoleManager Instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSingleton() => Instance = null;
    public GameState currentState;
    public GamePhase currentPhase;
    public int currentDay = 1;
    [Min(0.1f)] public float nightDuration = 15f;
    [Min(0.1f)] public float discussionDuration = 30f;
    [Min(0.1f)] public float votingDuration = 20f;
    [Min(0.1f)] public float roleRevealDuration = 25f;
    public string Winner { get; private set; }
    public float PhaseTimeRemaining { get; private set; }
    private bool started;
    [Header("Thuong logic prototype")]
    public bool manualLogicPrototype;
    [Header("Phase 1-4 integration (optional for legacy scenes)")]
    public StatusEffectSystem statusEffects;
    public AbilitySystem abilities;
    public bool HasIntegratedLogic => !manualLogicPrototype && statusEffects != null && abilities != null;
    private bool resolving;
    private DeathResolver observedDeathSystem;
    public event System.Action<GamePhase, GamePhase> PhaseChanged;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    private void OnDestroy()
    {
        if (observedDeathSystem != null) observedDeathSystem.DeathResolved -= HandleDeath;
        if (Instance == this) Instance = null;
    }
    private void Start() { if (!manualLogicPrototype) BeginGame(); }
    private EventManager PrepareEventManager()
    {
        if (EventManager.Instance != null)
            return EventManager.Instance;

        // Các manager gameplay của prototype phải nằm trên object đang active.
        // AddComponent gọi Awake và thiết lập Instance khi object đang active.
        return gameObject.AddComponent<EventManager>();
    }
    public void BeginGame()
    {
        if (manualLogicPrototype) return;
        Debug.Log("GAMEROLEMANAGER: BeginGame() chạy");
        
        if (started)
            return;

        if (TaskManager.Instance == null ||
            DayTimer.Instance == null ||
            PlayerManager.Instance == null ||
            NightManager.Instance == null ||
            VoteManager.Instance == null ||
            DeathResolver.Instance == null ||
            WinConditionManager.Instance == null)
        {
            Debug.LogError("GameRoleManager: missing gameplay managers.");
            enabled = false;
            return;
        }

        EventManager events = PrepareEventManager();

        started = true;
        if (HasIntegratedLogic)
        {
            abilities.SetMatchRunning(false);
            NightManager.Instance.ResetForNewMatch();
            observedDeathSystem = statusEffects.deathSystem;
            if (observedDeathSystem != null) observedDeathSystem.DeathResolved += HandleDeath;
        }

        if (RoleManager.Instance != null)
            RoleManager.Instance.AssignRole();
        if (HasIntegratedLogic) abilities.SetMatchRunning(true);

        bool hasEventSchedule = events.InitializeForMatch(
            WinConditionManager.Instance.MaxDay);

        if (!hasEventSchedule)
        {
            // Lỗi phần debug không ngăn flow gameplay hiện có chạy tiếp.
            Debug.LogWarning(
                "[Event Debug] Ván tiếp tục nhưng chưa có lịch event.");
        }

        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.Count > 0)
        {
            SetPhase(GamePhase.RoleReveal);
            PhaseTimeRemaining = roleRevealDuration;
        }
        else
        {
            Debug.LogWarning("GameRoleManager: no assigned roles to reveal; starting day.");
            StartDay();
        }
    }
    private void Update()
    {
        if (manualLogicPrototype || !started || currentState == GameState.GameOver || currentState == GameState.Day) return;
        PhaseTimeRemaining = Mathf.Max(0, PhaseTimeRemaining - Time.deltaTime);
        if (PhaseTimeRemaining > 0) return;
        if (currentState == GameState.RoleReveal)
        {
            StartDay();
        }
        else if (currentState == GameState.Night)
        {
            FinishNight();
        }
        else if (currentState == GameState.Discussion) StartVoting();
        else if (currentState == GameState.Voting) FinishVoting();
    }
    public void SetPhase(GamePhase phase)
    {
        if (currentState == GameState.GameOver) return;
        GamePhase previous = currentPhase;
        currentPhase = phase;
        switch (phase)
        {
            case GamePhase.RoleReveal: currentState = GameState.RoleReveal; break;
            case GamePhase.DayStart: case GamePhase.Task: currentState = GameState.Day; break;
            case GamePhase.Night: case GamePhase.ResolveNight: currentState = GameState.Night; break;
            case GamePhase.Discussion: currentState = GameState.Discussion; break;
            case GamePhase.Voting: currentState = GameState.Voting; break;
            case GamePhase.ResolveVote: currentState = GameState.VotingResult; break;
            case GamePhase.GameOver: currentState = GameState.GameOver; break;
            case GamePhase.Setup: currentState = GameState.RoleReveal; break;
            case GamePhase.DayResolution: currentState = GameState.VotingResult; break;
        }
        if (HasIntegratedLogic && previous != phase)
        {
            bool wasResolving = resolving;
            resolving = true;
            try
            {
                statusEffects.ProcessPhaseStart(phase, PlayerManager.Instance.players);
                abilities.BeginPhase(phase);
            }
            finally { resolving = wasResolving; }
        }
        if (previous != phase) PhaseChanged?.Invoke(previous, phase);
    }
    public void StartDay()
    {
        if (!started || currentState == GameState.GameOver)
            return;

        SetPhase(GamePhase.DayStart);
        RoleManager.Instance?.NotifyDayStart();
        PlayerManager.Instance.UnlockPlayers();
        TaskManager.Instance.StartNewDay();
        DayTimer.Instance.StartTimer();

        // Chỉ kiểm tra/log lịch đã tạo, tuyệt đối không random ở đây.
        if (EventManager.Instance != null)
            EventManager.Instance.NotifyDayStarted(currentDay);
        if (HasIntegratedLogic) WinConditionManager.Instance.CheckWinCondition();
    }
    public void SkipRoleReveal()
    {
        if (!started || currentState != GameState.RoleReveal) return;
        PhaseTimeRemaining = 0;
        StartDay();
    }
    public void EndDay()
    {
        if (!started || currentState != GameState.Day) return;
        DayTimer.Instance.StopTimer();
        PlayerManager.Instance.LockPlayers();
        SetPhase(GamePhase.Night);
        NightManager.Instance.StartNight();
        PhaseTimeRemaining = nightDuration;
    }
    public void StartVoting()
    {
        if (currentState != GameState.Discussion) return;
        SetPhase(GamePhase.Voting);
        RoleManager.Instance?.NotifyVoteStart();
        VoteManager.Instance.StartVote();
        PhaseTimeRemaining = votingDuration;
    }
    public void FinishVoting()
    {
        if (currentState != GameState.Voting || resolving) return;
        resolving = true;
        try
        {
            SetPhase(GamePhase.ResolveVote);
            // Vote and Hunter trap use the pre-expiry state; then tick DayResolution effects.
            VoteManager.Instance.ResolveVote();
            ResolveHunterTraps();
            if (HasIntegratedLogic && currentState != GameState.GameOver) SetPhase(GamePhase.DayResolution);
            WinConditionManager.Instance.CheckWinCondition(true);
            if (currentState == GameState.GameOver) return;
            currentDay++;
            StartDay();
        }
        finally { resolving = false; }
    }
    public void FinishNight()
    {
        if (!started || currentState != GameState.Night || resolving) return;
        resolving = true;
        try
        {
            if (HasIntegratedLogic) SetPhase(GamePhase.ResolveNight);
            NightManager.Instance.ResolveNight();
            WinConditionManager.Instance.CheckWinCondition();
            if (currentState == GameState.GameOver) return;
            SetPhase(GamePhase.Discussion);
            if (HasIntegratedLogic) WinConditionManager.Instance.CheckWinCondition();
            if (currentState != GameState.GameOver) PhaseTimeRemaining = discussionDuration;
        }
        finally { resolving = false; }
    }
    private void HandleDeath(DeathResult result)
    {
        if (started && HasIntegratedLogic && !resolving && result.Outcome == DeathOutcome.Killed)
            WinConditionManager.Instance.CheckWinCondition();
    }
    private void ResolveHunterTraps()
    {
        if (RoleManager.Instance == null)
            return;

        foreach (BaseRole role in RoleManager.Instance.playerRoles.Values)
            if (role is HunterRole hunter)
                hunter.ResolveTrap();
    }
    public void VillagerWin() { EndGame("Villagers"); }
    public void WerewolfWin() { EndGame("Werewolves"); }
    public void LoversWin() { EndGame("Lovers"); }
    public void WhiteWolfWin() { EndGame("White Wolf"); }
    public void KillerWin() { EndGame("Killer"); }
    public void MadmanWin() { EndGame("Madman"); }
    public void FoxSpiritWin() { EndGame("Fox Spirit"); }
    public void EndLogicGame(string winner) { if (manualLogicPrototype) EndGame(winner); }
    public void ResetLogicMatch()
    {
        if (!manualLogicPrototype) return;
        Winner = null;
        PhaseTimeRemaining = 0;
        currentState = GameState.RoleReveal;
        currentPhase = GamePhase.Setup;
        currentDay = 1;
    }
    private void EndGame(string winner)
    {
        if (currentState == GameState.GameOver) return;
        if (winner != "Fox Spirit" && WinConditionManager.Instance != null &&
            WinConditionManager.Instance.FoxSpiritHasWon())
            winner = "Fox Spirit";
        Winner = winner;
        SetPhase(GamePhase.GameOver);
        if (HasIntegratedLogic)
        {
            abilities.SetMatchRunning(false);
            NightManager.Instance.ClearActions();
        }
        PhaseTimeRemaining = 0;
        DayTimer.Instance?.StopTimer();
        PlayerManager.Instance?.LockPlayers();
        Debug.Log(winner + " Win");
    }
}


