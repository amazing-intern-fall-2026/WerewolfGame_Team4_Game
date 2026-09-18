using UnityEngine;

public class SeerRole : BaseRole
{
    public SeerRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Seer;
        faction = FactionType.Villager;
    }
    public override string UseNightAbility(int TargetID)
    {
        PlayerData target = PlayerManger.Instance.GetplayerByID(TargetID);
        if (target == null || !target.isAlive) return string.Empty;
        Debug.Log("Seer Checked : "+target.playerName+target.roleType);
        return "Kết quả soi: " + target.playerName + " là " + RoleManger.GetRoleDisplayName(target.roleType) + ".";
    }
}
