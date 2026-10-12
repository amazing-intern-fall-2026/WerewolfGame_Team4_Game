using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ThuongPhaseFiveChecks
{
    private static int checks;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/Thuong/Check Phase 5 (Targets)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        checks = 0;
        var previous = new Dictionary<FieldInfo, object>();
        var scene = EditorSceneManager.NewPreviewScene();
        var definitions = new List<ThuongRoleDefinition>();
        try
        {
            var players = Add<PlayerManager>(scene, previous);
            var game = Add<GameRoleManager>(scene, previous);
            var night = Add<NightManager>(scene, previous);
            var abilities = Add<AbilitySystem>(scene, previous);
            var status = Add<StatusEffectSystem>(scene, previous);
            var death = Add<DeathResolver>(scene, previous);
            var roles = Add<RoleManager>(scene, previous);
            game.abilities = abilities; game.statusEffects = status;
            status.deathSystem = death;
            abilities.phaseController = game; abilities.nightSystem = night;
            players.CreateTestPlayer(7);
            var doctor = CopyRole(ThuongLogicPrototypeBuilder.Root + "/Roles/Doctor.asset", definitions);
            var wolf = CopyRole(ThuongLogicPrototypeBuilder.Root + "/Roles/Wolf.asset", definitions);
            Require(doctor.allowSelfTarget && doctor.targetFactionRule == TargetFactionRule.Any,
                "Doctor asset allows self/any faction");
            Require(!wolf.allowSelfTarget && wolf.targetFactionRule == TargetFactionRule.OtherFaction,
                "Wolf asset forbids self/same faction");
            var gameplayWolf = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinition>(
                ThuongGameplayIntegrationInstaller.Root + "/Roles/DogSpirit.asset");
            Require(gameplayWolf != null && !gameplayWolf.allowSelfTarget && !gameplayWolf.canTargetDead &&
                gameplayWolf.targetFactionRule == TargetFactionRule.OtherFaction, "Gameplay wolf uses Phase 5 rules");
            ConfigurePlayers(players, roles, doctor, wolf);
            ResetNight(game);
            var actor = players.GetplayerByID(0);
            var target = players.GetplayerByID(2);

            foreach (TargetFactionRule rule in Enum.GetValues(typeof(TargetFactionRule)))
            {
                doctor.targetFactionRule = rule;
                foreach (FactionType faction in new[] { FactionType.Villager, FactionType.Monster, FactionType.Neutral })
                {
                    target.faction = faction;
                    bool expected = rule switch
                    {
                        TargetFactionRule.Any => true,
                        TargetFactionRule.SameFaction => faction == FactionType.Villager,
                        TargetFactionRule.OtherFaction => faction != FactionType.Villager,
                        TargetFactionRule.VillageOnly => faction == FactionType.Villager,
                        TargetFactionRule.WolvesOnly => faction == FactionType.Monster,
                        TargetFactionRule.NeutralOnly => faction == FactionType.Neutral,
                        _ => false
                    };
                    var result = TargetValidator.Validate(actor, target, doctor);
                    Require(result.IsValid == expected && (expected || result.FailureReason == ActionFailureReason.FactionForbidden),
                        $"Faction matrix {rule}/{faction}");
                }
            }
            doctor.targetFactionRule = TargetFactionRule.Any; target.faction = FactionType.Villager;
            Require(TargetValidator.Validate(actor, actor, doctor).IsValid, "Doctor self-target accepted");
            Require(TargetValidator.Validate(players.GetplayerByID(1), players.GetplayerByID(1), wolf).FailureReason ==
                ActionFailureReason.SelfTargetForbidden, "Wolf self-target rejected");
            Require(TargetValidator.Validate(actor, null, doctor).FailureReason == ActionFailureReason.TargetMissing,
                "Missing target has a reason");
            Require(TargetValidator.Validate(null, target, doctor).FailureReason == ActionFailureReason.ActorMissing,
                "Missing actor has a reason");
            Require(TargetValidator.Validate(actor, target, null).FailureReason == ActionFailureReason.RoleMissing,
                "Missing role has a reason");
            var dead = players.GetplayerByID(5);
            Require(TargetValidator.Validate(actor, dead, doctor).FailureReason == ActionFailureReason.TargetDead,
                "Dead target rejected by default");
            doctor.canTargetDead = true;
            Require(TargetValidator.Validate(actor, dead, doctor).IsValid, "CanTargetDead permits dead targets explicitly");
            doctor.canTargetDead = false;

            var wolfActor = players.GetplayerByID(1);
            ExpectFailure(abilities.TryUseAbility(wolfActor, wolfActor), ActionFailureReason.SelfTargetForbidden);
            ExpectFailure(abilities.TryUseAbility(wolfActor, players.GetplayerByID(3)), ActionFailureReason.FactionForbidden);
            ExpectFailure(abilities.TryUseAbility(wolfActor, dead), ActionFailureReason.TargetDead);
            ExpectFailure(abilities.TryUseAbility(wolfActor, null), ActionFailureReason.TargetMissing);
            ExpectFailure(abilities.TryUseAbility(wolfActor, new PlayerData { playerID = target.playerID }),
                ActionFailureReason.TargetMissing);
            ExpectFailure(abilities.TryUseAbility(new PlayerData { playerID = wolfActor.playerID, roleDefinition = wolf }, target),
                ActionFailureReason.ActorMissing);
            Require(night.PendingActionCount == 0 && !wolfActor.hasUseNightAction && abilities.Validate(wolfActor, target).IsValid,
                "Rejected requests neither queue actions nor spend the actor's use");

            var ui = Add<RoleAbilityUI>(scene, previous);
            ui.Initialize(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThuongPrototypeBuilder.Root + "/Art/PrototypeFont.asset"), 1);
            SceneManager.MoveGameObjectToScene(Field<Canvas>(ui, "abilityCanvas").gameObject, scene);
            CheckUIAndAPI(game, ui, doctor, wolf);

            ResetNight(game);
            target.isAlive = true; target.faction = FactionType.Villager;
            Require(abilities.TryUseAbility(wolfActor, target).IsValid, "Living enemy is queued");
            target.faction = FactionType.Monster;
            AbilityResolution? resolved = null;
            abilities.AbilityResolved += result => resolved = result;
            night.ResolveNight();
            Require(target.isAlive && resolved.HasValue && !resolved.Value.Succeeded &&
                resolved.Value.Message.Contains("phe"), "Queued target changing faction is revalidated at resolution");
            Debug.Log($"THUONG_PHASE5_EDIT_PASSED: {checks} assertions.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            foreach (var pair in previous) pair.Key.SetValue(null, pair.Value);
        }
    }

    public static void InstallAndCheck()
    {
        ThuongGameplayIntegrationInstaller.Install();
        Run();
        ThuongGameplayIntegrationChecks.Run();
        VoteClickRegression.Run();
        if (!System.IO.File.ReadAllText("Logs/vote-click-result.txt").StartsWith("PASS"))
            throw new Exception("Vote click regression failed.");
        ThuongPrototypeChecks.Run();
    }

    public static void CheckRuntime(GameRoleManager game)
    {
        checks = 0;
        var doctor = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinition>(ThuongLogicPrototypeBuilder.Root + "/Roles/Doctor.asset");
        var wolf = RoleManager.Instance.roleDefinitions.Find(RoleType.DogSpirit);
        ConfigurePlayers(PlayerManager.Instance, RoleManager.Instance, doctor, wolf);
        game.abilities.SetMatchRunning(false); game.abilities.SetMatchRunning(true);
        game.StartDay(); game.EndDay();
        var ui = UnityEngine.Object.FindAnyObjectByType<GameHUD>().GetComponent<RoleAbilityUI>();
        CheckUIAndAPI(game, ui, doctor, wolf);
        // Leave the runtime preview on the gameplay wolf's filtered target panel.
        ui.localPlayerID = 1; ui.SendMessage("Update");
        Invoke(ui, "ShowWindow", true); Invoke(ui, "OpenTargets");
        Debug.Log($"THUONG_PHASE5_PLAY_PASSED: {checks} assertions on the integrated gameplay scene.");
    }

    private static void CheckUIAndAPI(GameRoleManager game, RoleAbilityUI ui,
        ThuongRoleDefinition doctor, ThuongRoleDefinition wolf)
    {
        var players = PlayerManager.Instance;
        var abilities = game.abilities;
        var wolfActor = players.GetplayerByID(1);
        ui.localPlayerID = 1; ui.SendMessage("Update");
        Invoke(ui, "ShowWindow", true); Invoke(ui, "OpenTargets"); ui.SendMessage("Update");
        Require(!Visible(ui, 1) && !Visible(ui, 3) && !Visible(ui, 5), "UI hides self, same-faction and dead targets");
        Require(Visible(ui, 0) && Visible(ui, 2) && Visible(ui, 4), "OtherFaction shows village and neutral targets");
        Invoke(ui, "SelectTarget", 3); ui.SendMessage("Update");
        Require(Field<TMP_Text>(ui, "targetFeedbackLabel").text.Contains("phe") && NightManager.Instance.PendingActionCount == 0,
            "Calling a hidden target's UI handler is rejected with the same faction reason");
        Require(!RoleManager.Instance.UseNightAbility(1, 3, out string feedback) && feedback.Contains("phe"),
            "Role API rejects same-faction target without going through UI");
        players.SetAliveState(2, false); ui.SendMessage("Update");
        Require(!Visible(ui, 2), "Open panel removes a target that dies");
        ExpectFailure(abilities.TryUseAbility(wolfActor, players.GetplayerByID(2)), ActionFailureReason.TargetDead);
        players.SetAliveState(2, true); ui.SendMessage("Update");
        Require(Visible(ui, 2), "Open panel restores a target that becomes alive");
        players.GetplayerByID(4).faction = FactionType.Monster; ui.SendMessage("Update");
        Require(!Visible(ui, 4), "Open panel removes a target changing to actor's faction");
        players.GetplayerByID(4).faction = FactionType.Neutral;
        Require(NightManager.Instance.PendingActionCount == 0 && !wolfActor.hasUseNightAction,
            "UI/API failures leave the wolf's action available");
        ui.localPlayerID = 0; ui.SendMessage("Update"); Invoke(ui, "OpenTargets");
        Require(Visible(ui, 0) && Visible(ui, 1) && Visible(ui, 4) && !Visible(ui, 5),
            "Doctor UI allows self and any living faction");
        Invoke(ui, "SelectTarget", 0);
        Require(NightManager.Instance.PendingActionCount == 1 && players.GetplayerByID(0).hasUseNightAction,
            "Doctor self-protection submits through the UI");
    }

    private static void ConfigurePlayers(PlayerManager players, RoleManager roles,
        ThuongRoleDefinition doctor, ThuongRoleDefinition wolf)
    {
        roles.playerRoles.Clear();
        foreach (var player in players.players)
        {
            player.ResetForNewMatch(); player.roleType = RoleType.Villager; player.faction = FactionType.Villager;
            player.roleDefinition = null; roles.playerRoles[player.playerID] = new VillagerRole(player);
        }
        players.GetplayerByID(0).roleDefinition = doctor;
        foreach (int id in new[] { 1, 3 })
        {
            var player = players.GetplayerByID(id);
            player.roleType = RoleType.DogSpirit; player.faction = FactionType.Monster; player.roleDefinition = wolf;
            roles.playerRoles[id] = new DogSpirit(player);
        }
        players.GetplayerByID(4).faction = FactionType.Neutral;
        players.SetAliveState(5, false);
    }

    private static void ResetNight(GameRoleManager game)
    {
        game.abilities.SetMatchRunning(false); game.abilities.SetMatchRunning(true);
        NightManager.Instance.ResetForNewMatch(); NightManager.Instance.StartNight();
        game.currentState = GameState.Night; game.currentPhase = GamePhase.Night;
    }

    private static ThuongRoleDefinition CopyRole(string path, List<ThuongRoleDefinition> definitions)
    {
        var source = AssetDatabase.LoadAssetAtPath<ThuongRoleDefinition>(path);
        if (source == null) throw new Exception("Missing role asset: " + path);
        var copy = UnityEngine.Object.Instantiate(source); definitions.Add(copy); return copy;
    }
    private static bool Visible(RoleAbilityUI ui, int id) =>
        Field<Dictionary<int, Button>>(ui, "targetButtons").TryGetValue(id, out var button) && button.gameObject.activeSelf;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    private static void Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    private static T Add<T>(Scene scene, Dictionary<FieldInfo, object> previous) where T : Component
    {
        var field = typeof(T).GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        if (field != null && !previous.ContainsKey(field)) previous[field] = field.GetValue(null);
        var root = new GameObject(typeof(T).Name); SceneManager.MoveGameObjectToScene(root, scene);
        var component = root.AddComponent<T>();
        if (field != null) field.SetValue(null, component);
        return component;
    }
    private static void ExpectFailure(ActionValidationResult result, ActionFailureReason reason) =>
        Require(!result.IsValid && result.FailureReason == reason && !string.IsNullOrWhiteSpace(result.Message),
            "API rejection reason: " + reason);
    private static void Require(bool valid, string label)
    {
        checks++;
        if (!valid) throw new Exception("Phase 5 check failed: " + label);
    }
}
