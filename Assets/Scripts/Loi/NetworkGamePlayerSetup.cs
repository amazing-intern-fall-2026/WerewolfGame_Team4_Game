using System.Collections.Generic;

public static class NetworkGamePlayerSetup
{
    private static readonly HashSet<ulong> activeMatchClientIds = new HashSet<ulong>();

    public static void SetActiveMatchClientIds(IEnumerable<ulong> clientIds)
    {
        activeMatchClientIds.Clear();
        if (clientIds != null)
        {
            foreach (var id in clientIds)
            {
                activeMatchClientIds.Add(id);
            }
        }
    }

    public static bool HasActiveMatchSetup => activeMatchClientIds.Count > 0;

    public static bool IsClientInMatch(ulong clientId)
    {
        if (!HasActiveMatchSetup) return true;
        return activeMatchClientIds.Contains(clientId);
    }

    public static HashSet<ulong> GetActiveMatchClientIds()
    {
        return new HashSet<ulong>(activeMatchClientIds);
    }

    public static void Clear()
    {
        activeMatchClientIds.Clear();
    }
}
