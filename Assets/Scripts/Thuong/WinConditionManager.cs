using UnityEngine;
public class WinConditionManager : MonoBehaviour
{
    public static WinConditionManager Instance;
    [SerializeField, Min(1)] private int maxDay = 7;
    [SerializeField] private bool useEliminationWin = false;
    private void Awake() { Instance = this; }
    public void CheckWinCondition() { CheckWinCondition(false); }
    public void CheckWinCondition(bool dayFinished)
    {
        var game = GameManager.Instance;
        if (game == null || game.currentState == GameState.GameOver) return;
        if (TaskManager.Instance != null && TaskManager.Instance.progress >= 100)
        { game.VillagerWin(); return; }
        if (dayFinished && game.currentDay >= maxDay)
        { game.WerewolfWin(); return; }
        if (!useEliminationWin || PlayerManger.Instance == null || PlayerManger.Instance.players.Count == 0) return;
        int monsters = 0, others = 0;
        foreach (var player in PlayerManger.Instance.players)
        {
            if (!player.isAlive) continue;
            if (player.faction == FactionType.Monster) monsters++; else others++;
        }
        if (monsters == 0) game.VillagerWin();
        else if (monsters >= others) game.WerewolfWin();
    }
}