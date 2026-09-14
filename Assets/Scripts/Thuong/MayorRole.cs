using UnityEngine;

public class MayorRole:BaseRole
{
    public MayorRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Mayor;
        faction = FactionType.Villager;

        owner.votPower = 2;
    }
}
