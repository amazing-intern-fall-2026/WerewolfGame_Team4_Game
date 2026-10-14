using UnityEngine;

public class WeaverOfFateRole : BaseRole
{
    public bool HasUsedAbility { get; private set; }

    public WeaverOfFateRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.WeaverOfFate;
        faction = FactionType.Villager;
    }

    public bool TryMakeLovers(int playerAID, int playerBID, out string feedback)
    {
        if (owner == null || !owner.isAlive)
        {
            feedback = "Bạn đã bị loại và không thể dùng kỹ năng.";
            return false;
        }

        if (GameRoleManager.Instance == null ||
            GameRoleManager.Instance.currentState != GameState.Night)
        {
            feedback = "Dệt Duyên chỉ có thể dùng vào ban đêm.";
            return false;
        }

        if (HasUsedAbility)
        {
            feedback = "Dệt Duyên chỉ dùng được một lần mỗi ván.";
            return false;
        }

        if (LoverManager.Instance == null)
        {
            feedback = "Chưa có LoverManager trong màn chơi.";
            return false;
        }

        if (!LoverManager.Instance.TryMakeLovers(
            playerAID,
            playerBID,
            out feedback))
            return false;

        HasUsedAbility = true;
        owner.hasUseNightAction = true;
        return true;
    }
}