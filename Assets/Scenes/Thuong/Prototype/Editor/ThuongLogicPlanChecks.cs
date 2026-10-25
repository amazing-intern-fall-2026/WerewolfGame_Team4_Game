using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ThuongLogicPlanChecks
{
    private static int checks;
    private static readonly List<string> passed = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Thuong/Check Logic Plan Phases 6-13")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        checks = 0; passed.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var previous = new Dictionary<FieldInfo, object>();
        var assets = new List<UnityEngine.Object>();
        try
        {
            var players = Add<PlayerManager>(scene, previous);
            var game = Add<GameRoleManager>(scene, previous); game.manualLogicPrototype = true;
            var roles = Add<RoleManager>(scene, previous);
            var deaths = Add<DeathResolver>(scene, previous);
            var effects = Add<StatusEffectSystem>(scene, previous); effects.deathSystem = deaths;
            var night = Add<NightManager>(scene, previous);
            var ability = Add<AbilitySystem>(scene, previous); ability.phaseController = game; ability.nightSystem = night;
            var votes = Add<VoteManager>(scene, previous);
            var wins = Add<WinConditionManager>(scene, previous);
            Add<LoverManager>(scene, previous);
            var timer = Add<DayTimer>(scene, previous);
            var tasks = Add<TaskManager>(scene, previous);
            var match = Add<ThuongLogicMatchController>(scene, previous);
            match.players = players; match.phaseController = game; match.roles = roles;
            match.deathSystem = deaths; match.statusSystem = effects; match.nightSystem = night;
            match.abilitySystem = ability; match.voteSystem = votes; match.winSystem = wins;
            var doctor = Definition(RoleType.VillageGuardian, FactionType.Villager, AbilityType.Protect, assets);
            doctor.allowSelfTarget = true;
            var wolf = Definition(RoleType.DogSpirit, FactionType.Monster, AbilityType.Kill, assets);
            wolf.targetFactionRule = TargetFactionRule.OtherFaction;
            var village = Definition(RoleType.Villager, FactionType.Villager, AbilityType.None, assets);
            PlayerData P(int id) => players.GetplayerByID(id);
            void Reset(int count = 8)
            {
                game.manualLogicPrototype = true;
                players.CreateTestPlayer(count);
                foreach (var player in players.players)
                    player.roleDefinition = player.playerID == 0 ? doctor : player.playerID == 1 ? wolf : village;
                match.StartMatch();
            }
            void Voting() { match.AdvanceMatch(); match.AdvanceMatch(); match.AdvanceMatch(); }
            void Advance(int count) { for (int i = 0; i < count; i++) match.AdvanceMatch(); }

            Reset();
            Expect(ability.TryUseAbility(P(1), P(1)), ActionFailureReason.SelfTargetForbidden);
            var forged = new PlayerData { playerID = 1, roleDefinition = wolf };
            Expect(ability.TryUseAbility(forged, P(2)), ActionFailureReason.ActorMissing);
            effects.ApplyEffect(P(0), StatusEffectType.Silenced, 1, 1, GamePhase.Discussion);
            Expect(ability.TryUseAbility(P(0), P(2)), ActionFailureReason.Silenced);
            Require(!P(0).hasUseNightAction && night.PendingActionCount == 0, "Phase 6 rejected action spends no use");
            effects.RemoveEffect(P(0), StatusEffectType.Silenced);
            Require(ability.TryUseAbility(P(0), P(0)).IsValid, "Phase 5 doctor can protect self");
            Expect(ability.TryUseAbility(P(0), P(2)), ActionFailureReason.AlreadyUsed);
            match.AdvanceMatch();
            Expect(ability.TryUseAbility(P(1), P(2)), ActionFailureReason.WrongPhase);
            Require(night.PendingActionCount == 0, "Phase 6 resolution rejects new submissions");
            Reset();
            P(0).roleDefinition = null;
            roles.playerRoles[0] = new CursedRole(P(0));
            game.SetPhase(GamePhase.ResolveNight);
            Require(!roles.UseNightAbility(0, 2, out _), "Phase 6 legacy API rejects NightResolution");
            Reset(); P(0).roleDefinition = null; roles.playerRoles[0] = new CursedRole(P(0));
            effects.ApplyEffect(P(0), StatusEffectType.Silenced, 1, 1, GamePhase.Discussion);
            Require(!roles.UseNightAbility(0, 2, out _), "Phase 9 legacy API rejects Silenced");
            effects.RemoveEffect(P(0), StatusEffectType.Silenced);
            Require(roles.UseNightAbility(0, 2, out _) && P(2).isCursed, "Phase 10 existing Cursed role creates timed effect");
            Require(!roles.UseNightAbility(0, 3, out _), "Phase 6 legacy API refuses repeated use");
            match.AdvanceMatch(); match.AdvanceMatch();
            Require(P(2).isAlive && P(2).effects.Single().RemainingTicks == 1, "Phase 10 legacy curse survives first night");
            Advance(5);
            Require(!P(2).isAlive && P(2).lastDeathCause == DeathCause.Curse && P(2).lastDeathSourceID == 0,
                "Phase 10 legacy curse kills at second discussion with source");
            Reset(); P(0).roleDefinition = null;
            var shaman = new ShamanRole(P(0)); roles.playerRoles[0] = shaman;
            Require(shaman.SelectCharm(false) && roles.UseNightAbility(0, 1, out _) && P(1).HasEffect(StatusEffectType.Silenced),
                "Phase 9 Shaman bad charm uses explicit ability silence");
            Require(!roles.UseNightAbility(1, 2, out _), "Phase 9 silenced wolf cannot submit");
            match.AdvanceMatch(); match.AdvanceMatch();
            Require(!P(1).HasEffect(StatusEffectType.Silenced), "Phase 9 silence expires at discussion");

            Reset(); Voting();
            P(0).votePower = 2;
            effects.ApplyEffect(P(0), StatusEffectType.ExtraVote, 2, 1, GamePhase.DayResolution, P(1));
            Require(votes.TryVote(0, 4), "Phase 8 weighted vote accepted");
            Require(!votes.TryVote(0, 5), "Phase 7 repeat vote rejected");
            Require(votes.TryVote(2, 3), "Phase 7 voter can submit before becoming blocked");
            effects.ApplyEffect(P(2), StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution, P(1));
            Expect(votes.ValidateVote(2, 4), ActionFailureReason.CannotVote);
            Require(!votes.TryVote(2, 4), "Phase 7 CannotVote rejects submission");
            match.AdvanceMatch();
            Require(votes.LastResolution.Tallies.Count == 1 && votes.LastResolution.Tallies[4] == 4,
                "Phases 7-8 tally ignores blocked earlier ballot and adds base plus temporary weight");
            Require(P(0).votePower == 2 && P(0).GetVoteWeight() == 2 && P(2).CanVote,
                "Phases 7-8 effects expire after tally without changing base power");
            Require(votes.LastResolution.BuildSummary().Contains("4 phiếu"), "Phase 8 UI result contains final weight");
            var resolved = votes.LastResolution; votes.ResolveVote();
            Require(votes.LastResolution == resolved, "Phase 7 resolution is idempotent");
            Reset(); Voting(); votes.TryVote(0, 3); votes.TryVote(2, 4); match.AdvanceMatch();
            Require(votes.LastResolution.IsTie && P(3).isAlive && P(4).isAlive, "Phase 7 tied vote kills nobody");

            foreach (var kind in new[] { AbilityType.ApplyCannotVote, AbilityType.GrantExtraVote, AbilityType.Silence, AbilityType.Curse })
            {
                Reset();
                var definition = Definition(RoleType.Villager, FactionType.Villager, kind, assets);
                definition.effectDurationTicks = 2; definition.effectTicksOnPhase = GamePhase.ResolveNight;
                P(0).roleDefinition = definition;
                Require(ability.TryUseAbility(P(0), P(2)).IsValid && P(2).effects.Count == 0, "Phase 6 effect ability queues " + kind);
                match.AdvanceMatch();
                Require(P(2).effects.Count == 1 && P(2).effects[0].RemainingTicks == 2, "Phase 3 new effect is not ticked early " + kind);
                Advance(5);
                Require(P(2).isAlive && P(2).effects[0].RemainingTicks == 1, "Phase 3 effect ticks only next night " + kind);
                Advance(5);
                Require(P(2).effects.Count == 0 && (kind != AbilityType.Curse || !P(2).isAlive && P(2).lastDeathCause == DeathCause.Curse),
                    "Phase 10 expiry removes effect and resolves curse " + kind);
            }
            Reset();
            effects.ApplyEffect(P(2), StatusEffectType.Cursed, 1, 2, GamePhase.ResolveNight, P(0));
            effects.ApplyEffect(P(2), StatusEffectType.Cursed, 1, 1, GamePhase.ResolveNight, P(1));
            Require(effects.RemoveEffect(P(2), StatusEffectType.Cursed, 1) && P(2).isCursed && P(2).effects.Count == 1,
                "Phase 3 removing one source preserves other curse");
            effects.ClearEffects(P(2));
            Require(!P(2).isCursed && P(2).effects.Count == 0, "Phase 3 cleansing clears legacy curse state");
            var daySkill = Definition(RoleType.Villager, FactionType.Villager, AbilityType.GrantExtraVote, assets);
            daySkill.usablePhase = GamePhase.Discussion; P(0).roleDefinition = daySkill;
            match.AdvanceMatch(); match.AdvanceMatch();
            Require(ability.TryUseAbility(P(0), P(2)).IsValid && P(2).HasEffect(StatusEffectType.ExtraVote) && night.PendingActionCount == 0,
                "Phase 6 daytime ability executes in its configured phase");

            foreach (DeathCause cause in Enum.GetValues(typeof(DeathCause)))
            {
                Reset(); effects.ApplyEffect(P(2), StatusEffectType.Protected, 1, 1, GamePhase.Discussion);
                var death = deaths.TryKill(new DeathRequest(P(2), cause, P(1)));
                bool blockable = deaths.protectionBlocks.Contains(cause);
                Require(blockable ? death.Outcome == DeathOutcome.Prevented && P(2).isAlive
                    : death.Outcome == DeathOutcome.Killed && P(2).lastDeathCause == cause && P(2).lastDeathSourceID == 1,
                    "Phase 11 central death rules " + cause);
                if (!blockable) Require(deaths.TryKill(new DeathRequest(P(2), cause)).Outcome == DeathOutcome.IgnoredAlreadyDead,
                    "Phase 11 repeated death ignored " + cause);
            }
            Reset(); P(2).roleType = RoleType.Idiot;
            Require(deaths.TryKill(new DeathRequest(P(2), DeathCause.Vote)).Outcome == DeathOutcome.Prevented,
                "Phase 11 Idiot vote immunity retained");
            Reset(); P(2).roleType = RoleType.WhiteHound;
            roles.playerRoles[2] = new WhiteHound(P(2));
            Require(deaths.TryKill(new DeathRequest(P(2), DeathCause.Monster, P(1))).Outcome == DeathOutcome.Prevented &&
                P(2).isWhiteHoundAwakened && P(2).faction == FactionType.Monster,
                "Phase 11 WhiteHound passive changes faction without death");
            Reset(4); P(0).loverID = 1; P(1).loverID = 0;
            int batches = 0; Action batch = () => batches++; deaths.DeathBatchResolved += batch;
            deaths.TryKill(new DeathRequest(P(0), DeathCause.Ability));
            deaths.DeathBatchResolved -= batch;
            Require(!P(0).isAlive && !P(1).isAlive && P(1).lastDeathCause == DeathCause.Lover && batches == 1,
                "Phase 11 lovers chain emits one completed death batch");
            Require(game.Winner == "Villagers", "Phase 12 victory sees the complete lovers death chain");
            Reset(4);
            using (deaths.BeginBatch()) foreach (var player in players.players) deaths.TryKill(new DeathRequest(player, DeathCause.Special));
            Require(game.Winner == "Draw" && game.Result.Players.All(player => !player.Alive),
                "Phases 12-13 simultaneous last deaths draw after full batch");
            string finalName = game.Result.Players[0].Name; P(0).playerName = "changed";
            Require(game.Result.Players[0].Name == finalName, "Phase 13 final roster is immutable");
            Require(!ability.MatchIsRunning && night.PendingActionCount == 0 && game.PhaseTimeRemaining == 0,
                "Phase 13 stops skills pending actions and phase timer");
            int day = game.currentDay; match.AdvanceMatch();
            Require(game.currentDay == day && game.currentPhase == GamePhase.GameOver && !votes.TryVote(0, 1),
                "Phase 13 locks phase and voting");
            Reset(4); deaths.TryKill(new DeathRequest(P(1), DeathCause.Vote));
            Require(game.Winner == "Villagers", "Phase 12 no threats means village win");
            Require(deaths.TryKill(new DeathRequest(P(2), DeathCause.Ability)).Outcome == DeathOutcome.InvalidRequest && P(2).isAlive,
                "Phase 13 rejects deaths after result");
            Reset(4); using (deaths.BeginBatch())
            { deaths.TryKill(new DeathRequest(P(2), DeathCause.Special)); deaths.TryKill(new DeathRequest(P(3), DeathCause.Special)); }
            Require(game.Winner == "Werewolves", "Phase 12 wolves win at parity");
            Reset(2);
            Require(game.Winner == "Werewolves", "Phase 12 offline parity also works in a two-player roster");
            Reset(4); P(1).roleType = RoleType.Killer; P(1).faction = FactionType.Neutral;
            wins.CheckWinCondition();
            Require(game.currentState != GameState.GameOver, "Phase 12 village cannot win while Killer survives");
            deaths.TryKill(new DeathRequest(P(3), DeathCause.Ability));
            Require(game.Winner == "Killer", "Phase 12 Killer own survival condition");
            Reset(4); P(1).faction = FactionType.Neutral; P(1).roleType = RoleType.Killer;
            var rule = ScriptableObject.CreateInstance<NeutralSurvivalWinRule>(); assets.Add(rule);
            rule.winnerLabel = "Solo Survivor";
            var neutral = Definition(RoleType.Killer, FactionType.Neutral, AbilityType.Kill, assets);
            neutral.neutralWinRule = rule; P(1).roleDefinition = neutral;
            using (deaths.BeginBatch()) foreach (var player in players.players.Where(player => player.playerID != 1))
                deaths.TryKill(new DeathRequest(player, DeathCause.Special));
            Require(game.Winner == "Solo Survivor", "Phase 12 custom neutral asset has an independent victory rule");
            Reset(4); P(2).roleType = RoleType.Madman; P(2).faction = FactionType.Neutral;
            Voting(); votes.TryVote(0, 2); match.AdvanceMatch();
            Require(game.Winner == "Madman", "Phase 12 Madman voted on first day wins");
            Reset(4); P(1).roleType = RoleType.WhiteHound; P(1).isWhiteHoundAwakened = true;
            deaths.TryKill(new DeathRequest(P(3), DeathCause.Special));
            Require(game.Winner == "White Wolf", "Phase 12 awakened WhiteHound own win");
            Reset(4); P(0).loverID = 1; P(1).loverID = 0;
            using (deaths.BeginBatch())
            { deaths.TryKill(new DeathRequest(P(2), DeathCause.Special)); deaths.TryKill(new DeathRequest(P(3), DeathCause.Special)); }
            Require(game.Winner == "Lovers", "Phase 12 last lovers win before parity");
            Reset(); P(0).roleType = RoleType.FoxSpirit; P(0).roleDefinition = null; P(0).faction = FactionType.Neutral;
            roles.playerRoles[0] = new FoxSpiritRole(P(0));
            Require(roles.UseNightAbility(0, 2, out _) && roles.UseNightAbility(0, 3, out _) && !roles.UseNightAbility(0, 4, out _),
                "Phase 6 Fox keeps two allowed actions per night");
            match.AdvanceMatch();
            Require(game.Winner == "Fox Spirit", "Phase 12 Fox wins after night resolution");
            Reset(4); game.currentDay = wins.MaxDay; Voting(); match.AdvanceMatch();
            Require(game.Winner == "Werewolves", "Phase 12 day limit follows existing game rule");
            Reset(); game.manualLogicPrototype = false; game.statusEffects = effects; game.abilities = ability;
            tasks.progress = 100; timer.StartTimer(); wins.CheckWinCondition();
            Require(game.Winner == "Villagers" && !ability.MatchIsRunning && !Field<bool>(timer, "running"),
                "Phase 13 integrated task victory stops both timers and skills");
            Reset();
            Require(ability.TryUseAbility(P(1), P(7)).IsValid && ability.TryUseAbility(P(0), P(0)).IsValid,
                "Main compatibility accepts independent night actions before a disconnect");
            night.CancelDisconnectedPlayerActions(7);
            Require(night.PendingActionCount == 1, "Main compatibility cancels only actions related to the disconnected player");
            Voting(); Require(votes.TryVote(7, 2), "Main compatibility records a ballot before the voter disconnects");
            var departed = P(7);
            Require(players.RemoveDisconnectedPlayer(7) && players.ConnectedPlayerCount == 7 &&
                players.IsDisconnected(7) && !departed.CanVote && players.GetplayerByID(7) == departed,
                "Main compatibility retains disconnected player lookup and blocks participation");
            match.AdvanceMatch();
            Require(votes.LastResolution.Tallies.Count == 0 && P(2).isAlive,
                "Main compatibility excludes a disconnected voter's pending ballot");
            departed.ResetForNewMatch();
            Require(!departed.isDisconnected && departed.CanVote && departed.lastDeathSourceID == -1,
                "Main compatibility resets both disconnect state and integrated death metadata");
            players.disconnectedPlayers.Clear();
            Reset(); P(0).useNetworkCanVote = P(0).networkCanVote = true;
            effects.ApplyEffect(P(0), StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution);
            Require(!P(0).CanVote && P(0).GetVoteWeight() == 0,
                "Merged voting rule honors CannotVote even when network permission is true");
            Reset(); tasks.progress = 0;
            CheckUI(scene, game, match, assets);
            Reset(); game.manualLogicPrototype = false; game.statusEffects = null; game.abilities = null;
            game.currentState = GameState.Night; game.currentPhase = GamePhase.Setup;
            P(0).roleDefinition = null; roles.playerRoles[0] = new CursedRole(P(0));
            Require(!ThuongLogicSceneScope.IsActive && roles.UseNightAbility(0, 2, out _),
                "Isolation preserves state-based ability calls in scenes without the new integration");
            game.currentState = GameState.Voting; votes.StartVote();
            Require(votes.TryVote(0, 2), "Isolation preserves state-based voting in scenes without the new integration");
            game.currentState = GameState.GameOver;
            Require(deaths.TryKill(new DeathRequest(P(2), DeathCause.Special)).Outcome == DeathOutcome.Killed,
                "Isolation leaves the legacy death lifecycle under its original manager");
            Directory.CreateDirectory("Logs/LogicPlan");
            File.WriteAllLines("Logs/LogicPlan/checks.txt", new[] { $"PASS: {checks} assertions" }.Concat(passed));
            Debug.Log($"THUONG_LOGIC_PLAN_PASSED: {checks} assertions for phases 5-13.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            foreach (var entry in previous) entry.Key.SetValue(null, entry.Value);
        }
    }

    private static void CheckUI(Scene scene, GameRoleManager game, ThuongLogicMatchController match, List<UnityEngine.Object> assets)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            assets.Add(font);
        }
        var ui = Add<RoleAbilityUI>(scene, new Dictionary<FieldInfo, object>());
        ui.Initialize(font, 0);
        SceneManager.MoveGameObjectToScene(Field<Canvas>(ui, "abilityCanvas").gameObject, scene);
        ui.SendMessage("Update"); Invoke(ui, "ShowWindow", true); Invoke(ui, "OpenTargets");
        var buttons = Field<Dictionary<int, Button>>(ui, "targetButtons");
        Require(buttons[0].gameObject.activeSelf, "Phase 5 UI includes permitted self protection");
        ui.localPlayerID = 1; ui.SendMessage("Update"); Invoke(ui, "OpenTargets");
        Require(!buttons[1].gameObject.activeSelf && buttons[2].gameObject.activeSelf,
            "Phase 5 UI hides self for wolves and shows enemy");
        var p2 = match.players.GetplayerByID(2); p2.faction = FactionType.Monster; ui.SendMessage("Update");
        Require(!buttons[2].gameObject.activeSelf, "Phase 5 open UI hides target after faction changes");
        Invoke(ui, "SelectTarget", 2);
        Require(match.nightSystem.PendingActionCount == 0 && Field<TMP_Text>(ui, "targetFeedbackLabel").text.Contains("phe"),
            "Phase 5 hidden target handler rejects invalid faction");
        p2.faction = FactionType.Villager;
        match.statusSystem.ApplyEffect(match.players.GetplayerByID(1), StatusEffectType.Silenced, 1, 1, GamePhase.Discussion);
        ui.SendMessage("Update");
        Require(!Field<Button>(ui, "useButton").interactable && buttons.Values.All(button => !button.interactable),
            "Phases 6-9 UI locks ability and targets while Silenced");
        var results = Add<ThuongMatchResultUI>(scene, new Dictionary<FieldInfo, object>());
        results.Initialize(font, match.StartMatch);
        Require(Field<GameObject>(results, "panel").GetComponent<Canvas>().isRootCanvas,
            "Phase 13 result owns a full-screen canvas independent of the HUD bounds");
        using (match.deathSystem.BeginBatch())
            foreach (var player in match.players.players) match.deathSystem.TryKill(new DeathRequest(player, DeathCause.Curse));
        results.SendMessage("Update");
        Require(Field<GameObject>(results, "panel").activeSelf && Field<TMP_Text>(results, "roster").text.Contains("Lời nguyền"),
            "Phase 13 result UI reveals roles life state and cause");
        Field<GameObject>(results, "panel").GetComponentInChildren<Button>(true).onClick.Invoke();
        results.SendMessage("Update");
        Require(!Field<GameObject>(results, "panel").activeSelf && game.Result == null && match.IsMatchRunning &&
            match.players.players.All(player => player.isAlive && player.effects.Count == 0 && !player.wasVotedOut),
            "Phase 13 replay button clears final result and resets the complete match");
    }
    private static ThuongRoleDefinition Definition(RoleType role, FactionType faction, AbilityType ability, List<UnityEngine.Object> assets)
    {
        var definition = ScriptableObject.CreateInstance<ThuongRoleDefinition>(); assets.Add(definition);
        definition.roleType = role; definition.faction = faction; definition.abilityType = ability;
        definition.displayName = role.ToString(); definition.roleID = role.ToString(); return definition;
    }
    private static T Add<T>(Scene scene, Dictionary<FieldInfo, object> previous) where T : Component
    {
        var singleton = typeof(T).GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        if (singleton != null && !previous.ContainsKey(singleton)) previous[singleton] = singleton.GetValue(null);
        var root = new GameObject(typeof(T).Name); SceneManager.MoveGameObjectToScene(root, scene);
        var component = root.AddComponent<T>(); if (singleton != null) singleton.SetValue(null, component); return component;
    }
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Expect(ActionValidationResult result, ActionFailureReason reason) =>
        Require(!result.IsValid && result.FailureReason == reason, "Validation: " + reason);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Logic plan check failed: " + message);
        checks++; passed.Add(message);
    }
}
