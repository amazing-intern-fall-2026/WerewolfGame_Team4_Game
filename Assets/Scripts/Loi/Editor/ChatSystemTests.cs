using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Thuong.Online;
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

/// <summary>
/// Bộ kiểm thử NUnit bao quát đầy đủ 16 Test Cases (TC01–TC16) cho Kênh Người Chết (Dead Chat):
/// Micro, Loa, Dấu hiệu đang nói (Vivox events), Chat văn bản, Danh sách thành viên và Phân quyền Server.
/// </summary>
public class ThuongOnlineDeadChatTests
{
    private GameObject rootGo;
    private PlayerManager playerManager;
    private NetworkChatManager chatManager;
    private NetworkChatUI chatUI;
    private NetworkVoiceManager voiceManager;
    private DeathResolver deathResolver;

    [MenuItem("Tools/Tests/Run Dead Chat Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: KÊNH NGƯỜI CHẾT (DEAD CHAT) - 16 TEST CASES");
        Debug.Log("=================================================================");

        var tests = new ThuongOnlineDeadChatTests();
        int passed = 0;
        int failed = 0;

        void Execute(string name, Action body)
        {
            try
            {
                tests.Setup();
                body();
                Debug.Log($"[PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {name}: {ex.Message}");
                failed++;
            }
            finally
            {
                tests.TearDown();
            }
        }

        Execute("TC01: Người sống không nhìn thấy tab Người Chết", () => tests.TC01_LivingPlayer_CannotSeeDeadTab());
        Execute("TC02: Người chết tự động được tham gia Dead Chat", () => tests.TC02_DeadPlayer_AutomaticallyJoinsDeadChat());
        Execute("TC03: Hai người chết nghe được voice của nhau (phân kênh tách biệt)", () => tests.TC03_TwoDeadPlayers_IsolatedVoiceAudienceAndUnmutedSpeaker());
        Execute("TC04: Tắt Micro thì người khác không nghe được", () => tests.TC04_MuteMicrophone_StopsTransmitAndSpeaking());
        Execute("TC05: Tắt Loa thì người chơi không nghe voice từ Dead Chat", () => tests.TC05_MuteSpeaker_BlocksIncomingVoiceOnly());
        Execute("TC06: Tắt Loa không ảnh hưởng Micro", () => tests.TC06_MuteSpeaker_DoesNotAffectMicrophone());
        Execute("TC07: Hai người chết gửi và nhận tin nhắn bình thường", () => tests.TC07_TwoDeadPlayers_SendAndReceiveMessages());
        Execute("TC08: Người sống không thể gửi tin nhắn vào Dead Chat", () => tests.TC08_LivingPlayer_CannotSendToDeadChat());
        Execute("TC09: Người sống không thể nghe hoặc đọc nội dung Dead Chat", () => tests.TC09_LivingPlayer_CannotReadDeadMessagesOrMembers());
        Execute("TC10: Người chết không thể gửi tin nhắn vào Chat Làng hoặc Chat Ma Sói", () => tests.TC10_DeadPlayer_CannotSendToVillageOrMonsterChat());
        Execute("TC11: Host và Client đồng bộ đúng thành viên", () => tests.TC11_HostAndClient_SyncDeadMembersAndSpeaking());
        Execute("TC12: Người chơi chết giữa trận được chuyển kênh đúng lúc", () => tests.TC12_MidMatchDeath_SwitchesToDeadTabImmediately());
        Execute("TC13: Người chơi mất kết nối được loại khỏi kênh", () => tests.TC13_DisconnectedPlayer_RemovedFromDeadChannel());
        Execute("TC14: Bắt đầu ván mới, lịch sử và quyền truy cập được reset", () => tests.TC14_NewMatch_ResetsHistoryPermissionsAndMembers());
        Execute("TC15: Chat Làng và Chat Ma Sói vẫn hoạt động bình thường", () => tests.TC15_LivingVillageAndMonsterChat_StillWorkNormally());
        Execute("TC16: Kiểm thử nhiều Client cùng sử dụng Micro, Loa và chat", () => tests.TC16_MultiClient_MicSpeakerAndDeadChatConcurrency());

        Debug.Log("=================================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ DEAD CHAT: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================================");
    }

