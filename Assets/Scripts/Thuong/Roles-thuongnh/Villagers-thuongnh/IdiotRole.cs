using UnityEngine;

public class IdiotRole : BaseRole
{
    public IdiotRole(PlayerData owner) : base(owner)
    { 
        roleType = RoleType.Idiot;
        faction = FactionType.Villager;
    }
}
