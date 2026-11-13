using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run explicitly in a preview scene: opening the project must not reset live
// singletons, save a test role pool, or create persistent gameplay objects.
public static class ThuongOnlineRoleConfigTestExecution
{
    public static int PassCount { get; private set; }
    public static int FailCount { get; private set; }
    public static List<string> Results { get; } = new();

    [MenuItem("Tools/Loi/Run Thuong Online Role Config Tests")]
    public static void Run()
    {
        if (Application.isPlaying || ThuongOnlineSession.Active != null ||
            (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening))
            throw new InvalidOperationException("Run role configuration checks outside an active game.");

        PassCount = FailCount = 0;
        Results.Clear();
        var previousPlayers = PlayerManager.Instance;
        var previousRoles = RoleManager.Instance;
        var previousGame = GameRoleManager.Instance;
        var previousWins = WinConditionManager.Instance;
        var previousDeath = DeathResolver.Instance;
        var previousMatch = NetworkGamePlayerSetup.GetActiveMatchClientIds();
        var previousLeft = NetworkGameManager.LeftPlayerClientIds.ToArray();
        var savedSetup = NetworkGameRoleSetup.GetRoles();
        var previousAdapter = ThuongOnlineRoleConfigAdapter.Instance;
        var savedConfig = previousAdapter?.GetCurrentConfig();
        bool wasCustomized = previousAdapter != null && previousAdapter.IsCustomized;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            PlayerManager.Instance = null; RoleManager.Instance = null;
            GameRoleManager.Instance = null; WinConditionManager.Instance = null; DeathResolver.Instance = null;
            NetworkGamePlayerSetup.Clear(); NetworkGameManager.LeftPlayerClientIds.Clear(); NetworkGameRoleSetup.Clear();
            T Add<T>() where T : Component
            {
                var root = new GameObject(typeof(T).Name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                return root.AddComponent<T>();
            }
            var players = Add<PlayerManager>();
            var roles = Add<RoleManager>();
            PlayerManager.Instance = players; RoleManager.Instance = roles;
            players.players = Enumerable.Range(0, 8).Select(index => new PlayerData
                { playerID = 10 + index * 7, playerName = "Role QA " + index, isAlive = true }).ToList();
            var adapter = previousAdapter != null ? previousAdapter : Add<ThuongOnlineRoleConfigAdapter>();
            var selected = new Dictionary<RoleType, int>
            {
                [RoleType.DogSpirit] = 2, [RoleType.Seer] = 1, [RoleType.VillageGuardian] = 1,
                [RoleType.Hunter] = 1, [RoleType.Villager] = 3, [RoleType.Shaman] = 0
            };
            adapter.HostSaveConfig(selected);
            Require(ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(42) &&
                ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig(), "Exact host-selected quantities are assigned");
            Require(players.players.All(player => player.roleType != RoleType.Shaman && player.roleType != RoleType.Cursed),
                "Unselected Shaman and Cursed roles are absent");

            var changed = new Dictionary<RoleType, int>(selected) { [RoleType.DogSpirit] = 1, [RoleType.Villager] = 4 };
            adapter.HostSaveConfig(changed);
            Require(ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(84) &&
                players.players.Count(player => player.roleType == RoleType.DogSpirit) == 1 &&
                players.players.Count(player => player.roleType == RoleType.Villager) == 4 &&
                ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig(), "Next round uses the changed host configuration");

            adapter.HostSaveConfig(selected);
            var assignments = new HashSet<string>();
            bool exactEveryRound = true;
            foreach (int seed in new[] { 1001, 2002, 3003 })
            {
                exactEveryRound &= ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(seed) &&
                    ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig();
                assignments.Add(string.Join(",", players.players.OrderBy(player => player.playerID).Select(player => player.roleType)));
            }
            Require(exactEveryRound && assignments.Count == 3, "Replay preserves quantities and randomizes recipients");
            Require(players.players.All(player => roles.playerRoles.TryGetValue(player.playerID, out var role) &&
                role.owner == player && role.roleType == player.roleType && role.faction == player.faction),
                "Role objects, player IDs, owners and factions agree");

            var bridge = NetworkRoleActionBridge.Instance != null ? NetworkRoleActionBridge.Instance : Add<NetworkRoleActionBridge>();
            string Before() => string.Join(",", players.players.Select(player => player.roleType));
            string before = Before();
            bridge.EnsureDefaultRolesAssigned();
            Require(Before() == before && ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig(),
                "Legacy default assignment cannot overwrite the saved host configuration");

            bool zeroSyncLog = false;
            Application.LogCallback syncLog = (message, stack, type) =>
            { if (message.Contains("Đã đồng bộ role mới cho 0 NetworkRoleSync")) zeroSyncLog = true; };
            Application.logMessageReceived += syncLog;
            try { NetworkRoleSync.SyncAllRoleSyncs(); }
            finally { Application.logMessageReceived -= syncLog; }
            Require(!zeroSyncLog, "An empty legacy role-sync set is not reported as a successful synchronization");

            var originalObjects = new Dictionary<int, BaseRole>(roles.playerRoles);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bridge.EnsureDefaultRolesAssigned();
                foreach (var player in players.players) player.NotifyChanged();
            }
            Require(Before() == before && originalObjects.All(entry => ReferenceEquals(entry.Value, roles.playerRoles[entry.Key])),
                "Repeated default-assignment attempts and notifications do not recreate roles");

            players.players[0].roleType = RoleType.Shaman;
            players.players[3].roleType = RoleType.Cursed;
            bool mismatchDetected = !ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig();
            Require(mismatchDetected && ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(42) &&
                ThuongOnlineRoleConfigAdapter.DoesCurrentPlayersMatchHostConfig() &&
                players.players.All(player => player.roleType != RoleType.Shaman && player.roleType != RoleType.Cursed),
                "Configuration audit detects mismatches and explicit setup restores the selected pool");
        }
        catch (Exception error)
        {
            FailCount++;
            Results.Add("FAIL " + error);
            throw;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerManager.Instance = previousPlayers; RoleManager.Instance = previousRoles;
            GameRoleManager.Instance = previousGame; WinConditionManager.Instance = previousWins; DeathResolver.Instance = previousDeath;
            NetworkGamePlayerSetup.SetActiveMatchClientIds(previousMatch);
            NetworkGameManager.LeftPlayerClientIds.Clear();
            foreach (var id in previousLeft) NetworkGameManager.LeftPlayerClientIds.Add(id);
            if (previousAdapter != null)
            {
                if (wasCustomized) previousAdapter.HostSaveConfig(savedConfig);
                else previousAdapter.HostRestoreDefaultConfig();
            }
            NetworkGameRoleSetup.Clear();
            if (savedSetup.Count > 0) NetworkGameRoleSetup.Save(savedSetup);
            Directory.CreateDirectory("Logs/MergeMain20261111");
            File.WriteAllLines("Logs/MergeMain20261111/role-config-results.txt",
                new[] { $"passed={PassCount} failed={FailCount}" }.Concat(Results));
        }
        Debug.Log($"THUONG_ROLE_CONFIG_CHECKS_PASSED checks={PassCount}");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        PassCount++; Results.Add("PASS " + description);
    }
}
