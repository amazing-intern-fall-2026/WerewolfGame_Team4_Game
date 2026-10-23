using System.Collections.Generic;
using UnityEngine;

// Shared asset contract for a neutral role's own victory condition.
public abstract class NeutralWinRule : ScriptableObject
{
    public abstract bool IsSatisfied(PlayerData candidate, IReadOnlyList<PlayerData> allPlayers);
    public abstract string BuildWinMessage(PlayerData candidate);
}
