using System.Collections.Generic;

public enum VoteChoiceKind : byte
{
    Player,
    NoElimination
}

public readonly struct VoteChoice
{
    public readonly VoteChoiceKind Kind;
    public readonly int TargetPlayerId;

    private VoteChoice(VoteChoiceKind kind, int targetPlayerId)
    { Kind = kind; TargetPlayerId = targetPlayerId; }

    public static VoteChoice ForPlayer(int playerId) => new(VoteChoiceKind.Player, playerId);
    public static VoteChoice NoElimination() => new(VoteChoiceKind.NoElimination, -1);
}

public sealed class VoteResolution
{
    public IReadOnlyDictionary<int, int> Tallies { get; }
    public bool IsTie { get; }
    public int NoEliminationVotes { get; }
    public bool NoEliminationWon { get; }
    public PlayerData Eliminated { get; }
    public string Message { get; }
    public VoteResolution(IReadOnlyDictionary<int, int> tallies, bool isTie, PlayerData eliminated, string message,
        int noEliminationVotes = 0, bool noEliminationWon = false)
    {
        Tallies = tallies; IsTie = isTie; Eliminated = eliminated; Message = message;
        NoEliminationVotes = noEliminationVotes; NoEliminationWon = noEliminationWon;
    }
    public string BuildSummary()
    {
        var text = new System.Text.StringBuilder(Message);
        foreach (var tally in Tallies)
            text.Append($" | {PlayerManager.Instance?.GetplayerByID(tally.Key)?.DisplayName ?? $"P{tally.Key + 1}"}: {tally.Value} phiếu");
        if (NoEliminationVotes > 0) text.Append($" | Không loại ai: {NoEliminationVotes} phiếu");
        return text.ToString();
    }
}
