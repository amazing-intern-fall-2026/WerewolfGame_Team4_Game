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
    public virtual bool HasNightAbility =>
        GetType().GetMethod(nameof(UseNightAbility)).DeclaringType != typeof(BaseRole);
    public virtual bool TryUseNightAbility(int targetID, out string feedback)
    {
        // Giữ tương thích với các Role đang ghi đè UseNightAbility.
        UseNightAbility(targetID);
        feedback = "Đã thực hiện kỹ năng ban đêm.";
        return true;
    }
    public virtual void OnVoteStart() { }
    public virtual void OnDeath() { }
    public virtual bool checkSpecialWin()
    {
        return false;
    }
}
