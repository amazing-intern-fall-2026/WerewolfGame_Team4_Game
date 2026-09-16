using UnityEngine;

public class BratRole : BaseRole
{
    public BratRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.Brat;
        faction = FactionType.Villager;
    }

    public void Peek()
    {
        if (!owner.isAlive)
            return;

        Debug.Log(owner.playerName + " đang nhìn trộm ban đêm.");
    }

    public void Caught()
    {
        DeathResolver.Instance.TryKillPlayer(
            owner.playerID,
            DeathCause.Monster
        );
    }
}