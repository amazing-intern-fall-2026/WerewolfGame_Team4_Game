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

        if (owner.serpentNightCount < 2)
        {
            Debug.Log(owner.playerName + " chưa thể giết. Xà tinh đang trong 2 đêm đầu.");
            return;
        }

        NightManager.Instance.SetMonsterTarget(TargetID);
    }
}