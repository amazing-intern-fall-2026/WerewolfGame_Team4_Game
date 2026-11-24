using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct VoteTallyOutcome
{
    public readonly Dictionary<int, int> Tallies;
    public readonly int WinningTargetId;
    public readonly int HighestVotes;
    public readonly int NoEliminationVotes;
    public readonly bool IsTie;
    public readonly bool NoEliminationWon;
    public readonly int TopChoiceCount;

    public VoteTallyOutcome(
        Dictionary<int, int> tallies,
        int winningTargetId,
        int highestVotes,
        int noEliminationVotes,
        bool isTie,
        bool noEliminationWon,
        int topChoiceCount)
    {
        Tallies = tallies;
        WinningTargetId = winningTargetId;
        HighestVotes = highestVotes;
        NoEliminationVotes = noEliminationVotes;
        IsTie = isTie;
        NoEliminationWon = noEliminationWon;
        TopChoiceCount = topChoiceCount;
    }
}

public class VoteManager : MonoBehaviour
{
    public static VoteManager Instance;
    private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
    private readonly Dictionary<int, VoteChoice> choices = new Dictionary<int, VoteChoice>();
    private bool resolved;
    public event System.Action<VoteResolution> VoteResolved;
    public VoteResolution LastResolution { get; private set; }
    public bool AllowVoteChanges { get; set; }
    public bool AllowSelfVote { get; set; } = true;
    public int GetVotedTarget(int voterID) => choices.TryGetValue(voterID, out var choice) &&
        choice.Kind == VoteChoiceKind.Player ? choice.TargetPlayerId : -1;
    public bool VotedNoElimination(int voterID) => choices.TryGetValue(voterID, out var choice) &&
        choice.Kind == VoteChoiceKind.NoElimination;

    private ActionValidationResult ValidateVoter(int voterID)
    {
        var game = GameRoleManager.Instance;
        if (resolved || game == null || game.currentState != GameState.Voting ||
            ThuongLogicSceneScope.IsActive && game.currentPhase != GamePhase.Voting)
            return ActionValidationResult.Fail(ActionFailureReason.WrongPhase, "Hiện không phải lượt bỏ phiếu.");
        var voter = PlayerManager.Instance?.GetplayerByID(voterID);
        if (voter == null) return ActionValidationResult.Fail(ActionFailureReason.ActorMissing, "Không tìm thấy người bỏ phiếu.");
        if (!voter.isAlive) return ActionValidationResult.Fail(ActionFailureReason.ActorDead, "Người đã chết không được bỏ phiếu.");
        if (!voter.CanVote) return ActionValidationResult.Fail(ActionFailureReason.CannotVote, "Cannot Vote: bạn đang bị khóa quyền bỏ phiếu.");
        if (voter.hasVoted && !AllowVoteChanges) return ActionValidationResult.Fail(ActionFailureReason.AlreadyUsed, "Bạn đã bỏ phiếu trong lượt này.");
        return ActionValidationResult.Success();
    }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public ActionValidationResult ValidateVote(int voterID, int targetID)
    {
        var voterValidation = ValidateVoter(voterID);
        if (!voterValidation.IsValid) return voterValidation;
        var target = PlayerManager.Instance?.GetplayerByID(targetID);
        if (target == null) return ActionValidationResult.Fail(ActionFailureReason.TargetMissing, "Hãy chọn mục tiêu bỏ phiếu.");
        if (!target.isAlive) return ActionValidationResult.Fail(ActionFailureReason.TargetDead, "Mục tiêu đã bị loại.");
        if (!AllowSelfVote && voterID == targetID)
            return ActionValidationResult.Fail(ActionFailureReason.SelfTargetForbidden, "Không thể bỏ phiếu cho chính mình.");
        return ActionValidationResult.Success();
    }

    public ActionValidationResult ValidateNoEliminationVote(int voterID) => ValidateVoter(voterID);
    public void StartVote()
    {
        votes.Clear();
        choices.Clear();
        LastResolution = null;
        resolved = false;
        if (PlayerManager.Instance?.players == null) return;
        foreach (var player in PlayerManager.Instance.players)
            if (player != null) player.hasVoted = false;
    }
    public void Vote(int voterID, int targetID)
    {
        TryVote(voterID, targetID);
    }
    public bool TryVote(int voterID, int targetID)
    {
        if (!ValidateVote(voterID, targetID).IsValid) return false;
        return RecordChoice(voterID, VoteChoice.ForPlayer(targetID));
    }

