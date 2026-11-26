using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class NightManager : MonoBehaviour
{
    public static NightManager Instance;

    public readonly struct NightAttackEvent
    {
        public readonly int EventId;
        public readonly int VictimId;
        public readonly DeathCause Cause;
        public readonly RoleType SourceRole;
        public readonly int AttackerId;

        public NightAttackEvent(int eventId, int victimId, DeathCause cause, RoleType sourceRole, int attackerId = -1)
        {
            EventId = eventId;
            VictimId = victimId;
            Cause = cause;
            SourceRole = sourceRole;
            AttackerId = attackerId;
        }
    }

    public static event System.Action<NightAttackEvent> NightAttackRegistered;
    public event System.Action<int, int, RoleType, bool> MonsterPackTargetChanged;
    public event System.Action MonsterVoteStateChanged;

    private int monsterTarget = -1;
    private int packTargetId = -1;
    private int monsterSelectorId = -1;
    private RoleType monsterSourceRole = RoleType.DogSpirit;
    private int monsterAttackEventId;
    private int monsterTargetVersion;
    private bool monsterAttackLocked;
    private int resolvedMonsterNight = -1;
    private readonly Dictionary<int, VoteChoice> monsterVoteChoices = new();
    private readonly Dictionary<int, int> monsterVoteTallies = new();
    private readonly Dictionary<int, int> monsterVoteSequence = new();
    private int nextMonsterVoteOrder;
    private bool monsterVoteTied;
    private int monsterVoteHighestCount;
    private bool monsterVoteClosed;
    private bool dogSpiritDecisionActive;
    private readonly List<int> dogSpiritTiedCandidateIds = new();
    private DeathResolver observedDeathResolver;
    private int protectedTarget = -1;
    private int killerTarget = -1;
    private int killerAttackEventId;
    private int evilNightCount = 0;
    public int CurrentNightNumber => evilNightCount;
    public int CurrentMonsterAttackEventId => monsterAttackEventId;
    public int ActiveMonsterTargetId => packTargetId;
    public int ActiveMonsterSelectorId => monsterSelectorId;
    public RoleType ActiveMonsterSourceRole => monsterSourceRole;
    public int MonsterTargetVersion => monsterTargetVersion;
    public bool IsMonsterAttackLocked => monsterAttackLocked;
    public bool IsMonsterVoteClosed => monsterVoteClosed || monsterAttackLocked;
    public bool IsDogSpiritDecisionActive => dogSpiritDecisionActive;
    public IReadOnlyList<int> DogSpiritTiedCandidateIds => dogSpiritTiedCandidateIds;
    public bool IsDogSpiritTiedCandidate(int targetId) =>
        dogSpiritDecisionActive && dogSpiritTiedCandidateIds.Contains(targetId);
    public bool IsMonsterVoteTied => monsterVoteTied;
    public int MonsterVoteHighestCount => monsterVoteHighestCount;
    public IReadOnlyDictionary<int, int> MonsterVoteTallies => monsterVoteTallies;
    public int GetMonsterVotedTarget(int voterId) =>
        monsterVoteChoices.TryGetValue(voterId, out var choice) && choice.Kind == VoteChoiceKind.Player
            ? choice.TargetPlayerId
            : -1;
    public int GetMonsterVoteCountForTarget(int targetId) =>
        monsterVoteTallies.TryGetValue(targetId, out int count) ? count : 0;
    private readonly System.Collections.Generic.List<QueuedAction> actions = new();
    private readonly List<PeekAction> peekActions = new();
    private readonly Dictionary<int, NightAttackEvent> activeNightAttacks = new();
    private readonly HashSet<int> rescuedAttackEventIds = new();
    private readonly Dictionary<int, WhiteHoundAttackEntry> whiteHoundAttacks = new();
    private int nextActionSequence;
    private int nextAttackEventId;
    private bool bratPeekPending;
    public int PendingActionCount => actions.Count;
    public bool HasPendingWhiteHoundAttack => whiteHoundAttacks.Count > 0;
    public bool HasUnresolvedNightResolution => actions.Count > 0 || peekActions.Count > 0 || bratPeekPending ||
        monsterTarget != -1 || whiteHoundAttacks.Count > 0 || protectedTarget != -1 || killerTarget != -1;
    private sealed class QueuedAction
    {
        public PlayerData actor, target;
        public ThuongRoleDefinition role;
        public int sequence;
        public int attackEventId;
    }
    private sealed class PeekAction
    {
        public int actorID, targetID;
        public string action;
    }
    private sealed class WhiteHoundAttackEntry
    {
        public int AttackerId;
        public int TargetId;
        public int AttackEventId;
    }

    private void OnDestroy()
    {
        if (observedDeathResolver != null)
        {
            observedDeathResolver.DeathResolved -= HandleDeathResolvedForMonsterVote;
            observedDeathResolver = null;
        }
        if (Instance == this) Instance = null;
    }

    private void HookDeathResolver()
    {
        if (DeathResolver.Instance == null || observedDeathResolver == DeathResolver.Instance) return;
        if (observedDeathResolver != null)
            observedDeathResolver.DeathResolved -= HandleDeathResolvedForMonsterVote;
        observedDeathResolver = DeathResolver.Instance;
        observedDeathResolver.DeathResolved += HandleDeathResolvedForMonsterVote;
    }

    private void HandleDeathResolvedForMonsterVote(DeathResult result)
    {
        if (result.Request.Target != null)
        {
            int deadId = result.Request.Target.playerID;
            var removedWhKeys = whiteHoundAttacks
                .Where(pair => pair.Key == deadId || pair.Value.TargetId == deadId)
                .Select(pair => pair.Key)
                .ToList();
            foreach (int whKey in removedWhKeys)
            {
                if (whiteHoundAttacks[whKey].AttackEventId > 0)
                    activeNightAttacks.Remove(whiteHoundAttacks[whKey].AttackEventId);
                whiteHoundAttacks.Remove(whKey);
            }
        }
        if (dogSpiritDecisionActive)
        {
            if (result.Request.Target != null && dogSpiritTiedCandidateIds.Remove(result.Request.Target.playerID))
            {
                if (dogSpiritTiedCandidateIds.Count == 0)
                {
                    ResolveDogSpiritDecisionFallback();
                    return;
                }
                if (dogSpiritTiedCandidateIds.Count == 1)
                {
                    LockMonsterAttackTarget(dogSpiritTiedCandidateIds[0], FindAnyAliveMonsterActor(), RoleType.DogSpirit);
                    return;
                }
                if (!HasConnectedNonSilencedDogSpirit())
                {
                    ResolveDogSpiritDecisionFallback();
                    return;
                }
                monsterTargetVersion++;
                MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
                MonsterVoteStateChanged?.Invoke();
            }
            else if (!HasConnectedNonSilencedDogSpirit())
            {
                ResolveDogSpiritDecisionFallback();
            }
            return;
        }
        if (monsterVoteChoices.Count == 0 || monsterVoteClosed || monsterAttackLocked) return;
        PruneInvalidMonsterVotes();
    }

    public void ResetForNewMatch() { evilNightCount = 0; resolvedMonsterNight = -1; ClearActions(); }
    public void ClearActions()
    {
        bool hadPackState = packTargetId != -1 || monsterSelectorId != -1 || monsterAttackLocked ||
                            monsterVoteClosed || dogSpiritDecisionActive || dogSpiritTiedCandidateIds.Count > 0 ||
                            monsterVoteChoices.Count > 0 || monsterVoteTallies.Count > 0;
        actions.Clear();
        peekActions.Clear();
        activeNightAttacks.Clear();
        rescuedAttackEventIds.Clear();
        whiteHoundAttacks.Clear();
        monsterVoteChoices.Clear();
        monsterVoteTallies.Clear();
        monsterVoteSequence.Clear();
        dogSpiritTiedCandidateIds.Clear();
        nextMonsterVoteOrder = 0;
        monsterVoteTied = false;
        monsterVoteHighestCount = 0;
        monsterVoteClosed = false;
        dogSpiritDecisionActive = false;
        bratPeekPending = false;
        nextActionSequence = 0;
        monsterTarget = packTargetId = monsterSelectorId = protectedTarget = killerTarget = -1;
        monsterAttackEventId = killerAttackEventId = 0;
        monsterSourceRole = RoleType.DogSpirit;
        monsterAttackLocked = false;
        monsterTargetVersion = 0;
        if (hadPackState)
        {
            MonsterPackTargetChanged?.Invoke(-1, -1, RoleType.DogSpirit, false);
            MonsterVoteStateChanged?.Invoke();
        }
    }

    public static bool IsMonsterPackMember(PlayerData player)
    {
        if (player == null) return false;
        // Bạch Khuyển là phe thứ ba tấn công độc lập, không thuộc Monster Vote
        return player.roleType == RoleType.DogSpirit ||
               player.roleType == RoleType.SerpentSpirit ||
               player.roleType == RoleType.Ogre;
    }

    public static bool IsMonsterPackRoleType(RoleType roleType, bool isWhiteHoundAwakened, FactionType faction)
    {
        // Bạch Khuyển hoàn toàn độc lập với Monster Vote cả trước và sau thức tỉnh
        return roleType == RoleType.DogSpirit ||
               roleType == RoleType.SerpentSpirit ||
               roleType == RoleType.Ogre;
    }

    public int CurrentEffectiveNightNumber()
    {
        if (evilNightCount > 0) return evilNightCount;
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentNightCycle.Value > 0)
            return NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentDay > 0)
            return GameRoleManager.Instance.currentDay;
        return 0;
    }

    public bool IsMonsterAttackLockedForCurrentNight()
    {
        if (monsterAttackLocked) return true;
        int currentNight = CurrentEffectiveNightNumber();
        return currentNight > 0 && resolvedMonsterNight == currentNight;
    }

    public static bool CanParticipateInMonsterPackAttack(PlayerData actor, int nightNumber, out string reason)
    {
        if (actor == null)
        {
            reason = "Không tìm thấy người chơi.";
            return false;
        }
        if (!actor.isAlive)
        {
            reason = "Người đã chết không thể tham gia tấn công.";
            return false;
        }
        if (actor.isDisconnected)
        {
            reason = "Người chơi đã mất kết nối.";
            return false;
        }
        if (actor.HasEffect(StatusEffectType.Silenced))
        {
            reason = "Người chơi đang bị khóa kỹ năng.";
            return false;
        }
        if (!IsMonsterPackMember(actor))
        {
            reason = "Người chơi không thuộc phe Quái vật.";
            return false;
        }
        if (actor.roleType == RoleType.SerpentSpirit && nightNumber <= 2)
        {
            reason = "Xà Tinh chỉ có thể tấn công từ đêm thứ ba!";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public static bool CanParticipateInWhiteHoundAttack(PlayerData actor, out string reason)
    {
        if (actor == null)
        {
            reason = "Không tìm thấy người chơi.";
            return false;
        }
        if (!actor.isAlive)
        {
            reason = "Người đã chết không thể tấn công.";
            return false;
        }
        if (actor.isDisconnected)
        {
            reason = "Người chơi đã mất kết nối.";
            return false;
        }
        if (actor.HasEffect(StatusEffectType.Silenced))
        {
            reason = "Người chơi đang bị khóa kỹ năng.";
            return false;
        }
        if (actor.roleType != RoleType.WhiteHound && actor.roleType != RoleType.WhiteWolf)
        {
            reason = "Chỉ Bạch Khuyển mới dùng được đòn tấn công độc lập này.";
            return false;
        }
        if (!actor.isWhiteHoundAwakened)
        {
            reason = "Bạch Khuyển chỉ tấn công sau khi thức tỉnh.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public static bool IsValidWhiteHoundTarget(PlayerData actor, PlayerData target, out string reason)
    {
        if (target == null)
        {
            reason = "Mục tiêu không tồn tại.";
            return false;
        }
        if (!target.isAlive)
        {
            reason = "Mục tiêu đã bị loại.";
            return false;
        }
        if (target.isDisconnected)
        {
            reason = "Mục tiêu đã mất kết nối.";
            return false;
        }
        if (actor != null && actor.playerID == target.playerID)
        {
            reason = "Bạch Khuyển không thể tự tấn công chính mình!";
            return false;
        }
        // Không giới hạn phe mục tiêu: Bạch Khuyển có thể tấn công cả Dân Làng lẫn Monster
        reason = string.Empty;
        return true;
    }

    public bool HasWhiteHoundSubmittedAttack(int attackerId) => whiteHoundAttacks.ContainsKey(attackerId);

    public int GetWhiteHoundTarget(int attackerId = -1)
    {
        if (attackerId >= 0)
            return whiteHoundAttacks.TryGetValue(attackerId, out var entry) ? entry.TargetId : -1;
        foreach (var entry in whiteHoundAttacks.Values)
            return entry.TargetId;
        return -1;
    }

    public int GetWhiteHoundAttackEventId(int attackerId = -1)
    {
        if (attackerId >= 0)
            return whiteHoundAttacks.TryGetValue(attackerId, out var entry) ? entry.AttackEventId : 0;
        foreach (var entry in whiteHoundAttacks.Values)
            return entry.AttackEventId;
        return 0;
    }

    public void SetWhiteHoundTarget(int targetId, int attackerId = -1)
    {
        if (whiteHoundAttacks.TryGetValue(attackerId, out var existing) && existing.AttackEventId > 0)
        {
            activeNightAttacks.Remove(existing.AttackEventId);
        }

        if (targetId < 0)
        {
            whiteHoundAttacks.Remove(attackerId);
            return;
        }

        int eventId = RegisterNightAttack(targetId, DeathCause.Monster, RoleType.WhiteHound, attackerId);
        whiteHoundAttacks[attackerId] = new WhiteHoundAttackEntry
        {
            AttackerId = attackerId,
            TargetId = targetId,
            AttackEventId = eventId
        };
    }

    public bool TrySubmitWhiteHoundAttack(PlayerData actor, int targetId, out string feedback)
    {
        HookDeathResolver();
        if (!CanParticipateInWhiteHoundAttack(actor, out feedback))
            return false;

        if (actor.hasUseNightAction || whiteHoundAttacks.ContainsKey(actor.playerID))
        {
            feedback = "Bạn đã chọn mục tiêu tấn công trong đêm này.";
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetId);
        if (!IsValidWhiteHoundTarget(actor, target, out feedback))
            return false;

        SetWhiteHoundTarget(targetId, actor.playerID);
        actor.hasUseNightAction = true;
        RecordSubmittedAction(actor, targetId, nameof(RoleType.WhiteHound));
        feedback = $"Đã chọn {target.DisplayName} để tấn công.";
        return true;
    }

    public static bool IsValidMonsterPackTarget(PlayerData actor, PlayerData target, out string reason)
    {
        if (target == null)
        {
            reason = "Mục tiêu không tồn tại.";
            return false;
        }
        if (!target.isAlive)
        {
            reason = "Mục tiêu đã bị loại.";
            return false;
        }
        if (target.isDisconnected)
        {
            reason = "Mục tiêu đã mất kết nối.";
            return false;
        }
        if (actor != null && actor.playerID == target.playerID)
        {
            reason = "Quái vật không thể tự tấn công chính mình!";
            return false;
        }
        if (IsMonsterPackMember(target))
        {
            reason = "Không thể tấn công đồng đội phe Quái vật.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool IsMonsterPackQueuedAction(QueuedAction action)
    {
        return action?.role != null && action.role.abilityType == AbilityType.Kill &&
               (action.role.sharedNightKill || IsMonsterPackMember(action.actor));
    }

    private static bool IsMonsterPeekAction(string action)
    {
        return action == nameof(RoleType.DogSpirit) ||
               action == nameof(RoleType.SerpentSpirit) ||
               action == nameof(RoleType.Ogre);
    }

    public void CancelDisconnectedPlayerActions(int playerId)
    {
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            var action = actions[i];
            if ((action.actor != null && action.actor.playerID == playerId) ||
                (action.target != null && action.target.playerID == playerId))
            {
                if (action.attackEventId > 0) activeNightAttacks.Remove(action.attackEventId);
                actions.RemoveAt(i);
            }
        }
        var removedWhKeys = whiteHoundAttacks
            .Where(pair => pair.Key == playerId || pair.Value.TargetId == playerId)
            .Select(pair => pair.Key)
            .ToList();
        foreach (int whKey in removedWhKeys)
        {
            if (whiteHoundAttacks[whKey].AttackEventId > 0)
                activeNightAttacks.Remove(whiteHoundAttacks[whKey].AttackEventId);
            whiteHoundAttacks.Remove(whKey);
        }
        if (dogSpiritDecisionActive)
        {
            bool tiedCandidateChanged = dogSpiritTiedCandidateIds.Remove(playerId);
            if (tiedCandidateChanged)
            {
                if (dogSpiritTiedCandidateIds.Count == 0)
                {
                    ExpireDogSpiritDecision();
                }
                else if (dogSpiritTiedCandidateIds.Count == 1)
                {
                    LockMonsterAttackTarget(dogSpiritTiedCandidateIds[0], FindAnyAliveMonsterActor(), RoleType.DogSpirit);
                }
                else if (!HasConnectedNonSilencedDogSpirit())
                {
                    ResolveDogSpiritDecisionFallback();
                }
                else
                {
                    monsterTargetVersion++;
                    MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
                    MonsterVoteStateChanged?.Invoke();
                }
            }
            else if (!HasConnectedNonSilencedDogSpirit())
            {
                // dogspirit mất kết nối thì mặc định phiếu cao nhất
                ResolveDogSpiritDecisionFallback();
            }
            if (protectedTarget == playerId) protectedTarget = -1;
            if (killerTarget == playerId)
            {
                killerTarget = -1;
                if (killerAttackEventId > 0) activeNightAttacks.Remove(killerAttackEventId);
                killerAttackEventId = 0;
            }
            Debug.Log($"NIGHT MANAGER: Đã cập nhật trạng thái ngắt kết nối trong lúc DogSpirit chốt mục tiêu cho Player {playerId}");
            return;
        }

        bool monsterStateAffected = false;
        if (!monsterVoteClosed)
        {
            if (monsterVoteChoices.Remove(playerId))
            {
                monsterVoteSequence.Remove(playerId);
                monsterStateAffected = true;
            }
            var invalidBallotVoters = monsterVoteChoices
                .Where(pair => pair.Value.Kind == VoteChoiceKind.Player && pair.Value.TargetPlayerId == playerId)
                .Select(pair => pair.Key)
                .ToList();
            foreach (int vId in invalidBallotVoters)
            {
                monsterVoteChoices.Remove(vId);
                monsterVoteSequence.Remove(vId);
                monsterStateAffected = true;
            }
        }

        if (monsterTarget == playerId || packTargetId == playerId)
        {
            monsterTarget = -1;
            packTargetId = -1;
            monsterSelectorId = -1;
            if (monsterAttackEventId > 0) activeNightAttacks.Remove(monsterAttackEventId);
            monsterAttackEventId = 0;
            monsterTargetVersion++;
            monsterStateAffected = true;
        }
        if (monsterStateAffected)
        {
            if (monsterVoteChoices.Count > 0)
            {
                RecalculateMonsterVoteOutcome(CurrentEffectiveNightNumber(), useQueuedAction: actions.Any(IsMonsterPackQueuedAction), queuedDef: null);
            }
            else
            {
                monsterVoteTallies.Clear();
                monsterVoteTied = false;
                monsterVoteHighestCount = 0;
                MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
                MonsterVoteStateChanged?.Invoke();
            }
        }
        if (protectedTarget == playerId) protectedTarget = -1;
        if (killerTarget == playerId)
        {
            killerTarget = -1;
            if (killerAttackEventId > 0) activeNightAttacks.Remove(killerAttackEventId);
            killerAttackEventId = 0;
        }
        Debug.Log($"NIGHT MANAGER: Đã hủy các action đêm liên quan đến Player {playerId}");
    }

    private int RegisterNightAttack(int victimId, DeathCause cause, RoleType sourceRole, int attackerId = -1)
    {
        int eventId = ++nextAttackEventId;
        var evt = new NightAttackEvent(eventId, victimId, cause, sourceRole, attackerId);
        activeNightAttacks[eventId] = evt;
        NightAttackRegistered?.Invoke(evt);
        return eventId;
    }

    public bool TryValidateAttackEventForSave(int attackEventId, int victimId, out string error)
    {
        if (attackEventId <= 0 || !activeNightAttacks.TryGetValue(attackEventId, out var evt))
        {
            error = "Sự kiện tấn công không tồn tại hoặc đã hết hạn.";
            return false;
        }

        if (evt.VictimId != victimId)
        {
            error = "Mục tiêu cứu không khớp với sự kiện tấn công.";
            return false;
        }

        if (rescuedAttackEventIds.Contains(attackEventId))
        {
            error = "Sự kiện tấn công này đã được cứu trước đó.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool MarkAttackEventRescued(int attackEventId)
    {
        if (attackEventId <= 0) return false;
        return rescuedAttackEventIds.Add(attackEventId);
    }

    public bool IsAttackEventRescued(int attackEventId)
    {
        return attackEventId > 0 && rescuedAttackEventIds.Contains(attackEventId);
    }

    public void QueueAbility(PlayerData actor, PlayerData target, ThuongRoleDefinition role)
    {
        bool isMonsterPackKill = role != null && role.abilityType == AbilityType.Kill &&
                                 (role.sharedNightKill || IsMonsterPackMember(actor));
        if (isMonsterPackKill)
        {
            if (IsMonsterAttackLockedForCurrentNight()) return;
            if (dogSpiritDecisionActive)
            {
                if (actor != null && target != null)
                {
                    TrySubmitDogSpiritDecision(actor, target.playerID, CurrentEffectiveNightNumber(), out _);
                }
                return;
            }
            if (monsterVoteClosed) return;
            if (actor != null && target != null)
            {
                HookDeathResolver();
                monsterVoteChoices[actor.playerID] = VoteChoice.ForPlayer(target.playerID);
                monsterVoteSequence[actor.playerID] = ++nextMonsterVoteOrder;
                RecalculateMonsterVoteOutcome(CurrentEffectiveNightNumber(), useQueuedAction: true, queuedDef: role);
                return;
            }
            // Fallback khi thiếu actor/target cụ thể
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (IsMonsterPackQueuedAction(actions[i]))
                {
                    if (actions[i].attackEventId > 0) activeNightAttacks.Remove(actions[i].attackEventId);
                    actions.RemoveAt(i);
                }
            }
            for (int i = peekActions.Count - 1; i >= 0; i--)
            {
                if (IsMonsterPeekAction(peekActions[i].action))
                    peekActions.RemoveAt(i);
            }
            monsterTarget = -1;
            if (monsterAttackEventId > 0) activeNightAttacks.Remove(monsterAttackEventId);
            monsterAttackEventId = 0;
        }

        int eventId = 0;
        if (role != null && role.abilityType == AbilityType.Kill && target != null)
        {
            var cause = isMonsterPackKill || (actor != null && actor.faction == FactionType.Monster)
                ? DeathCause.Monster : DeathCause.Ability;
            eventId = RegisterNightAttack(target.playerID, cause, role.roleType, actor != null ? actor.playerID : -1);
            if (isMonsterPackKill)
            {
                monsterAttackEventId = eventId;
                packTargetId = target.playerID;
                monsterSelectorId = actor != null ? actor.playerID : -1;
                monsterSourceRole = role.roleType;
                monsterTargetVersion++;
            }
        }

        actions.Add(new QueuedAction { actor = actor, target = target, role = role, sequence = nextActionSequence++, attackEventId = eventId });
        if (isMonsterPackKill)
        {
            MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
            MonsterVoteStateChanged?.Invoke();
        }
    }

    private void Awake()
    {
        Instance = this;
        HookDeathResolver();
    }

    public void StartNight()
    {
        HookDeathResolver();
        actions.Clear();
        peekActions.Clear();
        activeNightAttacks.Clear();
        rescuedAttackEventIds.Clear();
        whiteHoundAttacks.Clear();
        monsterVoteChoices.Clear();
        monsterVoteTallies.Clear();
        monsterVoteSequence.Clear();
        dogSpiritTiedCandidateIds.Clear();
        nextMonsterVoteOrder = 0;
        monsterVoteTied = false;
        monsterVoteHighestCount = 0;
        monsterVoteClosed = false;
        dogSpiritDecisionActive = false;
        bratPeekPending = false;
        nextActionSequence = 0;
        monsterTarget = -1;
        packTargetId = -1;
        monsterSelectorId = -1;
        monsterAttackEventId = 0;
        monsterSourceRole = RoleType.DogSpirit;
        monsterAttackLocked = false;
        resolvedMonsterNight = -1;
        monsterTargetVersion = 0;
        protectedTarget = -1;
        killerTarget = -1;
        killerAttackEventId = 0;
        evilNightCount++;
        MonsterPackTargetChanged?.Invoke(-1, -1, RoleType.DogSpirit, false);
        MonsterVoteStateChanged?.Invoke();

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
        SetMonsterTarget(id, RoleType.DogSpirit, -1);
    }

    public void SetMonsterTarget(int id, RoleType sourceRole)
    {
        SetMonsterTarget(id, sourceRole, -1);
    }

    public void SetMonsterTarget(int id, RoleType sourceRole, int selectorId)
    {
        if (IsMonsterAttackLockedForCurrentNight() || monsterVoteClosed) return;
        if (id >= 0 && packTargetId == id && monsterTarget == id && monsterSourceRole == sourceRole)
        {
            return;
        }

        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPackQueuedAction(actions[i]))
            {
                if (actions[i].attackEventId > 0) activeNightAttacks.Remove(actions[i].attackEventId);
                actions.RemoveAt(i);
            }
        }
        for (int i = peekActions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPeekAction(peekActions[i].action))
                peekActions.RemoveAt(i);
        }
        if (monsterAttackEventId > 0) activeNightAttacks.Remove(monsterAttackEventId);
        monsterTarget = id;
        packTargetId = id;
        monsterSelectorId = selectorId;
        monsterSourceRole = sourceRole;
        monsterTargetVersion++;
        monsterAttackEventId = id >= 0 ? RegisterNightAttack(id, DeathCause.Monster, sourceRole, selectorId) : 0;
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
    }

    public bool TrySubmitMonsterPackTarget(PlayerData actor, int targetId, RoleType sourceRole, int nightNumber, out string feedback)
    {
        if (dogSpiritDecisionActive)
            return TrySubmitDogSpiritDecision(actor, targetId, nightNumber, out feedback);
        return TryCastMonsterVote(actor, targetId, nightNumber, out feedback);
    }

    public static bool CanParticipateInDogSpiritDecision(PlayerData actor, out string reason)
    {
        if (actor == null)
        {
            reason = "Không tìm thấy người chơi.";
            return false;
        }
        if (actor.roleType != RoleType.DogSpirit)
        {
            reason = "Chỉ Chó Tinh (DogSpirit) mới được chọn mục tiêu hòa phiếu cuối cùng.";
            return false;
        }
        if (!actor.isAlive)
        {
            reason = "Chó Tinh đã chết, không thể quyết định mục tiêu.";
            return false;
        }
        if (actor.isDisconnected)
        {
            reason = "Chó Tinh đã mất kết nối.";
            return false;
        }
        if (actor.HasEffect(StatusEffectType.Silenced))
        {
            reason = "Chó Tinh đang bị khóa kỹ năng.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public bool HasAliveNonSilencedDogSpirit()
    {
        var roster = PlayerManager.Instance?.players;
        if (roster == null) return false;
        for (int i = 0; i < roster.Count; i++)
        {
            var player = roster[i];
            if (player != null &&
                player.roleType == RoleType.DogSpirit &&
                player.isAlive &&
                !player.HasEffect(StatusEffectType.Silenced))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasConnectedNonSilencedDogSpirit()
    {
        var roster = PlayerManager.Instance?.players;
        if (roster == null) return false;
        for (int i = 0; i < roster.Count; i++)
        {
            var player = roster[i];
            if (CanParticipateInDogSpiritDecision(player, out _))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsValidDogSpiritDecisionTarget(int targetId, out string reason)
    {
        if (!dogSpiritDecisionActive)
        {
            reason = "Hiện không ở bước Chó Tinh chốt mục tiêu.";
            return false;
        }
        if (!dogSpiritTiedCandidateIds.Contains(targetId))
        {
            reason = "Chỉ được chọn một trong những người chơi có số phiếu cao nhất.";
            return false;
        }
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetId);
        return IsValidMonsterPackTarget(null, target, out reason);
    }

    /// <summary>
    /// Evaluates Monster Vote when night vote ends:
    /// - Unique highest vote target -> lock and attack directly (returns false).
    /// - Tie at highest votes:
    ///   - If eligible DogSpirit(s) already voted unanimously for one tied candidate -> use that DogSpirit vote directly (returns false).
    ///   - If DogSpirit voted outside tied candidates, abstained, or multiple DogSpirits split inside tied candidates:
    ///     - If at least one DogSpirit is alive, connected, and non-silenced -> opens DogSpirit tie-breaker step (returns true).
    ///     - If DogSpirit is absent, dead, silenced, or disconnected -> defaults deterministically to highest-voted candidate (returns false).
    /// </summary>
    public bool BeginDogSpiritTieDecisionIfNeeded()
    {
        if (dogSpiritDecisionActive) return true;
        if (IsMonsterAttackLockedForCurrentNight()) return false;

        HookDeathResolver();
        int effectiveNight = CurrentEffectiveNightNumber();
        if (monsterVoteChoices.Count > 0)
        {
            RecalculateMonsterVoteOutcome(effectiveNight, useQueuedAction: actions.Any(IsMonsterPackQueuedAction), queuedDef: null);
        }

        monsterVoteClosed = true;
        dogSpiritTiedCandidateIds.Clear();

        // Trường hợp A: Không hòa phiếu -> người nhiều phiếu nhất bị tấn công luôn, DogSpirit không cần chọn thêm
        if (!monsterVoteTied && packTargetId >= 0 && monsterVoteHighestCount > 0)
        {
            dogSpiritDecisionActive = false;
            monsterAttackLocked = true;
            monsterTargetVersion++;
            MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
            MonsterVoteStateChanged?.Invoke();
            return false;
        }

        // Trường hợp B: Hòa phiếu cao nhất (> 0 phiếu)
        if (monsterVoteTied && monsterVoteHighestCount > 0)
        {
            foreach (var pair in monsterVoteTallies.OrderBy(item => item.Key))
            {
                if (pair.Value != monsterVoteHighestCount) continue;
                var candidate = PlayerManager.Instance?.GetplayerByID(pair.Key);
                if (IsValidMonsterPackTarget(null, candidate, out _))
                {
                    dogSpiritTiedCandidateIds.Add(pair.Key);
                }
            }

            if (dogSpiritTiedCandidateIds.Count == 1)
            {
                LockMonsterAttackTarget(dogSpiritTiedCandidateIds[0], FindAnyAliveMonsterActor(), RoleType.DogSpirit);
                return false;
            }

            if (dogSpiritTiedCandidateIds.Count >= 2)
            {
                // Nếu DogSpirit đã bỏ phiếu cho 1 người nằm trong nhóm hòa -> dùng luôn lựa chọn đó (chốt trực tiếp)
                if (TryGetUnanimousDogSpiritTiedVote(dogSpiritTiedCandidateIds, out int decidedTargetId, out PlayerData decidingDogSpirit))
                {
                    LockMonsterAttackTarget(decidedTargetId, decidingDogSpirit, RoleType.DogSpirit);
                    return false;
                }

                // Lựa chọn của DogSpirit không nằm trong nhóm hòa, chưa vote, hoặc nhiều DogSpirit vote lệch nhau trong nhóm hòa:
                // - Nếu còn DogSpirit kết nối -> yêu cầu DogSpirit chọn lại trong nhóm hòa
                if (HasConnectedNonSilencedDogSpirit())
                {
                    ClearMonsterAttackActionsAndState();
                    packTargetId = -1;
                    monsterSelectorId = -1;
                    monsterSourceRole = RoleType.DogSpirit;
                    dogSpiritDecisionActive = true;
                    monsterAttackLocked = false;
                    monsterTargetVersion++;
                    MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
                    MonsterVoteStateChanged?.Invoke();
                    return true;
                }

                // DogSpirit không tồn tại, đã chết, bị khóa hoặc mất kết nối -> mặc định chọn mục tiêu cao phiếu nhất tất định
                int fallbackTargetId = SelectDeterministicHighestTieCandidate(dogSpiritTiedCandidateIds);
                if (fallbackTargetId >= 0)
                {
                    LockMonsterAttackTarget(fallbackTargetId, FindAnyAliveMonsterActor(), RoleType.DogSpirit);
                    return false;
                }
            }

            dogSpiritTiedCandidateIds.Clear();
        }

        ClearMonsterAttackActionsAndState();
        packTargetId = -1;
        monsterSelectorId = -1;
        monsterSourceRole = RoleType.DogSpirit;
        dogSpiritDecisionActive = false;
        monsterAttackLocked = true;
        monsterTargetVersion++;
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
        MonsterVoteStateChanged?.Invoke();
        return false;
    }

    private bool TryGetUnanimousDogSpiritTiedVote(List<int> tiedIds, out int targetId, out PlayerData dogSpiritActor)
    {
        targetId = -1;
        dogSpiritActor = null;
        int distinctCandidateCount = 0;

        foreach (var ballot in monsterVoteChoices)
        {
            if (ballot.Value.Kind != VoteChoiceKind.Player) continue;
            PlayerData voter = PlayerManager.Instance?.GetplayerByID(ballot.Key);
            if (!CanParticipateInDogSpiritDecision(voter, out _)) continue;
            int votedTarget = ballot.Value.TargetPlayerId;
            if (!tiedIds.Contains(votedTarget)) continue;

            if (targetId == -1)
            {
                targetId = votedTarget;
                dogSpiritActor = voter;
                distinctCandidateCount = 1;
            }
            else if (votedTarget != targetId)
            {
                distinctCandidateCount++;
                break;
            }
        }

        if (distinctCandidateCount == 1 && targetId >= 0)
        {
            return true;
        }

        targetId = -1;
        dogSpiritActor = null;
        return false;
    }

    private int SelectDeterministicHighestTieCandidate(IReadOnlyList<int> tiedIds)
    {
        if (tiedIds == null || tiedIds.Count == 0) return -1;

        int bestTargetId = -1;
        int bestLastSeq = int.MaxValue;

        for (int i = 0; i < tiedIds.Count; i++)
        {
            int candidateId = tiedIds[i];
            PlayerData candidate = PlayerManager.Instance?.GetplayerByID(candidateId);
            if (!IsValidMonsterPackTarget(null, candidate, out _)) continue;

            int candidateLastSeq = -1;
            foreach (var ballot in monsterVoteChoices)
            {
                if (ballot.Value.Kind != VoteChoiceKind.Player || ballot.Value.TargetPlayerId != candidateId) continue;
                int seq = monsterVoteSequence.TryGetValue(ballot.Key, out int order) ? order : 0;
                if (seq > candidateLastSeq) candidateLastSeq = seq;
            }

            int scoreSeq = candidateLastSeq >= 0 ? candidateLastSeq : int.MaxValue - 1;
            if (bestTargetId < 0 || scoreSeq < bestLastSeq || (scoreSeq == bestLastSeq && candidateId < bestTargetId))
            {
                bestTargetId = candidateId;
                bestLastSeq = scoreSeq;
            }
        }

        return bestTargetId;
    }

    private PlayerData FindAnyAliveMonsterActor()
    {
        PlayerData fallback = null;
        var roster = PlayerManager.Instance?.players;
        if (roster == null) return null;
        for (int i = 0; i < roster.Count; i++)
        {
            var p = roster[i];
            if (p == null || !p.isAlive || !IsMonsterPackMember(p)) continue;
            if (p.roleType == RoleType.DogSpirit && !p.isDisconnected) return p;
            if (fallback == null) fallback = p;
        }
        return fallback;
    }

    private void LockMonsterAttackTarget(int targetId, PlayerData actor, RoleType sourceRole)
    {
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetId);
        if (target == null) return;

        ClearMonsterAttackActionsAndState();
        dogSpiritTiedCandidateIds.Clear();
        dogSpiritDecisionActive = false;
        monsterVoteClosed = true;
        monsterVoteTied = false;
        monsterAttackLocked = true;
        packTargetId = targetId;
        monsterSelectorId = actor != null ? actor.playerID : -1;
        monsterSourceRole = actor != null
            ? (actor.roleType == RoleType.WhiteHound || actor.roleType == RoleType.WhiteWolf ? RoleType.WhiteHound : actor.roleType)
            : sourceRole;
        monsterTargetVersion++;

        bool useQueued = actor != null && RoleManager.UsesConfiguredAbility(actor) && actor.roleDefinition != null;
        int eventId = RegisterNightAttack(targetId, DeathCause.Monster, monsterSourceRole, monsterSelectorId);
        monsterAttackEventId = eventId;
        if (useQueued)
        {
            monsterTarget = -1;
            actions.Add(new QueuedAction
            {
                actor = actor,
                target = target,
                role = actor.roleDefinition,
                sequence = nextActionSequence++,
                attackEventId = eventId
            });
        }
        else
        {
            monsterTarget = targetId;
        }

        if (actor != null)
        {
            actor.hasUseNightAction = true;
            RecordSubmittedAction(actor, targetId, monsterSourceRole.ToString());
        }
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
        MonsterVoteStateChanged?.Invoke();
    }

    public bool TrySubmitDogSpiritDecision(PlayerData actor, int targetId, int nightNumber, out string feedback)
    {
        HookDeathResolver();
        int effectiveNight = nightNumber > 0 ? nightNumber : CurrentEffectiveNightNumber();
        if (effectiveNight > 0 && resolvedMonsterNight == effectiveNight)
        {
            feedback = "Phe Quái vật đã xử lý đòn tấn công trong đêm này.";
            return false;
        }
        if (IsMonsterAttackLockedForCurrentNight())
        {
            feedback = "Mục tiêu tấn công của phe Quái vật đã được chốt trước đó.";
            return false;
        }
        if (!dogSpiritDecisionActive)
        {
            feedback = "Hiện chưa đến bước Chó Tinh chọn mục tiêu hòa phiếu.";
            return false;
        }
        if (!CanParticipateInDogSpiritDecision(actor, out feedback))
        {
            return false;
        }
        if (!IsValidDogSpiritDecisionTarget(targetId, out feedback))
        {
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetId);
        if (target == null)
        {
            feedback = "Mục tiêu không tồn tại.";
            return false;
        }

        LockMonsterAttackTarget(targetId, actor, RoleType.DogSpirit);
        feedback = $"Chó Tinh đã chốt mục tiêu tấn công cuối cùng: {target.DisplayName}!";
        return true;
    }

    public void ResolveDogSpiritDecisionFallback()
    {
        if (!dogSpiritDecisionActive) return;
        int fallbackTargetId = SelectDeterministicHighestTieCandidate(dogSpiritTiedCandidateIds);
        if (fallbackTargetId >= 0)
        {
            LockMonsterAttackTarget(fallbackTargetId, FindAnyAliveMonsterActor(), RoleType.DogSpirit);
            return;
        }
        ExpireDogSpiritDecision();
    }

    public void ExpireDogSpiritDecision()
    {
        if (!dogSpiritDecisionActive) return;
        ClearMonsterAttackActionsAndState();
        dogSpiritTiedCandidateIds.Clear();
        dogSpiritDecisionActive = false;
        monsterVoteClosed = true;
        monsterAttackLocked = true;
        packTargetId = -1;
        monsterSelectorId = -1;
        monsterSourceRole = RoleType.DogSpirit;
        monsterTargetVersion++;
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
        MonsterVoteStateChanged?.Invoke();
    }

    private void ClearMonsterAttackActionsAndState()
    {
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPackQueuedAction(actions[i]))
            {
                if (actions[i].attackEventId > 0) activeNightAttacks.Remove(actions[i].attackEventId);
                actions.RemoveAt(i);
            }
        }
        for (int i = peekActions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPeekAction(peekActions[i].action))
                peekActions.RemoveAt(i);
        }
        if (monsterAttackEventId > 0) activeNightAttacks.Remove(monsterAttackEventId);
        monsterAttackEventId = 0;
        monsterTarget = -1;
    }

    public bool TryCastMonsterVote(PlayerData actor, int targetId, int nightNumber, out string feedback)
    {
        HookDeathResolver();
        int effectiveNight = nightNumber > 0 ? nightNumber : CurrentEffectiveNightNumber();
        if (dogSpiritDecisionActive)
        {
            return TrySubmitDogSpiritDecision(actor, targetId, effectiveNight, out feedback);
        }
        if (IsMonsterAttackLockedForCurrentNight() || monsterVoteClosed)
        {
            feedback = "Lượt bỏ phiếu của phe Quái vật trong đêm này đã kết thúc.";
            return false;
        }

        if (!CanParticipateInMonsterPackAttack(actor, effectiveNight, out feedback))
        {
            return false;
        }

        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetId);
        if (!IsValidMonsterPackTarget(actor, target, out feedback))
        {
            return false;
        }

        bool isChanging = monsterVoteChoices.TryGetValue(actor.playerID, out var previousChoice) &&
                          previousChoice.Kind == VoteChoiceKind.Player;
        if (isChanging && previousChoice.TargetPlayerId == targetId)
        {
            feedback = $"Bạn đã bỏ phiếu cho P{targetId + 1:00} rồi.";
            return false;
        }

        monsterVoteChoices[actor.playerID] = VoteChoice.ForPlayer(targetId);
        monsterVoteSequence[actor.playerID] = ++nextMonsterVoteOrder;
        actor.hasUseNightAction = true;

        bool useQueued = actions.Any(IsMonsterPackQueuedAction) || RoleManager.UsesConfiguredAbility(actor);
        RecalculateMonsterVoteOutcome(effectiveNight, useQueued, actor.roleDefinition);

        if (monsterVoteTied)
        {
            feedback = $"Đã bỏ phiếu cho {target.DisplayName}. Hiện đang hòa phiếu ({monsterVoteHighestCount} phiếu) — chưa chốt mục tiêu.";
        }
        else if (packTargetId >= 0)
        {
            var leading = PlayerManager.Instance?.GetplayerByID(packTargetId);
            string leadName = leading != null ? leading.DisplayName : $"Player {packTargetId + 1}";
            feedback = $"Đã {(isChanging ? "đổi phiếu sang" : "bỏ phiếu cho")} {target.DisplayName}. Dẫn đầu: {leadName} ({monsterVoteHighestCount} phiếu).";
        }
        else
        {
            feedback = $"Đã bỏ phiếu cho {target.DisplayName}.";
        }
        return true;
    }

    public void PruneInvalidMonsterVotes()
    {
        if (monsterVoteChoices.Count == 0 || monsterVoteClosed || monsterAttackLocked) return;
        int effectiveNight = CurrentEffectiveNightNumber();
        bool changed = false;
        foreach (int voterId in monsterVoteChoices.Keys.ToList())
        {
            var voter = PlayerManager.Instance?.GetplayerByID(voterId);
            if (!CanParticipateInMonsterPackAttack(voter, effectiveNight, out _))
            {
                monsterVoteChoices.Remove(voterId);
                monsterVoteSequence.Remove(voterId);
                changed = true;
                continue;
            }
            var choice = monsterVoteChoices[voterId];
            if (choice.Kind == VoteChoiceKind.Player)
            {
                var target = PlayerManager.Instance?.GetplayerByID(choice.TargetPlayerId);
                if (target == null || !target.isAlive || target.isDisconnected || IsMonsterPackMember(target))
                {
                    monsterVoteChoices.Remove(voterId);
                    monsterVoteSequence.Remove(voterId);
                    changed = true;
                }
            }
        }
        if (changed)
        {
            RecalculateMonsterVoteOutcome(effectiveNight, useQueuedAction: actions.Any(IsMonsterPackQueuedAction), queuedDef: null);
        }
    }

    private void RecalculateMonsterVoteOutcome(int effectiveNight, bool useQueuedAction, ThuongRoleDefinition queuedDef)
    {
        // Bảo đảm phiếu chỉ tính cho các Monster còn sống, hợp lệ và chưa mất kết nối
        foreach (int voterId in monsterVoteChoices.Keys.ToList())
        {
            var voter = PlayerManager.Instance?.GetplayerByID(voterId);
            if (!CanParticipateInMonsterPackAttack(voter, effectiveNight, out _))
            {
                monsterVoteChoices.Remove(voterId);
                monsterVoteSequence.Remove(voterId);
            }
        }

        // Preserve any queued target for ThuongPhaseFiveChecks if only 1 queued action and target was mutated after submission
        bool singleQueuedAction = useQueuedAction && monsterVoteChoices.Count == 1;
        VoteTallyOutcome outcome = VoteManager.EvaluatePlayerBallots(
            monsterVoteChoices,
            id => PlayerManager.Instance?.GetplayerByID(id),
            voter => CanParticipateInMonsterPackAttack(voter, effectiveNight, out _) ? 1 : 0,
            target => target != null && target.isAlive && !target.isDisconnected && (singleQueuedAction || !IsMonsterPackMember(target)));

        monsterVoteTallies.Clear();
        foreach (var pair in outcome.Tallies)
        {
            monsterVoteTallies[pair.Key] = pair.Value;
        }
        monsterVoteTied = outcome.IsTie;
        monsterVoteHighestCount = outcome.HighestVotes;

        int winningId = (!outcome.IsTie && outcome.WinningTargetId >= 0) ? outcome.WinningTargetId : -1;
        RoleType winningSourceRole = RoleType.DogSpirit;
        int representativeVoterId = -1;
        PlayerData representativeActor = null;

        if (winningId >= 0)
        {
            int bestOrder = -1;
            bool foundDogSpirit = false;
            foreach (var ballot in monsterVoteChoices)
            {
                if (ballot.Value.Kind != VoteChoiceKind.Player || ballot.Value.TargetPlayerId != winningId) continue;
                var voter = PlayerManager.Instance?.GetplayerByID(ballot.Key);
                if (voter == null) continue;
                int order = monsterVoteSequence.TryGetValue(ballot.Key, out int seq) ? seq : 0;
                bool isDog = voter.roleType == RoleType.DogSpirit;
                if ((!foundDogSpirit && isDog) || (isDog == foundDogSpirit && order > bestOrder))
                {
                    if (isDog) foundDogSpirit = true;
                    bestOrder = order;
                    representativeVoterId = voter.playerID;
                    representativeActor = voter;
                    winningSourceRole = (voter.roleType == RoleType.WhiteHound || voter.roleType == RoleType.WhiteWolf)
                        ? RoleType.WhiteHound
                        : voter.roleType;
                }
            }
        }

        ApplyMonsterVoteSlot(winningId, winningSourceRole, representativeVoterId, representativeActor, useQueuedAction, queuedDef);
    }

    private void ApplyMonsterVoteSlot(
        int winningId,
        RoleType winningSourceRole,
        int representativeVoterId,
        PlayerData representativeActor,
        bool useQueuedAction,
        ThuongRoleDefinition queuedDef)
    {
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPackQueuedAction(actions[i]))
            {
                if (actions[i].attackEventId > 0) activeNightAttacks.Remove(actions[i].attackEventId);
                actions.RemoveAt(i);
            }
        }
        for (int i = peekActions.Count - 1; i >= 0; i--)
        {
            if (IsMonsterPeekAction(peekActions[i].action))
                peekActions.RemoveAt(i);
        }
        if (monsterAttackEventId > 0) activeNightAttacks.Remove(monsterAttackEventId);
        monsterAttackEventId = 0;

        packTargetId = winningId;
        monsterSelectorId = representativeVoterId;
        monsterSourceRole = winningSourceRole;
        monsterTargetVersion++;

        if (winningId >= 0)
        {
            var winningTarget = PlayerManager.Instance?.GetplayerByID(winningId);
            var effectiveDef = representativeActor?.roleDefinition ?? queuedDef;
            if (useQueuedAction && effectiveDef != null && representativeActor != null && winningTarget != null)
            {
                monsterTarget = -1;
                int eventId = RegisterNightAttack(winningId, DeathCause.Monster, winningSourceRole, representativeVoterId);
                monsterAttackEventId = eventId;
                actions.Add(new QueuedAction
                {
                    actor = representativeActor,
                    target = winningTarget,
                    role = effectiveDef,
                    sequence = nextActionSequence++,
                    attackEventId = eventId
                });
            }
            else
            {
                monsterTarget = winningId;
                monsterAttackEventId = RegisterNightAttack(winningId, DeathCause.Monster, winningSourceRole, representativeVoterId);
            }
        }
        else
        {
            monsterTarget = -1;
        }

        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
        MonsterVoteStateChanged?.Invoke();
    }

    public void SetMonsterSelector(int targetId, int selectorId)
    {
        if (packTargetId != targetId || monsterSelectorId == selectorId) return;
        monsterSelectorId = selectorId;
        monsterTargetVersion++;
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, monsterAttackLocked);
    }

    public void SetProtectedTarget(int id)
    {
        protectedTarget = id;
    }

    public void SetKillerTarget(int id)
    {
        if (killerAttackEventId > 0) activeNightAttacks.Remove(killerAttackEventId);
        killerTarget = id;
        killerAttackEventId = id >= 0 ? RegisterNightAttack(id, DeathCause.Killer, RoleType.Killer) : 0;
    }

    public void RecordSubmittedAction(PlayerData actor, int targetID, string action)
    {
        if (actor == null || GameRoleManager.Instance?.currentPhase != GamePhase.Night)
            return;

        if (IsMonsterPeekAction(action))
        {
            for (int i = peekActions.Count - 1; i >= 0; i--)
            {
                if (IsMonsterPeekAction(peekActions[i].action))
                    peekActions.RemoveAt(i);
            }
        }

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
        foreach (var whEntry in whiteHoundAttacks.Values)
            AddUnrecordedTarget(entries, whEntry.TargetId, "Bạch khuyển", RoleType.WhiteHound);
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

        int effectiveNight = CurrentEffectiveNightNumber();
        if (dogSpiritDecisionActive)
        {
            ResolveDogSpiritDecisionFallback();
        }
        else if (!monsterVoteClosed && !monsterAttackLocked)
        {
            BeginDogSpiritTieDecisionIfNeeded();
            if (dogSpiritDecisionActive)
            {
                ResolveDogSpiritDecisionFallback();
            }
        }
        bool monsterKillExecutedThisNight = effectiveNight > 0 && resolvedMonsterNight == effectiveNight;
        monsterVoteClosed = true;
        monsterAttackLocked = true;
        MonsterPackTargetChanged?.Invoke(packTargetId, monsterSelectorId, monsterSourceRole, true);
        MonsterVoteStateChanged?.Invoke();

        // Stable priority keeps protection ahead of attacks regardless of submission order.
        actions.Sort((left, right) =>
        {
            int priority = (left.role.abilityType == AbilityType.Kill ? 1 : 0)
                .CompareTo(right.role.abilityType == AbilityType.Kill ? 1 : 0);
            return priority != 0 ? priority : left.sequence.CompareTo(right.sequence);
        });
        var snapshot = actions.ToArray();
        actions.Clear();
        foreach (var action in snapshot)
        {
            if (IsMonsterPackQueuedAction(action))
            {
                if (monsterKillExecutedThisNight) continue;
                monsterKillExecutedThisNight = true;
                if (effectiveNight > 0) resolvedMonsterNight = effectiveNight;
            }
            ResolveAction(action);
        }

        if (monsterTarget != -1 && !monsterKillExecutedThisNight)
        {
            PlayerData target = PlayerManager.Instance.GetplayerByID(monsterTarget);
            if (target != null && target.isAlive && !target.isDisconnected && !IsMonsterPackMember(target))
            {
                monsterKillExecutedThisNight = true;
                if (effectiveNight > 0) resolvedMonsterNight = effectiveNight;
                DeathResolver.Instance.TryKill(new DeathRequest(
                    target, DeathCause.Monster, sourceRole: monsterSourceRole, attackEventId: monsterAttackEventId));
            }
        }

        // Xử lý đòn tấn công độc lập của Bạch Khuyển (tách biệt hoàn toàn với Monster Vote)
        var pendingWhiteHound = whiteHoundAttacks.Values.ToArray();
        whiteHoundAttacks.Clear();
        foreach (var whEntry in pendingWhiteHound)
        {
            PlayerData whTarget = PlayerManager.Instance.GetplayerByID(whEntry.TargetId);
            PlayerData whActor = whEntry.AttackerId >= 0 ? PlayerManager.Instance.GetplayerByID(whEntry.AttackerId) : null;
            if (whTarget != null && whTarget.isAlive && !whTarget.isDisconnected &&
                (whActor == null || whActor.playerID != whTarget.playerID))
            {
                DeathResolver.Instance.TryKill(new DeathRequest(
                    whTarget, DeathCause.Monster, whActor, sourceRole: RoleType.WhiteHound, attackEventId: whEntry.AttackEventId));
            }
        }

        int currentNight = evilNightCount;
        if (currentNight <= 0 && NetworkPhaseSync.Instance != null)
            currentNight = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        if (currentNight <= 0 && GameRoleManager.Instance != null)
            currentNight = GameRoleManager.Instance.currentDay;

        if (killerTarget != -1 && currentNight > 0 && currentNight % 2 == 0)
        {
            PlayerData killerTargetPlayer = PlayerManager.Instance.GetplayerByID(killerTarget);
            if (killerTargetPlayer != null && killerTargetPlayer.isAlive)
            {
                DeathResolver.Instance.TryKill(new DeathRequest(
                    killerTargetPlayer, DeathCause.Killer, sourceRole: RoleType.Killer, attackEventId: killerAttackEventId));
            }
        }

        foreach (var player in PlayerManager.Instance.players)
            if (player != null) player.status.isProtected = false;

        monsterTarget = -1;
        monsterAttackEventId = 0;
        monsterSourceRole = RoleType.DogSpirit;
        protectedTarget = -1;
        killerTarget = -1;
        killerAttackEventId = 0;
        peekActions.Clear();
        activeNightAttacks.Clear();
        rescuedAttackEventIds.Clear();
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
            var result = DeathResolver.Instance.TryKill(new DeathRequest(target, cause, actor, attackEventId: action.attackEventId));
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
