using System;

public enum LocalAbilityType
{
    None,
    Kill,
    InspectRole,
    Protect
}

[Serializable]
public sealed class LocalRoleAbilityDefinition
{
    public LocalAbilityType type;
    public string displayName;
    public string description;
    public bool requiresTarget;
}

public static class LocalRoleAbilityCatalog
{
    public static LocalRoleAbilityDefinition Get(RoleType roleType)
    {
        switch (roleType)
        {
            case RoleType.DogSprit:
                return new LocalRoleAbilityDefinition
                {
                    type = LocalAbilityType.Kill,
                    displayName = "Cắn",
                    description = "Chọn một người chơi còn sống để tấn công trong đêm.",
                    requiresTarget = true
                };
            case RoleType.Seer:
                return new LocalRoleAbilityDefinition
                {
                    type = LocalAbilityType.InspectRole,
                    displayName = "Soi Role",
                    description = "Chọn một người chơi để kiểm tra Role.",
                    requiresTarget = true
                };
            case RoleType.VillageGuardian:
                return new LocalRoleAbilityDefinition
                {
                    type = LocalAbilityType.Protect,
                    displayName = "Bảo vệ",
                    description = "Chọn một người chơi để bảo vệ trong đêm.",
                    requiresTarget = true
                };
            default:
                return new LocalRoleAbilityDefinition
                {
                    type = LocalAbilityType.None,
                    displayName = "Không có kỹ năng chủ động",
                    description = "Role này hiện chỉ có nội tại hoặc tham gia thảo luận/bỏ phiếu.",
                    requiresTarget = false
                };
        }
    }
}
