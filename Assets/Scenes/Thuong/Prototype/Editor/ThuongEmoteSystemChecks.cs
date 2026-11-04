using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Emotes;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ThuongEmoteSystemChecks
{
    private static int checks;
    private const string Output = "Logs/Emotes/Thuong";

    [MenuItem("Tools/Thuong/Emotes/Check And Build QA")]
    public static void RunAndBuildQa()
    {
        Run();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Thuong/OnlineSceneGamePlay.unity" },
            target = BuildTarget.StandaloneWindows64,
            locationPathName = Output + "/Player/ThuongEmotes.exe",
            options = BuildOptions.Development | (File.Exists(Output + "/Player/ThuongEmotes.exe") ? BuildOptions.BuildScriptsOnly : BuildOptions.None),
            extraScriptingDefines = new[] { "THUONG_EMOTE_QA" }
        });
        Check(report.summary.result == BuildResult.Succeeded, "Thuong online scene builds");
        Debug.Log("THUONG_EMOTE_BUILD_PASSED");
    }

    [MenuItem("Tools/Thuong/Emotes/Run Checks")]
    public static void Run()
    {
        checks = 0;
        Check(PlayerEmoteCatalog.Select(Vector2.zero) == 0, "Center cancels");
        Check(PlayerEmoteCatalog.Select(new Vector2(68, 0)) == 0, "Dead-zone boundary");
        for (int id = 1; id <= PlayerEmoteCatalog.Count; id++)
        {
            Check(PlayerEmoteCatalog.Select(PlayerEmoteCatalog.Direction(id) * 150) == id, "Mouse direction " + id);
            Check(PlayerEmoteCatalog.Select(PlayerEmoteCatalog.Direction(id), 0) == id, "Keyboard direction " + id);
        }
        Check(!PlayerEmoteCatalog.IsValid(0) && !PlayerEmoteCatalog.IsValid(255), "Reject invalid IDs");
        foreach (GamePhase phase in new[] { GamePhase.Task, GamePhase.Night, GamePhase.Discussion, GamePhase.Voting })
        {
            Check(PlayerEmoteCatalog.CanExpress(true, phase), "Alive eligibility " + phase);
            Check(!PlayerEmoteCatalog.CanExpress(false, phase), "Dead eligibility " + phase);
        }
        foreach (GamePhase phase in new[] { GamePhase.Setup, GamePhase.RoleReveal, GamePhase.ResolveNight, GamePhase.GameOver })
            Check(!PlayerEmoteCatalog.CanExpress(true, phase), "Locked phase " + phase);
        var authority = new ThuongEmoteAuthority();
        Check(authority.TryAccept(1, 10, 10, 1, 1, true, 100), "First server acceptance");
        Check(!authority.TryAccept(1, 10, 10, 2, 2, true, 100.1), "Host cooldown");
        Check(!authority.TryAccept(1, 10, 10, 2, 2, true, 102), "Rejected request cannot replay later");
        Check(authority.TryAccept(2, 10, 10, 1, 8, true, 100.1), "Independent cooldown per sender");
        Check(!authority.TryAccept(1, 10, 9, 3, 1, true, 102), "Old match rejected");
        Check(!authority.TryAccept(1, 10, 10, 3, 255, true, 102), "Invalid emote rejected");
        Check(!authority.TryAccept(1, 10, 10, 3, 1, false, 102), "Dead/phase/minigame policy rejected");
        Check(authority.TryAccept(1, 10, 10, 4, 1, true, 102), "Eligible later request accepted");
        authority.Clear();
        Check(authority.TryAccept(1, 11, 11, 1, 1, true, 102), "Fresh match resets state");
        Check(!authority.TryAccept(1, 11, 11, 2, 1, true, double.NaN), "Invalid time rejected");
        foreach (string file in Directory.GetFiles("Assets/Scripts/Thuong/Emotes", "*.cs"))
        {
            string text = File.ReadAllText(file);
            Check(!text.Contains("NetworkPlayerState") && !text.Contains("NetworkPhaseSync") && !text.Contains("NetworkGameOverManager"),
                "No Loi implementation dependency: " + Path.GetFileName(file));
        }
        var root = new GameObject("Emote vector checks", typeof(RectTransform));
        try
        {
            var graphic = root.AddComponent<PlayerEmoteGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(80, 80);
            var method = typeof(PlayerEmoteGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            for (int id = 1; id <= PlayerEmoteCatalog.Count; id++)
            {
                graphic.EmoteId = id;
                using var vertices = new VertexHelper();
                method.Invoke(graphic, new object[] { vertices });
                Check(vertices.currentVertCount > 0, "Face renders " + id);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/editor-checks.txt", "PASS: " + checks + " Thuong emote checks\n");
        Debug.Log("THUONG_EMOTE_CHECKS_PASSED: " + checks);
    }

    public static void RenderPreviews()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Thuong emote preview", typeof(RectTransform));
        var ui = root.AddComponent<PlayerEmoteWheelUI>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (root.GetComponent<Canvas>() == null) typeof(PlayerEmoteWheelUI).GetMethod("Awake", flags).Invoke(ui, null);
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Scenes/Thuong/Prototype/Art/PrototypeFont.asset");
        foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.font = font;
        typeof(PlayerEmoteWheelUI).GetMethod("OpenWheel", flags).Invoke(ui, null);
        typeof(PlayerEmoteWheelUI).GetMethod("SetSelection", flags).Invoke(ui, new object[] { 8 });
        var cameraRoot = new GameObject("Preview camera", typeof(Camera));
        var camera = cameraRoot.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(36, 51, 48, 255);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var previousQuality = QualitySettings.renderPipeline;
        var previousDefault = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        try
        {
            QualitySettings.renderPipeline = null; UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            Directory.CreateDirectory(Output);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                    Check(texture.GetPixels32().Count(p => p.r > 150 && p.g > 100) > 5000, "Visible wheel");
                    File.WriteAllBytes($"{Output}/wheel-{size.x}x{size.y}.png", texture.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                    UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>())
            { label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "Label fits: " + label.text); }
            Debug.Log("THUONG_EMOTE_VISUALS_PASSED");
        }
        finally
        {
            QualitySettings.renderPipeline = previousQuality; UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = previousDefault;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("THUONG EMOTE CHECK FAILED: " + message);
        checks++;
    }
}
