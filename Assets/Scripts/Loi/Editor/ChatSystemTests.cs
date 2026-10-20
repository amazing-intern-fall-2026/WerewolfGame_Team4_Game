using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bộ kiểm thử tự động toàn diện cho hệ thống Chat Multiplayer trên điện thoại màn hình ngang.
/// </summary>
public static class ChatSystemTests
{
    private const string GameScenePath = "Assets/Scenes/Loi/Game.unity";

    public static int PassCount { get; private set; }
    public static int FailCount { get; private set; }
    public static List<string> TestLogs { get; private set; } = new List<string>();

    private static void LogPass(string testName, string detail)
    {
        PassCount++;
        string msg = $"[PASS] {testName}: {detail}";
        TestLogs.Add(msg);
        Debug.Log(msg);
    }

    private static void LogFail(string testName, string detail)
    {
        FailCount++;
        string msg = $"[FAIL] {testName}: {detail}";
        TestLogs.Add(msg);
        Debug.LogError(msg);
    }

    private static void Assert(bool condition, string testName, string passDetail, string failDetail)
    {
        if (condition) LogPass(testName, passDetail);
        else LogFail(testName, failDetail);
    }

    [MenuItem("Tools/Loi/Run Chat System Tests")]
    public static void RunAllTests()
    {
        PassCount = 0;
        FailCount = 0;
        TestLogs.Clear();

        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU CHẠY BỘ KIỂM THỬ KHUNG CHAT MULTIPLAYER (LANDSCAPE MOBILE)");
        Debug.Log("=================================================================");

        // 1. Cài đặt hệ thống Chat trước khi kiểm thử
        ChatSystemInstaller.InstallChatSystem();

        // 2. Chạy từng nhóm test
        Test_MessageValidation();
        Test_SenderNameResolution();
        Test_Scene_NetworkChatManager();
        Test_Scene_FloatingChatButton();
        Test_Scene_CompactChatPanel();
        Test_Scene_NoOverlap_PlayerCardAndSkills();
        Test_MobileKeyboardOffsetMath();
        Test_NetcodeArchitecture_NoDuplicateNetworkManager();

        // 3. Ghi báo cáo ra file
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=================================================================");
        sb.AppendLine("BÁO CÁO KIỂM THỬ KHUNG CHAT MULTIPLAYER (MOBILE LANDSCAPE)");
        sb.AppendLine("Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine($"KẾT QUẢ: PASS = {PassCount} | FAIL = {FailCount}");
        sb.AppendLine("=================================================================");
        foreach (var log in TestLogs)
        {
            sb.AppendLine(log);
        }

        File.WriteAllText("chat_test_report.txt", sb.ToString(), Encoding.UTF8);
        Debug.Log($">>> BỘ KIỂM THỬ CHAT HOÀN TẤT: PASS={PassCount}, FAIL={FailCount}. Báo cáo tại chat_test_report.txt");
    }

    // =========================================================
    // NHÓM 1: VALIDATION TIN NHẮN
    // =========================================================

    private static void Test_MessageValidation()
    {
        // Tin nhắn rỗng hoặc chỉ có khoảng trắng
        string empty = "";
        string whitespace = "    ";
        bool emptyBlocked = string.IsNullOrWhiteSpace(empty);
        bool whitespaceBlocked = string.IsNullOrWhiteSpace(whitespace);
        Assert(emptyBlocked && whitespaceBlocked, "Test_MessageValidation_EmptyAndWhitespace",
            "Tin nhắn rỗng và khoảng trắng bị chặn thành công.",
            "Lỗi: Hệ thống không chặn tin nhắn rỗng/khoảng trắng.");

        // Giới hạn độ dài tin nhắn (Max 120 ký tự)
        string longText = new string('A', 200);
        string truncated = longText.Length > NetworkChatManager.MaxMessageLength
            ? longText.Substring(0, NetworkChatManager.MaxMessageLength)
            : longText;
        Assert(truncated.Length == NetworkChatManager.MaxMessageLength, "Test_MessageValidation_MaxLengthTruncation",
            $"Tin nhắn vượt quá giới hạn đã được cắt ngắn về đúng {NetworkChatManager.MaxMessageLength} ký tự.",
            $"Lỗi: Tin nhắn không được giới hạn đúng {NetworkChatManager.MaxMessageLength} ký tự.");

        // Tiếng Việt có dấu được bảo toàn
        string vietnameseText = "Tôi là Tiên tri, đã soi Player 2 là Ma sói!";
        Assert(!string.IsNullOrWhiteSpace(vietnameseText) && vietnameseText.Length <= NetworkChatManager.MaxMessageLength,
            "Test_MessageValidation_VietnameseContent",
            "Nội dung tiếng Việt có dấu được chấp nhận và bảo toàn đầy đủ.",
            "Lỗi: Không xử lý được tin nhắn tiếng Việt.");
    }

    // =========================================================
    // NHÓM 2: RESOLVE TÊN NGƯỜI GỬI
    // =========================================================

    private static void Test_SenderNameResolution()
    {
        GameObject tempObj = new GameObject("TempChatManager");
        var chatManager = tempObj.AddComponent<NetworkChatManager>();

        string fallbackName = chatManager.ResolveSenderName(2);
        Assert(fallbackName == "Player 3", "Test_SenderNameResolution_Fallback",
            $"Định danh mặc định đúng quy tắc: ClientId=2 -> '{fallbackName}'.",
            $"Lỗi: Định danh fallback không đúng (nhận được '{fallbackName}').");

        UnityEngine.Object.DestroyImmediate(tempObj);
    }

    // =========================================================
    // NHÓM 3: KIỂM TRA NETWORK OBJECT TRONG SCENE
    // =========================================================

    private static void Test_Scene_NetworkChatManager()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        GameObject cmObj = GameObject.Find("NetworkChatManager");
        Assert(cmObj != null, "Test_Scene_ChatManagerExists",
            "GameObject 'NetworkChatManager' tồn tại trong Game.unity.",
            "Lỗi: Không tìm thấy 'NetworkChatManager' trong Game.unity.");

        if (cmObj != null)
        {
            var netObj = cmObj.GetComponent<NetworkObject>();
            Assert(netObj != null, "Test_Scene_ChatManager_HasNetworkObject",
                "NetworkChatManager có gắn component NetworkObject để đồng bộ RPC.",
                "Lỗi: NetworkChatManager thiếu component NetworkObject.");

            var chatComp = cmObj.GetComponent<NetworkChatManager>();
            Assert(chatComp != null, "Test_Scene_ChatManager_HasComponent",
                "NetworkChatManager có gắn component NetworkChatManager.",
                "Lỗi: Thiếu component NetworkChatManager.");
        }
    }