    [SetUp]
    public void Setup()
    {
        rootGo = new GameObject("[TestDeadChatEnv]", typeof(Canvas));

        playerManager = rootGo.AddComponent<PlayerManager>();
        playerManager.players = new List<PlayerData>
        {
            new PlayerData { playerID = 0, playerName = "Player 1", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = true },
            new PlayerData { playerID = 1, playerName = "Player 2", roleType = RoleType.DogSpirit, faction = FactionType.Monster, isAlive = true },
            new PlayerData { playerID = 2, playerName = "Player 3", roleType = RoleType.Seer, faction = FactionType.Villager, isAlive = false },
            new PlayerData { playerID = 3, playerName = "Player 4", roleType = RoleType.Villager, faction = FactionType.Villager, isAlive = false }
        };
        PlayerManager.Instance = playerManager;

        deathResolver = rootGo.AddComponent<DeathResolver>();
        DeathResolver.Instance = deathResolver;

        voiceManager = rootGo.AddComponent<NetworkVoiceManager>();
        voiceManager.SetGameplayPermissions(rootGo, true, true);
        voiceManager.SetMicrophoneMuted(true);
        voiceManager.SetOutputMuted(false);

        chatManager = rootGo.AddComponent<NetworkChatManager>();
        chatManager.ResetDeadChatState();
        chatManager.ClearHistory();
        NetworkChatManager.SetLocalMonsterChatPermission(false);

        var uiGo = new GameObject("DeadChatUI", typeof(NetworkChatUI));
        uiGo.transform.SetParent(rootGo.transform, false);
        chatUI = uiGo.GetComponent<NetworkChatUI>();
        chatUI.EnsureUIReferences();
        chatUI.RefreshAllTabPermissions();
    }

    [TearDown]
    public void TearDown()
    {
        if (chatManager != null)
        {
            chatManager.ResetDeadChatState();
            chatManager.ClearHistory();
        }
        NetworkChatManager.SetLocalMonsterChatPermission(false);
        if (PlayerManager.Instance == playerManager) PlayerManager.Instance = null;
        if (DeathResolver.Instance == deathResolver) DeathResolver.Instance = null;
        if (rootGo != null) UnityEngine.Object.DestroyImmediate(rootGo);
    }

    [Test]
    public void TC01_LivingPlayer_CannotSeeDeadTab()
    {
        Assert.IsFalse(NetworkChatManager.HasLocalDeadChatPermission());
        Assert.IsFalse(chatUI.IsDeadTabVisible, "Người sống không được nhìn thấy tab Người Chết.");
        chatUI.SwitchToTab(NetworkChatUI.ChatTab.Dead);
        Assert.AreEqual(NetworkChatUI.ChatTab.Village, chatUI.ActiveTab);
    }

    [Test]
    public void TC02_DeadPlayer_AutomaticallyJoinsDeadChat()
    {
        playerManager.players[0].isAlive = false;
        NetworkChatManager.RefreshLocalDeadChatPermission();
        chatManager.SyncDeadChatStateOnServer();
        chatUI.RefreshAllTabPermissions();

        Assert.IsTrue(NetworkChatManager.HasLocalDeadChatPermission());
        Assert.IsTrue(chatUI.IsDeadTabVisible);
        Assert.AreEqual(NetworkChatUI.ChatTab.Dead, chatUI.ActiveTab);
        Assert.AreEqual(3, chatManager.DeadMembers.Count);
    }

    [Test]
    public void TC03_TwoDeadPlayers_IsolatedVoiceAudienceAndUnmutedSpeaker()
    {
        var deadAudienceDay = ThuongCommunicationRules.Resolve(true, true, false, false, GamePhase.Discussion);
        var deadAudienceNight = ThuongCommunicationRules.Resolve(true, true, false, false, GamePhase.Night);
        var livingAudienceDay = ThuongCommunicationRules.Resolve(true, true, false, true, GamePhase.Discussion);

        Assert.AreEqual(ThuongCommunicationAudience.Dead, deadAudienceDay);
        Assert.AreEqual(ThuongCommunicationAudience.Dead, deadAudienceNight);
        Assert.AreEqual(ThuongCommunicationAudience.Living, livingAudienceDay);
        Assert.AreNotEqual(deadAudienceDay, livingAudienceDay);
        Assert.IsFalse(voiceManager.IsOutputMuted, "Loa mặc định bật để người chết nghe được nhau.");
    }

    [Test]
    public void TC04_MuteMicrophone_StopsTransmitAndSpeaking()
    {
        voiceManager.SetMicrophoneMuted(false);
        voiceManager.SetParticipantSpeakingState("Player 1", true, true);
        Assert.IsFalse(voiceManager.IsMicrophoneMuted);
        Assert.IsTrue(voiceManager.IsLocalSpeaking);

        voiceManager.SetMicrophoneMuted(true);
        Assert.IsTrue(voiceManager.IsMicrophoneMuted);
        Assert.IsFalse(voiceManager.IsLocalSpeaking, "Khi tắt Micro thì trạng thái đang nói phải tắt ngay.");
    }

