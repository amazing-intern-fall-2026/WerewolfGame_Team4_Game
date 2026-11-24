using UnityEngine;

public class SerpentSpirit : BaseRole
{
    public SerpentSpirit(PlayerData owner) : base(owner)
    {
        roleType = RoleType.SerpentSpirit;
        faction = FactionType.Monster;
    }

    public override void UseNightAbility(int TargetID)
    {
        if (!owner.isAlive)
            return;

        int nightNum = NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0
            ? NightManager.Instance.CurrentNightNumber
            : owner.serpentNightCount;
        if (nightNum < 3)
        {
            Debug.Log(owner.playerName + " chưa thể giết. Xà tinh đang trong 2 đêm đầu.");
            return;
        }

        NightManager.Instance.SetMonsterTarget(TargetID, RoleType.SerpentSpirit, owner.playerID);
    }
}