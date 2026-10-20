using UnityEngine;

public enum AbilityType { None, Protect, Kill, ApplyCannotVote, GrantExtraVote, Silence, Curse }
public enum TargetFactionRule { Any, SameFaction, OtherFaction, VillageOnly, WolvesOnly, NeutralOnly }

[CreateAssetMenu(menuName = "Thuong/Werewolf Role", fileName = "Role")]
public sealed class ThuongRoleDefinition : ScriptableObject
{
    public string roleID;
    public string displayName;
    [TextArea] public string description;
    public string abilityName;
    public Sprite portrait;
    public Sprite abilityIcon;
    public RoleType roleType = RoleType.Villager;
    public FactionType faction = FactionType.Villager;
    public AbilityType abilityType;
    [Tooltip("Keep the existing special-role implementation instead of the generic queued ability.")]
    public bool useLegacyAbility;
    [Tooltip("Preserve the gameplay prototype's single shared wolf target (last submission wins).")]
    public bool sharedNightKill;
    public bool forbidConsecutiveNightTarget;
    public GamePhase usablePhase = GamePhase.Night;
    public bool allowSelfTarget;
    public bool canTargetDead;
    public TargetFactionRule targetFactionRule;
    [Min(1)] public int maxUsesPerPhase = 1;
    [Min(1)] public int effectStrength = 1;
    [Min(1)] public int effectDurationTicks = 1;
    public GamePhase effectTicksOnPhase = GamePhase.Discussion;
    public NeutralWinRule neutralWinRule;
}
