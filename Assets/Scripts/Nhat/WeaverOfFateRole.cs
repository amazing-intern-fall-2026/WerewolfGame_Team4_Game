using UnityEngine;

public class WeaverOfFateRole : BaseRole
{
    public WeaverOfFateRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.WeaverOffate;
        faction = FactionType.Villager;
    }

    public void MakeLovers(int playerAID, int playerBID)
    {
        if (LoverManager.Instance == null)
            return;

        LoverManager.Instance.MakeLovers(
            playerAID,
            playerBID
        );
    }
}