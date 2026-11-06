using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// <summary>
/// Tự động bảo toàn và ép vị trí chuẩn nguyên bản cho các nút trong MainMenu scene
/// bất cứ khi nào Unity Editor biên dịch hoặc mở scene.
/// </summary>
[InitializeOnLoad]
public static class MainMenuLayoutEnforcer
{
    static MainMenuLayoutEnforcer()
    {
        EditorApplication.delayCall += EnforceLayout;
        EditorSceneManager.sceneOpened += (scene, mode) => EnforceLayout();
    }

    [MenuItem("Tools/UI/Enforce MainMenu Layout")]
    public static void EnforceLayout()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "MainMenu") return;

        bool changed = false;

        // 1. HostButton
        var hostButton = GameObject.Find("HostButton");
        if (hostButton != null)
        {
            var rect = hostButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                var targetPos = new Vector2(-183f, -67f);
                var targetSize = new Vector2(400f, 108f);
                if (rect.anchoredPosition != targetPos || rect.sizeDelta != targetSize)
                {
                    Undo.RecordObject(rect, "Enforce HostButton Layout");
                    rect.anchoredPosition = targetPos;
                    rect.sizeDelta = targetSize;
                    EditorUtility.SetDirty(rect);
                    changed = true;
                }
            }

            var tmp = hostButton.GetComponentInChildren<TMP_Text>();
            if (tmp != null && tmp.text.Contains("(HOST)"))
            {
                Undo.RecordObject(tmp, "Enforce HostButton Text");
                tmp.text = "TẠO PHÒNG";
                EditorUtility.SetDirty(tmp);
                changed = true;
            }
        }

        // 2. ClientButton (Join)
        var clientButton = GameObject.Find("ClientButton");
        if (clientButton != null)
        {
            var rect = clientButton.GetComponent<RectTransform>();
            if (rect != null)
            {
                var targetPos = new Vector2(-183f, -303f);
                var targetSize = new Vector2(400f, 108f);
                if (rect.anchoredPosition != targetPos || rect.sizeDelta != targetSize)
                {
                    Undo.RecordObject(rect, "Enforce ClientButton Layout");
                    rect.anchoredPosition = targetPos;
                    rect.sizeDelta = targetSize;
                    EditorUtility.SetDirty(rect);
                    changed = true;
                }
            }

            var tmp = clientButton.GetComponentInChildren<TMP_Text>();
            if (tmp != null && tmp.text.Contains("(JOIN)"))
            {
                Undo.RecordObject(tmp, "Enforce ClientButton Text");
                tmp.text = "VÀO PHÒNG ";
                EditorUtility.SetDirty(tmp);
                changed = true;
            }
        }

        // 3. GameTitle
        var gameTitle = GameObject.Find("GameTitle");
        if (gameTitle != null)
        {
            var rect = gameTitle.GetComponent<RectTransform>();
            if (rect != null)
            {
                var targetPos = new Vector2(-134f, 180f);
                var targetSize = new Vector2(760f, 70f);
                if (rect.anchoredPosition != targetPos || rect.sizeDelta != targetSize)
                {
                    Undo.RecordObject(rect, "Enforce GameTitle Layout");
                    rect.anchoredPosition = targetPos;
                    rect.sizeDelta = targetSize;
                    EditorUtility.SetDirty(rect);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[MainMenuLayoutEnforcer] Đã tự động khôi phục vị trí chuẩn và lưu scene MainMenu thành công!");
        }
    }
}

