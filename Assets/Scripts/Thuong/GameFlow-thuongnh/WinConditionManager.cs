using UnityEngine;
public class WinConditionManager : MonoBehaviour
{
    public static WinConditionManager Instance;
    [SerializeField, Min(1)] private int maxDay = 7;
    public int MaxDay => maxDay;
    [SerializeField] private bool useEliminationWin = false;
    private void Awake() { Instance = this; }
    public void CheckWinCondition() { CheckWinCondition(false); }
    public void CheckWinCondition(bool dayFinished)
    {
        var game = GameRoleManager.Instance;
        if (game == null || game.currentState == GameState.GameOver) return;

        if (TaskManager.Instance != null && TaskManager.Instance.progress >= 100)
        {
            game.VillagerWin();
            return;
        }

        if (PlayerManager.Instance == null || PlayerManager.Instance.players.Count == 0)
            return;

        int aliveMonsters = 0;
        int aliveVillagers = 0;
        int aliveWhiteWolf = 0;
        int aliveThirdParty = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            if (player.faction == FactionType.Monster)
                aliveMonsters++;
            else if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf)
                aliveWhiteWolf++;
            else if (player.roleType == RoleType.Madman || player.roleType == RoleType.FoxSpirit || player.roleType == RoleType.Killer)
                aliveThirdParty++;
            else
                aliveVillagers++;
        }

        // Phe trắng thắng khi chỉ còn mỗi WhiteHound và 2 dân làng
        if (aliveWhiteWolf > 0 && aliveMonsters == 0 && aliveVillagers <= 2)
        {
            game.WhiteWolfWin();
            return;
        }

        // Madman thắng nếu bị treo cổ ngày đầu tiên
        if (aliveThirdParty > 0 && aliveMonsters == 0 && aliveVillagers <= 2 && aliveThirdParty == 1)
        {
            if (PlayerManager.Instance.players.Exists(p => p != null && p.roleType == RoleType.Madman && p.isAlive == true))
            {
                game.MadmanWin();
                return;
            }
        }

        // Sát nhân thắng nếu chỉ còn sát nhân và 2 dân làng
        if (aliveThirdParty > 0 && aliveMonsters == 0 && aliveVillagers <= 2)
        {
            if (PlayerManager.Instance.players.Exists(p => p != null && p.roleType == RoleType.Killer && p.isAlive))
            {
                game.KillerWin();
                return;
            }
        }

        if (dayFinished && game.currentDay >= maxDay)
        {
            if (LivingLoversExist()) game.LoversWin();
            else game.WerewolfWin();
            return;
        }

        if (!useEliminationWin) return;

        if (aliveMonsters == 0)
        {
            if (LivingLoversExist()) game.LoversWin();
            else game.VillagerWin();
        }
        else if (aliveMonsters >= aliveVillagers + aliveWhiteWolf + aliveThirdParty)
        {
            game.WerewolfWin();
        }
    }

    private bool LivingLoversExist()
    {
        if (PlayerManager.Instance == null)
            return false;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive || player.loverID == -1)
                continue;

            PlayerData lover = PlayerManager.Instance.GetplayerByID(player.loverID);
            if (lover != null && lover.isAlive && lover.loverID == player.playerID)
                return true;
        }

        return false;
    }
}


