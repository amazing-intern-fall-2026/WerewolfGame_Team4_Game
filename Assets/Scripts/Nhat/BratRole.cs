using UnityEngine;

public class BratRole : BaseRole
{
    public const float CaughtChance = 0.25f;

    public BratRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Brat;
        faction = FactionType.Villager;
    }

    public override void UseNightAbility(int targetID)
    {
        TryPeek(out _);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        return TryPeek(out feedback);
    }

    public bool TryPeek(out string feedback)
    {
        GameRoleManager game = GameRoleManager.Instance;
        bool isNight = ThuongLogicSceneScope.IsActive
            ? game != null && game.currentPhase == GamePhase.Night
            : game != null && game.currentState == GameState.Night;

        if (owner == null || !owner.isAlive)
        {
            feedback = "Bạn đã bị loại và không thể nhìn trộm.";
            return false;
        }

        if (!isNight)
        {
            feedback = "Chỉ có thể nhìn trộm vào ban đêm.";
            return false;
        }

        if (owner.hasUseNightAction)
        {
            feedback = "Bạn đã nhìn trộm trong đêm này.";
            return false;
        }

        if (owner.HasEffect(StatusEffectType.Silenced))
        {
            feedback = "Bạn đang bị khóa kỹ năng.";
            return false;
        }

        if (NightManager.Instance == null || DeathResolver.Instance == null)
        {
            feedback = "Hệ thống xử lý đêm chưa sẵn sàng.";
            return false;
        }

        owner.hasUseNightAction = true;
    NightManager.Instance.MarkBratPeekSubmitted();
        if (Random.value < CaughtChance)
        {
            Caught();
            owner.NotifyChanged();
            feedback = "Sói phát hiện bạn đang nhìn trộm. Bạn đã bị loại.";
            return true;
        }

        feedback = NightManager.Instance.BuildPeekReport();
        owner.NotifyChanged();
        return true;
    }

    public void Caught()
    {
        if (owner != null && owner.isAlive)
            DeathResolver.Instance?.TryKillPlayer(owner.playerID, DeathCause.Monster);
    }
}