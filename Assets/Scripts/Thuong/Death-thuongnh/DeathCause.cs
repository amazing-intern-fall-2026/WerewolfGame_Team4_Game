using UnityEngine;

public enum DeathCause
{
    Vote,
    Monster,
    Poison,
    Trap,
    Killer,
    Lover,
    DeathHerald,
    // Append values so existing scenes and network role code keep their enum IDs.
    Ability,
    Curse,
    Special
}
