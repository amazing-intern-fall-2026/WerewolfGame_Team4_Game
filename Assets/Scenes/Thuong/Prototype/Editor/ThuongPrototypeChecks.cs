using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
            var game = GameManager.Instance;
            var player = UnityEngine.Object.FindAnyObjectByType<PrototypeControls>();
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
                Require(game.currentDay == 1 && tasks.progress == 0, "Reload restarts standalone Editor scene");
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
    private static void SavePreview()
    {
        var camera = Camera.main;
        var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        var rt = new RenderTexture(1600, 900, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.sortingOrder = 100;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
        File.WriteAllBytes("Logs/thuong-prototype-preview.png", texture.EncodeToPNG());
        camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.Destroy(texture); rt.Release(); UnityEngine.Object.Destroy(rt);
    }
}