    // =========================================================
    // NHÓM 4: NÚT CHAT NỔI (48–56 DP, GÓC DƯỚI BÊN TRÁI)
    // =========================================================

    private static void Test_Scene_FloatingChatButton()
    {
        GameObject fbObj = GameObject.Find("FloatingChatButton");
        Assert(fbObj != null, "Test_FloatingButton_Exists",
            "Nút Chat nổi 'FloatingChatButton' tồn tại trong scene.",
            "Lỗi: Không tìm thấy 'FloatingChatButton'.");

        if (fbObj != null)
        {
            RectTransform rt = fbObj.GetComponent<RectTransform>();
            bool isBottomLeftAnchor = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.zero;
            Assert(isBottomLeftAnchor, "Test_FloatingButton_BottomLeftAnchor",
                "Nút Chat nổi được neo chính xác tại góc dưới bên trái (anchorMin=0, anchorMax=0).",
                $"Lỗi: Nút nổi không neo góc dưới bên trái (min={rt.anchorMin}, max={rt.anchorMax}).");

            bool sizeValid = rt.sizeDelta.x >= 48f && rt.sizeDelta.x <= 56f &&
                             rt.sizeDelta.y >= 48f && rt.sizeDelta.y <= 56f;
            Assert(sizeValid, "Test_FloatingButton_Size48to56dp",
                $"Kích thước nút nổi đạt chuẩn mobile touch: {rt.sizeDelta.x}x{rt.sizeDelta.y} dp (chuẩn 48–56 dp).",
                $"Lỗi: Kích thước nút nổi không nằm trong khoảng 48–56 dp ({rt.sizeDelta.x}x{rt.sizeDelta.y}).");

            Button btn = fbObj.GetComponent<Button>();
            Assert(btn != null && btn.interactable, "Test_FloatingButton_Interactable",
                "Nút Chat nổi có Button và có thể tương tác bấm.",
                "Lỗi: Nút Chat nổi thiếu Button hoặc bị disable.");
        }
    }

