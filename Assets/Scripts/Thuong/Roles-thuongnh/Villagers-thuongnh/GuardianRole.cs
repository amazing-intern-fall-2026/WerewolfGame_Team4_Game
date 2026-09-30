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
        TryUseNightAbility(TargetID, out _);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (targetID == lastTarget)
        {
            feedback = "Không thể bảo vệ cùng một người trong hai đêm liên tiếp.";
            return false;
        }
        if (NightManager.Instance == null)
        {
            feedback = "Không tìm thấy hệ thống xử lý ban đêm.";
            return false;
        }

        NightManager.Instance.SetProtectedTarget(targetID);
        lastTarget = targetID;
        feedback = $"Đã chọn bảo vệ Player {targetID + 1}.";
        return true;
    }
}
