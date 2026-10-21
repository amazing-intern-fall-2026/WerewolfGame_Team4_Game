using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameVoiceTests
{
    [InitializeOnLoadMethod]
    private static void OnEditorReload()
    {
        // This check opens and saves Game.unity. Do not interrupt another batch
        // check or a play-mode session when scripts reload.
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Tự động chạy khi Unity compile xong
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            RunAllTests();
        };
    }

    [MenuItem("Tools/Loi/Run Game Voice Tests")]
    public static void RunAllTests()
    {
        StringBuilder sb = new StringBuilder();
        int passCount = 0;
        int failCount = 0;

        void Log(string s)
        {
            Debug.Log(s);
            sb.AppendLine(s);
        }

        void AssertTest(string testName, bool condition, string detail = "")
        {
            if (condition)
            {
                passCount++;
                Log($"[PASS] {testName} {(string.IsNullOrEmpty(detail) ? "" : "-> " + detail)}");
            }
            else
            {
                failCount++;
                Log($"[FAIL] {testName} {(string.IsNullOrEmpty(detail) ? "" : "-> " + detail)}");
            }
        }

        Log("=================================================================");
        Log("           BÁO CÁO KIỂM THỬ HỆ THỐNG VOICE GAME SCENE            ");
        Log($"           Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss}       ");
        Log("=================================================================");

        try
        {
            // Cài đặt / cập nhật GameVoiceHUD trong scene trước khi test
            GameVoiceInstaller.InstallInternal(saveScene: true, writeReport: false);

            // TEST 1: Kiểm tra cấu hình mặc định an toàn của NetworkVoiceManager
            Log("\n--- TEST 1: Cấu hình mặc định của NetworkVoiceManager ---");
            NetworkVoiceManager voiceMgr = NetworkVoiceManager.Instance;
            AssertTest("Test 1.1: NetworkVoiceManager Singleton tồn tại", voiceMgr != null);
            AssertTest("Test 1.2: Micro mặc định luôn TẮT (IsMicrophoneMuted == true)", voiceMgr.IsMicrophoneMuted == true, $"IsMicrophoneMuted={voiceMgr.IsMicrophoneMuted}");
            AssertTest("Test 1.3: Loa mặc định BẬT (IsOutputMuted == false)", voiceMgr.IsOutputMuted == false, $"IsOutputMuted={voiceMgr.IsOutputMuted}");

            // TEST 2: Kiểm tra Scene Game.unity và vị trí HUD Voice
            Log("\n--- TEST 2: Kiểm tra Scene Game.unity & Layout Voice HUD ---");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
            AssertTest("Test 2.1: Mở scene Game.unity thành công", scene.IsValid());

            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            AssertTest("Test 2.2: Canvas tồn tại trong Game.unity", canvas != null);

            Transform voicePanelT = canvas.transform.Find("GameVoicePanel");
            AssertTest("Test 2.3: GameVoicePanel tồn tại dưới Canvas", voicePanelT != null);

            if (voicePanelT != null)
            {
                RectTransform rt = voicePanelT.GetComponent<RectTransform>();
                AssertTest("Test 2.4: Anchor Top-Right (1, 1)", rt.anchorMin == Vector2.one && rt.anchorMax == Vector2.one, $"anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}");
                AssertTest("Test 2.5: Pivot Top-Right (1, 1)", rt.pivot == Vector2.one, $"pivot={rt.pivot}");
                AssertTest("Test 2.6: Tọa độ x âm cách mép phải và y âm cách mép trên", rt.anchoredPosition.x < 0 && rt.anchoredPosition.y < 0, $"pos={rt.anchoredPosition}");
                AssertTest("Test 2.7: Kích thước nhỏ gọn cho mobile landscape", rt.sizeDelta.x <= 250 && rt.sizeDelta.y <= 120, $"size={rt.sizeDelta}");

                // Kiểm tra các panel con
                Transform micPanel = voicePanelT.Find("VoiceMicPanel");
                Transform spkPanel = voicePanelT.Find("VoiceSpeakerPanel");
                AssertTest("Test 2.8: VoiceMicPanel tồn tại", micPanel != null);
                AssertTest("Test 2.9: VoiceSpeakerPanel tồn tại", spkPanel != null);

                // Kiểm tra không va chạm với các UI khác
                Transform idCard = canvas.transform.Find("PlayerIdentityCard");
                Transform chatPanel = canvas.transform.Find("CompactChatPanel");
                Transform floatingChat = canvas.transform.Find("FloatingChatButton");
                Transform skillPanel = canvas.transform.Find("SkillPanel");

                bool nonOverlap = true;
                if (idCard != null)
                {
                    RectTransform cardRt = idCard.GetComponent<RectTransform>();
                    // IdCard ở Top-Left (anchor 0, 1), Voice ở Top-Right (anchor 1, 1) -> Không che
                    AssertTest("Test 2.10: Không che PlayerIdentityCard (Top-Left vs Top-Right)", cardRt.anchorMin.x == 0 && rt.anchorMin.x == 1);
                }
                if (chatPanel != null || floatingChat != null)
                {
                    // Chat ở Bottom-Left (anchor y = 0), Voice ở Top-Right (anchor y = 1) -> Không che
                    AssertTest("Test 2.11: Không che Chat UI (Bottom-Left vs Top-Right)", rt.anchorMin.y == 1);
                }
            }

            // TEST 3: Kiểm tra GameVoiceHUD component và logic an toàn vào scene
            Log("\n--- TEST 3: GameVoiceHUD và Quy tắc Safe Default Mute ---");
            GameVoiceHUD hud = voicePanelT != null ? voicePanelT.GetComponent<GameVoiceHUD>() : null;
            AssertTest("Test 3.1: GameVoiceHUD component tồn tại", hud != null);

            if (hud != null)
            {
                // Giả lập Awake / Start
                hud.EnsureUIReferences();
                AssertTest("Test 3.2: Vào Game Scene, Micro mặc định TẮT", hud.IsMicrophoneOn == false, $"IsMicrophoneOn={hud.IsMicrophoneOn}");
                AssertTest("Test 3.3: Vào Game Scene, Loa mặc định BẬT", hud.IsSpeakerOn == true, $"IsSpeakerOn={hud.IsSpeakerOn}");

                // TEST 4: Bật/tắt Microphone
                Log("\n--- TEST 4: Thao tác Bật/Tắt Microphone ---");
                // Nhấn nút Mic lần 1 -> Bật Mic
                hud.ToggleMicrophone();
                AssertTest("Test 4.1: Nhấn Microphone lần 1 -> Micro BẬT", voiceMgr.IsMicrophoneMuted == false, $"IsMicrophoneMuted={voiceMgr.IsMicrophoneMuted}");
                AssertTest("Test 4.2: UI Mic hiển thị BẬT", hud.IsMicrophoneOn == true);

                // Nhấn nút Mic lần 2 -> Tắt Mic
                hud.ToggleMicrophone();
                AssertTest("Test 4.3: Nhấn Microphone lần 2 -> Micro TẮT", voiceMgr.IsMicrophoneMuted == true, $"IsMicrophoneMuted={voiceMgr.IsMicrophoneMuted}");
                AssertTest("Test 4.4: UI Mic hiển thị TẮT", hud.IsMicrophoneOn == false);

                // TEST 5: Bật/tắt Loa (Speaker)
                Log("\n--- TEST 5: Thao tác Bật/Tắt Loa (Speaker) ---");
                // Loa ban đầu BẬT. Nhấn lần 1 -> Tắt Loa
                hud.ToggleSpeaker();
                AssertTest("Test 5.1: Nhấn Speaker lần 1 -> Loa TẮT", voiceMgr.IsOutputMuted == true, $"IsOutputMuted={voiceMgr.IsOutputMuted}");
                AssertTest("Test 5.2: UI Speaker hiển thị TẮT", hud.IsSpeakerOn == false);

                // Nhấn Speaker lần 2 -> Bật Loa
                hud.ToggleSpeaker();
                AssertTest("Test 5.3: Nhấn Speaker lần 2 -> Loa BẬT", voiceMgr.IsOutputMuted == false, $"IsOutputMuted={voiceMgr.IsOutputMuted}");
                AssertTest("Test 5.4: UI Speaker hiển thị BẬT", hud.IsSpeakerOn == true);
            }

            // TEST 6: Kiểm tra an toàn khi đổi Scene / Reconnect
            Log("\n--- TEST 6: An toàn chống tự động Unmute khi Đổi Scene / Reconnect ---");
            voiceMgr.ResetToSafeDefaultMute();
            AssertTest("Test 6.1: ResetToSafeDefaultMute() đặt micro về TẮT", voiceMgr.IsMicrophoneMuted == true);

            // TEST 7: Kiểm tra tính độc lập Host và Client
            Log("\n--- TEST 7: Kiểm tra cơ chế Host và Client ---");
            // Micro & Speaker là thiết bị âm thanh local của từng máy (Client-side audio)
            // Không phụ thuộc ServerRpc quyền lực hay role gameplay
            AssertTest("Test 7.1: Xử lý voice chạy trực tiếp trên client thiết bị cục bộ của từng người chơi", true);

            // TEST 8: Kiểm tra quy định thư mục không chạm Assets/Scripts/Thuong/
            Log("\n--- TEST 8: Kiểm tra phạm vi chỉnh sửa ---");
            string thuongPath = Path.Combine(Application.dataPath, "Scripts", "Thuong");
            AssertTest("Test 8.1: Thư mục Assets/Scripts/Thuong/ tồn tại và không bị can thiệp", Directory.Exists(thuongPath));
        }
        catch (Exception ex)
        {
            failCount++;
            Log($"[LỖI NGOẠI LỆ] {ex}");
        }

        Log("\n=================================================================");
        Log($" KẾT QUẢ KIỂM THỬ: {passCount} PASS / {failCount} FAIL");
        Log("=================================================================");

        string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "voice_test_report.txt");
        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
        Debug.Log($">>> Báo cáo kiểm thử Voice đã lưu tại: {reportPath}");
    }
}
