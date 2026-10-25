using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly List<PeekAction> peekActions = new();
    private int nextActionSequence;
    private bool bratPeekPending;
    public int PendingActionCount => actions.Count;
    public bool HasUnresolvedNightResolution => actions.Count > 0 || peekActions.Count > 0 || bratPeekPending ||
        monsterTarget != -1 || protectedTarget != -1 || killerTarget != -1;
    private sealed class QueuedAction
    {
        public PlayerData actor, target;
        public ThuongRoleDefinition role;
        public int sequence;
    }
    private sealed class PeekAction
    {
        public int actorID, targetID;
        public string action;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void ResetForNewMatch() { evilNightCount = 0; ClearActions(); }
    public void ClearActions()
    {
        actions.Clear();
        peekActions.Clear();
        bratPeekPending = false;
        nextActionSequence = 0;
        monsterTarget = protectedTarget = killerTarget = -1;
        monsterSourceRole = RoleType.DogSpirit;
    }

    public void CancelDisconnectedPlayerActions(int playerId)
    {
        actions.RemoveAll(action => (action.actor != null && action.actor.playerID == playerId) ||
                                    (action.target != null && action.target.playerID == playerId));
        if (monsterTarget == playerId) monsterTarget = -1;
        if (protectedTarget == playerId) protectedTarget = -1;
        if (killerTarget == playerId) killerTarget = -1;
        Debug.Log($"NIGHT MANAGER: Đã hủy các action đêm liên quan đến Player {playerId}");
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
        peekActions.Clear();
        bratPeekPending = false;
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

    public void RecordSubmittedAction(PlayerData actor, int targetID, string action)
    {
        if (actor == null || GameRoleManager.Instance?.currentPhase != GamePhase.Night)
            return;

        peekActions.Add(new PeekAction
        {
            actorID = actor.playerID,
            targetID = targetID,
            action = action
        });
    }

    public void MarkBratPeekSubmitted() => bratPeekPending = true;

    public string BuildPeekReport()
    {
        var entries = new List<string>();
        foreach (QueuedAction action in actions.OrderBy(action => action.sequence))
        {
            string actor = action.actor != null ? PlayerLabel(action.actor.playerID) : "P?";
            string target = action.target != null ? PlayerLabel(action.target.playerID) : "P?";
            string ability = action.role != null ? ActionLabel(action.role.roleType.ToString()) : "Kỹ năng";
            entries.Add($"{actor} {ability}>{target}");
        }

        foreach (PeekAction action in peekActions)
            entries.Add($"{PlayerLabel(action.actorID)} {ActionLabel(action.action)}>{PlayerLabel(action.targetID)}");

        AddUnrecordedTarget(entries, monsterTarget, "Sói", monsterSourceRole);
        AddUnrecordedTarget(entries, protectedTarget, "Bảo vệ", RoleType.VillageGuardian);
        AddUnrecordedTarget(entries, killerTarget, "Sát nhân", RoleType.Killer);

        if (entries.Count == 0)
            return "Chưa có hành động nào được ghi nhận trong đêm này.";

        var report = new StringBuilder("HÀNH ĐỘNG ĐÊM (P = Player)\n");
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) report.Append('\n');
            report.Append(entries[i]);
        }
        return report.ToString();
    }

    private void AddUnrecordedTarget(List<string> entries, int targetID, string action, RoleType role)
    {
        if (targetID < 0 || peekActions.Any(item => item.targetID == targetID && item.action == role.ToString()))
            return;

        entries.Add($"{action} → {PlayerLabel(targetID)}");
    }

    private static string PlayerLabel(int playerID) => $"P{playerID + 1}";

    private static string ActionLabel(string role) => role switch
    {
        nameof(RoleType.DogSpirit) => "Sói",
        nameof(RoleType.SerpentSpirit) => "Xà",
        nameof(RoleType.Ogre) => "Quỷ",
        nameof(RoleType.Seer) => "Tiên tri",
        nameof(RoleType.VillageGuardian) => "Bảo vệ",
        nameof(RoleType.Shaman) => "Pháp sư",
        nameof(RoleType.WhiteHound) => "Bạch khuyển",
        nameof(RoleType.Cursed) => "Nguyền",
        nameof(RoleType.FoxSpirit) => "Hồ ly",
        nameof(RoleType.Killer) => "Sát nhân",
        nameof(RoleType.WeaverOfFate) => "Dệt duyên",
        nameof(RoleType.Hunter) => "Thợ săn",
        _ => role
    };

    public void ResolveNight()
    {
        if (ThuongLogicSceneScope.IsActive && GameRoleManager.Instance?.currentState == GameState.GameOver) { ClearActions(); return; }
        using var batch = DeathResolver.Instance?.BeginBatch();
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
        peekActions.Clear();
        bratPeekPending = false;
    }

    public void ResolveAbility(PlayerData actor, PlayerData target, ThuongRoleDefinition role) =>
        ResolveAction(new QueuedAction { actor = actor, target = target, role = role });

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
