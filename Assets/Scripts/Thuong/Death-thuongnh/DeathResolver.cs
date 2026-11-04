using UnityEngine;

public class DeathResolver:MonoBehaviour
{
    public static DeathResolver Instance;
    public bool consumeProtectionOnBlock = true;
    public System.Collections.Generic.List<DeathCause> protectionBlocks = new() { DeathCause.Monster, DeathCause.Killer, DeathCause.Ability };
    public event System.Action<DeathResult> DeathResolved;
    public event System.Action DeathBatchResolved;
    private int batchDepth;
    private bool batchChanged;
    public System.IDisposable BeginBatch()
    {
        batchDepth++;
        return new ResolutionBatch(this);
    }
    private sealed class ResolutionBatch : System.IDisposable
    {
        private DeathResolver owner;
        public ResolutionBatch(DeathResolver owner) { this.owner = owner; }
        public void Dispose()
        {
            if (owner == null) return;
            var resolver = owner;
            owner = null;
            if (--resolver.batchDepth != 0 || !resolver.batchChanged) return;
            resolver.batchChanged = false;
            resolver.DeathBatchResolved?.Invoke();
        }
    }

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
        using var batch = BeginBatch();
        var target = request.Target;
        var cause = request.Cause;
        var sourceRole = request.SourceRole;
        if (target == null || PlayerManager.Instance?.GetplayerByID(target.playerID) != target)
            return Publish(request, DeathOutcome.InvalidRequest, "Không tìm thấy mục tiêu hợp lệ.");
        if (!target.isAlive)
            return Publish(request, DeathOutcome.IgnoredAlreadyDead, $"{target.DisplayName} đã chết trước đó.");
        if (ThuongLogicSceneScope.IsActive && GameRoleManager.Instance?.currentState == GameState.GameOver)
            return Publish(request, DeathOutcome.InvalidRequest, "Trận đấu đã kết thúc.");
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
            if (ThuongLogicSceneScope.IsActive)
            {
                target.faction = FactionType.Monster;
                target.NotifyChanged();
            }
            batchChanged = true;
            return Publish(request, DeathOutcome.Prevented, $"{target.DisplayName} thức tỉnh thành Bạch Khuyển.");
        }

        target.isAlive = false;
        target.hasDeathRecord = true;
        target.lastDeathCause = cause;
        target.lastDeathSourceID = request.Source?.playerID ?? -1;
        target.wasVotedOut = cause == DeathCause.Vote;
        batchChanged = true;
        if (ThuongLogicSceneScope.IsActive)
        {
            if (StatusEffectSystem.Instance != null) StatusEffectSystem.Instance.ClearEffects(target);
            else { target.effects?.Clear(); target.isCursed = false; }
            target.status.isProtected = false;
        }
        target.NotifyChanged();
        NetworkPlayerStateSync.SyncGameplayAliveState(targetID, false);
        if (target.roleType == RoleType.Hunter && !target.hasUsedHunterShot)
        {
            target.hasHunterTrap = true;
            Debug.Log($"[DeathResolver] Thợ Săn ({target.DisplayName}) bị tiêu diệt do {cause} -> Nhận quyền bắn phát đạn trả thù 1 lần (hasHunterTrap = true).");
        }

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

