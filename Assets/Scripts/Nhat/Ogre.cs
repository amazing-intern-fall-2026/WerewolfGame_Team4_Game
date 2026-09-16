using UnityEngine;

public class Ogre : BaseRole
{
    public Ogre(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Ogre;
        faction = FactionType.Monster;
    }

    public override void UseNightAbility(int TargetID)
    {
        NightManager.Instance.SetMonsterTarget(TargetID);
    }
}