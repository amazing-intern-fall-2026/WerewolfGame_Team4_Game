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
        if (owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManager.Instance?.GetplayerByID(TargetID);

        if (target == null || !target.isAlive)
            return;

        target.status ??= new PlayerStatus();
        target.isCursed = true;
        target.status.isSilenced = true;

        Debug.Log(
            owner.playerName +
            " đã nguyền " +
            target.playerName
        );
    }
}
