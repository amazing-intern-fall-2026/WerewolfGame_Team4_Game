using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public static PlayerManager Instance;

    public List<PlayerData> players = new List<PlayerData>();
    public void Awake()
    {
        Instance = this;
        if (players.Count == 0) CreateTestPlayer(5);
    }
    public void CreateTestPlayer(int amount)
    {
        players.Clear();
        for (int i = 0; i < amount; i++)
        {
            PlayerData player = new PlayerData();

            player.playerID = i;
            player.playerName = "player" + (i + 1);
            players.Add(player);
        }
    }
    public PlayerData GetplayerByID(int id)
    {
        return players.Find(p => p.playerID == id);
    }

    // test 
    private void Start()
    {



        foreach (PlayerData player in players)
        {
            Debug.Log(
                "ID: " + player.playerID +
                " | Name: " + player.playerName +
                " | Alive: " + player.isAlive +
                " | Vote Power: " + player.votPower
            );
        }
    }

    public void LockPlayers() { SetMovement(false); }
    public void UnlockPlayers() { SetMovement(true); }
    private void SetMovement(bool value)
    {
        foreach (var movement in FindObjectsByType<Assets.Scripts.Thuong.PlayerMovement>())
            movement.SetCanMove(value);
    }
}
