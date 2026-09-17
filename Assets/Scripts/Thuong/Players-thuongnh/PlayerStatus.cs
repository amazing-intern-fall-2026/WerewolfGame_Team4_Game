using UnityEngine;
[System.Serializable]
public class PlayerStatus
{
    public bool isLover;
    public int loverID=-1;

    public bool isSilenced;

    public bool isCharmed;

    public bool isProtected;

    public bool hasDeathMark;

    public int deathMarkRemainInNight = 0;

    public bool hasDelayDeath;
}