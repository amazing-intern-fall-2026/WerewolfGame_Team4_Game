using System;
using System.Linq;
using Thuong.Maps;
using Thuong.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ThuongOnlineMapSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/Thuong/OnlineSceneGamePlay.unity";
    private const string PreviewName = "Thuong Online Map Preview";

    [MenuItem("Tools/Thuong/Maps/Use Large Village In Online Scene")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var definition = AssetDatabase.LoadAssetAtPath<ThuongMapDefinition>(ThuongMapInstaller.Root + "/Definitions/village_large.asset");
        if (!ThuongMapValidator.Validate(definition, out string error)) throw new InvalidOperationException(error);
        var entry = scene.GetRootGameObjects().Select(root => root.GetComponent<ThuongOnlineSceneEntry>()).Single(value => value != null);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Map" || root.name == "TaskStations") root.SetActive(false);
        var preview = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PreviewName);
        if (preview == null)
        {
            preview = new GameObject(PreviewName);
            SceneManager.MoveGameObjectToScene(preview, scene);
        }
        foreach (Transform child in preview.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        var world = (GameObject)PrefabUtility.InstantiatePrefab(definition.worldPrefab, preview.transform);
        world.transform.localPosition = Vector3.zero;
        world.transform.localRotation = Quaternion.identity;
        world.transform.localScale = Vector3.one;
        preview.SetActive(true);
        var serializedEntry = new SerializedObject(entry);
        serializedEntry.FindProperty("initialMap").objectReferenceValue = definition;
        serializedEntry.FindProperty("editorMapPreview").objectReferenceValue = preview;
        serializedEntry.ApplyModifiedPropertiesWithoutUndo();
        var follow = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThuongTopDownCamera>(true)).Single();
        follow.boundsMin = definition.boundsMin;
        follow.boundsMax = definition.boundsMax;
        follow.Refresh(true);
        EditorUtility.SetDirty(entry);
        EditorUtility.SetDirty(follow);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the online scene.");
        Selection.activeGameObject = preview;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("THUONG_ONLINE_LARGE_MAP_INSTALLED: saved scene default and editor preview; runtime still owns map/station binding.");
    }
}
