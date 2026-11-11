using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThuongOnlineAuthorityChecks
{
    private static int checks;
    private static readonly List<string> passed = new();
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        checks++; passed.Add(description);
    }

    public static void VerifyAndBuild()
    {
        Run();
        ThuongMainFlowChecks.VerifyAndBuild();
    }

    [MenuItem("Tools/Thuong/Online/Check Role Assignment And Vote Authority")]
    public static void Run()
    {
        checks = 0; passed.Clear();
        foreach (int count in Enumerable.Range(2, 24))
        foreach (ThuongRolePreset preset in Enum.GetValues(typeof(ThuongRolePreset)))
        foreach (ThuongMatchMode mode in Enum.GetValues(typeof(ThuongMatchMode)))
        {
            if (mode == ThuongMatchMode.RealtimeProximity && preset != ThuongRolePreset.Basic) continue;
            var rules = new ThuongRoomRules { preset = preset, mode = mode };
            var defaults = ThuongOnlineRoleConfigAdapter.DefaultRolesForRoom(count, rules);
            var data = defaults.Select(entry => new RoleAmountData(entry.Key, entry.Value));
            Require(RolePoolBuilder.TryBuildConfigured(count, data, rules.AllowedRoles, out var pool, out _) && pool.Count == count,
                $"Default configuration: {count} players, {preset}, {mode}");
        }
        var basic = new ThuongRoomRules { preset = ThuongRolePreset.Basic }.AllowedRoles;
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.DogSpirit, 1), new RoleAmountData(RoleType.Hunter, 1), new RoleAmountData(RoleType.Villager, 1) }, basic, out _, out _),
            "Unsupported realtime role is rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.DogSpirit, 1), new RoleAmountData(RoleType.Villager, 3) }, null, out _, out _), "Excess roles rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.DogSpirit, 1), new RoleAmountData(RoleType.Villager, 1) }, null, out _, out _), "Missing roles rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData((RoleType)999, 1), new RoleAmountData(RoleType.Villager, 2) }, null, out _, out _), "Unknown role rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.DogSpirit, 1), new RoleAmountData(RoleType.DogSpirit, 1), new RoleAmountData(RoleType.Villager, 1) }, null, out _, out _), "Duplicate configuration entries rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.DogSpirit, -1), new RoleAmountData(RoleType.Villager, 4) }, null, out _, out _), "Negative quantity rejected");
        Require(!RolePoolBuilder.TryBuildConfigured(3, new[] { new RoleAmountData(RoleType.Villager, 3) }, null, out _, out _), "Pool without wolves rejected");

        var previousPlayers = PlayerManager.Instance; var previousRoles = RoleManager.Instance;
        var previousGame = GameRoleManager.Instance; var previousVotes = VoteManager.Instance;
        var previousWins = WinConditionManager.Instance; var previousDeath = DeathResolver.Instance;
        var previousMatch = NetworkGamePlayerSetup.GetActiveMatchClientIds();
        var previousLeft = NetworkGameManager.LeftPlayerClientIds.ToArray();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            PlayerManager.Instance = null; RoleManager.Instance = null; GameRoleManager.Instance = null;
            VoteManager.Instance = null; WinConditionManager.Instance = null; DeathResolver.Instance = null;
            NetworkGamePlayerSetup.Clear(); NetworkGameManager.LeftPlayerClientIds.Clear();
            T Add<T>() where T : Component
            {
                var root = new GameObject(typeof(T).Name); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                return root.AddComponent<T>();
            }
            var players = Add<PlayerManager>(); var roles = Add<RoleManager>(); var game = Add<GameRoleManager>(); var votes = Add<VoteManager>();
            PlayerManager.Instance = players; RoleManager.Instance = roles; GameRoleManager.Instance = game; VoteManager.Instance = votes;
            players.players = Enumerable.Range(0, 8).Select(index => new PlayerData { playerID = 10 + index * 7, playerName = "QA " + index }).ToList();
            var selected = new[] { new RoleAmountData(RoleType.DogSpirit, 2), new RoleAmountData(RoleType.Villager, 3),
                new RoleAmountData(RoleType.Seer, 1), new RoleAmountData(RoleType.VillageGuardian, 1), new RoleAmountData(RoleType.Hunter, 1) };
            Require(RolePoolBuilder.TryBuildConfigured(8, selected, RoleCatalog.AllConcreteRoles, out var pool, out _), "Exact selected pool accepted");
            roles.exactRolePoolOverride = pool;
            var wolfSlots = new HashSet<int>(); var assignments = new HashSet<string>();
            for (int seed = 0; seed < 64; seed++)
            {
                roles.AssignRole(seed);
                Require(selected.All(entry => players.players.Count(player => player.roleType == entry.Role) == entry.Amount) && roles.playerRoles.Count == 8,
                    "Selected quantities retained for seed " + seed);
                Require(players.players.All(player => roles.playerRoles[player.playerID].owner == player && roles.playerRoles[player.playerID].roleType == player.roleType),
                    "Player IDs and role owners agree for seed " + seed);
                foreach (var player in players.players.Where(player => player.roleType == RoleType.DogSpirit)) wolfSlots.Add(player.playerID);
                assignments.Add(string.Join(",", players.players.OrderBy(player => player.playerID).Select(player => player.roleType)));
            }
            Require(wolfSlots.Count == 8 && assignments.Count > 32, "Every roster position can receive a wolf; role distribution changes across seeds");
            roles.exactRolePoolOverride = null;

            players.CreateTestPlayer(3);
            game.currentState = GameState.Voting; game.currentPhase = GamePhase.Voting;
            votes.StartVote(); votes.AllowSelfVote = false;
            Require(!votes.TryVote(0, 0), "Production voter validation rejects self-vote");
            Require(votes.TryVote(0, 1) && !votes.TryVote(0, 2), "Offline single-vote rule remains available");
            votes.AllowVoteChanges = true;
            Require(votes.TryVote(0, 2) && votes.GetVotedTarget(0) == 2 && players.GetplayerByID(0).hasVoted, "Revote replaces choice while keeping hasVoted recorded");
            Require(votes.TryVoteNoElimination(0) && votes.VotedNoElimination(0) && votes.GetVotedTarget(0) == -1, "Player ballot can change to skip");
            Require(votes.TryVote(0, 1) && !votes.VotedNoElimination(0), "Skip ballot can change to player");
            players.GetplayerByID(0).isAlive = false;
            Require(!votes.TryVote(0, 2), "Dead voter cannot change a ballot");
            players.GetplayerByID(0).isAlive = true;
            players.GetplayerByID(0).effects.Add(new StatusEffectInstance(StatusEffectType.CannotVote, 1, 1, GamePhase.Voting));
            Require(!votes.TryVoteNoElimination(0), "CannotVote blocks revote and skip");
            players.GetplayerByID(0).effects.Clear();
            players.GetplayerByID(0).isDisconnected = true;
            Require(!votes.TryVote(0, 2), "Disconnected voter cannot change a ballot");
            players.GetplayerByID(0).isDisconnected = false;
            game.currentPhase = GamePhase.Night; game.currentState = GameState.Night;
            Require(!votes.TryVote(0, 2), "Wrong phase cannot change a ballot");
            game.currentState = GameState.Voting; game.currentPhase = GamePhase.Voting;
            votes.StartVote();
            players.GetplayerByID(0).votePower = 2;
            Require(votes.TryVote(0, 1) && votes.TryVoteNoElimination(0) && votes.TryVote(1, 2) && votes.TryVoteNoElimination(2), "Weighted vote setup through production methods");
            votes.ResolveVote();
            var result = votes.LastResolution;
            Require(result.NoEliminationWon && result.NoEliminationVotes == 3 && result.Tallies[2] == 1 && result.Tallies.Count == 1,
                "Resolution counts only the latest ballot, including weights and skip");
            Require(!votes.TryVote(1, 0), "Resolved ballot cannot change");
            votes.ResolveVote(); Require(ReferenceEquals(result, votes.LastResolution), "Resolution is idempotent");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerManager.Instance = previousPlayers; RoleManager.Instance = previousRoles; GameRoleManager.Instance = previousGame;
            VoteManager.Instance = previousVotes; WinConditionManager.Instance = previousWins; DeathResolver.Instance = previousDeath;
            NetworkGamePlayerSetup.SetActiveMatchClientIds(previousMatch);
            NetworkGameManager.LeftPlayerClientIds.Clear(); foreach (var id in previousLeft) NetworkGameManager.LeftPlayerClientIds.Add(id);
        }
        Directory.CreateDirectory("Logs/OnlineFixes20261110");
        File.WriteAllLines("Logs/OnlineFixes20261110/rules-results.txt", new[] { "PASS checks=" + checks }.Concat(passed));
        Debug.Log("THUONG_AUTHORITY_RULES_PASSED checks=" + checks);
    }
}
