using UnityEngine;

public class NightManager : MonoBehaviour
{
    public static NightManager Instance;
    int montserTarget = -1;
    int protectedTarget=-1;

    private void Awake()
    {
        Instance = this;
    }
    public void StartNight()
    {
        montserTarget = -1;
        protectedTarget = -1;
        RoleManger.Instance?.NotifyNightStart();
        foreach (var player in PlayerManger.Instance.players)
        {
            player.status.isProtected = false;
            player.hasUseNightAction = false;
            if (player.roleType == RoleType.SerpentSpirit)
                player.serpentNightCount++;
        }
    }
    public void SetMonsterTarget(int id)
    { 
        montserTarget = id;
    }
    public void SetProtectedTarget(int id)
    { 
        protectedTarget = id;
    }
    public void ResolveNight()
    {
        if (protectedTarget != -1)
        { 
            PlayerData target =PlayerManger.Instance.GetplayerByID(protectedTarget);
            if (target != null && target.isAlive) target.status.isProtected = true;
        }
        if (montserTarget != -1)
        {
            DeathResolver.Instance.TryKillPlayer(montserTarget,DeathCause.Monster);
        }
        foreach (var player in PlayerManger.Instance.players) player.status.isProtected = false;
        montserTarget = -1;
        protectedTarget = -1;
    }
}
