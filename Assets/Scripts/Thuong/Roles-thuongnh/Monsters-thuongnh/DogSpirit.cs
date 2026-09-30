using UnityEngine;

public class DogSpirit:BaseRole
{
    public DogSpirit(PlayerData owner) : base(owner)
    {
        roleType = RoleType.DogSpirit;
        faction = FactionType.Monster;

    }
    public override void UseNightAbility(int TargetID)
    {
       NightManager.Instance?.SetMonsterTarget(TargetID);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (NightManager.Instance == null)
        {
            feedback = "Không tìm thấy hệ thống xử lý ban đêm.";
            return false;
        }

        UseNightAbility(targetID);
        feedback = $"Đã chọn Player {targetID + 1} để tấn công.";
        return true;
    }
}
