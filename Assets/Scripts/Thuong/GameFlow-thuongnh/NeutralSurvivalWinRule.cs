using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Thuong/Win Rules/Neutral Survival", fileName = "NeutralSurvival")]
public sealed class NeutralSurvivalWinRule : NeutralWinRule
{
    public string winnerLabel = "Neutral";
    [Min(0)] public int maximumOtherSurvivors;
    public bool requireNoWolves = true;
    public bool requireNoOpposingKillers = true;

    public override bool IsSatisfied(PlayerData candidate, IReadOnlyList<PlayerData> allPlayers)
    {
        if (candidate == null || !candidate.isAlive || candidate.faction != FactionType.Neutral || allPlayers == null) return false;
        int others = 0;
        foreach (var player in allPlayers)
        {
            if (player == null || !player.isAlive || player == candidate) continue;
            if (requireNoWolves && player.faction == FactionType.Monster) return false;
            if (requireNoOpposingKillers && (player.roleType == RoleType.Killer || player.isWhiteHoundAwakened)) return false;
            others++;
        }
        return others <= maximumOtherSurvivors;
    }

    public override string BuildWinMessage(PlayerData candidate) =>
        string.IsNullOrWhiteSpace(winnerLabel) ? candidate.DisplayName : winnerLabel;
}
