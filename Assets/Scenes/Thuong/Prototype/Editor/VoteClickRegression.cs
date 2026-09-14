using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class VoteClickRegression
{
    private const string Request = "Logs/vote-click.request";
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        };
    }
    [MenuItem("Tools/Thuong/Check Vote Click")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var previous = new Dictionary<FieldInfo, object>();
        var preview = EditorSceneManager.OpenPreviewScene(ThuongPrototypeBuilder.ScenePath);
        RenderTexture rt = null;
        try
        {
            var components = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            foreach (var component in components)
            {
                if (component == null) throw new Exception("Missing script in scene");
                var field = component.GetType().GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.FieldType != component.GetType()) continue;
                if (!previous.ContainsKey(field)) previous[field] = field.GetValue(null);
                field.SetValue(null, component);
            }
            PlayerManger.Instance.CreateTestPlayer(5);
            GameManager.Instance.currentState = GameState.Voting;
            VoteManger.Instance.StartVote();
            var hud = components.OfType<GameHUD>().Single();
            hud.meetingPanel.SetActive(true);
            hud.resultPanel.SetActive(false);
            var button = components.OfType<VoteButton>().Single(b => b.targetID == 1);
            button.SendMessage("Awake");
            button.SendMessage("OnEnable");
            button.SendMessage("Update");
            var canvas = components.OfType<Canvas>().Single();
            var camera = components.OfType<Camera>().Single();
            canvas.transform.localScale = Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 100;
            rt = new RenderTexture(1600, 900, 24);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(components.OfType<EventSystem>().Single())
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            if (hits.Count == 0) throw new Exception("Vote UI has no raycast hit.");
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (receiver != button.gameObject)
                throw new Exception("Vote blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
            if (!PlayerManger.Instance.GetplayerByID(0).hasVoted) throw new Exception("Pointer click did not register vote.");
            VoteManger.Instance.StartVote();
            button.SendMessage("OnDisable");
            button.SendMessage("OnEnable");
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
            if (!PlayerManger.Instance.GetplayerByID(0).hasVoted)
                throw new Exception("Re-enabled button lost its click handler.");
            if (VoteManger.Instance.TryVote(0, 2)) throw new Exception("Duplicate vote accepted.");
            if (VoteManger.Instance.GetVotedTarget(0) != 1) throw new Exception("Selected target not preserved.");
            File.WriteAllText("Logs/vote-click-result.txt",
                "PASS: UI raycast, Button pointer click, re-enable listener, duplicate rejection and selected-target feedback.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/vote-click-result.txt", error.ToString());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (var pair in previous) pair.Key.SetValue(null, pair.Value);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
