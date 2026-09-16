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
        PlayerData playerA =
            PlayerManger.Instance.GetplayerByID(playerAID);

        PlayerData playerB =
            PlayerManger.Instance.GetplayerByID(playerBID);

        if (playerA == null || playerB == null)
            return;

        if (playerA == playerB)
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
        if (deadPlayer.loverID == -1)
            return;

        PlayerData lover =
            PlayerManger.Instance.GetplayerByID(deadPlayer.loverID);

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