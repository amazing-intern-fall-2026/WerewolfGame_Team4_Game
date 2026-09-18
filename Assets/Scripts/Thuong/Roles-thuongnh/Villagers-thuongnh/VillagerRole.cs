using UnityEngine;
public class VillagerRole : BaseRole
{
    public VillagerRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Villager;
        faction = FactionType.Villager;
    }
}
    
