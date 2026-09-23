<<<<<<< Updated upstream
// Player data manager is PlayerManger.cs; movement is PlayerMovement.cs.
=======
﻿using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [SerializeField]
    public List<PlayerData> players = new List<PlayerData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        Debug.Log(
            "PLAYER MANAGER AWAKE | Scene = "
            + gameObject.scene.name
            + " | Active = "
            + gameObject.activeInHierarchy
        );
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
        if (players == null)
            return null;

        foreach (var player in players)
        {
            if (player != null && player.playerID == playerID)
                return player;
        }

        return null;
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
>>>>>>> Stashed changes
