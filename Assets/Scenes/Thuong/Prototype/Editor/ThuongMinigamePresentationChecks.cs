using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Minigames;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ThuongMinigamePresentationChecks
{
    private const string Key = "ThuongMinigamePresentationChecks";
    private static int index, stage, checks, baseScenes, baseCameras;
    private static uint sequence;
    private static double next, began;
    private static ThuongMinigameCatalog catalog;
    private static ThuongMinigameAuthority authority;
    private static ThuongMinigameWindow window;
    private static ThuongMinigameState state;

    [MenuItem("Tools/Thuong/Minigames/Check Rendering And Player Scripts")]
    public static void Run()
    {
        CompilePlayerScripts();
        ThuongMinigameChecks.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/Thuong/OnlineSceneGamePlay.unity");
        SessionState.SetBool(Key + "Options", EditorSettings.enterPlayModeOptionsEnabled);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode();
    }

    public static void CompilePlayerScripts()
    {
        Directory.CreateDirectory("Logs/Minigames");
        var compilation = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.DevelopmentBuild }, "Temp/ThuongMinigamePlayerScripts");
        if (compilation.assemblies == null || !compilation.assemblies.Any(path => Path.GetFileName(path) == "Assembly-CSharp.dll"))
            throw new Exception("Windows player script compilation failed.");
        Debug.Log("THUONG_MINIGAME_PLAYER_SCRIPTS_PASSED");
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - began > 150) throw new Exception("Presentation checks timed out.");
            if (!EditorApplication.isPlaying || Time.time < .5f) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (Time.unscaledTimeAsDouble < next) return;
            if (stage == 0)
            {
                catalog = Resources.Load<ThuongMinigameCatalog>(ThuongMinigameCatalog.ResourcePath);
                baseScenes = SceneManager.sceneCount; baseCameras = Object.FindObjectsByType<Camera>().Length;
                stage = 1;
            }
            if (stage == 1)
            {
                if (index >= catalog.entries.Count) { Finish(null); return; }
                var entry = catalog.entries[index];
                authority = new ThuongMinigameAuthority(); sequence = 0;
                Require(authority.TryBegin(1, entry.taskId, index, 1, entry.kind, entry.rules, catalog.IngredientScores(),
                    Time.unscaledTimeAsDouble, Time.unscaledTimeAsDouble + 120, out state, out _), "Begin preview " + entry.kind);
                window = new GameObject("Task presentation check").AddComponent<ThuongMinigameWindow>();
                window.Open(catalog, entry, state, Object.FindAnyObjectByType<GameHUD>().phaseText.font,
                    input =>
                    {
                        var updated = authority.Apply(1, state.attemptId, ++sequence, input, Time.unscaledTimeAsDouble);
                        if (updated != null) { state = updated; window.Apply(state); }
                    }, () => { }, () => { }, () => Time.unscaledTimeAsDouble);
                next = Math.Max(Time.unscaledTimeAsDouble + .4, state.startsAt + .1); stage = 2;
                return;
            }
            if (stage == 2)
            {
                var raw = Field<RawImage>(window, "image");
                Require(raw.texture != null && raw.rectTransform.rect.width > 0 && Math.Abs(raw.rectTransform.rect.width / raw.rectTransform.rect.height - 16f / 9) < .02, "RawImage keeps viewport aspect " + state.kind);
                Require(SceneManager.GetActiveScene().name == "OnlineSceneGamePlay" && Time.timeScale == 1, "Rendering leaves active scene and time scale intact");
                Require(Object.FindObjectsByType<EventSystem>().Length == 1 && Object.FindObjectsByType<AudioListener>().Count(item => item.enabled) == 1, "Rendering shares event system and listener");
                // Test bounds through the same EventSystem adapter used for mouse and touch.
                var pointer = raw.GetComponent<ThuongMinigamePointer>();
                uint before = sequence;
                pointer.OnPointerDown(new PointerEventData(EventSystem.current) { position = new Vector2(-1000, -1000) });
                Require(sequence == before, "Outside pointer cannot dispatch " + state.kind);
                if (state.kind == ThuongMinigameKind.Trapsweeper)
                {
                    var view = Field<ThuongTaskMinigameView>(window, "view");
                    view.Pointer(new Vector2(.5f, .5f), true, true);
                    Require(state.cells[12] == -3, "Touch flag mode uses the same viewport mapping");
                    next = Time.unscaledTimeAsDouble + .15; stage = 3; return;
                }
                Capture(); stage = 5; return;
            }
            if (stage == 3)
            {
                Field<ThuongTaskMinigameView>(window, "view").Pointer(new Vector2(.5f, .5f), true, true);
                next = Time.unscaledTimeAsDouble + .15; stage = 4; return;
            }
            if (stage == 4)
            {
                Field<ThuongTaskMinigameView>(window, "view").Pointer(new Vector2(.5f, .5f), true, false);
                Require(state.cells[12] == 0 && state.score > 0, "First reveal is safe and appears in view");
                next = Time.unscaledTimeAsDouble + .3; stage = 5; return;
            }
            if (stage == 5)
            {
                if (state.kind == ThuongMinigameKind.Trapsweeper) Capture();
                window.Dispose(); Object.Destroy(window.gameObject); window = null;
                next = Time.unscaledTimeAsDouble + .25; stage = 6; return;
            }
            if (stage == 6)
            {
                Require(SceneManager.sceneCount == baseScenes && Object.FindObjectsByType<Camera>().Length == baseCameras &&
                    Object.FindObjectsByType<ThuongTaskMinigameView>().Length == 0, "Dispose unloads scene, camera and adapter " + catalog.entries[index].kind);
                index++; stage = 1;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Capture()
    {
        foreach (var label in window.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        var rt = (RenderTexture)Field<RawImage>(window, "image").texture;
        var previous = RenderTexture.active;
        var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32(); var first = colors[0];
            Require(colors.Any(color => color.r != first.r || color.g != first.g || color.b != first.b), "Camera renders visible game content " + state.kind);
        }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
        ThuongMedievalSceneChecks.SavePreview("Logs/Minigames/view-" + state.kind + ".png");
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Key, false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "Options", false);
        if (window != null) { window.Dispose(); Object.Destroy(window.gameObject); }
        var result = error == null ? "THUONG_MINIGAME_PRESENTATION_PASSED: " + checks + " assertions; six rendered windows and cleanup" : "THUONG_MINIGAME_PRESENTATION_FAILED: " + error;
        File.WriteAllText("Logs/Minigames/presentation.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
}
