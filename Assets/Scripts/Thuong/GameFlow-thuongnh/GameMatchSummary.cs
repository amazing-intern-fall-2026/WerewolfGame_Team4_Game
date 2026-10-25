using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GameMatchSummary
{
    public string Winner { get; }
    public int Day { get; }
    public IReadOnlyList<FinalPlayerSummary> Players { get; }

    public GameMatchSummary(string winner, int day, IEnumerable<PlayerData> players)
    {
        Winner = winner;
        Day = day;
        Players = Array.AsReadOnly((players ?? Enumerable.Empty<PlayerData>())
            .Where(player => player != null).OrderBy(player => player.playerID)
            .Select(player => new FinalPlayerSummary(player)).ToArray());
    }
}

public readonly struct FinalPlayerSummary
{
    public int PlayerID { get; }
    public string Name { get; }
    public string RoleName { get; }
    public RoleType Role { get; }
    public FactionType Faction { get; }
    public bool Alive { get; }
    public bool HasDeathRecord { get; }
    public DeathCause DeathCause { get; }
    public int DeathSourceID { get; }
    public FinalPlayerSummary(PlayerData player)
    {
        PlayerID = player.playerID; Name = player.DisplayName; Role = player.roleType;
        RoleName = string.IsNullOrWhiteSpace(player.roleDefinition?.displayName)
            ? player.roleType.ToString() : player.roleDefinition.displayName;
        Faction = player.faction; Alive = player.isAlive;
        HasDeathRecord = player.hasDeathRecord; DeathCause = player.lastDeathCause;
        DeathSourceID = player.lastDeathSourceID;
    }
}
