using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Thuong.Online;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ThuongCompactVoiceChecks
{
    private const string Output = "Logs/ThuongCompactVoice20261113";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> passed = new();
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException(description);
        passed.Add(description);
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Output + "/ui-checks.txt", new[] { "PASS checks=" + passed.Count }.Concat(passed));
    }

    [MenuItem("Tools/Thuong/Communications/Check Compact Voice And Online UI")]
    public static void Run()
    {
        ThuongCommunicationChecks.Run();
        ThuongRoomUpgradeOnlineChecks.RunReviewSmoke();
    }

    public static void RemoveFixtureOverlays()
    {
        // InitializeOnLoad fixtures can create an unparented result canvas before
        // the online scenario starts, then destroy its manager without its canvas.
        // Remove only this known fixture residue, before hosting a fresh match.
        var residue = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(canvas => canvas.name == "GameOverDedicatedCanvas").ToArray();
        foreach (var canvas in residue) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        Debug.Log("COMPACT_VOICE_QA: removed pre-scenario fixture overlays=" + residue.Length);
    }

    public static void Capture(ThuongOnlineSession session, string stage)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        if (stage == "lobby") passed.Clear();
        var mic = Get<Button>(session, "microphoneButton");
        var speaker = Get<Button>(session, "speakerButton");
        var hint = Get<GameObject>(session, "communicationHint");
        var refresh = session.GetType().GetMethod("UpdateCommunications", Flags);
        refresh.Invoke(session, null);
        Require(!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Any(canvas => canvas.name == "GameOverDedicatedCanvas" && canvas.isActiveAndEnabled),
            stage + ": no legacy result overlay appears in the running lobby or match");
        Require(!hint.activeSelf, stage + ": status hint stays hidden by default");
        Require(!mic.interactable && !speaker.interactable, stage + ": disconnected voice cannot toggle devices");
        Require(mic.GetComponentInChildren<TMP_Text>().text.Contains("CHỜ"), stage + ": unavailable voice is not reported as enabled");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
        {
            string name = stage + "-" + size.x + "x" + size.y;
            ThuongMedievalSceneChecks.SavePreview(Output + "/" + name + ".png", size.x, size.y, camera =>
            {
                Rect ScreenBounds(RectTransform rect)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    var points = corners.Select(point => camera.WorldToScreenPoint(point)).ToArray();
                    return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                        points.Max(point => point.x), points.Max(point => point.y));
                }
                var controls = new[] { mic, speaker };
                var others = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(button => button.isActiveAndEnabled && !controls.Contains(button) &&
                        button.GetComponentInParent<Canvas>()?.isActiveAndEnabled == true).ToArray();
                foreach (var control in controls)
                {
                    Rect bounds = ScreenBounds((RectTransform)control.transform);
                    Require(bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= size.x && bounds.yMax <= size.y,
                        name + ": " + control.name + " remains on screen");
                    Require(others.All(button => !bounds.Overlaps(ScreenBounds((RectTransform)button.transform))),
                        name + ": " + control.name + " does not cover Chat or any other button");
                }
                foreach (var area in new[] { "TaskPanel", "TopBar", "Lobby table", "Character selection" })
                {
                    var panel = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                        .FirstOrDefault(rect => rect.name == area && rect.gameObject.activeInHierarchy && rect.GetComponentInParent<Canvas>().enabled);
                    if (panel != null) Require(controls.All(button => !ScreenBounds((RectTransform)button.transform).Overlaps(ScreenBounds(panel))),
                        name + ": voice leaves " + area + " clear");
                }
            });
        }
        var pointer = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(mic.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        refresh.Invoke(session, null);
        Require(hint.activeSelf, stage + ": hover explains unavailable voice even on a disabled button");
        ThuongMedievalSceneChecks.SavePreview(Output + "/" + stage + "-hint.png", 1280, 720);
        ExecuteEvents.Execute(mic.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        refresh.Invoke(session, null);
        Require(!hint.activeSelf, stage + ": leaving the button hides the hint");
        Debug.Log("THUONG_COMPACT_VOICE_UI_PASSED: " + stage + " cumulative checks=" + passed.Count);
    }
}
