using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Maps;
using Thuong.Minigames;
using Thuong.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ThuongRoomUpgradeChecks
{
    private static int checks;
    private static readonly List<string> passed = new();
    public static void VerifyAndBuild()
    {
        Run();
        ThuongMinigameChecks.Run();
        ThuongEmoteSystemChecks.Run();
        ThuongMapPlayerBuild.BuildRelease();
    }
    public static void Require(bool valid, string description)
    {
        if (!valid) throw new InvalidOperationException("Room upgrade: " + description);
        checks++; passed.Add(description);
    }

    [MenuItem("Tools/Thuong/Room Upgrades/Check Rules Tasks And Routes")]
    public static void Run()
    {
        checks = 0; passed.Clear();
        foreach (ThuongRolePreset preset in Enum.GetValues(typeof(ThuongRolePreset)))
        for (int count = 2; count <= 25; count++)
        for (int seed = 0; seed < 16; seed++)
        {
            var rules = new ThuongRoomRules { preset = preset };
            var pool = RolePoolBuilder.Build(count, seed, rules.AllowedRoles);
            Require(pool.Count == count && pool.All(rules.AllowedRoles.Contains) && pool.Contains(RoleType.DogSpirit) && pool.Contains(RoleType.Villager),
                $"Preset {preset}, {count} players, seed {seed}: compatible complete pool");
        }
        var automatic = new ThuongRoomRules();
        Require(automatic.Resolve(25).discussion > automatic.Resolve(2).discussion, "Large rooms get more discussion time");
        var manual = new ThuongRoomRules { automaticTimes = false, day = 135, night = 25, discussion = 75, voting = 40 };
        Require(manual.Resolve(25).day == 135 && manual.Resolve(2).voting == 40, "Manual durations are preserved across player counts");
        Require(!new ThuongRoomRules { day = 59 }.IsValid && !new ThuongRoomRules { preset = (ThuongRolePreset)255 }.IsValid,
            "Invalid durations/presets rejected");
        CheckTaskBalance(); CheckCapacity(); CheckRoutes();
        Directory.CreateDirectory("Logs/RoomUpgrades");
        File.WriteAllLines("Logs/RoomUpgrades/rules-tasks-routes.txt", new[] { "PASS: " + checks + " checks" }.Concat(passed));
        Debug.Log("THUONG_ROOM_UPGRADE_UNIT_PASSED: " + checks);
    }

    private static void CheckTaskBalance()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var previousPlayers = PlayerManager.Instance; var previousGame = GameRoleManager.Instance; var previousTasks = TaskManager.Instance;
        var previousWins = WinConditionManager.Instance;
        var assets = new List<TaskData>();
        try
        {
            T Add<T>() where T : Component
            {
                var root = new GameObject(typeof(T).Name); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                return root.AddComponent<T>();
            }
            var players = Add<PlayerManager>(); var game = Add<GameRoleManager>(); var tasks = Add<TaskManager>();
            PlayerManager.Instance = players; GameRoleManager.Instance = game; TaskManager.Instance = tasks; WinConditionManager.Instance = null;
            game.currentState = GameState.Day;
            for (int i = 0; i < 6; i++) { var task = ScriptableObject.CreateInstance<TaskData>(); assets.Add(task); tasks.allTasks.Add(task); }
            foreach (int count in new[] { 2, 4, 6, 12, 25 })
            {
                players.players = Enumerable.Range(0, count).Select(id => new PlayerData { playerID = id, isAlive = true }).ToList();
                tasks.ConfigureCooperative(count); tasks.ResetForNewMatch(); tasks.StartNewDay();
                Require(tasks.DailyTarget == Mathf.Max(4, count * 2), "Daily quota scales: " + count);
                Require(tasks.currentTasks.Sum(tasks.TargetFor) == tasks.DailyTarget, "Task quotas sum to the daily target: " + count);
                Require(tasks.currentTasks.Count * tasks.CapacityFor(tasks.currentTasks[0]) >= count, "Enough simultaneous slots for every player: " + count);
                foreach (var task in tasks.currentTasks)
                {
                    int target = tasks.TargetFor(task);
                    for (int id = 0; id < target; id++) Require(tasks.TryCompleteTask(id, task), "Distinct contribution accepted");
                    float progress = tasks.progress;
                    Require(!tasks.TryCompleteTask(0, task) && tasks.progress == progress, "Duplicate/full task cannot reward twice");
                }
                Require(Mathf.Approximately(tasks.progress, 40) && Mathf.Approximately(tasks.DailyProgress, 100), "Full day gives exactly 40% for " + count + " players");
                tasks.StartNewDay(); Require(!tasks.HasContributed(0, tasks.currentTasks[0]) && tasks.CompletedToday == 0, "New day clears personal contribution limits");
                tasks.ResetForNewMatch(); Require(tasks.progress == 0 && tasks.DailyTarget == 0, "Replay clears quotas and progress");
            }
            tasks.ConfigureCooperative(0); tasks.StartNewDay();
            Require(tasks.currentTasks.Count == 4 && tasks.TryCompleteTask(0, tasks.currentTasks[0]) && tasks.progress == 10,
                "Offline legacy four-task/ten-percent behavior preserved");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene); foreach (var asset in assets) Object.DestroyImmediate(asset);
            PlayerManager.Instance = previousPlayers; GameRoleManager.Instance = previousGame; TaskManager.Instance = previousTasks; WinConditionManager.Instance = previousWins;
        }
    }

    private static void CheckCapacity()
    {
        var entry = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath).entries.First();
        var authority = new ThuongMinigameAuthority();
        for (ulong owner = 0; owner < 5; owner++)
            Require(authority.TryBegin(owner, "shared", 0, 1, entry.kind, entry.rules, new[] { 2, -2, 0 }, 0, 150, out _, out _, 5), "Five people can share a station");
        Require(authority.Occupancy(1, "shared") == 5 && !authority.TryBegin(5, "shared", 0, 1, entry.kind, entry.rules,
            new[] { 2, -2, 0 }, 0, 150, out _, out _, 5), "Sixth player is rejected by station capacity");
        var state = authority.Get(0);
        authority.Cancel(0, state.attemptId, "cancel");
        Require(authority.Occupancy(1, "shared") == 4 && authority.TryBegin(5, "shared", 0, 1, entry.kind, entry.rules,
            new[] { 2, -2, 0 }, 0, 150, out _, out _, 5), "Cancelling releases exactly one slot");
        Require(!authority.TryBegin(1, "other", 1, 1, entry.kind, entry.rules, new[] { 2, -2, 0 }, 0, 150, out _, out _, 5), "One active attempt per owner");
    }

    private static void CheckRoutes()
    {
        var map = ScriptableObject.CreateInstance<ThuongMapDefinition>();
        try
        {
            map.boundsMin = Vector2.zero; map.boundsMax = new Vector2(10, 10);
            map.obstacles = new[] { new Rect(4, 0, 2, 7) };
            map.stations = new[] { new ThuongMapStation { maskSlot = 0, position = new Vector2(8, 1) } };
            var navigation = new ThuongMapNavigation(map);
            Require(navigation.TryPath(new Vector2(2, 1), 0, out var route) && route.Any(p => p.y > 7), "Guide routes around a blocking wall");
            for (int i = 1; i < route.Count; i++) Require(ThuongMapCollision.HasLine(map, route[i - 1], route[i]), "Every guide segment respects map collision");
        }
        finally { Object.DestroyImmediate(map); }
        var catalog = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
        foreach (var definition in catalog.Released)
        {
            var navigation = new ThuongMapNavigation(definition);
            for (int slot = 0; slot < definition.stations.Length; slot++)
                Require(navigation.TryPath(definition.spawns[0], slot, out _), "Guidance reaches " + definition.mapId + " station " + slot);
        }
    }
}
