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
        if (game.manualLogicPrototype) { EvaluateLogicPrototype(game); return; }

        if (dayFinished && game.currentDay == 1 &&
            PlayerManager.Instance != null &&
            PlayerManager.Instance.players.Exists(player =>
                player != null && player.roleType == RoleType.Madman &&
                !player.isAlive && player.wasVotedOut))
        {
            game.MadmanWin();
            return;
        }

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

            if ((player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf) &&
                player.isWhiteHoundAwakened)
                aliveWhiteWolf++;
            else if (player.faction == FactionType.Monster)
                aliveMonsters++;
            else if (player.roleType == RoleType.Madman || player.roleType == RoleType.FoxSpirit || player.roleType == RoleType.Killer)
                aliveThirdParty++;
            else
                aliveVillagers++;
        }

        if (LoversAreLastTwo())
        {
            game.LoversWin();
            return;
        }

        // Phe trắng thắng khi chỉ còn mỗi WhiteHound và 2 dân làng
        if (aliveWhiteWolf > 0 && aliveMonsters == 0 && aliveVillagers <= 2)
        {
            game.WhiteWolfWin();
            return;
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
            game.WerewolfWin();
            return;
        }

        if (!useEliminationWin) return;

        if (aliveMonsters == 0)
        {
            game.VillagerWin();
        }
        else if (aliveMonsters >= aliveVillagers + aliveWhiteWolf + aliveThirdParty)
        {
            game.WerewolfWin();
        }
    }

    private bool LoversAreLastTwo()
    {
        if (PlayerManager.Instance == null)
            return false;

        PlayerData firstAlive = null;
        PlayerData secondAlive = null;
        int aliveCount = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive)
                continue;

            aliveCount++;
            if (aliveCount > 2)
                return false;

            if (firstAlive == null)
                firstAlive = player;
            else
                secondAlive = player;
        }

        return aliveCount == 2 &&
               firstAlive.loverID == secondAlive.playerID &&
               secondAlive.loverID == firstAlive.playerID;
    }

    private void EvaluateLogicPrototype(GameRoleManager game)
    {
        if (PlayerManager.Instance?.players == null) return;
        int alive = 0, wolves = 0, village = 0;
        foreach (var player in PlayerManager.Instance.GetAlivePlayers())
        {
            alive++;
            if (player.faction == FactionType.Monster) wolves++;
            if (player.faction == FactionType.Villager) village++;
            var rule = player.roleDefinition?.neutralWinRule;
            if (player.faction == FactionType.Neutral && rule != null &&
                rule.IsSatisfied(player, PlayerManager.Instance.players))
            { game.EndLogicGame(rule.BuildWinMessage(player)); return; }
        }
        if (alive == 0) game.EndLogicGame("Draw");
        else if (wolves == 0 && village > 0) game.VillagerWin();
        else if (wolves == 0 && village == 0) game.EndLogicGame("Neutral");
        else if (wolves >= alive - wolves) game.WerewolfWin();
    }

    public bool FoxSpiritHasWon()
    {
        if (RoleManager.Instance == null)
            return false;

        foreach (BaseRole role in RoleManager.Instance.playerRoles.Values)
            if (role is FoxSpiritRole foxSpirit && foxSpirit.HasWon)
                return true;

        return false;
    }
}