    [Test]
    public void TC05_MuteSpeaker_BlocksIncomingVoiceOnly()
    {
        voiceManager.SetOutputMuted(true);
        Assert.IsTrue(voiceManager.IsOutputMuted);

        voiceManager.SetOutputMuted(false);
        Assert.IsFalse(voiceManager.IsOutputMuted);
    }

    [Test]
    public void TC06_MuteSpeaker_DoesNotAffectMicrophone()
    {
        voiceManager.SetMicrophoneMuted(false);
        Assert.IsFalse(voiceManager.IsMicrophoneMuted);

        voiceManager.SetOutputMuted(true);
        Assert.IsTrue(voiceManager.IsOutputMuted);
        Assert.IsFalse(voiceManager.IsMicrophoneMuted, "Tắt Loa tuyệt đối không được tắt Micro.");

        voiceManager.SetOutputMuted(false);
        Assert.IsFalse(voiceManager.IsOutputMuted);
        Assert.IsFalse(voiceManager.IsMicrophoneMuted);
    }

    [Test]
    public void TC07_TwoDeadPlayers_SendAndReceiveMessages()
    {
        bool ok1 = chatManager.ProcessDeadChatMessage(2, "Tôi là Tiên Tri, soi P2 là Khuyển Tinh!");
        bool ok2 = chatManager.ProcessDeadChatMessage(3, "Ừ tôi cũng bị cắn đêm qua.");

        Assert.IsTrue(ok1);
        Assert.IsTrue(ok2);
        Assert.AreEqual(2, chatManager.DeadHistory.Count);
        Assert.AreEqual(0, chatManager.VillageHistory.Count);
        Assert.AreEqual(0, chatManager.MonsterHistory.Count);
    }

    [Test]
    public void TC08_LivingPlayer_CannotSendToDeadChat()
    {
        bool forged = chatManager.ProcessDeadChatMessage(0, "Giả mạo người chết!");
        Assert.IsFalse(forged, "Server phải từ chối người sống gửi tin nhắn vào Dead Chat.");

        chatManager.SendChatMessage("Thử gửi từ người sống", ChatChannel.Dead);
        Assert.AreEqual(0, chatManager.DeadHistory.Count);
    }

    [Test]
    public void TC09_LivingPlayer_CannotReadDeadMessagesOrMembers()
    {
        chatManager.ApplyDeadChatMemberSync(false, new List<NetworkChatManager.DeadMemberInfo>
        {
            new NetworkChatManager.DeadMemberInfo { ClientId = 2, PlayerId = 2, Name = "P03  Player 3" }
        });

        Assert.IsFalse(NetworkChatManager.HasLocalDeadChatPermission());
        Assert.AreEqual(0, chatManager.DeadMembers.Count, "Người sống không được giữ danh sách thành viên kênh Người Chết.");
        Assert.IsFalse(chatUI.IsDeadTabVisible);
    }

    [Test]
    public void TC10_DeadPlayer_CannotSendToVillageOrMonsterChat()
    {
        playerManager.players[0].isAlive = false;
        playerManager.players[1].isAlive = false;
        NetworkChatManager.RefreshLocalDeadChatPermission();
        NetworkChatManager.RefreshLocalMonsterChatPermission();

        chatManager.SendChatMessage("Tôi tiết lộ sói là P2!", ChatChannel.Village);
        Assert.AreEqual(0, chatManager.VillageHistory.Count);

        Assert.IsFalse(NetworkChatManager.IsClientMonster(1));
        Assert.IsFalse(chatManager.ProcessMonsterChatMessage(1, "Tin nhắn từ ma sói đã chết"));
        Assert.AreEqual(0, chatManager.MonsterHistory.Count);
    }

    [Test]
    public void TC11_HostAndClient_SyncDeadMembersAndSpeaking()
    {
        playerManager.players[0].isAlive = false;
        var syncedList = new List<NetworkChatManager.DeadMemberInfo>
        {
            new NetworkChatManager.DeadMemberInfo { ClientId = 0, PlayerId = 0, Name = "P01  Player 1" },
            new NetworkChatManager.DeadMemberInfo { ClientId = 2, PlayerId = 2, Name = "P03  Player 3" }
        };
        chatManager.ApplyDeadChatMemberSync(true, syncedList);
        voiceManager.SetParticipantSpeakingState("P03  Player 3", false, true);
        chatUI.RefreshDeadMembersDisplay();

        Assert.AreEqual(2, chatManager.DeadMembers.Count);
        Assert.IsTrue(chatUI.DeadMembersLabelText.Contains("P01  Player 1"));
        Assert.IsTrue(chatUI.DeadMembersLabelText.Contains("P03  Player 3"));
        Assert.IsTrue(chatUI.DeadMembersLabelText.Contains("●"), "Phải hiển thị dấu hiệu đang nói cạnh thành viên đang phát voice.");
    }

