using System.Collections.Generic;
using UnityEngine;

public class FoxSpiritRole : BaseRole
{
    private const int CharmedTargetsPerNight = 2;
    private readonly HashSet<int> charmedPlayers = new HashSet<int>();
    private int charmedThisNight;

    public FoxSpiritRole(PlayerData owner) : base(owner)
    {
        roleType = RoleType.FoxSpirit;
        faction = FactionType.Neutral;
    }

    public override void UseNightAbility(int targetID)
    {
        TryUseNightAbility(targetID, out _);
    }

    public override void OnNightStart()
    {
        charmedThisNight = 0;
    }

    public override bool TryUseNightAbility(int targetID, out string feedback)
    {
        if (owner == null || !owner.isAlive)
        {
            feedback = "Cáo tinh đã bị loại.";
            return false;
        }

        if (charmedThisNight >= CharmedTargetsPerNight)
        {
            feedback = "Bạn đã mê hoặc đủ hai người trong đêm nay.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null || !target.isAlive || charmedPlayers.Contains(targetID))
        {
            feedback = "Mục tiêu không hợp lệ hoặc đã bị mê hoặc.";
            return false;
        }

        target.status ??= new PlayerStatus();
        target.status.isCharmed = true;
        charmedPlayers.Add(targetID);
        charmedThisNight++;
        Debug.Log(owner.playerName + " đã mê hoặc " + target.playerName + ".");
        feedback = $"Đã mê hoặc Player {targetID + 1}.";
        return true;
    }

    public bool IsCharmed(int targetID)
    {
        return charmedPlayers.Contains(targetID);
    }

    public bool HasUsedAllNightActions => charmedThisNight >= CharmedTargetsPerNight;

    public bool HasWon
    {
        get
        {
            if (owner == null || !owner.isAlive || charmedPlayers.Count < CharmedTargetsPerNight ||
                PlayerManager.Instance == null)
                return false;

            foreach (int playerID in charmedPlayers)
            {
                PlayerData player = PlayerManager.Instance.GetplayerByID(playerID);
                if (player == null || !player.isAlive)
                    return false;
            }

            return true;
        }
    }
}

