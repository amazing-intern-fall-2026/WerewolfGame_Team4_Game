using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [SerializeField]
    public List<PlayerData> players = new List<PlayerData>();
    [SerializeField]
    public List<PlayerData> disconnectedPlayers = new List<PlayerData>();
    [Header("Offline prototype")]
    [Min(0)] public int prototypeLobbySize;

    public int ConnectedPlayerCount => players != null ? players.Count : 0;
    public int AlivePlayerCount => GetAlivePlayerCount();

    public int GetAlivePlayerCount()
    {
        if (players == null) return 0;
        int count = 0;
        foreach (var p in players)
        {
            if (p != null && p.isAlive && !p.isDisconnected)
                count++;
        }
        return count;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        // Chỉ tạo lobby mẫu khi scene prototype chưa có người chơi thật.
        if (prototypeLobbySize > 0 && (players == null || players.Count == 0))
            CreateTestPlayer(prototypeLobbySize);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterPlayer(PlayerData player)
    {
        if (player == null)
            return;

        if (!players.Contains(player))
            players.Add(player);
    }

    public void CreateTestPlayer(int count)
    {
        players ??= new List<PlayerData>();
        players.Clear();

        for (int i = 0; i < count; i++)
        {
            players.Add(new PlayerData
            {
                playerID = i,
                playerName = "Player " + i,
                isAlive = true,
                votePower = 1,
                hasVoted = false,
                hasUseNightAction = false,
                status = new PlayerStatus()
            });
        }
    }

    public PlayerData GetplayerByID(int playerID)
    {
        if (players != null)
        {
            foreach (var player in players)
            {
                if (player != null && player.playerID == playerID)
                    return player;
            }
        }

        if (disconnectedPlayers != null)
        {
            foreach (var player in disconnectedPlayers)
            {
                if (player != null && player.playerID == playerID)
                    return player;
            }
        }

        return null;
    }

    public bool IsAlive(int playerID)
    {
        PlayerData player = GetplayerByID(playerID);
        return player != null && player.isAlive && !player.isDisconnected;
    }

    public bool IsDisconnected(int playerID)
    {
        if (disconnectedPlayers != null)
        {
            foreach (var player in disconnectedPlayers)
            {
                if (player != null && player.playerID == playerID)
                    return true;
            }
        }

        PlayerData p = GetplayerByID(playerID);
        return p != null && p.isDisconnected;
    }

    public bool RemoveDisconnectedPlayer(int playerID)
    {
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null && players[i].playerID == playerID)
            {
                PlayerData p = players[i];
                p.isDisconnected = true;
                p.isAlive = false;
                p.hasVoted = false;
                p.hasUseNightAction = false;
                players.RemoveAt(i);
                disconnectedPlayers ??= new List<PlayerData>();
                if (!disconnectedPlayers.Contains(p))
                {
                    disconnectedPlayers.Add(p);
                }
                p.NotifyChanged();
                Debug.Log($"[PlayerManager] Đã loại player {playerID} (Disconnected). Số player active còn lại: {players.Count}");
                return true;
            }
        }

        return false;
    }

    public IEnumerable<PlayerData> GetAlivePlayers()
    {
        if (players == null)
            yield break;

        foreach (PlayerData player in players)
        {
            if (player != null && player.isAlive && !player.isDisconnected)
                yield return player;
        }
    }

    public bool SetAliveState(int playerID, bool isAlive)
    {
        PlayerData player = GetplayerByID(playerID);
        if (player == null || player.isAlive == isAlive)
            return false;

        player.isAlive = isAlive;
        if (isAlive)
        {
            player.hasDeathRecord = false;
            player.hasVoted = false;
            player.hasUseNightAction = false;
        }

        player.NotifyChanged();
        return true;
    }

    public void UnlockPlayers()
    {
        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player != null)
                player.status.isSilenced = false;
        }
    }

    public void LockPlayers()
    {
        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player != null)
                player.status.isSilenced = true;
        }
    }
}

