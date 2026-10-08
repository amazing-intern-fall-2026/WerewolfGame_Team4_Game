using System.Collections.Generic;

public static class NetworkGamePlayerNameSetup
{
    private static readonly Dictionary<ulong, string> playerNames =
        new Dictionary<ulong, string>();

    public static void Save(
        ulong clientId,
        string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player " + (clientId + 1);

        playerName = playerName.Trim();

        if (playerName.Length > 20)
            playerName = playerName.Substring(0, 20);

        playerNames[clientId] = playerName;
    }

    public static string GetName(ulong clientId)
    {
        if (playerNames.TryGetValue(
                clientId,
                out string playerName))
        {
            return playerName;
        }

        return "Player " + (clientId + 1);
    }

    public static bool HasName(ulong clientId)
    {
        return playerNames.ContainsKey(clientId);
    }

    public static void Clear()
    {
        playerNames.Clear();
    }
}