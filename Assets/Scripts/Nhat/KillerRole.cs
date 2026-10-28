using UnityEngine;

public class KillerRole : BaseRole
{
    public int nightCounter = 0;

    public KillerRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Killer;
        faction = FactionType.Neutral;
    }

    public override void UseNightAbility(int targetID)
    {
        if (owner == null || !owner.isAlive)
            return;

        NightManager night = NightManager.Instance;
        if (night == null)
            return;

        int currentNight = night.CurrentNightNumber;
        if (currentNight <= 0 && NetworkPhaseSync.Instance != null)
            currentNight = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        if (currentNight <= 0 && GameRoleManager.Instance != null)
            currentNight = GameRoleManager.Instance.currentDay;

        if (currentNight <= 0 || currentNight % 2 != 0)
            return;

        night.SetKillerTarget(targetID);
        Debug.Log(owner.playerName + " đã chọn giết " + targetID + ".");
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (owner == null || !owner.isAlive)
        {
            feedback = "Người chơi đã bị loại.";
            return false;
        }

        NightManager night = NightManager.Instance;
        if (night == null)
        {
            feedback = "Chưa có hệ thống xử lý ban đêm.";
            return false;
        }

        int currentNight = night.CurrentNightNumber;
        if (currentNight <= 0 && NetworkPhaseSync.Instance != null)
            currentNight = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        if (currentNight <= 0 && GameRoleManager.Instance != null)
            currentNight = GameRoleManager.Instance.currentDay;

        if (currentNight <= 0 || currentNight % 2 != 0)
        {
            feedback = $"Sát Nhân chỉ có thể tấn công vào đêm chẵn (Đêm hiện tại: {currentNight}).";
            return false;
        }

        night.SetKillerTarget(targetID);
        feedback = $"Đã chọn Player {targetID + 1} để tấn công đêm nay.";
        Debug.Log(owner.playerName + " đã chọn giết " + targetID + ".");
        return true;
    }
}
