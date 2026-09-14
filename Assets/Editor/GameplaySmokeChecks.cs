using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in a separate Unity batchmode process via -executeMethod.
public static class GameplaySmokeChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run checks in Unity batchmode.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var task = ScriptableObject.CreateInstance<TaskData>();
        var inactiveTask = ScriptableObject.CreateInstance<TaskData>();
        try
        {
            var root = new GameObject("Smoke Checks");
            var players = root.AddComponent<PlayerManger>();
            PlayerManger.Instance = players;
            players.CreateTestPlayer(2);
            var roles = root.AddComponent<RoleManger>(); RoleManger.Instance = roles;
            roles.AssignRole();
            Require(roles.playerRoles.Count == 2, "Assign roles with fewer than five players");
            var tasks = root.AddComponent<TaskManager>(); TaskManager.Instance = tasks;
            var timer = root.AddComponent<DayTimer>(); DayTimer.Instance = timer;
            var night = root.AddComponent<NightManager>(); NightManager.Instance = night;
            var vote = root.AddComponent<VoteManger>(); VoteManger.Instance = vote;
            var deaths = root.AddComponent<DeathResolver>(); DeathResolver.Instance = deaths;
            var wins = root.AddComponent<WinConditionManager>(); WinConditionManager.Instance = wins;
            var game = root.AddComponent<GameManager>(); GameManager.Instance = game;
            task.progressValue = 40;
            tasks.allTasks.Add(task);
            tasks.allTasks.Add(task);
            tasks.allTasks.Add(null);
            game.BeginGame();
            Require(tasks.currentTasks.Count == 1, "Task pool removes nulls and duplicates");
            tasks.CompleteTask(inactiveTask);
            Require(tasks.progress == 0, "Reject inactive task");
            tasks.CompleteTask(task); tasks.CompleteTask(task);
            Require(tasks.progress == 40 && !task.isCompleted, "Complete once without mutating asset");
            game.EndDay();
            tasks.CompleteTask(task);
            Require(tasks.progress == 40 && game.currentState == GameState.Nigt, "Night blocks tasks");
            game.SetPhase(GamePhase.Discussion); game.StartVoting();
            vote.Vote(0, 1); vote.Vote(0, 0);
            Require(players.players[0].hasVoted, "Record voter");
            game.FinishVoting();
            Require(!players.players[1].isAlive && players.players[0].isAlive, "Vote kills only chosen target");
            Require(game.currentDay == 2 && game.currentState == GameState.Day, "Vote returns to next day");
            tasks.CompleteTask(task);
            Require(tasks.progress == 80, "New day resets completion and preserves progress");
            players.players[0].status.isProtected = true;
            Require(!deaths.TryKillPlayer(0, DeathCause.Monster), "Protection blocks monster");
            Require(deaths.TryKillPlayer(0, DeathCause.Vote), "Vote bypasses protection");
            game.currentDay = 7;
            wins.CheckWinCondition();
            Require(game.currentState != GameState.GameOver, "Last day remains playable");
            tasks.StartNewDay(); tasks.CompleteTask(task);
            Require(tasks.progress == 100 && game.Winner == "Villagers", "100 percent wins immediately");
            game.WerewolfWin();
            Require(game.Winner == "Villagers", "Winner is terminal");
            game.currentState = GameState.Day; tasks.progress = 0;
            wins.CheckWinCondition(true);
            Require(game.Winner == "Werewolves", "Deadline loses after final day");
            Debug.Log("GAMEPLAY_SMOKE_CHECKS_PASSED");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(task);
            UnityEngine.Object.DestroyImmediate(inactiveTask);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerManger.Instance = null; RoleManger.Instance = null; TaskManager.Instance = null;
            DayTimer.Instance = null; NightManager.Instance = null; VoteManger.Instance = null;
            DeathResolver.Instance = null; WinConditionManager.Instance = null; GameManager.Instance = null;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Smoke check failed: " + message);
    }
}
