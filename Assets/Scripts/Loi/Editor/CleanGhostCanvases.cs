#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tự động dọn dẹp các bản sao ghost của "Role Ability Canvas" trong Game scene.
/// Canvas này vốn dĩ được sinh dynamic ở runtime bởi RoleAbilityUI, việc bị serialize
/// vào file scene dẫn đến lỗi tràn bộ nhớ TLS Allocator ALLOC_TEMP_TLS (size 574)
/// và làm xung đột tham chiếu nút kỹ năng.
/// </summary>
[InitializeOnLoad]
public static class CleanGhostCanvases
{
    static CleanGhostCanvases()
    {
        EditorApplication.delayCall += CleanActiveSceneIfApplicable;
    }

    [MenuItem("Tools/Loi/Clean Ghost Role Ability Canvases")]
    public static void CleanActiveSceneIfApplicable()
    {
        // Domain reload also invokes the delayed callback when entering Play Mode.
        // Runtime canvases belong to RoleAbilityUI and must never be removed here.
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || scene.path != "Assets/Scenes/Loi/Game.unity") return;

        var roots = scene.GetRootGameObjects();
        int removedCount = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            var r = roots[i];
            if (r != null && r.name == "Role Ability Canvas")
            {
                Object.DestroyImmediate(r);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CleanGhostCanvases] Đã xóa thành công {removedCount} ghost 'Role Ability Canvas' và lưu scene sạch.");
        }
    }
}
#endif
