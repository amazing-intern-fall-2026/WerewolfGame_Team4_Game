using UnityEngine;
public class WinConditionManager : MonoBehaviour
{
    public static WinConditionManager Instance;
    public event System.Action MatchStateChanged;
    public void NotifyMatchStateChanged() => MatchStateChanged?.Invoke();
    [SerializeField, Min(1)] private int maxDay = 7;
    public int MaxDay => maxDay;
    [SerializeField] private bool useEliminationWin = false;
    private GameRoleManager matchEvaluatorOwner;
    private System.Action<bool> matchEvaluator;
    public bool HasMatchEvaluator(GameRoleManager owner) => owner != null && matchEvaluatorOwner == owner && matchEvaluator != null;
    public void InstallMatchEvaluator(GameRoleManager owner, System.Action<bool> evaluator)
    {
        if (owner == null || owner.gameObject.scene != gameObject.scene)
            throw new System.ArgumentException("The match evaluator must belong to this gameplay scene.");
        matchEvaluatorOwner = owner;
        matchEvaluator = evaluator;
    }
    public void RemoveMatchEvaluator(GameRoleManager owner, System.Action<bool> evaluator)
    {
        if (matchEvaluatorOwner != owner || matchEvaluator != evaluator) return;
        matchEvaluatorOwner = null;
        matchEvaluator = null;
    }
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void CheckWinCondition() { CheckWinCondition(false); }
    public void CheckWinCondition(bool dayFinished)
    {
        var game = GameRoleManager.Instance;
        if (game == null || game.currentState == GameState.GameOver) return;
        if (NetworkGameOverManager.HasPendingHunterDeathShot()) return;
        if (HasMatchEvaluator(game)) { matchEvaluator(dayFinished); return; }
        if (game.manualLogicPrototype || game.HasIntegratedLogic) { EvaluateIntegrated(game, dayFinished); return; }

        if (dayFinished && game.currentDay == 1 &&
            PlayerManager.Instance != null &&
            PlayerManager.Instance.players.Exists(player =>
                player != null && player.roleType == RoleType.Madman &&
                !player.isAlive && player.wasVotedOut))
        {
            game.MadmanWin();
            return;
        }

        if (FoxSpiritHasWon())
        {
            game.FoxSpiritWin();
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
        int aliveKillers = 0;

        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null || !player.isAlive) continue;

            if ((player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf) &&
                player.isWhiteHoundAwakened)
                aliveWhiteWolf++;
            else if (player.faction == FactionType.Monster)
                aliveMonsters++;
            else if (player.roleType == RoleType.Madman || player.roleType == RoleType.FoxSpirit || player.roleType == RoleType.Killer)
            {
                aliveThirdParty++;
                if (player.roleType == RoleType.Killer)
                    aliveKillers++;
            }
            else
                aliveVillagers++;
        }

        if (LoversAreLastTwo())
        {
            game.LoversWin();
            return;
        }

        // Phe trắng thắng khi chỉ còn mỗi WhiteHound và 2 dân làng
        if (aliveWhiteWolf == 1 && aliveMonsters == 0 && aliveKillers == 0 && aliveVillagers <= 2)
        {
            game.WhiteWolfWin();
            return;
        }

        // Sát nhân thắng nếu chỉ còn sát nhân và 2 dân làng
        if (aliveKillers == 1 && aliveMonsters == 0 && aliveWhiteWolf == 0 && aliveVillagers <= 2)
        {
            game.KillerWin();
            return;
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

    private void EvaluateIntegrated(GameRoleManager game, bool dayFinished)
    {
        if (PlayerManager.Instance?.players == null) return;
        if (NetworkGameOverManager.HasPendingHunterDeathShot()) return;
        var state = new System.Collections.Generic.List<Thuong.Online.ThuongWinPlayer>();
        foreach (var player in PlayerManager.Instance.players)
        {
            if (player == null) continue;
            var rule = player.roleDefinition?.neutralWinRule;
            if (player.faction == FactionType.Neutral && rule != null &&
                rule.IsSatisfied(player, PlayerManager.Instance.players))
            { game.FinishLocalMatch(rule.BuildWinMessage(player)); return; }
            bool foxQualified = RoleManager.Instance != null &&
                RoleManager.Instance.playerRoles.TryGetValue(player.playerID, out var role) &&
                role is FoxSpiritRole fox && fox.HasWon;
            state.Add(new Thuong.Online.ThuongWinPlayer(player.playerID, player.DisplayName, 0,
                player.roleType, player.faction, player.isAlive, player.wasVotedOut,
                player.loverID, player.isWhiteHoundAwakened, foxQualified));
        }
        var settings = new Thuong.Online.ThuongOnlineWinSettings
        { eliminationWin = true, minimumPlayersForParity = 1 };
        var outcome = Thuong.Online.ThuongMatchOutcome.Evaluate(state, settings, game.currentDay,
            game.currentPhase, game.manualLogicPrototype ? 0 : TaskManager.Instance != null ? TaskManager.Instance.progress : 0,
            maxDay, dayFinished || game.currentPhase == GamePhase.DayResolution);
        if (outcome != null) game.FinishLocalMatch(outcome.WinnerId);
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


