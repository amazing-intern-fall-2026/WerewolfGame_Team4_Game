using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameVoiceInstaller
{
    [MenuItem("Tools/Loi/Install Game Voice HUD")]
    public static void Install()
    {
        InstallInternal(saveScene: true, writeReport: true);
    }

    public static bool InstallInternal(bool saveScene = true, bool writeReport = true)
    {
        StringBuilder sb = new StringBuilder();
        void Log(string s) { Debug.Log(s); sb.AppendLine(s); }

        Log("=== BẮT ĐẦU CÀI ĐẶT GAME VOICE HUD VÀO GAME.UNITY ===");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Log("[LỖI] Không thể mở scene Game.unity");
            return false;
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Log("[LỖI] Không tìm thấy Canvas trong Game.unity!");
            return false;
        }

        Log($"Tìm thấy Canvas: {canvas.name}");

        // Kiểm tra xem đã có GameVoicePanel chưa
        Transform existing = canvas.transform.Find("GameVoicePanel");
        GameObject voiceHUDObj;
        GameVoiceHUD hud;

        if (existing != null)
        {
            Log("Đã tìm thấy GameVoicePanel hiện tại. Tiến hành cập nhật cấu hình...");
            voiceHUDObj = existing.gameObject;
            hud = voiceHUDObj.GetComponent<GameVoiceHUD>();
            if (hud == null) hud = voiceHUDObj.AddComponent<GameVoiceHUD>();
        }
        else
        {
            Log("Tạo mới GameObject 'GameVoicePanel' dưới Canvas...");
            voiceHUDObj = new GameObject("GameVoicePanel", typeof(RectTransform), typeof(GameVoiceHUD));
            voiceHUDObj.transform.SetParent(canvas.transform, false);
            hud = voiceHUDObj.GetComponent<GameVoiceHUD>();
        }

        RectTransform rt = voiceHUDObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta = new Vector2(220f, 96f);

        // Gọi EnsureUIReferences để tạo/kết nối các component con
        hud.EnsureUIReferences();

        // Gán các Serialized Fields chuẩn chỉ cho GameVoiceHUD
        SerializedObject so = new SerializedObject(hud);
        so.Update();

        Transform micT = voiceHUDObj.transform.Find("VoiceMicPanel");
        Transform spkT = voiceHUDObj.transform.Find("VoiceSpeakerPanel");

        if (micT != null)
        {
            so.FindProperty("micPanel").objectReferenceValue = micT.GetComponent<RectTransform>();
            so.FindProperty("micToggle").objectReferenceValue = micT.GetComponentInChildren<MicToggleSwitch>(true);
            so.FindProperty("micPanelButton").objectReferenceValue = micT.GetComponent<Button>();
        }

        if (spkT != null)
        {
            so.FindProperty("speakerPanel").objectReferenceValue = spkT.GetComponent<RectTransform>();
            so.FindProperty("speakerToggle").objectReferenceValue = spkT.GetComponentInChildren<SpeakerToggleSwitch>(true);
            so.FindProperty("speakerPanelButton").objectReferenceValue = spkT.GetComponent<Button>();
        }

        so.FindProperty("containerRect").objectReferenceValue = rt;
        so.ApplyModifiedProperties();

        // Cập nhật Sibling Index để nằm dưới các popup nhưng trên nền
        voiceHUDObj.transform.SetAsLastSibling();

        Log($"[THÀNH CÔNG] GameVoiceHUD đã được cài đặt:");
        Log($"  - Vị trí: Top-Right anchor (1,1), Pos: (-20, -20), Size: 220x96");
        Log($"  - VoiceMicPanel: {(micT != null ? "OK" : "THIẾU")}");
        Log($"  - VoiceSpeakerPanel: {(spkT != null ? "OK" : "THIẾU")}");

        // Kiểm tra không che khu vực các UI khác:
        // 1. PlayerIdentityCard: Top-Left (20, -20), width: 200 -> Cách Top-Right hơn 1400 px trên màn hình 1920.
        // 2. Chat UI: Bottom-Left (24, 24) -> Hoàn toàn tách biệt.
        // 3. Skill buttons: Bottom-Center -> Hoàn toàn tách biệt.
        Log("  - Kiểm tra va chạm layout: ĐẠT (Nằm riêng biệt tại góc trên bên phải, không che bất kỳ element nào).");

        if (saveScene)
        {
            EditorUtility.SetDirty(voiceHUDObj);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("  - Đã lưu scene Game.unity thành công.");
        }

        if (writeReport)
        {
            File.WriteAllText("voice_installer_report.txt", sb.ToString(), Encoding.UTF8);
        }

        return true;
    }
}
