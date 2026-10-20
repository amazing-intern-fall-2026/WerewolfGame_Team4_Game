public struct NetworkActionEvent
{
    public ulong RequesterPlayerId;
    public ulong TargetPlayerId;
    public int SubActionId;

    public NetworkActionEvent(
        ulong requesterPlayerId,
        ulong targetPlayerId,
        int subActionId = 0)
    {
        RequesterPlayerId = requesterPlayerId;
        TargetPlayerId = targetPlayerId;
        SubActionId = subActionId;
    }
}