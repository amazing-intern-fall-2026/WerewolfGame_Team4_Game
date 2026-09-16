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
        if (!owner.hasHunterTrap)
            return;

        PlayerData target =
            PlayerManger.Instance.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        owner.hunterTargetID = targetID;
        owner.hasHunterTrap = false;

        Debug.Log(
            owner.playerName +
            " đặt bẫy lên " +
            target.playerName
        );
    }
}