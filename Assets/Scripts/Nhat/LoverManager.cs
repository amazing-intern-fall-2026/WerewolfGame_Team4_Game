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

        PlayerData playerA =
            PlayerManager.Instance.GetplayerByID(playerAID);

        PlayerData playerB =
            PlayerManager.Instance.GetplayerByID(playerBID);

        if (playerA == null || playerB == null || playerAID == playerBID)
            return;

        if (playerA.loverID != -1 || playerB.loverID != -1)
            return;

        playerA.loverID = playerBID;
        playerB.loverID = playerAID;

        Debug.Log(
            playerA.playerName +
            " ❤️ " +
            playerB.playerName
        );
    }

    public void LoverDied(PlayerData deadPlayer)
    {
        if (deadPlayer == null || deadPlayer.loverID == -1 ||
            PlayerManager.Instance == null || DeathResolver.Instance == null)
            return;

        PlayerData lover =
            PlayerManager.Instance.GetplayerByID(deadPlayer.loverID);

        if (lover == null)
            return;

        if (lover.isAlive)
        {
            DeathResolver.Instance.TryKillPlayer(
                lover.playerID,
                DeathCause.Lover
            );
        }
    }
}
