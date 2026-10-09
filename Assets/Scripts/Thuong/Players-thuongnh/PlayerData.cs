using UnityEngine;
[System.Serializable]
public class PlayerData
{
    [field: System.NonSerialized] public event System.Action<PlayerData> Changed;
    public int playerID;
    public string playerName;

    public RoleType roleType;

    public FactionType faction;

    public bool isAlive =true ;

    public int votePower = 1;

    public bool hasVoted;

    public bool hasUseNightAction;

    public int serpentNightCount;

    public bool hasHunterTrap;

    public int hunterTargetID = -1;

    public int loverID = -1;

    public bool isWhiteHoundAwakened;

    public bool isCursed;

    public PlayerStatus status = new PlayerStatus();

    public ThuongRoleDefinition roleDefinition;
    public bool hasDeathRecord;
    public DeathCause lastDeathCause;
    public System.Collections.Generic.List<StatusEffectInstance> effects = new();

    public string DisplayName => string.IsNullOrWhiteSpace(playerName) ? $"Player {playerID + 1}" : playerName;
    public bool CanVote => isAlive && !HasEffect(StatusEffectType.CannotVote);
    public int GetVoteWeight() => CanVote ? Mathf.Max(1, votePower) + GetEffectStrength(StatusEffectType.ExtraVote) : 0;
    public bool HasEffect(StatusEffectType type) => effects != null && effects.Exists(effect => effect.Type == type);
    public int GetEffectStrength(StatusEffectType type)
    {
        int total = 0;
        if (effects != null)
            foreach (var effect in effects)
                if (effect.Type == type) total += effect.Strength;
        return total;
    }

    public void NotifyChanged() => Changed?.Invoke(this);

    public void ResetForNewMatch()
    {
        isAlive = true;
        hasDeathRecord = false;
        lastDeathCause = default;
        hasVoted = hasUseNightAction = false;
        votePower = 1;
        serpentNightCount = 0;
        hasHunterTrap = isWhiteHoundAwakened = isCursed = false;
        hunterTargetID = loverID = -1;
        status = new PlayerStatus();
        effects ??= new();
        effects.Clear();
        NotifyChanged();
    }
}
