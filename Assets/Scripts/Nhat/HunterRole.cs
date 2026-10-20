using UnityEngine;

public class HunterRole : BaseRole
{
    public HunterRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Hunter;
        faction = FactionType.Villager;
    }

    public override void OnDeath()
    {
        owner.hasHunterTrap = true;

        Debug.Log(
            owner.playerName +
            " đã chết và có thể đặt bẫy."
        );
    }

    public void SetTrap(int targetID)
    {
        TrySetTrap(targetID, out _);
    }

    public bool TrySetTrap(int targetID, out string feedback)
    {
        if (owner == null || owner.isAlive || !owner.hasHunterTrap)
        {
            feedback = "Thợ Săn chưa thể đặt bẫy.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive || target.playerID == owner.playerID)
        {
            feedback = "Mục tiêu không tồn tại hoặc đã bị loại.";
            return false;
        }

        owner.hunterTargetID = targetID;
        owner.hasHunterTrap = false;
        feedback = "Đã đặt bẫy lên " + target.playerName + ".";
        Debug.Log(owner.playerName + " đặt bẫy lên " + target.playerName);
        return true;
    }

    public bool ResolveTrap()
    {
        if (owner == null || owner.hunterTargetID < 0 || DeathResolver.Instance == null)
            return false;

        int targetID = owner.hunterTargetID;
        owner.hunterTargetID = -1;
        return DeathResolver.Instance.TryKillPlayer(targetID, DeathCause.Trap);
    }
}
