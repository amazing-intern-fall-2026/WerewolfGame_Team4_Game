using UnityEngine;

public class SeerRole : BaseRole
{
    public SeerRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Seer;
        faction = FactionType.Villager;
    }
    public override void UseNightAbility(int TargetID)
    {
        PlayerData target = PlayerManger.Instance.GetplayerByID(TargetID);
        if (target == null || !target.isAlive) return;
        Debug.Log("Seer Checked : "+target.playerName+target.roleType);
    }
}