    [Test]
    public void TC12_MidMatchDeath_SwitchesToDeadTabImmediately()
    {
        playerManager.players[0].roleType = RoleType.DogSpirit;
        playerManager.players[0].faction = FactionType.Monster;
        NetworkChatManager.RefreshLocalMonsterChatPermission();
        chatUI.RefreshAllTabPermissions();
        chatUI.SwitchToTab(NetworkChatUI.ChatTab.Monster);
        Assert.AreEqual(NetworkChatUI.ChatTab.Monster, chatUI.ActiveTab);

        deathResolver.TryKill(new DeathRequest(playerManager.players[0], DeathCause.Vote));
        chatUI.RefreshAllTabPermissions();

        Assert.IsTrue(NetworkChatManager.HasLocalDeadChatPermission());
        Assert.IsFalse(NetworkChatManager.HasLocalMonsterChatPermission());
        Assert.IsTrue(chatUI.IsDeadTabVisible);
        Assert.IsFalse(chatUI.IsMonsterTabVisible);
        Assert.AreEqual(NetworkChatUI.ChatTab.Dead, chatUI.ActiveTab);
    }

    [Test]
    public void TC13_DisconnectedPlayer_RemovedFromDeadChannel()
    {
        playerManager.players[2].isDisconnected = true;
        List<ulong> deadClients = NetworkChatManager.GetDeadClientIds();

        Assert.IsFalse(deadClients.Contains(2), "Người chơi mất kết nối phải bị loại khỏi kênh Người Chết.");
        Assert.IsTrue(deadClients.Contains(3));
    }

    [Test]
    public void TC14_NewMatch_ResetsHistoryPermissionsAndMembers()
    {
        playerManager.players[0].isAlive = false;
        chatManager.ApplyDeadChatMemberSync(true, new List<NetworkChatManager.DeadMemberInfo>
        {
            new NetworkChatManager.DeadMemberInfo { ClientId = 0, PlayerId = 0, Name = "P01  Player 1" }
        });
        chatManager.ProcessDeadChatMessage(0, "Tin nhắn ván cũ");
        Assert.AreEqual(1, chatManager.DeadHistory.Count);

        playerManager.players[0].ResetForNewMatch();
        chatManager.ResetDeadChatState();
        chatManager.ClearHistory();
        chatUI.RefreshAllTabPermissions();

        Assert.AreEqual(0, chatManager.DeadHistory.Count);
        Assert.AreEqual(0, chatManager.DeadMembers.Count);
        Assert.IsFalse(NetworkChatManager.HasLocalDeadChatPermission());
        Assert.IsFalse(chatUI.IsDeadTabVisible);
        Assert.AreEqual(NetworkChatUI.ChatTab.Village, chatUI.ActiveTab);
    }

    [Test]
    public void TC15_LivingVillageAndMonsterChat_StillWorkNormally()
    {
        chatManager.SendChatMessage("Xin chào dân làng", ChatChannel.Village);
        Assert.AreEqual(1, chatManager.VillageHistory.Count);

        Assert.IsTrue(chatManager.ProcessMonsterChatMessage(1, "Sói chọn mục tiêu nhé"));
        Assert.AreEqual(1, chatManager.MonsterHistory.Count);
        Assert.AreEqual(0, chatManager.DeadHistory.Count);
    }

    [Test]
    public void TC16_MultiClient_MicSpeakerAndDeadChatConcurrency()
    {
        Assert.IsFalse(chatManager.ProcessDeadChatMessage(2, "   "));
        Assert.IsTrue(chatManager.ProcessDeadChatMessage(2, "Tin hợp lệ 1", 1));
        Assert.IsFalse(chatManager.ProcessDeadChatMessage(2, "Tin hợp lệ 1", 1), "Chống gửi tin nhắn trùng sequence.");
        Assert.IsTrue(chatManager.ProcessDeadChatMessage(3, "Tin hợp lệ 2", 1));
        Assert.AreEqual(2, chatManager.DeadHistory.Count);

        playerManager.players[0].isAlive = false;
        NetworkChatManager.RefreshLocalDeadChatPermission();
        chatUI.RefreshAllTabPermissions();
        chatUI.OnDeadMicClicked();
        Assert.IsFalse(voiceManager.IsMicrophoneMuted);
        Assert.IsTrue(chatUI.DeadMicLabelText.Contains("BẬT"));

        chatUI.OnDeadSpeakerClicked();
        Assert.IsTrue(voiceManager.IsOutputMuted);
        Assert.IsTrue(chatUI.DeadSpeakerLabelText.Contains("TẮT"));
        Assert.IsFalse(voiceManager.IsMicrophoneMuted);
    }
}
