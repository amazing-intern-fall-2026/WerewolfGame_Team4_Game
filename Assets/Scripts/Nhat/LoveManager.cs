using UnityEngine;

public class LoveManager : MonoBehaviour
{
    public static LoverManager Instance => LoverManager.Instance;

    public void MakeLovers(int playerAID, int playerBID)
    {
        if (LoverManager.Instance != null)
            LoverManager.Instance.MakeLovers(playerAID, playerBID);
    }

    public void LoverDied(PlayerData deadPlayer)
    {
        if (LoverManager.Instance != null)
            LoverManager.Instance.LoverDied(deadPlayer);
    }
}
