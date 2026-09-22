using System.Collections.Generic;
using UnityEngine;

public class FoxSpiritRole : BaseRole
{
    private readonly HashSet<int> charmedPlayers = new HashSet<int>();

    public FoxSpiritRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.FoxSpirit;
        faction = FactionType.Neutral;
    }

    public override void UseNightAbility(int targetID)
    {
        if (owner == null || !owner.isAlive)
            return;

        PlayerData target = PlayerManger.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive)
            return;

        charmedPlayers.Add(targetID);
        Debug.Log(owner.playerName + " đã mê hoặc " + target.playerName + ".");
    }

    public bool IsCharmed(int targetID)
    {
        return charmedPlayers.Contains(targetID);
    }
}
