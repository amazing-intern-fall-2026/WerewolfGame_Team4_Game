using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Thuong;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ThuongMedievalSceneChecks
{
    private static int stage;
    private static GameRoleManager originalGame;
    private static double started;
    private static float next;
    private static Vector3 startPosition;
    private static Keyboard testKeyboard;
    private static Keyboard[] nativeKeyboards;
    private static Key[] held = Array.Empty<Key>();
    private static readonly Key[] directions = { Key.W, Key.D, Key.A, Key.S };

    [MenuItem("Tools/Thuong/Check Medieval Top-Down Scene")]
    public static void CheckSavedScene()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ThuongPrototypeBuilder.ScenePath);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(c => c != null), "No missing scripts");
            var follow = components.OfType<ThuongTopDownCamera>().Single();
            var camera = follow.GetComponent<Camera>();
            Require(camera.orthographic && camera.transform.rotation == Quaternion.identity, "Camera looks along Z at the XY world");
            var player = components.OfType<PlayerMovement>().Single();
            Require(player.transform.localScale == Vector3.one && player.GetComponent<Rigidbody2D>().gravityScale == 0,
                "Uniform character scale and zero top-down gravity");
            var actor = player.GetComponentInChildren<ThuongTopDownCharacter>();
            Require(actor != null && actor.cast.Count == 20 && actor.cast.controllers.All(c => c != null) &&
                    actor.cast.idleSprites.Distinct().Count() == 20, "Twenty separate skins independent of secret roles");
            foreach (AnimatorController controller in actor.cast.controllers)
            {
                Require(new[] { "isWalking", "isDead", "LastInputX", "LastInputY" }.All(p => controller.parameters.Any(parameter => parameter.name == p)), "Animation/input parameters");
                Require(controller.animationClips.Length == 9, "Four idle directions, four walk directions and death");
                foreach (var clip in controller.animationClips)
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    Require(AnimationUtility.GetObjectReferenceCurve(clip, binding).All(key => key.value is Sprite sprite && sprite != null),
                        "Animation sprite references survive reimport: " + clip.name);
            }
            var stations = components.OfType<TaskStation>().ToArray();
            Require(stations.Length == 6 && stations.All(s => s.task != null && s.player == player.transform &&
                s.marker.GetComponent<SpriteRenderer>().sprite.name == "QuestMarker"), "Six preserved interactions with imported markers");
            Require(components.OfType<SpriteRenderer>().Where(r => r.enabled).All(r => r.sprite != null && r.sprite.name != "Block"), "No active world placeholder blocks");
            Require(components.OfType<ThuongMedievalTheme>().Single().paper != null, "Imported UI art");
            CheckRoutes(components, stations);
            Debug.Log("THUONG_MEDIEVAL_EDIT_PASSED: camera, skins, animation, source sprites, UI and six reachable stations.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // Check that props/colliders have not sealed any task behind a house/fence.
    private static void CheckRoutes(Component[] components, TaskStation[] stations)
    {
        Rect[] obstacles = components.OfType<BoxCollider2D>().Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<PlayerMovement>() == null)
            .Select(c => {
                Vector2 center = c.transform.TransformPoint(c.offset);
                Vector2 size = Vector2.Scale(c.size, c.transform.lossyScale) + Vector2.one * .46f;
                return new Rect(center-size/2, size);
            }).ToArray();
        const float step = .25f;
        const int width = 112, height = 96;
        Vector2 Position(Vector2Int cell) => new Vector2(-12.875f+cell.x*step, -10.875f+cell.y*step);
        var queue = new Queue<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        var start = new Vector2Int(56, 44);
        queue.Enqueue(start); seen.Add(start);
        Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var offset in offsets)
            {
                var cell = current + offset;
                if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height || seen.Contains(cell)) continue;
                if (obstacles.Any(r => r.Contains(Position(cell)))) continue;
                seen.Add(cell); queue.Enqueue(cell);
            }
        }
        foreach (var station in stations)
            Require(seen.Any(cell => Vector2.Distance(Position(cell), station.transform.position) < station.interactionRadius-.1f),
                "Walkable route to " + station.task.name);
    }

    public static void RunPlayMode()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use batchmode for automated keyboard input.");
        EditorSceneManager.OpenScene(ThuongPrototypeBuilder.ScenePath);
        SessionState.SetBool("ThuongMedievalOriginalPlayOptions", EditorSettings.enterPlayModeOptionsEnabled);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool("ThuongMedievalPlayCheck", true);
        Resume(); EditorApplication.EnterPlaymode();
    }

    public static void RunRegression()
    {
        CheckSavedScene();
        ThuongPhaseFiveChecks.Run();
        ThuongPrototypeChecks.Run();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool("ThuongMedievalPlayCheck", false)) return;
        started = EditorApplication.timeSinceStartup; next = .2f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Pump()
    {
        if (InputState.currentUpdateType == InputUpdateType.Dynamic && testKeyboard != null)
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(held));
    }
    private static PlayerMovement Local() => Object.FindObjectsByType<PlayerMovement>().Single(p => p.GetComponent<PrototypeControls>().enabled);
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup-started > 120) throw new Exception("Medieval play check timed out at stage " + stage);
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (Time.time < next) return;
            var game = GameRoleManager.Instance;
            if (game == null || Object.FindObjectsByType<ThuongTopDownCharacter>().Length != 20) return;
            var local = Local(); var actor = local.GetComponentInChildren<ThuongTopDownCharacter>();
            if (stage == 0)
            {
                InputSystem.settings = Object.Instantiate(InputSystem.settings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                testKeyboard = InputSystem.AddDevice<Keyboard>("ThuongMedievalTestKeyboard");
                nativeKeyboards = InputSystem.devices.OfType<Keyboard>().Where(k => k != testKeyboard && k.enabled).ToArray();
                foreach (var keyboard in nativeKeyboards) InputSystem.DisableDevice(keyboard);
                testKeyboard.MakeCurrent();
                InputSystem.onBeforeUpdate += Pump;
                Require(game.currentState == GameState.RoleReveal && ThuongMedievalTheme.Active != null, "Role reveal and medieval UI boot");
                SavePreview("Logs/thuong-medieval-role.png");
                GameObject.Find("Role Ability Canvas").transform.Find("Role Reveal/Role Card/Role description/OK")
                    .GetComponent<Button>().onClick.Invoke();
                Require(game.currentState == GameState.Day, "Role OK starts the first day");
                SavePreview("Logs/thuong-medieval-gameplay.png");
                startPosition = local.transform.position; held = new[] { Key.W };
                stage = 1; next = Time.time+.5f;
            }
            else if (stage >= 1 && stage <= 4)
            {
                var animator = actor.animator;
                Require(animator.GetBool("isWalking"), $"Walking animation responds to keyboard (stage={stage}, phase={game.currentState}, alive={local.IsAlive}, velocity={local.GetComponent<Rigidbody2D>().linearVelocity}, keys={string.Join(",", held)}, keyboard={Keyboard.current?.name})");
                float x = animator.GetFloat("LastInputX"), y = animator.GetFloat("LastInputY");
                Require(stage == 1 ? y > .9f : stage == 2 ? x > .9f : stage == 3 ? x < -.9f : y < -.9f, $"Four directional facings (stage={stage}, x={x}, y={y}, keys={string.Join(",", held)})");
                if (stage == 1) Require(local.transform.position.y > startPosition.y+.2f, "Top-down movement changes Y");
                if (stage < 4) { held = new[] { directions[stage] }; stage++; next = Time.time+.35f; }
                else { held = new[] { Key.F2 }; stage = 5; next = Time.time+.2f; }
            }
            else if (stage == 5)
            {
                Require(local.playerID == 1 && Camera.main.GetComponent<ThuongTopDownCamera>().target == local.transform, "F2 changes both control and camera target");
                Require(actor.animator.runtimeAnimatorController == actor.cast.controllers[1], "Second player has its own skin");
                held = Array.Empty<Key>();
                var station = Object.FindObjectsByType<TaskStation>().First(s => TaskManager.Instance.currentTasks.Contains(s.task));
                local.GetComponent<Rigidbody2D>().position = (Vector2)station.transform.position + Vector2.down*.8f;
                stage = 6; next = Time.time+.2f;
            }
            else if (stage == 6) { held = new[] { Key.E }; stage = 7; next = Time.time+.3f; }
            else if (stage == 7)
            {
                held = Array.Empty<Key>();
                Require(TaskManager.Instance.progress == 10 && TaskManager.Instance.DailyProgress == 25, "E completes an imported station with unchanged progress");
                var camera = Camera.main.GetComponent<ThuongTopDownCamera>(); camera.overview = true; camera.Refresh(true);
                SavePreview("Logs/thuong-medieval-village.png");
                camera.overview = false; camera.Refresh(true);
                game.EndDay(); stage = 8; next = Time.time+1;
            }
            else if (stage == 8)
            {
                Require(game.currentState == GameState.Night, "Night phase preserved");
                SavePreview("Logs/thuong-medieval-night.png");
                game.discussionDuration = .4f; game.votingDuration = .7f;
                game.FinishNight(); stage = 9; next = Time.time+.2f;
            }
            else if (stage == 9)
            {
                if (game.currentState != GameState.Voting) return;
                var button = Object.FindObjectsByType<VoteButton>().First(b => b.targetID != local.playerID);
                button.CastVote(); Require(PlayerManager.Instance.GetplayerByID(local.playerID).hasVoted, "Vote works with the themed roster");
                SavePreview("Logs/thuong-medieval-vote.png");
                stage = 10; next = Time.time+.2f;
            }
            else if (stage == 10)
            {
                if (game.currentDay < 2) return;
                Require(game.currentState == GameState.Day && TaskManager.Instance.progress == 10 && TaskManager.Instance.DailyProgress == 0, "Day/night/vote loop retains total progress and resets daily progress");
                originalGame = game; held = new[] { Key.R }; stage = 11; next = Time.time+.3f;
            }
            else if (stage == 11)
            {
                if (game == originalGame) return;
                held = Array.Empty<Key>();
                Require(game.currentDay == 1 && TaskManager.Instance.progress == 0 && game.currentState == GameState.RoleReveal, "R reloads the skinned scene and restarts the match");
                Require(Object.FindObjectsByType<ThuongTopDownCharacter>().Length == 20 && Camera.main.GetComponent<ThuongTopDownCamera>().target != null,
                    "All skins and the camera reconnect after reload");
                Debug.Log("THUONG_MEDIEVAL_PLAY_PASSED: WASD in four directions, F2 camera/skin, E interaction, night, voting and R reload.");
                Finish(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool("ThuongMedievalPlayCheck", false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ThuongMedievalOriginalPlayOptions", true);
        InputSystem.onBeforeUpdate -= Pump; EditorApplication.update -= Tick;
        if (nativeKeyboards != null) foreach (var keyboard in nativeKeyboards) if (keyboard.added) InputSystem.EnableDevice(keyboard);
        if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
        EditorApplication.Exit(code);
    }
    private static void Require(bool value, string description)
    { if (!value) throw new InvalidOperationException("Medieval check failed: " + description); }

    // A separate UI camera preserves the real full-screen HUD when the world
    // camera uses a smaller viewport. Original layers/canvas settings are restored.
    public static void SavePreview(string path)
    {
        Object.FindAnyObjectByType<GameHUD>().SendMessage("Update");
        foreach (var ui in Object.FindObjectsByType<RoleAbilityUI>()) ui.SendMessage("Update");
        foreach (var station in Object.FindObjectsByType<TaskStation>()) station.SendMessage("Update");
        var world = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Where(c => c.isRootCanvas)
            .Select(c => (canvas:c, mode:c.renderMode, camera:c.worldCamera, distance:c.planeDistance)).ToArray();
        var uiObjects = canvases.SelectMany(entry => entry.canvas.GetComponentsInChildren<Transform>(true))
            .Select(t => (go:t.gameObject, layer:t.gameObject.layer)).ToArray();
        var rt = new RenderTexture(1600, 900, 24);
        var texture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        var uiTarget = new RenderTexture(1600, 900, 24);
        var uiTexture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        var uiObject = new GameObject("Temporary screenshot UI camera", typeof(Camera));
        var uiCamera = uiObject.GetComponent<Camera>();
        var oldTarget = world.targetTexture; var oldActive = RenderTexture.active; int oldMask = world.cullingMask;
        try
        {
            uiCamera.transform.position = new Vector3(0, 0, -100);
            uiCamera.orthographic = true; uiCamera.orthographicSize = 5;
            uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.backgroundColor = Color.clear;
            uiCamera.cullingMask = 1 << 5; uiCamera.targetTexture = uiTarget;
            foreach (var entry in uiObjects) entry.go.layer = 5;
            foreach (var entry in canvases)
            { entry.canvas.renderMode = RenderMode.ScreenSpaceCamera; entry.canvas.worldCamera = uiCamera; entry.canvas.planeDistance = 1; }
            world.cullingMask &= ~(1 << 5); world.targetTexture = rt;
            world.GetComponent<ThuongTopDownCamera>()?.Refresh(true);
            RenderTexture.active = rt; GL.Clear(true, true, new Color32(26, 33, 25, 255));
            Canvas.ForceUpdateCanvases(); world.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            uiCamera.Render(); RenderTexture.active = uiTarget;
            uiTexture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); uiTexture.Apply();
            var pixels = texture.GetPixels32(); var overlay = uiTexture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                int alpha = overlay[i].a;
                if (alpha == 0) continue;
                pixels[i] = new Color32((byte)((overlay[i].r*alpha+pixels[i].r*(255-alpha))/255),
                    (byte)((overlay[i].g*alpha+pixels[i].g*(255-alpha))/255),
                    (byte)((overlay[i].b*alpha+pixels[i].b*(255-alpha))/255), 255);
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            world.targetTexture = oldTarget; world.cullingMask = oldMask; RenderTexture.active = oldActive;
            foreach (var entry in canvases)
            { entry.canvas.renderMode = entry.mode; entry.canvas.worldCamera = entry.camera; entry.canvas.planeDistance = entry.distance; }
            foreach (var entry in uiObjects) if (entry.go != null) entry.go.layer = entry.layer;
            Object.DestroyImmediate(uiObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(uiTexture);
            rt.Release(); uiTarget.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(uiTarget);
        }
    }
}
