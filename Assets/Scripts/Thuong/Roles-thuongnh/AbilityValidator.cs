public enum ActionFailureReason
{
    None, MatchNotRunning, ActorMissing, ActorDead, RoleMissing, NoAbility, WrongPhase,
    AlreadyUsed, Silenced, TargetMissing, TargetDead, SelfTargetForbidden, FactionForbidden, RepeatedTarget
}

public readonly struct ActionValidationResult
{
    public bool IsValid { get; }
    public ActionFailureReason FailureReason { get; }
    public string Message { get; }
    private ActionValidationResult(bool valid, ActionFailureReason reason, string message)
    { IsValid = valid; FailureReason = reason; Message = message; }
    public static ActionValidationResult Success(string message = "Hợp lệ.") => new(true, ActionFailureReason.None, message);
    public static ActionValidationResult Fail(ActionFailureReason reason, string message) => new(false, reason, message);
}

public static class TargetValidator
{
    public static ActionValidationResult Validate(PlayerData actor, PlayerData target, ThuongRoleDefinition role)
    {
        if (actor == null) return ActionValidationResult.Fail(ActionFailureReason.ActorMissing, "Không tìm thấy người dùng.");
        if (role == null) return ActionValidationResult.Fail(ActionFailureReason.RoleMissing, "Chưa gán role asset.");
        if (target == null) return ActionValidationResult.Fail(ActionFailureReason.TargetMissing, "Hãy chọn mục tiêu.");
        if (!role.canTargetDead && !target.isAlive)
            return ActionValidationResult.Fail(ActionFailureReason.TargetDead, "Mục tiêu đã chết.");
        if (!role.allowSelfTarget && actor.playerID == target.playerID)
            return ActionValidationResult.Fail(ActionFailureReason.SelfTargetForbidden, "Không được chọn chính mình.");
        bool allowed = role.targetFactionRule switch
        {
            TargetFactionRule.Any => true,
            TargetFactionRule.SameFaction => actor.faction == target.faction,
            TargetFactionRule.OtherFaction => actor.faction != target.faction,
            TargetFactionRule.VillageOnly => target.faction == FactionType.Villager,
            TargetFactionRule.WolvesOnly => target.faction == FactionType.Monster,
            TargetFactionRule.NeutralOnly => target.faction == FactionType.Neutral,
            _ => false
        };
        return allowed ? ActionValidationResult.Success() :
            ActionValidationResult.Fail(ActionFailureReason.FactionForbidden, "Không được chọn phe này.");
    }
}

public static class AbilityValidator
{
    public static ActionValidationResult Validate(bool running, GamePhase phase, PlayerData actor, PlayerData target, int uses)
    {
        if (!running) return ActionValidationResult.Fail(ActionFailureReason.MatchNotRunning, "Trận chưa bắt đầu hoặc đã kết thúc.");
        if (actor == null) return ActionValidationResult.Fail(ActionFailureReason.ActorMissing, "Không tìm thấy người dùng.");
        if (!actor.isAlive) return ActionValidationResult.Fail(ActionFailureReason.ActorDead, "Người đã chết không thể dùng kỹ năng.");
        var role = actor.roleDefinition;
        if (role == null) return ActionValidationResult.Fail(ActionFailureReason.RoleMissing, "Chưa gán role asset.");
        if (role.abilityType == AbilityType.None) return ActionValidationResult.Fail(ActionFailureReason.NoAbility, "Role không có kỹ năng chủ động.");
        if (phase != role.usablePhase) return ActionValidationResult.Fail(ActionFailureReason.WrongPhase, $"Kỹ năng chỉ dùng trong {role.usablePhase}.");
        if (uses >= role.maxUsesPerPhase) return ActionValidationResult.Fail(ActionFailureReason.AlreadyUsed, "Đã dùng hết lượt kỹ năng trong phase này.");
        // The legacy isSilenced flag also locks movement; only the explicit effect blocks abilities here.
        if (actor.HasEffect(StatusEffectType.Silenced)) return ActionValidationResult.Fail(ActionFailureReason.Silenced, "Người chơi đang bị Silenced.");
        return TargetValidator.Validate(actor, target, role);
    }
}
