using System.Collections.Generic;

public static class NetworkGamePlayerNameSetup
{
    private static readonly Dictionary<ulong, string> playerNames =
        new Dictionary<ulong, string>();

    private static readonly HashSet<string> allocatedDefaultNames =
        new HashSet<string>();

    private static int nextDefaultPlayerNumber = 1;

    public static void ResetDefaultIndex()
    {
        nextDefaultPlayerNumber = 1;
        allocatedDefaultNames.Clear();
    }

    /// <summary>
    /// Tự động cấp tên mặc định duy nhất theo thứ tự tham gia phòng: Player 1, Player 2, Player 3, ...
    /// Khi 1 người rời phòng (ví dụ Player 1 rời), người ở lại giữ nguyên tên.
    /// Người mới vào được cấp số tiếp theo chưa sử dụng (ví dụ Player 4).
    /// </summary>
    public static string GenerateNextDefaultName()
    {
        while (allocatedDefaultNames.Contains("Player " + nextDefaultPlayerNumber))
        {
            nextDefaultPlayerNumber++;
        }

        string assigned = "Player " + nextDefaultPlayerNumber;
        allocatedDefaultNames.Add(assigned);
        nextDefaultPlayerNumber++;
        return assigned;
    }

    public static void Save(
        ulong clientId,
        string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            if (playerNames.TryGetValue(clientId, out string existing) && !string.IsNullOrWhiteSpace(existing))
            {
                playerName = existing;
            }
            else
            {
                playerName = GenerateNextDefaultName();
            }
        }

        playerName = playerName.Trim();

        if (playerName.Length > 20)
            playerName = playerName.Substring(0, 20);

        playerNames[clientId] = playerName;

        if (playerName.StartsWith("Player "))
        {
            allocatedDefaultNames.Add(playerName);
        }
    }

    public static string GetName(ulong clientId)
    {
        if (playerNames.TryGetValue(
                clientId,
                out string playerName) && !string.IsNullOrWhiteSpace(playerName))
        {
            return playerName;
        }

        string defaultName = GenerateNextDefaultName();
        playerNames[clientId] = defaultName;
        return defaultName;
    }

    public static bool HasName(ulong clientId)
    {
        return playerNames.ContainsKey(clientId);
    }

    public static void Remove(ulong clientId)
    {
        playerNames.Remove(clientId);
    }

    public static void Clear()
    {
        playerNames.Clear();
        ResetDefaultIndex();
    }
}