using UnityEngine;

public class DeathResolver:MonoBehaviour
{
    public static DeathResolver Instance;
    public bool consumeProtectionOnBlock = true;
    public System.Collections.Generic.List<DeathCause> protectionBlocks = new() { DeathCause.Monster, DeathCause.Killer, DeathCause.Ability };
    public event System.Action<DeathResult> DeathResolved;

    private void Awake()
    {
        Instance = this;
    }
    public bool TryKillPlayer(int targetID, DeathCause cause)
    {
        return TryKillPlayer(targetID, cause, null);
    }

    public bool TryKillPlayer(int targetID, DeathCause cause, RoleType? sourceRole)
    {
        return TryKill(new DeathRequest(PlayerManager.Instance?.GetplayerByID(targetID), cause,
            sourceRole: sourceRole)).Outcome == DeathOutcome.Killed;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public DeathResult TryKill(DeathRequest request)
    {
        var target = request.Target;
        var cause = request.Cause;
        var sourceRole = request.SourceRole;
        if (target == null || PlayerManager.Instance?.GetplayerByID(target.playerID) != target)
            return Publish(request, DeathOutcome.InvalidRequest, "Không tìm thấy mục tiêu hợp lệ.");
        if (!target.isAlive)
            return Publish(request, DeathOutcome.IgnoredAlreadyDead, $"{target.DisplayName} đã chết trước đó.");
        int targetID = target.playerID;
        if (target.roleType == RoleType.Idiot && cause == DeathCause.Vote)
        {
            return Publish(request, DeathOutcome.Prevented, $"{target.DisplayName} miễn nhiễm bỏ phiếu theo luật Thằng Khờ.");
        }
        target.status ??= new PlayerStatus();
        bool protectedByEffect = target.HasEffect(StatusEffectType.Protected);
        if (!request.BypassProtection && protectionBlocks.Contains(cause) &&
            (protectedByEffect || target.status.isProtected))
        {
            if (consumeProtectionOnBlock)
            {
                if (protectedByEffect)
                {
                    if (StatusEffectSystem.Instance != null) StatusEffectSystem.Instance.ConsumeProtection(target);
                    else
                    {
                        target.effects.Remove(target.effects.Find(effect => effect.Type == StatusEffectType.Protected));
                        target.NotifyChanged();
                    }
                }
                else { target.status.isProtected = false; target.NotifyChanged(); }
            }
            return Publish(request, DeathOutcome.Prevented, $"{target.DisplayName} được bảo vệ và sống sót.");
        }
        if (target.status.isCharmed &&
            (cause == DeathCause.Monster || cause == DeathCause.Killer))
        {
            return Publish(request, DeathOutcome.Prevented, $"{target.DisplayName} được bùa mê bảo vệ.");
        }

        if (target.roleType == RoleType.WhiteHound &&
            sourceRole == RoleType.DogSpirit && !target.isWhiteHoundAwakened)
        {
            if (RoleManager.Instance != null &&
                RoleManager.Instance.playerRoles.TryGetValue(targetID, out var whiteHoundRole) &&
                whiteHoundRole is WhiteHound whiteHound)
            {
                whiteHound.BecomeMonster();
            }
            else
            {
                target.faction = FactionType.Monster;
                target.isWhiteHoundAwakened = true;
            }
            target.NotifyChanged();
            return Publish(request, DeathOutcome.Prevented, $"{target.DisplayName} thức tỉnh thành Bạch Khuyển.");
        }

        // Record before the alive-state event so UI observes a complete death.
        target.hasDeathRecord = true;
        target.lastDeathCause = cause;
        PlayerManager.Instance.SetAliveState(targetID, false);
        NetworkPlayerStateSync.SyncGameplayAliveState(targetID, false);
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(targetID, out var role))
            role.OnDeath();

        LoverManager.Instance?.LoverDied(target);
        return Publish(request, DeathOutcome.Killed, $"{target.DisplayName} chết do {cause}.");
    }

    private DeathResult Publish(DeathRequest request, DeathOutcome outcome, string message)
    {
        var result = new DeathResult(request, outcome, message);
        DeathResolved?.Invoke(result);
        return result;
    }
}

