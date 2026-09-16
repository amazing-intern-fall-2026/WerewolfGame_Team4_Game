using UnityEngine;

public class ShamanRole : BaseRole
{
    private bool hasGoodCharm = true;
    private bool hasBadCharm = true;

    public ShamanRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Shaman;
        faction = FactionType.Villager;
    }

    public void UseGoodCharm(int targetID)
    {
        if (!hasGoodCharm)
            return;

        PlayerData target = PlayerManger.Instance.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        hasGoodCharm = false;

        Debug.Log("Shaman dùng bùa lợi lên " + target.playerName);
    }

    public void UseBadCharm(int targetID)
    {
        if (!hasBadCharm)
            return;

        PlayerData target = PlayerManger.Instance.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        hasBadCharm = false;

        Debug.Log("Shaman dùng bùa hại lên " + target.playerName);
    }
}