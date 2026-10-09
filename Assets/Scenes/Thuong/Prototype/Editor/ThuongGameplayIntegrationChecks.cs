using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Thuong;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ThuongGameplayIntegrationChecks
{
    private static int checks;
    [MenuItem("Tools/Thuong/Check Integrated Gameplay")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        checks = 0;
        var scene = EditorSceneManager.OpenPreviewScene(ThuongPrototypeBuilder.ScenePath);
        var previous = new Dictionary<FieldInfo, object>();
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(component => component != null), "No missing scene scripts");
            foreach (var component in components)
            {
                var field = component.GetType().GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.FieldType != component.GetType()) continue;
                if (!previous.ContainsKey(field)) previous[field] = field.GetValue(null);
                field.SetValue(null, component);
            }
            var game = components.OfType<GameRoleManager>().Single();
            var players = components.OfType<PlayerManager>().Single();
            var roles = components.OfType<RoleManager>().Single();
            var night = components.OfType<NightManager>().Single();
            var death = components.OfType<DeathResolver>().Single();
            var votes = components.OfType<VoteManager>().Single();
            var tasks = components.OfType<TaskManager>().Single();
            var hud = components.OfType<GameHUD>().Single();
            Require(game.HasIntegratedLogic && !components.OfType<ThuongLogicMatchController>().Any(), "One automatic flow, no test controller");
            Require(game.statusEffects.deathSystem == death && game.abilities.phaseController == game && game.abilities.nightSystem == night,
                "Saved integration references");
            Require(players.prototypeLobbySize == 20 && components.OfType<TaskStation>().Count() == 6 && tasks.allTasks.Count == 6 &&
                tasks.tasksPerDay == 4 && components.OfType<DayTimer>().Single().dayDuration == 45, "Original lobby, stations and task timers preserved");
            Require(roles.roleDefinitions.Validate(out _) && roles.roleDefinitions.definitions.Count == 3, "Core catalog, no special-role replacement");
            players.CreateTestPlayer(players.prototypeLobbySize);
            game.BeginGame();
            // Preview scenes do not invoke Awake on dynamically added runtime managers.
            var events = game.GetComponent<EventManager>();
            if (events != null) events.SendMessage("Awake");
            Require(game.currentState == GameState.RoleReveal && roles.playerRoles.Count == 20 && game.abilities.MatchIsRunning,
                "Original random role reveal boots with integrated abilities");
            foreach (var player in players.players)
                Require(player.roleDefinition == roles.roleDefinitions.Find(player.roleType), "Random role maps to matching definition or legacy fallback");
            game.SkipRoleReveal();
            Require(game.currentState == GameState.Day && tasks.currentTasks.Count == 4, "Day/task loop retained");
            Require(EventManager.Instance != null && EventManager.Instance.HasSchedule, "Original match event schedule retained");
            int scheduledDay = EventManager.Instance.ScheduledDay;
            var scheduledEvent = EventManager.Instance.SelectedEvent;

            ResetActors(game, players, roles);
            hud.SendMessage("Start");
            var ui = hud.GetComponent<RoleAbilityUI>();
            var abilityCanvas = Field<Canvas>(ui, "abilityCanvas");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(abilityCanvas.gameObject, scene);
            Require(hud.GetComponent<ThuongMeetingRosterUI>() != null && hud.meetingPanel.GetComponentsInChildren<VoteButton>(true).Count(row => row.gameObject.activeSelf) == 20,
                "Vote scroll list covers the complete lobby");
            var prefab = components.OfType<ThuongFourRoleOfflineTest>().Single().playerPrefab;
            Require(prefab != null && prefab.GetComponent<PlayerMovement>() != null && prefab.GetComponent<Rigidbody2D>() != null &&
                prefab.GetComponent<BoxCollider2D>() != null && prefab.GetComponentInChildren<ThuongCharacterView>().appearance != null,
                "Replacement-art prefab retains identity, input and physics");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ThuongGameplayIntegrationInstaller.Root + "/Prefabs/TaskStation.prefab").GetComponent<TaskStation>() != null,
                "Task art template retains interaction logic");

            foreach (bool wolfFirst in new[] { false, true })
            {
                ResetActors(game, players, roles);
                game.EndDay();
                int first = wolfFirst ? 1 : 0, second = wolfFirst ? 0 : 1;
                Require(roles.UseNightAbility(first, 2, out _) && roles.UseNightAbility(second, 2, out _), "Both UI-compatible actions submit");
                Require(players.IsAlive(2) && !players.GetplayerByID(2).HasEffect(StatusEffectType.Protected), "Actions defer until night resolution");
                game.FinishNight();
                Require(players.IsAlive(2) && game.currentState == GameState.Discussion, "Protect precedes attack in either order");
            }
            ResetActors(game, players, roles);
            game.EndDay();
            Require(roles.UseNightAbility(1, 2, out _) && roles.UseNightAbility(3, 4, out _), "Two wolves can choose the pack target");
            game.FinishNight();
            Require(players.IsAlive(2) && !players.IsAlive(4), "Only the last shared wolf target is attacked");
            ResetActors(game, players, roles);
            game.EndDay(); roles.UseNightAbility(1, 2, out _); night.SetMonsterTarget(4, RoleType.Ogre); game.FinishNight();
            Require(players.IsAlive(2) && !players.IsAlive(4), "Legacy attack replaces configured pack target");
            ResetActors(game, players, roles);
            game.EndDay(); night.SetMonsterTarget(4, RoleType.Ogre); roles.UseNightAbility(1, 2, out _); game.FinishNight();
            Require(!players.IsAlive(2) && players.IsAlive(4), "Configured attack replaces legacy pack target");

            ResetActors(game, players, roles);
            game.EndDay(); roles.UseNightAbility(0, 2, out _); game.FinishNight(); game.StartVoting(); game.FinishVoting();
            Require(game.currentState == GameState.Day && game.currentDay == 2, "Vote returns to day two rather than test-scene Night");
            game.EndDay();
            Require(!roles.UseNightAbility(0, 2, out string repeated) && repeated.Contains("liên tiếp"), "Guardian consecutive-night rule retained");
            Require(roles.UseNightAbility(0, 4, out _) && !roles.UseNightAbility(0, 0, out _), "Guardian accepts another target, no self-target shortcut");
            game.FinishNight();
            Require(!players.GetplayerByID(4).HasEffect(StatusEffectType.Protected), "Unused protection expires at Discussion in original flow");

            ResetActors(game, players, roles);
            game.EndDay(); ui.SendMessage("Update"); Invoke(ui, "OpenTargets"); Invoke(ui, "SelectTarget", 2);
            Require(night.PendingActionCount == 1 && Field<TMP_Text>(ui, "roleLabel").text == "BẢO VỆ", "Existing skill UI uses role asset and new submission path");
            game.statusEffects.ApplyEffect(players.GetplayerByID(1), StatusEffectType.Silenced, 1, 1, GamePhase.Discussion);
            ui.localPlayerID = 1; ui.SendMessage("Update");
            Invoke(ui, "SelectTarget", 4);
            Require(night.PendingActionCount == 1 && !roles.UseNightAbility(1, 4, out _), "Silenced blocks both UI and configured API");
            ui.localPlayerID = 0; game.FinishNight(); game.StartVoting();
            Require(votes.TryVote(0, 1), "Voting uses current living player state");
            game.statusEffects.ApplyEffect(players.GetplayerByID(0), StatusEffectType.ExtraVote, 2, 1, GamePhase.DayResolution);
            game.FinishVoting();
            Require(votes.LastResolution.Tallies[1] == 3 && players.GetplayerByID(0).GetVoteWeight() == 1,
                "Vote resolves before temporary weight expires");
            Require(tasks.progress == 0 && tasks.DailyProgress == 0 && game.currentState == GameState.Day, "Day tasks still refresh after vote");
            Require(EventManager.Instance.ScheduledDay == scheduledDay && EventManager.Instance.SelectedEvent == scheduledEvent,
                "Integration does not reroll the event each day");

            ResetActors(game, players, roles);
            game.EndDay(); game.FinishNight(); game.StartVoting();
            votes.TryVote(0, 1);
            game.statusEffects.ApplyEffect(players.GetplayerByID(0), StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution);
            game.FinishVoting();
            Require(players.IsAlive(1) && votes.LastResolution.Tallies.Count == 0 && players.GetplayerByID(0).CanVote, "CannotVote excludes choice then expires");

            ResetActors(game, players, roles);
            var hunter = players.GetplayerByID(5); hunter.roleType = RoleType.Hunter; hunter.roleDefinition = null;
            roles.playerRoles[5] = new HunterRole(hunter);
            death.TryKillPlayer(5, DeathCause.Ability);
            Require(hunter.hasHunterTrap && !hunter.isAlive, "Legacy Hunter death callback survives integration");
            game.EndDay(); game.FinishNight(); ui.localPlayerID = 5; ui.SendMessage("Update");
            Invoke(ui, "OpenHunterTrapTargets"); Invoke(ui, "SelectTarget", 4);
            game.StartVoting(); game.FinishVoting();
            Require(!players.IsAlive(4) && hunter.hunterTargetID == -1, "Dead Hunter's original UI and post-vote trap remain intact");
            ResetActors(game, players, roles);
            var fox = players.GetplayerByID(6); fox.roleType = RoleType.FoxSpirit; fox.roleDefinition = null;
            roles.playerRoles[6] = new FoxSpiritRole(fox);
            game.EndDay();
            Require(roles.UseNightAbility(6, 2, out _) && roles.UseNightAbility(6, 4, out _) && !roles.UseNightAbility(6, 7, out _),
                "Legacy Fox retains two charms rather than generic single-use behavior");

            ResetActors(game, players, roles);
            Require(tasks.TryCompleteTask(0, tasks.currentTasks[0]) && tasks.progress == 10 && tasks.DailyProgress == 25,
                "Existing task completion remains functional");
            tasks.progress = 90;
            Require(tasks.TryCompleteTask(0, tasks.currentTasks[1]) && game.Winner == "Villagers" && !game.abilities.MatchIsRunning && night.PendingActionCount == 0,
                "Original 100 percent task win locks integrated actions");
            Debug.Log($"THUONG_GAMEPLAY_INTEGRATION_EDIT_PASSED: {checks} assertions.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var pair in previous) pair.Key.SetValue(null, pair.Value);
        }
    }
    public static void InstallAndCheck()
    {
        ThuongGameplayIntegrationInstaller.Install();
        Run();
        VoteClickRegression.Run();
        if (!System.IO.File.ReadAllText("Logs/vote-click-result.txt").StartsWith("PASS")) throw new Exception("Legacy vote click regression failed.");
        ThuongPrototypeChecks.Run();
    }
    private static void ResetActors(GameRoleManager game, PlayerManager players, RoleManager roles)
    {
        roles.playerRoles.Clear();
        foreach (var player in players.players)
        {
            player.ResetForNewMatch(); player.roleType = RoleType.Villager; player.faction = FactionType.Villager;
            player.roleDefinition = roles.roleDefinitions.Find(RoleType.Villager);
            roles.playerRoles[player.playerID] = new VillagerRole(player);
        }
        foreach (var pair in new[] { (0, RoleType.VillageGuardian), (1, RoleType.DogSpirit), (3, RoleType.DogSpirit) })
        {
            var player = players.GetplayerByID(pair.Item1); player.roleType = pair.Item2;
            player.roleDefinition = roles.roleDefinitions.Find(pair.Item2);
            player.faction = player.roleDefinition.faction; roles.playerRoles[player.playerID] = RoleCatalog.Create(pair.Item2, player);
        }
        game.abilities.SetMatchRunning(false); game.abilities.SetMatchRunning(true);
        NightManager.Instance.ResetForNewMatch(); VoteManager.Instance.StartVote();
        game.currentDay = 1; game.StartDay();
    }
    private static T Field<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Invoke(object owner, string method, params object[] values) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, values);
    private static void Require(bool valid, string label) { if (!valid) throw new Exception("Gameplay integration: " + label); checks++; }
}
