// Adapts the existing Nhật role classes to the night schedule of Thương.
public static class RoleActionRules
{
    public static bool TryUseNightAbility(BaseRole role, int targetID, out string feedback)
    {
        NightManager night = NightManager.Instance;
        if (night == null)
        {
            feedback = "Chưa có hệ thống xử lý ban đêm.";
            return false;
        }

        if (ThuongLogicSceneScope.IsActive && StatusEffectSystem.Instance != null)
        {
            var target = PlayerManager.Instance?.GetplayerByID(targetID);
            if (role is CursedRole)
            {
                bool applied = StatusEffectSystem.Instance.ApplyEffect(target, StatusEffectType.Cursed,
                    1, 2, GamePhase.Discussion, role.owner) != null;
                feedback = applied ? "Đã đặt lời nguyền trong hai đêm." : "Không thể đặt lời nguyền lên mục tiêu này.";
                return applied;
            }
            if (role is ShamanRole shaman && target != null)
            {
                bool protectedBefore = target.status?.isProtected ?? false;
                bool movementBefore = target.status?.isSilenced ?? false;
                bool hadGood = shaman.HasGoodCharm;
                if (!shaman.TryUseNightAbility(targetID, out feedback)) return false;
                bool good = hadGood && !shaman.HasGoodCharm;
                // Keep the legacy role's one-use inventory; translate only its effect in this flow.
                target.status.isProtected = protectedBefore;
                target.status.isSilenced = movementBefore;
                StatusEffectSystem.Instance.ApplyEffect(target,
                    good ? StatusEffectType.Protected : StatusEffectType.Silenced, 1, 1, GamePhase.Discussion, role.owner);
                return true;
            }
        }

        if (role.roleType == RoleType.SerpentSpirit && night.CurrentNightNumber <= 2)
        {
            feedback = "Xà Tinh chỉ có thể tấn công từ đêm thứ ba.";
            return false;
        }

        if (role.roleType == RoleType.Killer)
        {
            int currentNight = night.CurrentNightNumber;
            if (currentNight <= 0 && NetworkPhaseSync.Instance != null)
            {
                currentNight = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
            }
            if (currentNight <= 0 && GameRoleManager.Instance != null)
            {
                currentNight = GameRoleManager.Instance.currentDay;
            }

            if (currentNight <= 0 || currentNight % 2 != 0)
            {
                feedback = $"Sát Nhân chỉ có thể tấn công vào đêm chẵn (Đêm hiện tại: {currentNight}).";
                return false;
            }

            // KillerRole's private use counter is independent of the match night.
            // Schedule from the authoritative night number so skipped turns work.
            night.SetKillerTarget(targetID);
            feedback = $"Đã chọn Player {targetID + 1} để tấn công đêm nay.";
            return true;
        }

        if (!role.TryUseNightAbility(targetID, out feedback))
            return false;

        switch (role.roleType)
        {
            case RoleType.SerpentSpirit:
            case RoleType.Ogre:
                feedback = $"Đã chọn Player {targetID + 1} để tấn công.";
                break;
            case RoleType.Cursed:
                feedback = $"Đã nguyền Player {targetID + 1}.";
                break;
            case RoleType.FoxSpirit:
                feedback = $"Đã mê hoặc Player {targetID + 1}.";
                break;
            case RoleType.WeaverOfFate:
                feedback = $"Đã ghép đôi với Player {targetID + 1}.";
                break;
        }
        return true;
    }
}
