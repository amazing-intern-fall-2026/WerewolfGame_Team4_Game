using UnityEngine;

public class LoverManager : MonoBehaviour
{
    public static LoverManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void MakeLovers(int playerAID, int playerBID)
    {
        if (PlayerManager.Instance == null)
            return;

        if (playerAID == playerBID)
            return;

        PlayerData playerA = PlayerManager.Instance.GetplayerByID(playerAID);
        PlayerData playerB = PlayerManager.Instance.GetplayerByID(playerBID);

        if (playerA == null || playerB == null)
            return;

        if (!playerA.isAlive || !playerB.isAlive)
            return;

        if (playerA.loverID != -1 || playerB.loverID != -1)
        {
            if (playerA.loverID == playerBID && playerB.loverID == playerAID)
                return;

            return;
        }

        SetLovers(playerA, playerB);

        Debug.Log(
            playerA.playerName +
            " ❤️ " +
            playerB.playerName
        );
    }

    public void LoverDied(PlayerData deadPlayer)
    {
        if (deadPlayer == null)
            return;

        if (deadPlayer.loverID == -1 ||
            PlayerManager.Instance == null || DeathResolver.Instance == null)
            return;

        PlayerData lover = PlayerManager.Instance.GetplayerByID(deadPlayer.loverID);

        if (lover == null)
        {
            deadPlayer.loverID = -1;
            return;
        }

        if (lover.playerID != deadPlayer.loverID || deadPlayer.playerID != lover.loverID)
        {
            deadPlayer.loverID = -1;
            lover.loverID = -1;
            return;
        }

        if (!lover.isAlive)
            return;

        deadPlayer.loverID = -1;
        lover.loverID = -1;

        if (deadPlayer.status != null)
        {
            deadPlayer.status.isLover = false;
            deadPlayer.status.loverID = -1;
        }

        if (lover.status != null)
        {
            lover.status.isLover = false;
            lover.status.loverID = -1;
        }

        DeathResolver.Instance.TryKillPlayer(
            lover.playerID,
            DeathCause.Lover
        );
    }

    private void SetLovers(PlayerData playerA, PlayerData playerB)
    {
        playerA.loverID = playerB.playerID;
        playerB.loverID = playerA.playerID;

        playerA.status ??= new PlayerStatus();
        playerB.status ??= new PlayerStatus();

        playerA.status.isLover = true;
        playerA.status.loverID = playerB.playerID;

        playerB.status.isLover = true;
        playerB.status.loverID = playerA.playerID;
    }
}
