// Adapts the existing Nhật role classes to the night schedule of Thương.
public static class RoleActionRules
{
    public static bool TryUseNightAbility(BaseRole role, int targetID, out string feedback)
    {
        NightManager night = NightManager.Instance;
        if (night == null)
        {
            feedback = "Chưa có hệ thống xử lý ban đêm.";
            return false;
        }

        if (role.roleType == RoleType.SerpentSpirit && night.CurrentNightNumber <= 2)
        {
            feedback = "Xà Tinh chỉ có thể tấn công từ đêm thứ ba.";
            return false;
        }

        if (role.roleType == RoleType.Killer)
        {
            if (night.CurrentNightNumber % 2 != 0)
            {
                feedback = "Sát Nhân chỉ có thể tấn công vào đêm chẵn.";
                return false;
            }

            // KillerRole's private use counter is independent of the match night.
            // Schedule from the authoritative night number so skipped turns work.
            night.SetKillerTarget(targetID);
            feedback = $"Đã chọn Player {targetID + 1} để tấn công đêm nay.";
            return true;
        }

        if (!role.TryUseNightAbility(targetID, out feedback))
            return false;

        switch (role.roleType)
        {
            case RoleType.SerpentSpirit:
            case RoleType.Ogre:
                feedback = $"Đã chọn Player {targetID + 1} để tấn công.";
                break;
            case RoleType.Cursed:
                feedback = $"Đã nguyền Player {targetID + 1}.";
                break;
            case RoleType.FoxSpirit:
                feedback = $"Đã mê hoặc Player {targetID + 1}.";
                break;
            case RoleType.WeaverOfFate:
                feedback = $"Đã ghép đôi với Player {targetID + 1}.";
                break;
        }
        return true;
    }
}
