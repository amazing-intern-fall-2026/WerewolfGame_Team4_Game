using System.Collections.Generic;
using UnityEngine;

// Phase 1 extension point only; concrete neutral rules belong to Phase 12.
public abstract class NeutralWinRule : ScriptableObject
{
    public abstract bool IsSatisfied(PlayerData candidate, IReadOnlyList<PlayerData> allPlayers);
    public abstract string BuildWinMessage(PlayerData candidate);
}
