using System.Collections.Generic;

public sealed class VoteResolution
{
    public IReadOnlyDictionary<int, int> Tallies { get; }
    public bool IsTie { get; }
    public PlayerData Eliminated { get; }
    public string Message { get; }
    public VoteResolution(IReadOnlyDictionary<int, int> tallies, bool isTie, PlayerData eliminated, string message)
    { Tallies = tallies; IsTie = isTie; Eliminated = eliminated; Message = message; }
}