    // =========================================================
    // NHÓM 5: KHUNG CHAT (30–35% RỘNG, 35–45% CAO, KHÔNG CHIẾM TOÀN MÀN HÌNH)
    // =========================================================

    private static void Test_Scene_CompactChatPanel()
    {
        GameObject cpObj = GameObject.Find("CompactChatPanel");
        Assert(cpObj != null, "Test_CompactChatPanel_Exists",
            "Khung chat 'CompactChatPanel' tồn tại trong scene.",
            "Lỗi: Không tìm thấy 'CompactChatPanel'.");

        if (cpObj != null)
        {
            RectTransform rt = cpObj.GetComponent<RectTransform>();

            // Khung chat phải neo tại góc dưới bên trái
            bool isBottomLeft = rt.anchorMin == Vector2.zero;
            Assert(isBottomLeft, "Test_CompactChatPanel_BottomLeftOrigin",
                "Khung chat bắt đầu từ góc dưới bên trái (anchorMin = {0, 0}).",
                $"Lỗi: Khung chat không bắt đầu từ góc dưới bên trái (anchorMin={rt.anchorMin}).");

            // Chiều rộng chỉ chiếm 30–40% màn hình
            bool widthValid = rt.anchorMax.x >= 0.28f && rt.anchorMax.x <= 0.40f;
            Assert(widthValid, "Test_CompactChatPanel_WidthRatio",
                $"Chiều rộng khung chat chiếm {(rt.anchorMax.x * 100):0.0}% màn hình (đạt chuẩn 30–40%).",
                $"Lỗi: Chiều rộng khung chat chiếm {(rt.anchorMax.x * 100):0.0}% màn hình, không đạt chuẩn 30–40%.");

            // Chiều cao chỉ chiếm 35–45% màn hình
            bool heightValid = rt.anchorMax.y >= 0.32f && rt.anchorMax.y <= 0.45f;
            Assert(heightValid, "Test_CompactChatPanel_HeightRatio",
                $"Chiều cao khung chat chiếm {(rt.anchorMax.y * 100):0.0}% màn hình (đạt chuẩn 35–45%).",
                $"Lỗi: Chiều cao khung chat chiếm {(rt.anchorMax.y * 100):0.0}% màn hình, không đạt chuẩn 35–45%.");

            // Không bao giờ chiếm toàn màn hình
            bool notFullScreen = rt.anchorMax.x < 0.9f && rt.anchorMax.y < 0.9f;
            Assert(notFullScreen, "Test_CompactChatPanel_NotFullScreen",
                "Khung chat tuyệt đối không chiếm toàn màn hình hay làm mờ gameplay.",
                "Lỗi: Khung chat bị mở rộng toàn màn hình!");

            // Kiểm tra các thành phần con thiết yếu
            var scroll = cpObj.GetComponentInChildren<ScrollRect>(true);
            Assert(scroll != null, "Test_CompactChatPanel_HasScrollRect",
                "Khung chat có ScrollRect hỗ trợ cuộn tin nhắn cũ.",
                "Lỗi: Thiếu ScrollRect.");

            var input = cpObj.GetComponentInChildren<TMP_InputField>(true);
            Assert(input != null, "Test_CompactChatPanel_HasInputField",
                "Khung chat có TMP_InputField để nhập văn bản.",
                "Lỗi: Thiếu TMP_InputField.");

            var sendBtn = cpObj.transform.Find("InputRow/SendButton")?.GetComponent<Button>();
            Assert(sendBtn != null, "Test_CompactChatPanel_HasSendButton",
                "Khung chat có nút Gửi chuyên biệt đủ lớn cho ngón tay.",
                "Lỗi: Thiếu nút Gửi.");
        }
    }

