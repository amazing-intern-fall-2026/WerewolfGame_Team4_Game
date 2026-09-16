using UnityEngine;

public class CursedRole : BaseRole
{
    public CursedRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Cursed;
        faction = FactionType.Villager;
    }

    public override void UseNightAbility(int TargetID)
    {
        PlayerData target = PlayerManger.Instance.GetplayerByID(TargetID);

        if (target == null || !target.isAlive)
            return;

        target.isCursed = true;

        Debug.Log(
            owner.playerName +
            " đã nguyền " +
            target.playerName
        );
    }
}