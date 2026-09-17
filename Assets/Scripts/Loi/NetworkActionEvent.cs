public struct NetworkActionEvent
{
    public ulong RequesterPlayerId;
    public ulong TargetPlayerId;

    public NetworkActionEvent(
        ulong requesterPlayerId,
        ulong targetPlayerId)
    {
        RequesterPlayerId = requesterPlayerId;
        TargetPlayerId = targetPlayerId;
    }
}