using System.Collections.Generic;
using UnityEngine;
public class VoteManager : MonoBehaviour
{
    public static VoteManager Instance;
    private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
    private readonly Dictionary<int, int> choices = new Dictionary<int, int>();
    private bool resolved;
    public event System.Action<VoteResolution> VoteResolved;
    public VoteResolution LastResolution { get; private set; }
    public int GetVotedTarget(int voterID) => choices.TryGetValue(voterID, out var target) ? target : -1;
    private void Awake() { Instance = this; }
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
        if (resolved || GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Voting ||
            PlayerManager.Instance == null) return false;
        var voter = PlayerManager.Instance.GetplayerByID(voterID);
        var target = PlayerManager.Instance.GetplayerByID(targetID);
        if (voter == null || target == null || !PlayerManager.Instance.IsAlive(voterID) ||
            !PlayerManager.Instance.IsAlive(targetID) || !voter.CanVote || voter.hasVoted) return false;
        voter.hasVoted = true;
        choices[voterID] = targetID;
        voter.NotifyChanged();
        return true;
    }
    public void ResolveVote()
    {
        // A repeated callback must not recalculate the same choices after a death.
        if (resolved) return;
        resolved = true;
        votes.Clear();
        // Store choices only; transient effects and alive-state are evaluated at resolution.
        foreach (var choice in choices)
        {
            var voter = PlayerManager.Instance?.GetplayerByID(choice.Key);
            var candidate = PlayerManager.Instance?.GetplayerByID(choice.Value);
            if (voter == null || candidate == null || !candidate.isAlive) continue;
            int weight = voter.GetVoteWeight();
            if (weight == 0) continue;
            votes.TryGetValue(candidate.playerID, out int tally);
            votes[candidate.playerID] = tally + weight;
        }
        int target = -1, highest = 0;
        bool tied = false;
        foreach (var vote in votes)
        {
            if (vote.Value > highest) { target = vote.Key; highest = vote.Value; tied = false; }
            else if (vote.Value == highest) tied = true;
        }
        var tallies = new Dictionary<int, int>(votes);
        votes.Clear();
        PlayerData eliminated = null;
        string message = tied ? "Phiếu hòa, không ai bị loại." : "Không có phiếu hợp lệ.";
        if (!tied && target >= 0)
        {
            var selected = PlayerManager.Instance.GetplayerByID(target);
            var death = DeathResolver.Instance.TryKill(new DeathRequest(selected, DeathCause.Vote));
            if (death.Outcome == DeathOutcome.Killed) eliminated = selected;
            message = death.Message;
        }
        LastResolution = new VoteResolution(tallies, tied, eliminated, message);
        VoteResolved?.Invoke(LastResolution);
    }
}

