using UnityEngine;

public class NightManager : MonoBehaviour
{
    public static NightManager Instance;

    private int monsterTarget = -1;
    private RoleType monsterSourceRole = RoleType.DogSpirit;
    private int protectedTarget = -1;
    private int killerTarget = -1;
    private int evilNightCount = 0;
    public int CurrentNightNumber => evilNightCount;
    private readonly System.Collections.Generic.List<QueuedAction> actions = new();
    private int nextActionSequence;
    public int PendingActionCount => actions.Count;
    private sealed class QueuedAction
    {
        public PlayerData actor, target;
        public ThuongRoleDefinition role;
        public int sequence;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void ResetForNewMatch() { evilNightCount = 0; ClearActions(); }
    public void ClearActions()
    {
        actions.Clear();
        nextActionSequence = 0;
        monsterTarget = protectedTarget = killerTarget = -1;
        monsterSourceRole = RoleType.DogSpirit;
    }
    public void QueueAbility(PlayerData actor, PlayerData target, ThuongRoleDefinition role)
    {
        if (role.sharedNightKill && role.abilityType == AbilityType.Kill)
        {
            // One pack attack, including legacy Serpent/Ogre submissions. Preserve last-target semantics.
            actions.RemoveAll(action => action.role.sharedNightKill && action.role.abilityType == AbilityType.Kill);
            monsterTarget = -1;
        }
        actions.Add(new QueuedAction { actor = actor, target = target, role = role, sequence = nextActionSequence++ });
    }

    private void Awake()
    {
        Instance = this;
    }

    public void StartNight()
    {
        actions.Clear();
        nextActionSequence = 0;
        monsterTarget = -1;
        monsterSourceRole = RoleType.DogSpirit;
        protectedTarget = -1;
        killerTarget = -1;
        evilNightCount++;

        RoleManager.Instance?.NotifyNightStart();

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null) continue;
            player.status ??= new PlayerStatus();
            player.status.isProtected = false;
            player.hasUseNightAction = false;
            if (player.roleType == RoleType.SerpentSpirit)
                player.serpentNightCount++;
        }
    }

    public void SetMonsterTarget(int id)
    {
        SetMonsterTarget(id, RoleType.DogSpirit);
    }

    public void SetMonsterTarget(int id, RoleType sourceRole)
    {
        actions.RemoveAll(action => action.role.sharedNightKill && action.role.abilityType == AbilityType.Kill);
        monsterTarget = id;
        monsterSourceRole = sourceRole;
    }

    public void SetProtectedTarget(int id)
    {
        protectedTarget = id;
    }

    public void SetKillerTarget(int id)
    {
        killerTarget = id;
    }

    public void ResolveNight()
    {
        PlayerData protectedPlayer = null;
        if (protectedTarget != -1)
        {
            protectedPlayer = PlayerManager.Instance.GetplayerByID(protectedTarget);
            if (protectedPlayer != null && protectedPlayer.isAlive)
                protectedPlayer.status.isProtected = true;
        }

        // Stable priority keeps protection ahead of attacks regardless of submission order.
        actions.Sort((left, right) =>
        {
            int priority = (left.role.abilityType == AbilityType.Kill ? 1 : 0)
                .CompareTo(right.role.abilityType == AbilityType.Kill ? 1 : 0);
            return priority != 0 ? priority : left.sequence.CompareTo(right.sequence);
        });
        var snapshot = actions.ToArray();
        actions.Clear();
        foreach (var action in snapshot) ResolveAction(action);

        if (monsterTarget != -1)
        {
            PlayerData target = PlayerManager.Instance.GetplayerByID(monsterTarget);
            if (target != null && target.isAlive)
            {
                DeathResolver.Instance.TryKillPlayer(
                    monsterTarget, DeathCause.Monster, monsterSourceRole);
            }
        }

        if (killerTarget != -1 && evilNightCount % 2 == 0)
        {
            PlayerData killerTargetPlayer = PlayerManager.Instance.GetplayerByID(killerTarget);
            if (killerTargetPlayer != null && killerTargetPlayer.isAlive)
            {
                DeathResolver.Instance.TryKillPlayer(killerTarget, DeathCause.Killer);
            }
        }

        foreach (var player in PlayerManager.Instance.players)
            if (player != null) player.status.isProtected = false;

        monsterTarget = -1;
        monsterSourceRole = RoleType.DogSpirit;
        protectedTarget = -1;
        killerTarget = -1;
    }

    private static void ResolveAction(QueuedAction action)
    {
        var actor = action.actor;
        var target = action.target;
        var role = action.role;
        var validation = TargetValidator.Validate(actor, target, role);
        if (!validation.IsValid || PlayerManager.Instance?.GetplayerByID(target.playerID) != target)
        {
            string message = validation.IsValid ? "Mục tiêu không thuộc trận hiện tại." : validation.Message;
            AbilitySystem.Instance?.Publish(new AbilityResolution(actor, target, false, message));
            return;
        }
        // Actor eligibility was checked at submission; a simultaneous death does not cancel a queued action.
        if (role.abilityType == AbilityType.Kill)
        {
            var cause = actor.faction == FactionType.Monster ? DeathCause.Monster : DeathCause.Ability;
            var result = DeathResolver.Instance.TryKill(new DeathRequest(target, cause, actor));
            AbilitySystem.Instance?.Publish(new AbilityResolution(actor, target, result.Outcome == DeathOutcome.Killed, result.Message));
            return;
        }
        StatusEffectType? type = role.abilityType switch
        {
            AbilityType.Protect => StatusEffectType.Protected,
            AbilityType.ApplyCannotVote => StatusEffectType.CannotVote,
            AbilityType.GrantExtraVote => StatusEffectType.ExtraVote,
            AbilityType.Silence => StatusEffectType.Silenced,
            AbilityType.Curse => StatusEffectType.Cursed,
            _ => null
        };
        if (type == null || StatusEffectSystem.Instance == null)
        {
            AbilitySystem.Instance?.Publish(new AbilityResolution(actor, target, false, "Không có hệ thống xử lý kỹ năng này."));
            return;
        }
        StatusEffectSystem.Instance.ApplyEffect(target, type.Value, role.effectStrength,
            role.effectDurationTicks, role.effectTicksOnPhase, actor);
        AbilitySystem.Instance?.Publish(new AbilityResolution(actor, target, true, $"{type} đã áp dụng lên {target.DisplayName}."));
    }
}
