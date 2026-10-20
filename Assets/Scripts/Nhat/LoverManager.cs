using UnityEngine;

public class LoverManager : MonoBehaviour
{
    public static LoverManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetForNewMatch()
    {
        if (PlayerManager.Instance == null || PlayerManager.Instance.players == null)
            return;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null)
                continue;

            player.loverID = -1;
        }
    }

    public void MakeLovers(int playerAID, int playerBID)
    {
        TryMakeLovers(playerAID, playerBID, out _);
    }

    public bool TryMakeLovers(int playerAID, int playerBID, out string feedback)
    {
        feedback = string.Empty;

        if (PlayerManager.Instance == null)
        {
            feedback = "Chưa có danh sách người chơi.";
            return false;
        }

        if (playerAID < 0 || playerBID < 0)
        {
            feedback = "Mã người chơi không hợp lệ.";
            return false;
        }

        if (playerAID == playerBID)
        {
            feedback = "Hai mục tiêu phải khác nhau.";
            return false;
        }

        PlayerData playerA = PlayerManager.Instance.GetplayerByID(playerAID);
        PlayerData playerB = PlayerManager.Instance.GetplayerByID(playerBID);

        if (playerA == null || playerB == null)
        {
            feedback = "Không tìm thấy người chơi hợp lệ.";
            return false;
        }

        if (!playerA.isAlive || !playerB.isAlive)
        {
            feedback = "Hai mục tiêu phải là người chơi còn sống.";
            return false;
        }

        if (playerA.loverID == playerBID && playerB.loverID == playerAID)
        {
            feedback = $"{playerA.playerName} và {playerB.playerName} đã là đôi.";
            return true;
        }

        if (playerA.loverID != -1 && playerA.loverID != playerBID)
        {
            PlayerData previousA = PlayerManager.Instance.GetplayerByID(playerA.loverID);
            if (previousA == null || previousA.loverID != playerA.playerID)
                playerA.loverID = -1;
            else
            {
                feedback = "Một trong hai người đã có đôi.";
                return false;
            }
        }

        if (playerB.loverID != -1 && playerB.loverID != playerAID)
        {
            PlayerData previousB = PlayerManager.Instance.GetplayerByID(playerB.loverID);
            if (previousB == null || previousB.loverID != playerB.playerID)
                playerB.loverID = -1;
            else
            {
                feedback = "Một trong hai người đã có đôi.";
                return false;
            }
        }

        playerA.loverID = playerBID;
        playerB.loverID = playerAID;

        Debug.Log($"{playerA.playerName} ❤️ {playerB.playerName}");
        feedback = $"Đã ghép đôi {playerA.playerName} và {playerB.playerName}.";
        return true;
    }

    public void LoverDied(PlayerData deadPlayer)
    {
        if (deadPlayer == null || PlayerManager.Instance == null || DeathResolver.Instance == null)
            return;

        if (deadPlayer.loverID == -1)
            return;

        PlayerData lover = PlayerManager.Instance.GetplayerByID(deadPlayer.loverID);

        if (lover == null)
        {
            deadPlayer.loverID = -1;
            return;
        }

        if (lover.loverID != deadPlayer.playerID)
        {
            deadPlayer.loverID = -1;
            if (lover.loverID == deadPlayer.playerID)
                lover.loverID = -1;
            return;
        }

        if (lover.isAlive)
        {
            lover.loverID = -1;
            deadPlayer.loverID = -1;
            DeathResolver.Instance.TryKillPlayer(lover.playerID, DeathCause.Lover);
            return;
        }

        deadPlayer.loverID = -1;
        lover.loverID = -1;
    }
}
