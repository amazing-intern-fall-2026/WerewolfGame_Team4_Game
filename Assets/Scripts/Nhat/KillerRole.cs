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

        if (nightCounter % 2 == 0)
        {
            NightManager.Instance.SetKillerTarget(targetID);
            Debug.Log(owner.playerName + " đã chọn giết " + targetID + ".");
        }

        nightCounter++;
    }
}
