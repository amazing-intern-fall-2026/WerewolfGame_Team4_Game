using UnityEngine;
[System.Serializable]
public class PlayerData
{
    public int playerID;
    public string playerName;

    public RoleType roleType;

    public FactionType faction;

    public bool isAlive =true ;

    public int votPower = 1;

    public bool hasVoted;

    public bool hasUseNightAction;

    public PlayerStatus status = new PlayerStatus();
}
