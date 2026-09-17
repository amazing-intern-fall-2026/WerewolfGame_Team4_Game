using UnityEngine;

public class GameRoleManager : MonoBehaviour
{
    public static GameRoleManager Instance;
    public GameState currentState;
    public GamePhase currentPhase;
    public int currentDay = 1;
    [Min(0.1f)] public float nightDuration = 15f;
    [Min(0.1f)] public float discussionDuration = 30f;
    [Min(0.1f)] public float votingDuration = 20f;
    public string Winner { get; private set; }
    public float PhaseTimeRemaining { get; private set; }
    private bool started;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Start() { BeginGame(); }
    private EventManagert PrepareEventManager()
    {
        if (EventManagert.Instance != null)
            return EventManagert.Instance;

        // Các manager gameplay của prototype phải nằm trên object đang active.
        // AddComponent gọi Awake và thiết lập Instance khi object đang active.
        return gameObject.AddComponent<EventManagert>();
    }
    public void BeginGame()
    {
        if (started)
            return;

        if (TaskManager.Instance == null ||
            DayTimer.Instance == null ||
            PlayerManger.Instance == null ||
            NightManager.Instance == null ||
            VoteManger.Instance == null ||
            DeathResolver.Instance == null ||
            WinConditionManager.Instance == null)
        {
            Debug.LogError("GameRoleManager: missing gameplay managers.");
            enabled = false;
            return;
        }

        EventManagert events = PrepareEventManager();

        started = true;

        if (RoleManger.Instance != null)
            RoleManger.Instance.AssignRole();

        bool hasEventSchedule = events.InitializeForMatch(
            WinConditionManager.Instance.MaxDay);

        if (!hasEventSchedule)
        {
            // Lỗi phần debug không ngăn flow gameplay hiện có chạy tiếp.
            Debug.LogWarning(
                "[Event Debug] Ván tiếp tục nhưng chưa có lịch event.");
        }

        StartDay();
    }
    private void Update()
    {
        if (!started || currentState == GameState.GameOver || currentState == GameState.Day) return;
        PhaseTimeRemaining = Mathf.Max(0, PhaseTimeRemaining - Time.deltaTime);
        if (PhaseTimeRemaining > 0) return;
        if (currentState == GameState.Nigt)
        {
            NightManager.Instance.ResolveNight();
            WinConditionManager.Instance.CheckWinCondition();
            if (currentState == GameState.GameOver) return;
            SetPhase(GamePhase.Discussion);
            PhaseTimeRemaining = discussionDuration;
        }
        else if (currentState == GameState.Discussion) StartVoting();
        else if (currentState == GameState.Voting) FinishVoting();
    }
    public void SetPhase(GamePhase phase)
    {
        if (currentState == GameState.GameOver) return;
        currentPhase = phase;
        switch (phase)
        {
            case GamePhase.DayStart: case GamePhase.Task: currentState = GameState.Day; break;
            case GamePhase.Night: case GamePhase.ResolveNight: currentState = GameState.Nigt; break;
            case GamePhase.Discussion: currentState = GameState.Discussion; break;
            case GamePhase.Voting: currentState = GameState.Voting; break;
            case GamePhase.ResolveVote: currentState = GameState.VotingResult; break;
            case GamePhase.GameOver: currentState = GameState.GameOver; break;
        }
    }
    public void StartDay()
    {
        if (!started || currentState == GameState.GameOver)
            return;

        SetPhase(GamePhase.DayStart);
        PlayerManger.Instance.UnlockPlayers();
        TaskManager.Instance.StartNewDay();
        DayTimer.Instance.StartTimer();

        // Chỉ kiểm tra/log lịch đã tạo, tuyệt đối không random ở đây.
        if (EventManagert.Instance != null)
            EventManagert.Instance.NotifyDayStarted(currentDay);
    }
    public void EndDay()
    {
        if (!started || currentState != GameState.Day) return;
        DayTimer.Instance.StopTimer();
        PlayerManger.Instance.LockPlayers();
        SetPhase(GamePhase.Night);
        NightManager.Instance.StartNight();
        PhaseTimeRemaining = nightDuration;
    }
    public void StartVoting()
    {
        if (currentState != GameState.Discussion) return;
        SetPhase(GamePhase.Voting);
        VoteManger.Instance.StartVote();
        PhaseTimeRemaining = votingDuration;
    }
    public void FinishVoting()
    {
        if (currentState != GameState.Voting) return;
        SetPhase(GamePhase.ResolveVote);
        VoteManger.Instance.ResolveVote();
        WinConditionManager.Instance.CheckWinCondition(true);
        if (currentState == GameState.GameOver) return;
        currentDay++;
        StartDay();
    }
    public void VillagerWin() { EndGame("Villagers"); }
    public void WerewolfWin() { EndGame("Werewolves"); }
    public void LoversWin() { EndGame("Lovers"); }
    private void EndGame(string winner)
    {
        if (currentState == GameState.GameOver) return;
        Winner = winner;
        SetPhase(GamePhase.GameOver);
        PhaseTimeRemaining = 0;
        DayTimer.Instance?.StopTimer();
        PlayerManger.Instance?.LockPlayers();
        Debug.Log(winner + " Win");
    }
}

