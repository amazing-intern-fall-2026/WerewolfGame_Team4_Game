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
        if (owner != null && !owner.hasUsedHunterShot)
        {
            owner.hasHunterTrap = true;

            Debug.Log(
                owner.playerName +
                " đã chết và có thể bắn phát đạn trả thù."
            );
        }
    }

    public void SetTrap(int targetID)
    {
        TrySetTrap(targetID, out _);
    }

    public bool TrySetTrap(int targetID, out string feedback)
    {
        if (owner == null || owner.isAlive || !owner.hasHunterTrap || owner.hasUsedHunterShot)
        {
            feedback = "Thợ Săn chưa thể bắn hoặc đã sử dụng quyền bắn rồi.";
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
        owner.hasUsedHunterShot = true;
        feedback = "Đã bắn mục tiêu " + target.playerName + ".";
        Debug.Log(owner.playerName + " đã bắn phát đạn trả thù vào " + target.playerName);
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
