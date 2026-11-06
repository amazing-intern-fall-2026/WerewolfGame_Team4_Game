using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Lets developers opt into MainMenu as the Play Mode start scene. By default,
/// Unity uses the currently open scene so scene-specific iteration and checks work.
/// </summary>
[InitializeOnLoad]
public static class MainMenuPlayModeEnforcer
{
    private const string MainMenuScenePath = "Assets/Scenes/Loi/MainMenu.unity";
    private const string ForceMainMenuPreferencePrefix = "WerewolfGame.ForceMainMenuPlayStart:";

    private static string ForceMainMenuPreferenceKey =>
        ForceMainMenuPreferencePrefix + Application.dataPath;

    static MainMenuPlayModeEnforcer()
    {
        if (EditorPrefs.GetBool(ForceMainMenuPreferenceKey, false))
        {
            SetMainMenuAsStartScene();
            return;
        }

        // Clear the start scene left behind by the previous always-on enforcer.
        // Do not clear another scene if a developer explicitly selected it.
        SceneAsset currentStartScene = EditorSceneManager.playModeStartScene;
        if (currentStartScene != null &&
            AssetDatabase.GetAssetPath(currentStartScene) == MainMenuScenePath)
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log("[MainMenuPlayModeEnforcer] Play Mode now starts from the currently open scene.");
        }
    }

    [MenuItem("Tools/Scenes/Set MainMenu As Start Scene")]
    public static void SetMainMenuAsStartScene()
    {
        EditorPrefs.SetBool(ForceMainMenuPreferenceKey, true);
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
        if (sceneAsset != null)
        {
            if (EditorSceneManager.playModeStartScene != sceneAsset)
            {
                EditorSceneManager.playModeStartScene = sceneAsset;
                Debug.Log($"[MainMenuPlayModeEnforcer] Đã đặt scene khởi đầu khi nhấn Play: '{MainMenuScenePath}'");
            }
        }
        else
        {
            Debug.LogWarning($"[MainMenuPlayModeEnforcer] Không tìm thấy Scene tại đường dẫn: {MainMenuScenePath}");
        }
    }

    [MenuItem("Tools/Scenes/Use Active Scene On Play")]
    public static void UseActiveSceneOnPlay()
    {
        EditorPrefs.SetBool(ForceMainMenuPreferenceKey, false);
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[MainMenuPlayModeEnforcer] Play Mode will start from the currently open scene.");
    }
}

