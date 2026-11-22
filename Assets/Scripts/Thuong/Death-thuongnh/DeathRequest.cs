public enum DeathOutcome { Killed, Prevented, IgnoredAlreadyDead, InvalidRequest }

public readonly struct DeathRequest
{
    public PlayerData Target { get; }
    public PlayerData Source { get; }
    public DeathCause Cause { get; }
    public RoleType? SourceRole { get; }
    public bool BypassProtection { get; }
    public int AttackEventId { get; }
    public DeathRequest(PlayerData target, DeathCause cause, PlayerData source = null,
        bool bypassProtection = false, RoleType? sourceRole = null, int attackEventId = 0)
    {
        Target = target;
        Cause = cause;
        Source = source;
        SourceRole = sourceRole ?? source?.roleType;
        BypassProtection = bypassProtection;
        AttackEventId = attackEventId;
    }
}

public readonly struct DeathResult
{
    public DeathRequest Request { get; }
    public DeathOutcome Outcome { get; }
    public string Message { get; }
    public DeathResult(DeathRequest request, DeathOutcome outcome, string message)
    {
        Request = request;
        Outcome = outcome;
        Message = message;
    }
}
