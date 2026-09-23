using System.Collections.Generic;
using UnityEngine;
public class VoteManager : MonoBehaviour
{
    public static VoteManager Instance;
    private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
    private readonly Dictionary<int, int> choices = new Dictionary<int, int>();
    public int GetVotedTarget(int voterID) => choices.TryGetValue(voterID, out var target) ? target : -1;
    private void Awake() { Instance = this; }
    public void StartVote()
    {
        votes.Clear();
        choices.Clear();
        foreach (var player in PlayerManger.Instance.players) player.hasVoted = false;
    }
    public void Vote(int voterID, int targetID)
    {
        TryVote(voterID, targetID);
    }
    public bool TryVote(int voterID, int targetID)
    {
        if (GameRoleManager.Instance == null || GameRoleManager.Instance.currentState != GameState.Voting ||
            PlayerManger.Instance == null) return false;
        var voter = PlayerManger.Instance.GetplayerByID(voterID);
        var target = PlayerManger.Instance.GetplayerByID(targetID);
        if (voter == null || target == null || !voter.isAlive || !target.isAlive || voter.hasVoted) return false;
        voter.hasVoted = true;
        choices[voterID] = targetID;
        if (!votes.ContainsKey(targetID)) votes[targetID] = 0;
        votes[targetID] += Mathf.Max(1, voter.votPower);
        return true;
    }
    public void ResolveVote()
    {
        int target = -1, highest = 0;
        bool tied = false;
        foreach (var vote in votes)
        {
            if (vote.Value > highest) { target = vote.Key; highest = vote.Value; tied = false; }
            else if (vote.Value == highest) tied = true;
        }
        votes.Clear();
        if (!tied && target >= 0) DeathResolver.Instance.TryKillPlayer(target, DeathCause.Vote);
    }
}
