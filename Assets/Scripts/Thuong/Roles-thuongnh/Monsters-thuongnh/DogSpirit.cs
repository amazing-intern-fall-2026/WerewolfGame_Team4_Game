using UnityEngine;

public class DogSpirit:BaseRole
{
    public DogSpirit(PlayerData owner) : base(owner)
    {
        roleType = RoleType.DogSprit;
        faction = FactionType.Monster;

    }
    public override void UseNightAbility(int TargetID)
    {
       NightManager.Instance.SetMonsterTarget(TargetID);
    }
}