    public bool TryVoteNoElimination(int voterID)
    {
        if (!ValidateNoEliminationVote(voterID).IsValid) return false;
        return RecordChoice(voterID, VoteChoice.NoElimination());
    }

    private bool RecordChoice(int voterID, VoteChoice choice)
    {
        if (resolved || GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Voting ||
            PlayerManager.Instance == null) return false;
        var voter = PlayerManager.Instance.GetplayerByID(voterID);
        if (voter == null || !PlayerManager.Instance.IsAlive(voterID) || !voter.CanVote || voter.hasVoted && !AllowVoteChanges) return false;
        if (choice.Kind == VoteChoiceKind.Player)
        {
            var target = PlayerManager.Instance.GetplayerByID(choice.TargetPlayerId);
            if (target == null || !PlayerManager.Instance.IsAlive(choice.TargetPlayerId)) return false;
            if (!AllowSelfVote && voterID == choice.TargetPlayerId) return false;
        }
        voter.hasVoted = true;
        choices[voterID] = choice;
        voter.NotifyChanged();
        return true;
    }
    public static VoteTallyOutcome EvaluatePlayerBallots(
        IReadOnlyDictionary<int, VoteChoice> ballotChoices,
        Func<int, PlayerData> resolvePlayer,
        Func<PlayerData, int> getWeight,
        Func<PlayerData, bool> isEligibleTarget = null)
    {
        var tallies = new Dictionary<int, int>();
        int noEliminationVotes = 0;

        if (ballotChoices != null && resolvePlayer != null && getWeight != null)
        {
            foreach (var ballot in ballotChoices)
            {
                var voter = resolvePlayer(ballot.Key);
                if (voter == null) continue;
                int weight = getWeight(voter);
                if (weight <= 0) continue;

                if (ballot.Value.Kind == VoteChoiceKind.NoElimination)
                {
                    noEliminationVotes += weight;
                    continue;
                }

                var candidate = resolvePlayer(ballot.Value.TargetPlayerId);
                if (candidate == null || !candidate.isAlive) continue;
                if (isEligibleTarget != null && !isEligibleTarget(candidate)) continue;

                tallies.TryGetValue(candidate.playerID, out int count);
                tallies[candidate.playerID] = count + weight;
            }
        }

        int target = -1;
        int highest = noEliminationVotes;
        int topChoices = noEliminationVotes > 0 ? 1 : 0;
        bool skipIsTop = noEliminationVotes > 0;

        foreach (var vote in tallies)
        {
            if (vote.Value > highest)
            {
                target = vote.Key;
                highest = vote.Value;
                topChoices = 1;
                skipIsTop = false;
            }
            else if (vote.Value == highest && highest > 0)
            {
                topChoices++;
                target = -1;
            }
        }

        bool tied = highest > 0 && topChoices > 1;
        bool noEliminationWon = highest > 0 && !tied && skipIsTop;
        return new VoteTallyOutcome(
            tallies,
            (!tied && !noEliminationWon) ? target : -1,
            highest,
            noEliminationVotes,
            tied,
            noEliminationWon,
            topChoices);
    }

    public void ResolveVote()
    {
        // A repeated callback must not recalculate the same choices after a death.
        if (resolved || GameRoleManager.Instance?.currentState == GameState.GameOver) return;
        using var batch = DeathResolver.Instance?.BeginBatch();
        resolved = true;
        votes.Clear();
        // Store choices only; transient effects and alive-state are evaluated at resolution.
        var outcome = EvaluatePlayerBallots(
            choices,
            id => PlayerManager.Instance?.GetplayerByID(id),
            voter => voter.GetVoteWeight());
        var tallies = new Dictionary<int, int>(outcome.Tallies);
        bool tied = outcome.IsTie;
        bool noEliminationWon = outcome.NoEliminationWon;
        int target = outcome.WinningTargetId;
        int noEliminationVotes = outcome.NoEliminationVotes;
        PlayerData eliminated = null;
        string message = tied ? "Phiếu hòa, không ai bị loại."
            : noEliminationWon ? "Người chơi đã chọn không loại ai."
            : "Không có phiếu hợp lệ.";
        if (!tied && !noEliminationWon && target >= 0)
        {
            var selected = PlayerManager.Instance.GetplayerByID(target);
            var death = DeathResolver.Instance.TryKill(new DeathRequest(selected, DeathCause.Vote));
            if (death.Outcome == DeathOutcome.Killed) eliminated = selected;
            message = death.Message;
        }
        LastResolution = new VoteResolution(tallies, tied, eliminated, message, noEliminationVotes, noEliminationWon);
        VoteResolved?.Invoke(LastResolution);
    }
}

