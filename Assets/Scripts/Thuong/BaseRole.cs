using UnityEngine;

public class BaseRole
{
    public PlayerData owner;
    public RoleType roleType;
    public FactionType faction;

    public BaseRole(PlayerData owner)
    {
        this.owner = owner;

    }
    public virtual void OnGameStart() { }
    public virtual void OnDayStart() { }
    public virtual void OnNightStart() { }
    public virtual void UseNightAbility(int TargetID) { }
    public virtual void OnVotStart() { }
    public virtual void OnDeath() { }
    public virtual bool checkSpecialWin()
    {
        return false;
    }
}
