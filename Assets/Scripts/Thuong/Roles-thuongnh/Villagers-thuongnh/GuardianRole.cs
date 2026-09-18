using UnityEngine;

public class GuardianRole:BaseRole
{
    public int lastTarget = -1;
    public GuardianRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.VillageGuardian;
        faction = FactionType.Villager;
    }
    public override void UseNightAbility(int TargetID)
    {
        if (TargetID == lastTarget)
        {
            return;
        }
        lastTarget= TargetID;
        NightManager.Instance.SetProtectedTarget(TargetID);
    }
}
