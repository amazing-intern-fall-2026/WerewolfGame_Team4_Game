using System.Collections.Generic;

public sealed class VoteResolution
{
    public IReadOnlyDictionary<int, int> Tallies { get; }
    public bool IsTie { get; }
    public PlayerData Eliminated { get; }
    public string Message { get; }
    public VoteResolution(IReadOnlyDictionary<int, int> tallies, bool isTie, PlayerData eliminated, string message)
    { Tallies = tallies; IsTie = isTie; Eliminated = eliminated; Message = message; }
    public string BuildSummary()
    {
        var text = new System.Text.StringBuilder(Message);
        foreach (var tally in Tallies)
            text.Append($" | {PlayerManager.Instance?.GetplayerByID(tally.Key)?.DisplayName ?? $"P{tally.Key + 1}"}: {tally.Value} phiếu");
        return text.ToString();
    }
}
