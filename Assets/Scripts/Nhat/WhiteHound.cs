using UnityEngine;

public class WhiteHound : BaseRole
{
    public WhiteHound(PlayerData owner) : base(owner)
    {
        roleType = RoleType.WhiteHound;

        // Ban đầu WhiteHound là dân làng
        faction = FactionType.Villager;
    }

    public override void OnDeath()
    {
        // Không dùng ở đây.
    }

    public override void UseNightAbility(int targetID)
    {
        if (owner == null || !owner.isAlive || !owner.isWhiteHoundAwakened)
            return;

        NightManager.Instance?.SetMonsterTarget(targetID, RoleType.WhiteHound);
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (owner == null || !owner.isAlive || !owner.isWhiteHoundAwakened)
        {
            feedback = "Bạch Khuyển chỉ tấn công sau khi thức tỉnh.";
            return false;
        }

        if (NightManager.Instance == null)
        {
            feedback = "Chưa có hệ thống xử lý ban đêm.";
            return false;
        }

        UseNightAbility(targetID);
        feedback = $"Đã chọn Player {targetID + 1} để tấn công.";
        return true;
    }

    public void BecomeMonster()
    {
        roleType = RoleType.WhiteHound;
        faction = FactionType.Monster;

        owner.isWhiteHoundAwakened = true;

        Debug.Log(owner.playerName + " đã trở thành WhiteHound!");
    }
}