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
        return TryKillPlayer(targetID, cause, null);
    }

    public bool TryKillPlayer(int targetID, DeathCause cause, RoleType? sourceRole)
    {
        PlayerData target = PlayerManager.Instance?.GetplayerByID(targetID);

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
        target.status ??= new PlayerStatus();
        if (target.status.isProtected && cause == DeathCause.Monster)
        { 
            target.status.isProtected = false;
            return false;
        }
        if (target.status.isCharmed &&
            (cause == DeathCause.Monster || cause == DeathCause.Killer))
        {
            return false;
        }

        if (target.roleType == RoleType.WhiteHound &&
            sourceRole == RoleType.DogSpirit && !target.isWhiteHoundAwakened)
        {
            if (RoleManager.Instance != null &&
                RoleManager.Instance.playerRoles.TryGetValue(targetID, out var whiteHoundRole) &&
                whiteHoundRole is WhiteHound whiteHound)
            {
                whiteHound.BecomeMonster();
            }
            else
            {
                target.faction = FactionType.Monster;
                target.isWhiteHoundAwakened = true;
            }
            return false;
        }

        if (!PlayerManager.Instance.SetAliveState(targetID, false))
            return false;
        NetworkPlayerStateSync.SyncGameplayAliveState(targetID, false);
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles.TryGetValue(targetID, out var role))
            role.OnDeath();

        LoverManager.Instance?.LoverDied(target);
        return true;
    }
}

