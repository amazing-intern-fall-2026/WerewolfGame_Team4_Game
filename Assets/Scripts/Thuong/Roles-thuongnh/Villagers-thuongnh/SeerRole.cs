using UnityEngine;

public class SeerRole : BaseRole
{
    public SeerRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Seer;
        faction = FactionType.Villager;
    }
    public override void UseNightAbility(int TargetID)
    {
        TryUseNightAbility(TargetID, out _);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
        {
            feedback = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        bool isDogSpirit = target.roleType == RoleType.DogSpirit;
        feedback = $"Tiên tri soi Player {target.playerID + 1}: " +
                   (isDogSpirit ? "Dogspirit" : "Không phải Dogspirit");
        Debug.Log(feedback);
        return true;
    }
}

