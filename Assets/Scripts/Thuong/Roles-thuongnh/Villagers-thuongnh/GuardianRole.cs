using UnityEngine;

public class GuardianRole:BaseRole
{
    public int lastTarget = -1;
    public GuardianRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.VillageGuardian;
        faction = FactionType.Villager;
    }
    public override string UseNightAbility(int TargetID)
    {
        if (TargetID == lastTarget)
        {
            return string.Empty;
        }
        lastTarget= TargetID;
        NightManager.Instance.SetProtectedTarget(TargetID);
        return "Đã chọn người chơi để bảo vệ.";
    }
}
