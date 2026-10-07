using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ThuongPrototypeChecks
{
    private static double started, next;
    private static int stage;
    private static Vector3 initialPosition;
    private static bool voted;
    private static Key[] heldKeys = Array.Empty<Key>();
    private static void PumpInput()
    {
        if (InputState.currentUpdateType == InputUpdateType.Dynamic && Keyboard.current != null)
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(heldKeys));
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batchmode only.");
        EditorSceneManager.OpenScene(ThuongPrototypeBuilder.ScenePath);
        SessionState.SetBool("ThuongOriginalPlayOptions", EditorSettings.enterPlayModeOptionsEnabled);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool("ThuongPrototypeCheck", true);
        Resume();
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool("ThuongPrototypeCheck", false)) return;
        started = EditorApplication.timeSinceStartup; next = .1;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            double now = Time.time;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Prototype test timed out at stage " + stage);
            if (!EditorApplication.isPlaying) return;
            EditorApplication.Step();
            if (now < next) return;
            var game = GameRoleManager.Instance;
            var player = UnityEngine.Object.FindObjectsByType<PrototypeControls>()
                .FirstOrDefault(p => p.GetComponent<Assets.Scripts.Thuong.PlayerMovement>()?.playerID == 0);
            Require(game != null && player != null, $"Scene gameplay components (game={game != null}, player={player != null})");
            var tasks = TaskManager.Instance;
            if (stage == 0)
            {
                InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
                InputSystem.onBeforeUpdate += PumpInput;
                Require(game.currentState == GameState.RoleReveal && game.PhaseTimeRemaining > 0,
                    "Role reveal starts after random assignment");
                Require(RoleManager.Instance.playerRoles.ContainsKey(0), "Local player has an assigned role");
                var roleCanvas = GameObject.Find("Role Ability Canvas");
                var reveal = roleCanvas != null ? roleCanvas.transform.Find("Role Reveal") : null;
                Require(reveal != null && reveal.gameObject.activeSelf, "Role card is visible");
                var card = reveal.Find("Role Card");
                Require(card.Find("Role portrait and name/Portrait frame/Role portrait PNG")
                    .GetComponent<Image>().sprite != null, "Role card has a portrait");
                Require(!string.IsNullOrWhiteSpace(card.Find("Role description/Description")
                    .GetComponent<TMP_Text>().text), "Role card explains the assigned role");
                if (game.HasIntegratedLogic) SavePreview("Logs/thuong-integrated-role-reveal.png");
                card.Find("Role description/OK").GetComponent<Button>().onClick.Invoke();
                Require(game.currentState == GameState.Day, "OK starts the first day immediately");
                Require(tasks.currentTasks.Count == 4, "Four random tasks");
                Require(UnityEngine.Object.FindObjectsByType<TaskStation>().Length == 6, "Six task stations");
                Require(UnityEngine.Object.FindAnyObjectByType<GameHUD>().progressSlider != null, "HUD wired");
                SavePreview();
                initialPosition = player.transform.position;
                heldKeys = new[] { Key.D };
                stage = 1; next = now + .5;
            }
            else if (stage == 1)
            {
                Require(player.transform.position.x > initialPosition.x + .1f,
                    $"Keyboard moves player (x={player.transform.position.x}, initial={initialPosition.x}, time={Time.time}, key={Keyboard.current.dKey.isPressed}, velocity={player.GetComponent<Rigidbody2D>().linearVelocity})");
                heldKeys = Array.Empty<Key>();
                var station = UnityEngine.Object.FindObjectsByType<TaskStation>()
                    .First(s => tasks.currentTasks.Contains(s.task));
                player.GetComponent<Rigidbody2D>().position = station.transform.position;
                stage = 2; next = now + .2;
            }
            else if (stage == 2)
            {
                // Batchmode's stepped keyboard edge events are not representative of a focused Game view.
                // Check task wiring and completion separately; E/R keys need a manual Editor check.
                var station = UnityEngine.Object.FindObjectsByType<TaskStation>()
                    .First(s => tasks.currentTasks.Contains(s.task));
                Require(station.player == player.transform, "Station references the controlled player");
                tasks.CompleteTask(station.task);
                stage = 3; next = now + .2;
            }
            else if (stage == 3)
            {
                heldKeys = Array.Empty<Key>();
                Require(tasks.progress == 10, "Task completion adds total progress");
                Require(tasks.DailyProgress == 25, "Daily progress is one of four");
                game.nightDuration = .3f; game.discussionDuration = .3f; game.votingDuration = .6f;
                game.EndDay(); stage = 4; next = now;
            }
            else if (stage == 4)
            {
                if (game.currentState == GameState.Voting && !voted)
                {
                    UnityEngine.Object.FindObjectsByType<VoteButton>().First(b => b.targetID == 1).CastVote();
                    voted = true;
                }
                if (game.currentDay < 2) return;
                Require(voted && game.currentState == GameState.Day, "Night discussion vote returns to day two");
                Require(tasks.progress == 10, "Progress persists");
                Require(tasks.DailyProgress == 0, "Daily progress resets");
                EditorSceneManager.LoadSceneInPlayMode(ThuongPrototypeBuilder.ScenePath,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                stage = 5; next = now + .5;
            }
            else if (stage == 5)
            {
                heldKeys = Array.Empty<Key>();
                if (game.currentState == GameState.RoleReveal) return;
                Require(game.currentState == GameState.Day,
                    "Role card closes automatically after its countdown");
                Require(game.currentDay == 1 && tasks.progress == 0, "Reload restarts standalone Editor scene");
                if (game.HasIntegratedLogic) CheckIntegratedRuntime(game);
                Debug.Log("THUONG_PROTOTYPE_PLAYMODE_PASSED");
                SessionState.SetBool("ThuongPrototypeCheck", false);
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ThuongOriginalPlayOptions", true);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SessionState.SetBool("ThuongPrototypeCheck", false);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ThuongOriginalPlayOptions", true);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
    private static void Require(bool ok, string label)
    { if (!ok) throw new Exception("Prototype check failed: " + label); }
    private static void CheckIntegratedRuntime(GameRoleManager game)
    {
        var players = PlayerManager.Instance;
        var roles = RoleManager.Instance;
        Require(players.players.Count == players.prototypeLobbySize &&
            UnityEngine.Object.FindObjectsByType<Assets.Scripts.Thuong.PlayerMovement>().Length == players.prototypeLobbySize &&
            UnityEngine.Object.FindObjectsByType<ThuongCharacterView>().Length == players.prototypeLobbySize,
            "Reload spawns the full lobby with replaceable character visuals");
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHUD>();
        Require(hud.meetingPanel.GetComponentsInChildren<VoteButton>(true).Length == players.prototypeLobbySize,
            "Play Mode voting exposes every player");
        // Fixed actors only inside this runtime test; the saved scene keeps random roles.
        foreach (var player in players.players)
        {
            player.ResetForNewMatch(); player.roleType = RoleType.Villager; player.faction = FactionType.Villager;
            player.roleDefinition = roles.roleDefinitions.Find(RoleType.Villager);
            roles.playerRoles[player.playerID] = new VillagerRole(player);
        }
        var guardian = players.GetplayerByID(0); guardian.roleType = RoleType.VillageGuardian;
        guardian.roleDefinition = roles.roleDefinitions.Find(RoleType.VillageGuardian);
        roles.playerRoles[0] = new GuardianRole(guardian);
        var wolf = players.GetplayerByID(1); wolf.roleType = RoleType.DogSpirit; wolf.faction = FactionType.Monster;
        wolf.roleDefinition = roles.roleDefinitions.Find(RoleType.DogSpirit); roles.playerRoles[1] = new DogSpirit(wolf);
        game.EndDay();
        var ui = hud.GetComponent<RoleAbilityUI>(); ui.localPlayerID = 0; ui.SendMessage("Update");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(RoleAbilityUI).GetMethod("OpenTargets", flags).Invoke(ui, null);
        typeof(RoleAbilityUI).GetMethod("SelectTarget", flags).Invoke(ui, new object[] { 2 });
        Require(NightManager.Instance.PendingActionCount == 1, "Original UI submits integrated protection in Play Mode");
        Require(roles.UseNightAbility(1, 2, out _), "Play Mode wolf submission accepted");
        game.FinishNight();
        Require(players.IsAlive(2) && game.currentState == GameState.Discussion, "Runtime protection resolves before attack");
        game.StartVoting(); hud.SendMessage("Update");
        SavePreview("Logs/thuong-integrated-meeting.png");
        game.statusEffects.ApplyEffect(players.GetplayerByID(2), StatusEffectType.Protected, 1, 1, GamePhase.Discussion, guardian);
        var roster = hud.GetComponent<PlayerRosterUI>(); roster.Refresh();
        var rosterWindow = hud.transform.Find("Player Roster Window").gameObject;
        rosterWindow.SetActive(true);
        Require(rosterWindow.GetComponentsInChildren<Text>().Any(label => label.text.Contains("Protected")),
            "Original roster displays integrated effects in Play Mode");
        SavePreview("Logs/thuong-integrated-roster.png");
        Debug.Log("THUONG_GAMEPLAY_INTEGRATION_PLAY_PASSED: full lobby, prefab visuals, UI ability, protection, voting and roster.");
    }
    private static void SavePreview(string path = "Logs/thuong-prototype-preview.png")
    {
        var camera = Camera.main;
        // Render every root UI canvas without raising the HUD above its popup canvases.
        // Also refresh after test callbacks, which can precede the next runtime Update.
        UnityEngine.Object.FindAnyObjectByType<GameHUD>().SendMessage("Update");
        foreach (var ui in UnityEngine.Object.FindObjectsByType<RoleAbilityUI>()) ui.SendMessage("Update");
        var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
            .Select(canvas => (canvas, order: canvas.sortingOrder)).ToArray();
        var canvases = allCanvases.Select(entry => entry.canvas)
            .Where(canvas => canvas.isRootCanvas)
            .Select(canvas => (canvas, mode: canvas.renderMode, camera: canvas.worldCamera, distance: canvas.planeDistance)).ToArray();
        var rt = new RenderTexture(1600, 900, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            // Screen-space-camera snapshots share sorting with world sprites. Lift every
            // UI canvas equally, preserving popup order, instead of lifting only the HUD.
            foreach (var entry in allCanvases) entry.canvas.sortingOrder = entry.order + 100;
            foreach (var entry in canvases)
            {
                entry.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                entry.canvas.worldCamera = camera; entry.canvas.planeDistance = 1;
            }
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            foreach (var entry in canvases)
            {
                entry.canvas.renderMode = entry.mode; entry.canvas.worldCamera = entry.camera;
                entry.canvas.planeDistance = entry.distance;
            }
            foreach (var entry in allCanvases) entry.canvas.sortingOrder = entry.order;
            UnityEngine.Object.Destroy(texture); rt.Release(); UnityEngine.Object.Destroy(rt);
        }
    }
}
