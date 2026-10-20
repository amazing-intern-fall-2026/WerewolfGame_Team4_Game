using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class InspectSceneUI
{
    public static void Inspect()
    {
        StringBuilder sb = new StringBuilder();
        void Log(string s) { Debug.Log(s); sb.AppendLine(s); }

        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);

        Log("=== BẮT ĐẦU KIỂM TRA TOÀN DIỆN SCENE GAME.UNITY ===");

        // 1. EventSystem
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            Log("[CẢNH BÁO NGUY HIỂM] KHÔNG TÌM THẤY EventSystem trong scene! Chuột và UI click sẽ hoàn toàn không hoạt động!");
        }
        else
        {
            Log($"[EVENT SYSTEM] Tìm thấy: {es.name}, active: {es.gameObject.activeInHierarchy}");
            var modules = es.GetComponents<BaseInputModule>();
            foreach (var m in modules)
            {
                Log($"  -> InputModule: {m.GetType().Name}, enabled: {m.enabled}");
            }
        }

        // 2. Canvas
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Log($"[CANVAS] Số lượng Canvas: {canvases.Length}");
        foreach (var c in canvases)
        {
            Log($"  Canvas: {c.name}, active: {c.gameObject.activeInHierarchy}, renderMode: {c.renderMode}, sortOrder: {c.sortingOrder}");
            var gr = c.GetComponent<GraphicRaycaster>();
            Log($"    GraphicRaycaster: {(gr != null ? "CÓ, enabled: " + gr.enabled : "KHÔNG CÓ (LỖI: không nhận click)")}");
        }

        // 3. Toàn bộ UI dưới Canvas chính
        Canvas mainCanvas = Object.FindAnyObjectByType<Canvas>();
        if (mainCanvas != null)
        {
            Log($"\n[CÁC GAMEOBJECT CON TRỰC TIẾP CỦA CANVAS '{mainCanvas.name}' (Theo thứ tự Sibling Index)]:");
            for (int i = 0; i < mainCanvas.transform.childCount; i++)
            {
                Transform child = mainCanvas.transform.GetChild(i);
                RectTransform rt = child.GetComponent<RectTransform>();
                Image img = child.GetComponent<Image>();
                CanvasGroup cg = child.GetComponent<CanvasGroup>();
                Button btn = child.GetComponent<Button>();

                string info = $"Sibling [{i}] '{child.name}': activeSelf={child.gameObject.activeSelf}";
                if (rt != null) info += $", pos=({rt.anchoredPosition.x},{rt.anchoredPosition.y}), size=({rt.sizeDelta.x}x{rt.sizeDelta.y})";
                if (img != null) info += $", Image(raycastTarget={img.raycastTarget}, color={img.color})";
                if (btn != null) info += $", Button(interactable={btn.interactable})";
                if (cg != null) info += $", CanvasGroup(blocksRaycasts={cg.blocksRaycasts}, alpha={cg.alpha})";

                Log(info);
            }
        }

        // 4. Kiểm tra PlayerIdentityCard
        Log("\n[KIỂM TRA PLAYER IDENTITY CARD]:");
        GameObject idCard = GameObject.Find("PlayerIdentityCard");
        if (idCard == null)
        {
            Log("[LỖI] GameObject.Find('PlayerIdentityCard') không tìm thấy!");
        }
        else
        {
            Log($"PlayerIdentityCard: activeSelf={idCard.activeSelf}, activeInHierarchy={idCard.activeInHierarchy}");
            Button btn = idCard.GetComponent<Button>();
            Log($"  Button: {(btn != null ? "CÓ" : "KHÔNG")}, interactable={(btn != null ? btn.interactable.ToString() : "N/A")}");
            Image img = idCard.GetComponent<Image>();
            Log($"  Image: {(img != null ? "CÓ, raycastTarget=" + img.raycastTarget : "KHÔNG")}");
            NetworkPlayerIdentityCardUI script = idCard.GetComponent<NetworkPlayerIdentityCardUI>();
            Log($"  NetworkPlayerIdentityCardUI: {(script != null ? "CÓ" : "KHÔNG")}");

            // Kiểm tra các phần tử con
            for (int j = 0; j < idCard.transform.childCount; j++)
            {
                Transform c = idCard.transform.GetChild(j);
                Image cImg = c.GetComponent<Image>();
                TextMeshProUGUI cTmp = c.GetComponent<TextMeshProUGUI>();
                string childInfo = $"    Con [{j}] '{c.name}': ";
                if (cImg != null) childInfo += $"Image(raycastTarget={cImg.raycastTarget}) ";
                if (cTmp != null) childInfo += $"TMP(raycastTarget={cTmp.raycastTarget}) ";
                Log(childInfo);
            }
        }

        // 5. Kiểm tra RoleDetailPanel & NetworkRoleDetailUI
        Log("\n[KIỂM TRA ROLE DETAIL PANEL & CONTROLLER]:");
        NetworkRoleDetailUI[] detailUIs = Object.FindObjectsByType<NetworkRoleDetailUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Log($"Số component NetworkRoleDetailUI trong scene: {detailUIs.Length}");
        foreach (var d in detailUIs)
        {
            Log($"  NetworkRoleDetailUI trên GameObject: '{d.gameObject.name}', activeSelf: {d.gameObject.activeSelf}, activeInHierarchy: {d.gameObject.activeInHierarchy}");
        }

        File.WriteAllText("scene_inspection.txt", sb.ToString(), Encoding.UTF8);
        Debug.Log(">>> ĐÃ GHI KẾT QUẢ KIỂM TRA SCENE VÀO scene_inspection.txt");
    }
}