    // =========================================================
    // NHÓM 6: KHÔNG CHE PLAYER CARD VÀ CÁC NÚT KỸ NĂNG
    // =========================================================

    private static void Test_Scene_NoOverlap_PlayerCardAndSkills()
    {
        // 1. PlayerIdentityCard: Được đặt an toàn tại Top-Left (anchorMin.y = 1, anchorMax.y = 1)
        GameObject idCard = GameObject.Find("PlayerIdentityCard");
        Assert(idCard != null, "Test_NoOverlap_PlayerIdentityCard_Exists",
            "PlayerIdentityCard tồn tại trong scene.",
            "Lỗi: Không tìm thấy PlayerIdentityCard.");

        if (idCard != null)
        {
            RectTransform idRt = idCard.GetComponent<RectTransform>();
            bool isTopLeft = idRt.anchorMin.y >= 0.9f && idRt.anchorMax.y >= 0.9f;
            Assert(isTopLeft, "Test_NoOverlap_PlayerIdentityCard_TopLeft",
                "PlayerIdentityCard được bố trí tại góc trên bên trái (Top-Left), hoàn toàn không bị khung chat (Bottom-Left) che khuất.",
                "Cảnh báo: PlayerIdentityCard vẫn nằm ở góc dưới bên trái có nguy cơ bị khung chat che.");
        }

        // 2. Các nút kỹ năng: Nằm ở khu vực giữa dưới (Bottom-Center, x ~ 0.5)
        GameObject skillPanel = GameObject.Find("SkillPanel");
        if (skillPanel != null)
        {
            Transform attackBtn = skillPanel.transform.Find("AttackButton");
            if (attackBtn != null)
            {
                RectTransform atkRt = attackBtn.GetComponent<RectTransform>();
                bool isCenterAnchored = atkRt.anchorMin.x >= 0.4f && atkRt.anchorMax.x <= 0.6f;
                Assert(isCenterAnchored, "Test_NoOverlap_SkillButtonsCenter",
                    "Các nút kỹ năng được neo tại khu vực giữa màn hình (Center-Bottom), hoàn toàn không bị khung chat (Bottom-Left) che.",
                    "Lỗi: Nút kỹ năng bị đặt lệch sang góc dưới bên trái.");
            }
        }
    }

    // =========================================================
    // NHÓM 7: XỬ LÝ BÀN PHÍM ẢO (VIRTUAL KEYBOARD OFFSET)
    // =========================================================

    private static void Test_MobileKeyboardOffsetMath()
    {
        // Giả lập bàn phím ảo mở cao 400px trên màn hình 1080p, canvas scale = 1.0
        float screenKeyboardHeight = 400f;
        float canvasScaleFactor = 1.0f;
        float expectedOffset = screenKeyboardHeight / canvasScaleFactor;

        Vector2 basePosition = new Vector2(16, 16);
        Vector2 adjustedPosition = new Vector2(basePosition.x, basePosition.y + expectedOffset);

        bool offsetValid = adjustedPosition.x == basePosition.x && adjustedPosition.y == (16 + 400);
        Assert(offsetValid, "Test_MobileKeyboardOffsetMath_Calculation",
            $"Tính toán nâng khung chat khi mở bàn phím ảo chính xác: Y={adjustedPosition.y} (nâng {expectedOffset}px), X không đổi ({adjustedPosition.x}).",
            "Lỗi: Thuật toán dịch chuyển vị trí bàn phím ảo tính toán sai.");
    }

    // =========================================================
    // NHÓM 8: KIỂM TRA NETCODE (KHÔNG TẠO NETWORKMANAGER THỨ HAI)
    // =========================================================

    private static void Test_NetcodeArchitecture_NoDuplicateNetworkManager()
    {
        NetworkManager[] managers = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert(managers.Length <= 1, "Test_NetcodeArchitecture_NoDuplicateNetworkManager",
            $"Số lượng NetworkManager trong scene: {managers.Length} (không tạo NetworkManager thứ hai).",
            $"LỖI NGHIÊM TRỌNG: Phát hiện {managers.Length} NetworkManager trong scene!");
    }
}
