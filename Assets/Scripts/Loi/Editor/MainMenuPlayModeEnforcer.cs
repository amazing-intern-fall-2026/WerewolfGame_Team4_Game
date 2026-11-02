using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Đảm bảo MainMenu luôn là Scene khởi đầu đầu tiên khi nhấn nút Play trong Unity Editor.
/// </summary>
[InitializeOnLoad]
public static class MainMenuPlayModeEnforcer
{
    private const string MainMenuScenePath = "Assets/Scenes/Loi/MainMenu.unity";

    static MainMenuPlayModeEnforcer()
    {
        EnsurePlayModeStartScene();
    }

    [MenuItem("Tools/Scenes/Set MainMenu As Start Scene")]
    public static void EnsurePlayModeStartScene()
    {
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
}

