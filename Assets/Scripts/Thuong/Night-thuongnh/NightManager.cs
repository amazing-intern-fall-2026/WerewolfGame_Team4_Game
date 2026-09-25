using UnityEngine;

public class NightManager : MonoBehaviour
{
    public static NightManager Instance;

    private int monsterTarget = -1;
    private int protectedTarget = -1;
    private int killerTarget = -1;
    private int evilNightCount = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void StartNight()
    {
        monsterTarget = -1;
        protectedTarget = -1;
        killerTarget = -1;
        evilNightCount++;

        RoleManager.Instance?.NotifyNightStart();

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null) continue;
            player.status.isProtected = false;
            player.hasUseNightAction = false;
            if (player.roleType == RoleType.SerpentSpirit)
                player.serpentNightCount++;
        }
    }

    public void SetMonsterTarget(int id)
    {
        monsterTarget = id;
    }

    public void SetProtectedTarget(int id)
    {
        protectedTarget = id;
    }

    public void SetKillerTarget(int id)
    {
        killerTarget = id;
    }

    public void ResolveNight()
    {
        PlayerData protectedPlayer = null;
        if (protectedTarget != -1)
        {
            protectedPlayer = PlayerManager.Instance.GetplayerByID(protectedTarget);
            if (protectedPlayer != null && protectedPlayer.isAlive)
                protectedPlayer.status.isProtected = true;
        }

        if (monsterTarget != -1)
        {
            PlayerData target = PlayerManager.Instance.GetplayerByID(monsterTarget);
            if (target != null && target.isAlive && (protectedPlayer == null || target.playerID != protectedPlayer.playerID))
            {
                DeathResolver.Instance.TryKillPlayer(monsterTarget, DeathCause.Monster);
            }
        }

        if (killerTarget != -1 && evilNightCount % 2 == 0)
        {
            PlayerData killerTargetPlayer = PlayerManager.Instance.GetplayerByID(killerTarget);
            if (killerTargetPlayer != null && killerTargetPlayer.isAlive)
            {
                if (protectedPlayer == null || killerTargetPlayer.playerID != protectedPlayer.playerID)
                    DeathResolver.Instance.TryKillPlayer(killerTarget, DeathCause.Killer);
            }
        }

        foreach (var player in PlayerManager.Instance.players)
            if (player != null) player.status.isProtected = false;

        monsterTarget = -1;
        protectedTarget = -1;
        killerTarget = -1;
    }
}

