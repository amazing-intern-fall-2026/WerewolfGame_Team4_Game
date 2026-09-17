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

    public int serpentNightCount;

    public bool hasHunterTrap;

    public int hunterTargetID = -1;

    public int loverID = -1;

    public bool isWhiteHoundAwakened;

    public bool isCursed;

    public PlayerStatus status = new PlayerStatus();
}
