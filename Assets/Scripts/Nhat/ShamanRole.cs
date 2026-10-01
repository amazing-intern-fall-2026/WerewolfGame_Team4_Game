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
        if (!hasGoodCharm || owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        target.status ??= new PlayerStatus();
        target.status.isProtected = true;
        hasGoodCharm = false;

        Debug.Log("Shaman dùng bùa lợi lên " + target.playerName);
    }

    public void UseBadCharm(int targetID)
    {
        if (!hasBadCharm || owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);

        if (target == null || !target.isAlive)
            return;

        target.status ??= new PlayerStatus();
        target.status.isSilenced = true;
        hasBadCharm = false;

        Debug.Log("Shaman dùng bùa hại lên " + target.playerName);
    }
}
