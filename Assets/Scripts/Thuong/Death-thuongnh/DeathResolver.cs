using UnityEngine;

public class DeathResolver:MonoBehaviour
{
    public static DeathResolver Instance;

    private void Awake()
    {
        Instance = this;
    }
    public bool TryKillPlayer(int targetID, DeathCause cause)
    { 
        PlayerData target =PlayerManager.Instance.GetplayerByID(targetID);

        if (target == null)
        { 
            return false;
        }
        if (!target.isAlive)
        {
            return false;
        }
        if (target.roleType == RoleType.Idiot && cause == DeathCause.Vote)
        {
            return false;
        }
        if (target.status.isProtected && cause == DeathCause.Monster)
        { 
            target.status.isProtected = false;
            return false;
        }
        target.isAlive = false;
        NetworkPlayerStateSync.SyncGameplayAliveState(targetID, false);
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(targetID, out var role))
            role.OnDeath();

        LoverManager.Instance?.LoverDied(target);
        return true;
    }
}

