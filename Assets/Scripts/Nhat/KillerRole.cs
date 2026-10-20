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
        if (night == null || night.CurrentNightNumber % 2 != 0)
            return;

        night.SetKillerTarget(targetID);
        Debug.Log(owner.playerName + " đã chọn giết " + targetID + ".");
    }
}
