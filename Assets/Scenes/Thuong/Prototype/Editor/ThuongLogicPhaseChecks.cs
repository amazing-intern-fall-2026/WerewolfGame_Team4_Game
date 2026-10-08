using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ThuongLogicPhaseChecks
{
    private const string SessionKey = "ThuongLogicPlayCheck";
    private const string ReadyKey = "ThuongLogicPlayReady";
    private static double started;
    private static int checks;

    [MenuItem("Tools/Thuong/Check Logic Phase 1-4")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        // Existing Phase 1 regression also covers movement, tasks, Hunter callback and voting UI.
        ThuongPhaseOneChecks.Run();
        if (!File.Exists(ThuongLogicPrototypeBuilder.ScenePath)) ThuongLogicPrototypeBuilder.Build();
        EditorSceneManager.OpenScene(ThuongLogicPrototypeBuilder.ScenePath);
        var match = UnityEngine.Object.FindFirstObjectByType<ThuongLogicMatchController>();
        BindSingletons(match);
        Require(UnityEngine.Object.FindObjectsByType<DeathResolver>(FindObjectsSortMode.None).Length == 1, "One central death resolver");
        Require(match.ValidateRoster(out _), "Unique IDs and valid role assets");
        var doctor = match.players.GetplayerByID(0);
        var wolf = match.players.GetplayerByID(1);
        var target = match.players.GetplayerByID(2);
        Require(doctor.roleDefinition.allowSelfTarget && wolf.roleDefinition.targetFactionRule == TargetFactionRule.OtherFaction, "Doctor/Wolf Inspector config");
        Require(match.statusSystem.deathSystem == match.deathSystem && match.abilitySystem.nightSystem == match.nightSystem, "Inspector system references");
        var ui = UnityEngine.Object.FindFirstObjectByType<ThuongLogicPrototypeUI>();
        ui.Initialize();
        ui.startButton.onClick.Invoke();
        Require(match.phaseController.currentPhase == GamePhase.Night && match.phaseController.currentDay == 1, "Start goes to Night round 1");

        // Independent sources stack; same source refresh never weakens its duration/strength.
        var effect = match.statusSystem.ApplyEffect(target, StatusEffectType.ExtraVote, 1, 2, GamePhase.DayResolution, doctor);
        match.statusSystem.ApplyEffect(target, StatusEffectType.ExtraVote, 2, 1, GamePhase.DayResolution, wolf);
        match.statusSystem.ApplyEffect(target, StatusEffectType.ExtraVote, 1, 1, GamePhase.DayResolution, doctor);
        Require(target.effects.Count == 2 && target.GetEffectStrength(StatusEffectType.ExtraVote) == 3 && effect.RemainingTicks == 2, "Stack and same-source refresh");
        var roster = ui.GetComponent<PlayerRosterUI>();
        roster.Refresh();
        Require(ui.rosterPanel.GetComponentsInChildren<Text>().Any(text => text.text.Contains("ExtraVote")), "Effects visible on player row");
        match.statusSystem.ProcessPhaseStart(GamePhase.Discussion, match.players.players);
        Require(effect.RemainingTicks == 2, "Unrelated phase does not tick");
        match.statusSystem.ProcessPhaseStart(GamePhase.DayResolution, match.players.players);
        Require(effect.RemainingTicks == 1 && target.effects.Count == 1, "Tick removes just the expired source");
        roster.Refresh();
        Require(ui.rosterPanel.GetComponentsInChildren<Text>().Any(text => text.text.Contains("ExtraVote x1 (1)")), "Remaining tick updates in UI");
        match.StartMatch();
        Require(target.effects.Count == 0 && !target.hasDeathRecord, "New match clears effects and death records");
        Require(ui.rosterPanel.GetComponentsInChildren<Text>().Any(text => text.text == "Role: Ẩn"), "Role hidden during the match");

        int kills = 0;
        Action<DeathResult> countKilled = result => { if (result.Outcome == DeathOutcome.Killed) kills++; };
        match.deathSystem.DeathResolved += countKilled;
        Require(match.deathSystem.TryKill(new DeathRequest(null, DeathCause.Special)).Outcome == DeathOutcome.InvalidRequest, "Missing death target rejected");
        match.statusSystem.ApplyEffect(target, StatusEffectType.Protected, 1, 1, GamePhase.Discussion, doctor);
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Ability)).Outcome == DeathOutcome.Prevented && target.isAlive, "Protected blocks generic Ability");
        Require(!target.HasEffect(StatusEffectType.Protected), "Block consumes one protection layer");
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Monster)).Outcome == DeathOutcome.Killed && target.lastDeathCause == DeathCause.Monster, "Kill writes cause");
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Monster)).Outcome == DeathOutcome.IgnoredAlreadyDead && kills == 1, "Repeated kill does not produce a second death");
        Require(ui.rosterPanel.GetComponentsInChildren<Text>().Any(text => text.text.Contains("Chết do Monster")), "Death record visible in roster");
        match.StartMatch();
        match.statusSystem.ApplyEffect(target, StatusEffectType.Protected, 1, 1, GamePhase.Discussion, doctor);
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Vote)).Outcome == DeathOutcome.Killed, "Protection does not block Vote");
        match.StartMatch();
        match.statusSystem.ApplyEffect(target, StatusEffectType.Protected, 1, 1, GamePhase.Discussion, doctor);
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Curse)).Outcome == DeathOutcome.Killed, "Protection does not block Curse");
        match.StartMatch();
        match.statusSystem.ApplyEffect(target, StatusEffectType.Protected, 1, 1, GamePhase.Discussion, doctor);
        Require(match.deathSystem.TryKill(new DeathRequest(target, DeathCause.Monster, bypassProtection: true)).Outcome == DeathOutcome.Killed, "Explicit bypass works");
        match.deathSystem.DeathResolved -= countKilled;

        // Exercise the generic Phase 3 expiry path without adding any Phase 10 role asset.
        match.StartMatch();
        var doctorDefinition = doctor.roleDefinition;
        var curseDefinition = ScriptableObject.CreateInstance<ThuongRoleDefinition>();
        try
        {
            curseDefinition.abilityType = AbilityType.Curse;
            curseDefinition.effectDurationTicks = 2;
            curseDefinition.effectTicksOnPhase = GamePhase.ResolveNight;
            doctor.roleDefinition = curseDefinition;
            Require(match.abilitySystem.TryUseAbility(doctor, target).IsValid, "Generic effect ability queues");
            match.AdvanceMatch();
            Require(target.effects.Single().RemainingTicks == 2, "New effect is not ticked in its creation resolution");
            for (int i = 0; i < 5; i++) match.AdvanceMatch();
            Require(target.effects.Single().RemainingTicks == 1 && target.isAlive, "Old effect ticks next resolution");
            for (int i = 0; i < 5; i++) match.AdvanceMatch();
            Require(!target.isAlive && target.lastDeathCause == DeathCause.Curse && target.effects.Count == 0, "Expiry sends curse through central death resolver");
        }
        finally { doctor.roleDefinition = doctorDefinition; UnityEngine.Object.DestroyImmediate(curseDefinition); }

        CheckProtectionOrder(match, ui, false);
        CheckProtectionOrder(match, ui, true);
        match.StartMatch();
        ui.SelectActor(0); ui.SelectTarget(3); ui.useButton.onClick.Invoke();
        ui.SelectActor(1); ui.SelectTarget(2); ui.useButton.onClick.Invoke();
        ui.nextButton.onClick.Invoke();
        Require(target.hasDeathRecord && !target.isAlive && target.lastDeathCause == DeathCause.Monster, "Unprotected attack kills through central resolver");
        var spare = match.players.GetplayerByID(3);
        Require(spare.HasEffect(StatusEffectType.Protected), "Unused protection survives NightResolution");
        ui.nextButton.onClick.Invoke();
        Require(!spare.HasEffect(StatusEffectType.Protected), "Protected expires at DayDiscussion");

        match.StartMatch();
        ui.nextButton.onClick.Invoke(); ui.nextButton.onClick.Invoke(); ui.nextButton.onClick.Invoke();
        Require(match.phaseController.currentPhase == GamePhase.Voting, "NightResolution → DayDiscussion → Voting");
        Require(match.voteSystem.TryVote(0, 1), "Existing voting pipeline accepts a living voter");
        ui.nextButton.onClick.Invoke();
        Require(wolf.lastDeathCause == DeathCause.Vote && !match.IsMatchRunning && !ui.nextButton.interactable, "Vote can end and lock match");
        match.StartMatch();
        for (int i = 0; i < 3; i++) match.AdvanceMatch();
        Require(match.voteSystem.TryVote(0, 1), "Weighted vote accepts a choice before effect changes");
        match.statusSystem.ApplyEffect(doctor, StatusEffectType.ExtraVote, 2, 1, GamePhase.DayResolution, doctor);
        match.AdvanceMatch();
        Require(match.voteSystem.LastResolution.Tallies[1] == 3 && doctor.GetVoteWeight() == 1,
            "Vote weights are computed at resolve, before DayResolution expiry");
        match.StartMatch();
        for (int i = 0; i < 3; i++) match.AdvanceMatch();
        Require(match.voteSystem.TryVote(0, 1), "Vote choice recorded for late eligibility check");
        match.statusSystem.ApplyEffect(doctor, StatusEffectType.CannotVote, 1, 1, GamePhase.DayResolution, wolf);
        Require(!match.voteSystem.TryVote(0, 2), "CannotVote blocks submission");
        match.AdvanceMatch();
        Require(wolf.isAlive && match.voteSystem.LastResolution.Tallies.Count == 0,
            "CannotVote removes a previously recorded choice at resolve");
        match.StartMatch();
        for (int i = 0; i < 3; i++) match.AdvanceMatch();
        match.voteSystem.TryVote(0, 1); match.voteSystem.TryVote(2, 0);
        match.AdvanceMatch();
        Require(match.voteSystem.LastResolution.IsTie && wolf.isAlive && doctor.isAlive, "A tied vote eliminates nobody");
        match.StartMatch();
        for (int i = 0; i < 3; i++) match.AdvanceMatch();
        match.voteSystem.TryVote(0, 1); match.voteSystem.TryVote(2, 1); match.voteSystem.TryVote(3, 0);
        int voteResults = 0;
        Action<VoteResolution> countVoteResults = _ => voteResults++;
        match.voteSystem.VoteResolved += countVoteResults;
        match.AdvanceMatch();
        var firstResolution = match.voteSystem.LastResolution;
        match.voteSystem.ResolveVote();
        Require(voteResults == 1 && match.voteSystem.LastResolution == firstResolution && doctor.isAlive,
            "Repeated resolve preserves the result and cannot eliminate the runner-up");
        match.voteSystem.VoteResolved -= countVoteResults;
        match.StartMatch();
        for (int i = 0; i < 5; i++) match.AdvanceMatch();
        Require(match.phaseController.currentPhase == GamePhase.Night && match.phaseController.currentDay == 2, "Full cycle reaches Night round 2");
        match.abilitySystem.TryUseAbility(wolf, target);
        match.StartMatch(); match.AdvanceMatch();
        Require(target.isAlive && target.effects.Count == 0 && !target.hasVoted, "Restart discards queued actions, effects and votes");
        // Save only the setup scene, never runtime test data.
        EditorSceneManager.OpenScene(ThuongLogicPrototypeBuilder.ScenePath);
        Debug.Log($"THUONG_LOGIC_EDIT_CHECKS_PASSED: {checks} assertions.");
        if (!Application.isBatchMode) return;
        SessionState.SetBool(SessionKey, true);
        SessionState.SetBool(ReadyKey, false);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    private static void CheckProtectionOrder(ThuongLogicMatchController match, ThuongLogicPrototypeUI ui, bool wolfFirst)
    {
        match.StartMatch();
        ui.SelectTarget(2);
        ui.SelectActor(wolfFirst ? 1 : 0); ui.useButton.onClick.Invoke();
        ui.SelectActor(wolfFirst ? 0 : 1); ui.useButton.onClick.Invoke();
        Require(match.nightSystem.PendingActionCount == 2, $"UI buttons submit two queued night actions (pending={match.nightSystem.PendingActionCount}, status={ui.statusText.text})");
        Require(match.players.IsAlive(2) && !match.players.GetplayerByID(2).HasEffect(StatusEffectType.Protected), "Night actions are queued, not immediate");
        ui.nextButton.onClick.Invoke();
        Require(match.players.IsAlive(2) && !match.players.GetplayerByID(2).hasDeathRecord && ui.logText.text.Contains("được bảo vệ"),
            (wolfFirst ? "Wolf-first resolves Protected before Kill" : "Doctor-first resolves Protected before Kill") +
            $" (phase={match.phaseController.currentPhase}, log={ui.logText.text})");
    }

    private static void BindSingletons(ThuongLogicMatchController match)
    {
        PlayerManager.Instance = match.players; GameRoleManager.Instance = match.phaseController;
        RoleManager.Instance = match.roles; DeathResolver.Instance = match.deathSystem;
        StatusEffectSystem.Instance = match.statusSystem; NightManager.Instance = match.nightSystem;
        AbilitySystem.Instance = match.abilitySystem; VoteManager.Instance = match.voteSystem; WinConditionManager.Instance = match.winSystem;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= HandlePlayState;
        EditorApplication.playModeStateChanged += HandlePlayState;
    }
    private static void HandlePlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        SessionState.SetBool(ReadyKey, true);
        Debug.Log("THUONG_LOGIC_PLAY_ENTERED");
    }
    private static void Tick()
    {
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Play Mode test timed out.");
            if (!SessionState.GetBool(ReadyKey, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // Batch editor callbacks can run before the first runtime Start after a reload.
            EditorApplication.Step();
            if (Time.timeSinceLevelLoad < .1f) return;
            var match = UnityEngine.Object.FindFirstObjectByType<ThuongLogicMatchController>();
            var ui = UnityEngine.Object.FindFirstObjectByType<ThuongLogicPrototypeUI>();
            Require(match != null && ui != null && !match.IsMatchRunning && !ui.nextButton.interactable, "Play Mode starts clean in Setup");
            CheckProtectionOrder(match, ui, false);
            CheckProtectionOrder(match, ui, true);
            match.StartMatch();
            ui.SelectActor(0); ui.SelectTarget(3); ui.useButton.onClick.Invoke();
            match.AdvanceMatch();
            Require(match.players.GetplayerByID(3).HasEffect(StatusEffectType.Protected), "Play Mode UI produces Protected effect");
            ui.GetComponent<PlayerRosterUI>().Refresh();
            Require(ui.rosterPanel.GetComponentsInChildren<Text>().Count(text => text.gameObject.name == "Name") == 4,
                "Roster contains exactly four active rows after script reload");
            Require(ui.rosterPanel.GetComponentsInChildren<Text>().Any(text => text.text.Contains("Protected x1 (1)")),
                "Current roster displays the protection duration");
            SavePreview(ui.GetComponent<Canvas>(), 1600, 900, "Logs/thuong-logic-phase4.png");
            SavePreview(ui.GetComponent<Canvas>(), 960, 540, "Logs/thuong-logic-phase4-small.png");
            Debug.Log("THUONG_PHASES_1_4_PASSED: legacy Phase 1, edit checks, Play Mode, Inspector references and UI previews.");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(SessionKey, false);
        SessionState.SetBool(ReadyKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= HandlePlayState;
        EditorApplication.Exit(code);
    }
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new Exception("Thuong logic check failed: " + message);
        checks++;
    }
    private static void SavePreview(Canvas canvas, int width, int height, string path)
    {
        var camera = Camera.main;
        var render = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = render;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            var pixels = texture.GetPixels32();
            var background = pixels[0];
            Require(pixels.Count(pixel => Math.Abs(pixel.r-background.r)+Math.Abs(pixel.g-background.g)+Math.Abs(pixel.b-background.b)>15)>1000, "UI preview has rendered content");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            render.Release(); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
