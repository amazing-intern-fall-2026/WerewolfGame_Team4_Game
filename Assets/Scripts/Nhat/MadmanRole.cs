using UnityEngine;

public class MadmanRole : BaseRole
{
    public MadmanRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Madman;
        faction = FactionType.Neutral;
    }

    public override bool checkSpecialWin()
    {
        return owner != null && !owner.isAlive && GameRoleManager.Instance != null &&
               GameRoleManager.Instance.currentState == GameState.Day;
    }
}
