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
        TryMakeLovers(playerAID, playerBID, out _);
    }

    public bool TryMakeLovers(int playerAID, int playerBID, out string feedback)
    {
        if (PlayerManager.Instance == null)
        {
            feedback = "Chưa có danh sách người chơi.";
            return false;
        }

        PlayerData playerA =
            PlayerManager.Instance.GetplayerByID(playerAID);

        PlayerData playerB =
            PlayerManager.Instance.GetplayerByID(playerBID);

        if (playerA == null || playerB == null || !playerA.isAlive || !playerB.isAlive || playerAID == playerBID)
        {
            feedback = "Hai mục tiêu phải là người chơi còn sống và khác nhau.";
            return false;
        }

        if (playerA.loverID != -1 || playerB.loverID != -1)
        {
            feedback = "Một trong hai người đã có đôi.";
            return false;
        }

        playerA.loverID = playerBID;
        playerB.loverID = playerAID;

        Debug.Log(
            playerA.playerName +
            " ❤️ " +
            playerB.playerName
        );

        feedback = $"Đã ghép đôi {playerA.playerName} và {playerB.playerName}.";
        return true;
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
