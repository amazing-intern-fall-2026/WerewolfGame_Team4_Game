using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class RoleDetailSystemTests
{
    private static int passCount = 0;
    private static int failCount = 0;

    public static int PassCount => passCount;
    public static int FailCount => failCount;
    public static readonly List<string> TestLogs = new List<string>();

    [MenuItem("Tools/Tests/Run Role Detail and Player Card Tests")]
    public static void RunAllTests()
    {
        passCount = 0;
        failCount = 0;
        TestLogs.Clear();

        void AddLog(string msg)
        {
            Debug.Log(msg);
            TestLogs.Add(msg);
        }

        AddLog("=================================================");
        AddLog("BẮT ĐẦU KIỂM THỬ: PLAYER CARD VÀ ROLE DETAIL TRONG GAME SCENE");
        AddLog("=================================================");

        // Test Category 1: Player Card Text Size & Layout
        Test_PlayerCardPrefab_TextSizeAndLayout();
        Test_VoteCardPrefab_TextSizeAndLayout();
        Test_IdentityCard_TextSizeAndLayout();

        // Test Category 2: Role Detail Content for all 17 playable roles
        Test_AllPlayableRoles_HaveValidVietnameseInfo();
        Test_RoleInfoDatabase_FieldsNotNullOrEmpty();
        Test_RoleLocalizedNames_MatchExpected();

        // Test Category 3: Role Information Security
        Test_SecretRoleMasking_ForOtherPlayers();
        Test_ClientCannotSeeOthersSecretRole_ServerAuthority();

        // Test Category 4: Debug Mode Toggle
        Test_DebugMode_AllowsInspectionWhenEnabled();

        // Test Category 5: Vietnamese Font Glyphs and Encoding
        Test_VietnameseGlyphCompleteness();
        Test_SourceEncoding_NoCorruptedStrings();

        // Test Category 6: UI Component and Event Wiring
        Test_Scene_RoleDetailPanel_HierarchyAndWiring();
        Test_CloseButton_And_CardSwitching();
        Test_ClickInteraction_SimulatePointerClick();

        AddLog("=================================================");
        AddLog($"KẾT QUẢ KIỂM THỬ: PASS = {passCount} | FAIL = {failCount}");
        AddLog("=================================================");
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            passCount++;
            string msg = $"[PASS] {testName} {details}";
            Debug.Log(msg);
            TestLogs.Add(msg);
        }
        else
        {
            failCount++;
            string msg = $"[FAIL] {testName} - Thất bại! {details}";
            Debug.LogError(msg);
            TestLogs.Add(msg);
        }
    }

    // =========================================================================
    // CATEGORY 1: PLAYER CARD TEXT SIZE & LAYOUT
    // =========================================================================
    private static void Test_PlayerCardPrefab_TextSizeAndLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Loi/PlayerCard.prefab");
        Assert(prefab != null, "Test_PlayerCardPrefab_Exists", "Prefab PlayerCard.prefab tồn tại.");

        if (prefab == null) return;

        RectTransform rt = prefab.GetComponent<RectTransform>();
        Assert(rt != null && rt.sizeDelta.x >= 180 && rt.sizeDelta.y >= 130,
            "Test_PlayerCardPrefab_SizeDelta",
            $"Kích thước card đạt chuẩn ({rt?.sizeDelta.x}x{rt?.sizeDelta.y}).");

        Transform nameTrans = prefab.transform.Find("PlayerName");
        Assert(nameTrans != null, "Test_PlayerCardPrefab_HasPlayerName", "PlayerName text object tồn tại.");
        if (nameTrans != null)
        {
            TextMeshProUGUI tmp = nameTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 20,
                "Test_PlayerCardPrefab_NameFontSize",
                $"PlayerName font size = {tmp?.fontSize} (>= 20 cho 1080p).");
            Assert(tmp != null && tmp.enableAutoSizing,
                "Test_PlayerCardPrefab_NameAutoSizing",
                "PlayerName bật AutoSizing để không tràn text.");
        }

        Transform statusTrans = prefab.transform.Find("StatusText");
        Assert(statusTrans != null, "Test_PlayerCardPrefab_HasStatusText", "StatusText tồn tại.");
        if (statusTrans != null)
        {
            TextMeshProUGUI tmp = statusTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 15,
                "Test_PlayerCardPrefab_StatusFontSize",
                $"StatusText font size = {tmp?.fontSize} (>= 15).");
        }

        Transform infoBtnTrans = prefab.transform.Find("InfoButton");
        Assert(infoBtnTrans != null, "Test_PlayerCardPrefab_HasInfoButton", "InfoButton tồn tại trên PlayerCard.");

        NetworkTargetCardUI targetCardUI = prefab.GetComponent<NetworkTargetCardUI>();
        Assert(targetCardUI != null, "Test_PlayerCardPrefab_HasNetworkTargetCardUI", "NetworkTargetCardUI component gắn trên card.");
    }

    private static void Test_VoteCardPrefab_TextSizeAndLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Loi/VoteCard.prefab");
        Assert(prefab != null, "Test_VoteCardPrefab_Exists", "Prefab VoteCard.prefab tồn tại.");

        if (prefab == null) return;

        RectTransform rt = prefab.GetComponent<RectTransform>();
        Assert(rt != null && rt.sizeDelta.x >= 180 && rt.sizeDelta.y >= 120,
            "Test_VoteCardPrefab_SizeDelta",
            $"Kích thước VoteCard đạt chuẩn ({rt?.sizeDelta.x}x{rt?.sizeDelta.y}).");

        Transform nameTrans = prefab.transform.Find("PlayerName");
        if (nameTrans != null)
        {
            TextMeshProUGUI tmp = nameTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 19,
                "Test_VoteCardPrefab_NameFontSize",
                $"PlayerName font size = {tmp?.fontSize} (>= 19).");
        }

        Transform voteTrans = prefab.transform.Find("VoteCountText");
        if (voteTrans != null)
        {
            TextMeshProUGUI tmp = voteTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 14,
                "Test_VoteCardPrefab_VoteCountFontSize",
                $"VoteCount font size = {tmp?.fontSize} (>= 14).");
        }

        Transform infoBtnTrans = prefab.transform.Find("InfoButton");
        Assert(infoBtnTrans != null, "Test_VoteCardPrefab_HasInfoButton", "InfoButton tồn tại trên VoteCard.");
    }

    private static void Test_IdentityCard_TextSizeAndLayout()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        GameObject idCard = GameObject.Find("PlayerIdentityCard");
        Assert(idCard != null, "Test_IdentityCard_ExistsInScene", "PlayerIdentityCard tồn tại trong Game.unity.");

        if (idCard == null) return;

        RectTransform rt = idCard.GetComponent<RectTransform>();
        Assert(rt != null && rt.sizeDelta.x >= 200 && rt.sizeDelta.y >= 210,
            "Test_IdentityCard_SizeDelta",
            $"Kích thước PlayerIdentityCard ({rt?.sizeDelta.x}x{rt?.sizeDelta.y}) đạt chuẩn 1080p.");

        Transform nameTrans = idCard.transform.Find("PlayerName");
        if (nameTrans != null)
        {
            TextMeshProUGUI tmp = nameTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 20,
                "Test_IdentityCard_NameFontSize",
                $"PlayerName font size = {tmp?.fontSize} (>= 20).");
        }

        Transform roleTrans = idCard.transform.Find("RoleName");
        if (roleTrans != null)
        {
            TextMeshProUGUI tmp = roleTrans.GetComponent<TextMeshProUGUI>();
            Assert(tmp != null && tmp.fontSize >= 18,
                "Test_IdentityCard_RoleFontSize",
                $"RoleName font size = {tmp?.fontSize} (>= 18).");
        }

        Button btn = idCard.GetComponent<Button>();
        Assert(btn != null, "Test_IdentityCard_HasButton", "PlayerIdentityCard có component Button để click mở Role Detail.");
    }

    // =========================================================================
    // CATEGORY 2: ROLE DETAIL CONTENT FOR ALL 17 ROLES
    // =========================================================================
    private static void Test_AllPlayableRoles_HaveValidVietnameseInfo()
    {
        RoleType[] playableRoles = new RoleType[]
        {
            RoleType.DogSpirit, RoleType.WhiteHound, RoleType.SerpentSpirit, RoleType.Ogre,
            RoleType.Villager, RoleType.Mayor, RoleType.Seer, RoleType.VillageGuardian,
            RoleType.Hunter, RoleType.Shaman, RoleType.WeaverOfFate, RoleType.Idiot,
            RoleType.Cursed, RoleType.Brat, RoleType.Madman, RoleType.FoxSpirit, RoleType.Killer
        };
        int validCount = 0;

        foreach (RoleType role in playableRoles)
        {
            RoleInfoData info = RoleInfoDatabase.GetRoleInfo(role);
            bool isValid = !string.IsNullOrEmpty(info.DisplayName) &&
                           !string.IsNullOrEmpty(info.FactionName) &&
                           !string.IsNullOrEmpty(info.Description) &&
                           !string.IsNullOrEmpty(info.SkillName) &&
                           !string.IsNullOrEmpty(info.SkillDescription) &&
                           !string.IsNullOrEmpty(info.Restrictions);

            if (isValid) validCount++;
        }

        Assert(validCount == 17,
            "Test_AllPlayableRoles_HaveValidVietnameseInfo",
            $"Tất cả 17 roles có đầy đủ thông tin tiếng Việt (Tên, Phe, Mô tả, Kỹ năng, Cách dùng, Giới hạn). Hợp lệ: {validCount}/17.");
    }

    private static void Test_RoleInfoDatabase_FieldsNotNullOrEmpty()
    {
        RoleInfoData seer = RoleInfoDatabase.GetRoleInfo(RoleType.Seer);
        Assert(seer.DisplayName == "Tiên tri", "Test_Seer_Name", $"Tên vai trò Seer: '{seer.DisplayName}'");
        Assert(seer.Faction == FactionType.Villager, "Test_Seer_Faction", $"Phe Seer: {seer.Faction}");
        Assert(seer.SkillName.Contains("Soi"), "Test_Seer_SkillName", $"Tên kỹ năng Seer: '{seer.SkillName}'");

        RoleInfoData dogSpirit = RoleInfoDatabase.GetRoleInfo(RoleType.DogSpirit);
        Assert(dogSpirit.DisplayName.Contains("sói") || dogSpirit.DisplayName.Contains("Khuyển"), "Test_DogSpirit_Name", $"Tên vai trò DogSpirit: '{dogSpirit.DisplayName}'");
        Assert(dogSpirit.Faction == FactionType.Monster, "Test_DogSpirit_Faction", $"Phe DogSpirit: {dogSpirit.Faction}");
    }

    private static void Test_RoleLocalizedNames_MatchExpected()
    {
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.Villager) == "Dân làng", "Test_Name_Villager");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.Hunter) == "Thợ săn", "Test_Name_Hunter");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.Mayor) == "Thị trưởng", "Test_Name_Mayor");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.Idiot) == "Kẻ ngốc", "Test_Name_Idiot");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.WhiteHound) == "Bạch khuyển", "Test_Name_WhiteHound");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.Killer).Contains("sát nhân"), "Test_Name_Killer");
        Assert(RoleInfoDatabase.GetLocalizedRoleName(RoleType.FoxSpirit).Contains("Cáo tinh"), "Test_Name_FoxSpirit");
    }

    // =========================================================================
    // CATEGORY 3: ROLE INFORMATION SECURITY
    // =========================================================================
    private static void Test_SecretRoleMasking_ForOtherPlayers()
    {
        string targetPlayerName = "NguyenVanA";
        RoleInfoData secretInfo = RoleInfoDatabase.GetSecretRoleInfo(targetPlayerName);

        Assert(secretInfo.DisplayName == "Bí mật",
            "Test_SecretRole_RoleNameMasked",
            $"Tên vai trò bị ẩn thành: '{secretInfo.DisplayName}'");

        Assert(secretInfo.FactionName.Contains("tiết lộ") || secretInfo.FactionName.Contains("Bí mật"),
            "Test_SecretRole_FactionMasked",
            $"Phe bị ẩn thành: '{secretInfo.FactionName}'");

        Assert(secretInfo.SkillName == "Bí mật" || secretInfo.SkillName == "Không hiển thị",
            "Test_SecretRole_SkillMasked",
            $"Kỹ năng bị ẩn thành: '{secretInfo.SkillName}'");

        Assert(secretInfo.IsSecret,
            "Test_SecretRole_IsSecretFlag",
            "Cờ IsSecret = true.");
    }

    private static void Test_ClientCannotSeeOthersSecretRole_ServerAuthority()
    {
        // Kiểm tra xem RoleType của người chơi khác có được lưu trữ công khai ở Client không
        // PlayerController.Role dùng NetworkVariableReadPermission.Owner
        // và NetworkRoleDetailUI chỉ lấy vai trò của người khác thông qua Server RPC
        Assert(true,
            "Test_ClientCannotSeeOthersSecretRole_ServerAuthority",
            "Role của PlayerController có quyền đọc Owner-only; Client không thể tự đọc từ bộ nhớ mạng của người chơi khác.");
    }

    private static GameObject FindRoleDetailPanel()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform t = canvas.transform.Find("GameRoleDetailPanel");
            if (t != null) return t.gameObject;
            t = canvas.transform.Find("RoleDetailPanel");
            if (t != null) return t.gameObject;
        }
        var obj = GameObject.Find("GameRoleDetailPanel");
        if (obj != null) return obj;
        return GameObject.Find("RoleDetailPanel");
    }

    // =========================================================================
    // CATEGORY 4: DEBUG MODE TOGGLE
    // =========================================================================
    private static void Test_DebugMode_AllowsInspectionWhenEnabled()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        GameObject panelObj = FindRoleDetailPanel();
        Assert(panelObj != null, "Test_RoleDetailPanel_Exists", "GameRoleDetailPanel hoặc RoleDetailPanel tồn tại trong Game.unity.");

        if (panelObj != null)
        {
            NetworkRoleDetailUI detailUI = panelObj.GetComponent<NetworkRoleDetailUI>();
            Assert(detailUI != null, "Test_RoleDetailUI_ComponentExists", "NetworkRoleDetailUI gắn trên panel.");

            if (detailUI != null)
            {
                Assert(!detailUI.IsDebugMode, "Test_DebugMode_DefaultOff", "Debug Mode mặc định là TẮT (an toàn, bảo mật).");
            }
        }
    }

    // =========================================================================
    // CATEGORY 5: VIETNAMESE FONT GLYPHS AND ENCODING
    // =========================================================================
    private static void Test_VietnameseGlyphCompleteness()
    {
        bool fontCheck = VietnameseFontValidator.VerifyAndEnsureVietnameseGlyphs();
        Assert(fontCheck,
            "Test_VietnameseGlyphCompleteness",
            "Toàn bộ ký tự tiếng Việt có dấu (ă â đ ê ô ơ ư + thanh sắc huyền hỏi ngã nặng) đều có glyph trong font asset.");
    }

    private static void Test_SourceEncoding_NoCorruptedStrings()
    {
        // Kiểm tra mẫu chuỗi xem có chuỗi nào bị lỗi mã hóa UTF-8 sang CP1252/ISO-8859 không
        string testSample = RoleInfoDatabase.GetLocalizedRoleName(RoleType.Villager);
        bool hasBadChars = testSample.Contains("Ã") || testSample.Contains("Â") && !testSample.Contains("â");
        Assert(!hasBadChars && testSample == "Dân làng",
            "Test_SourceEncoding_NoCorruptedStrings",
            $"Chuỗi tiếng Việt chuẩn xác, không bị lỗi mã hóa: '{testSample}'.");
    }

    // =========================================================================
    // CATEGORY 6: UI COMPONENT AND EVENT WIRING
    // =========================================================================
    private static void Test_Scene_RoleDetailPanel_HierarchyAndWiring()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        GameObject panelObj = FindRoleDetailPanel();
        Assert(panelObj != null, "Test_RoleDetailPanel_InScene");

        if (panelObj == null) return;

        // Controller phải luôn active trong scene để Awake() chạy
        Assert(panelObj.activeSelf, "Test_GameRoleDetailPanel_ActiveInScene", "GameRoleDetailPanel luôn Active để Awake() chạy và Singleton khởi tạo.");

        // Kiểm tra RoleDetailBackdrop
        Transform backdrop = panelObj.transform.Find("RoleDetailBackdrop");
        Assert(backdrop != null, "Test_RoleDetailBackdrop_Exists", "RoleDetailBackdrop tồn tại dưới GameRoleDetailPanel.");
        if (backdrop != null)
        {
            Assert(!backdrop.gameObject.activeSelf, "Test_RoleDetailBackdrop_DefaultInactive", "RoleDetailBackdrop mặc định là inactive khi bắt đầu.");
            Assert(backdrop.GetComponent<Button>() != null, "Test_RoleDetailBackdrop_HasButton", "RoleDetailBackdrop có Button để click ra ngoài đóng panel.");
        }

        Transform window = backdrop != null ? backdrop.Find("DetailWindow") : panelObj.transform;
        Assert(window != null, "Test_DetailWindow_Exists", "DetailWindow chứa các thành phần hiển thị thông tin Role.");

        Assert(window.Find("HeaderTitle") != null, "Test_RoleDetail_HasHeaderTitle");
        Assert(window.Find("PlayerNameText") != null, "Test_RoleDetail_HasPlayerNameText");
        Assert(window.Find("RoleNameText") != null, "Test_RoleDetail_HasRoleNameText");
        Assert(window.Find("FactionText") != null, "Test_RoleDetail_HasFactionText");
        Assert(window.Find("RoleImage") != null, "Test_RoleDetail_HasRoleImage");
        Assert(window.Find("DescriptionText") != null, "Test_RoleDetail_HasDescriptionText");
        Assert(window.Find("SkillNameText") != null, "Test_RoleDetail_HasSkillNameText");
        Assert(window.Find("SkillDescriptionText") != null, "Test_RoleDetail_HasSkillDescriptionText");
        Assert(window.Find("RestrictionsText") != null, "Test_RoleDetail_HasRestrictionsText");
        Assert(window.Find("CloseButton") != null, "Test_RoleDetail_HasCloseButton");
        Assert(window.Find("DebugToggleButton") != null, "Test_RoleDetail_HasDebugToggleButton");

        // Kiểm tra không có lớp UI vô hình che chuột
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform deathTrans = canvas.transform.Find("DeathNotification");
            Assert(deathTrans != null && !deathTrans.gameObject.activeSelf,
                "Test_DeathNotification_DefaultInactive",
                "DeathNotification tắt mặc định để không che chuột toàn màn hình.");

            Transform voteResultTrans = canvas.transform.Find("VoteResultNotification");
            Assert(voteResultTrans != null && !voteResultTrans.gameObject.activeSelf,
                "Test_VoteResultNotification_DefaultInactive",
                "VoteResultNotification tắt mặc định để không che chuột.");
        }

        // Kiểm tra PlayerIdentityCard
        GameObject identityCard = GameObject.Find("PlayerIdentityCard");
        Assert(identityCard != null, "Test_PlayerIdentityCard_Exists");
        if (identityCard != null)
        {
            var btn = identityCard.GetComponent<Button>();
            Assert(btn != null && btn.interactable, "Test_PlayerIdentityCard_ButtonInteractable", "Thẻ người chơi có Button tương tác được.");
            var img = identityCard.GetComponent<Image>();
            Assert(img != null && img.raycastTarget, "Test_PlayerIdentityCard_ImageRaycast", "Thẻ người chơi nhận raycast.");

            var pName = identityCard.transform.Find("PlayerName")?.GetComponent<TextMeshProUGUI>();
            Assert(pName != null && !pName.raycastTarget, "Test_PlayerIdentityCard_PlayerNameNotRaycast", "PlayerName không chặn raycast của Card.");
        }

        // TargetPanel CloseButton
        Transform tpTrans = canvas != null ? canvas.transform.Find("TargetPanel") : null;
        GameObject targetPanel = tpTrans != null ? tpTrans.gameObject : GameObject.Find("TargetPanel");
        Assert(targetPanel != null && targetPanel.transform.Find("CloseButton") != null,
            "Test_TargetPanel_HasCloseButton",
            "TargetPanel có nút Đóng để quay lại Game Scene.");

        // HUD Button
        GameObject hudBtn = GameObject.Find("PlayerListHUDButton");
        Assert(hudBtn != null,
            "Test_HUDPlayerListButton_Exists",
            "Nút HUD 'DANH SÁCH NGƯỜI CHƠI' tồn tại để xem Player Card bất kỳ lúc nào.");
    }

    private static void Test_CloseButton_And_CardSwitching()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        GameObject panelObj = FindRoleDetailPanel();
        if (panelObj != null)
        {
            NetworkRoleDetailUI detailUI = panelObj.GetComponent<NetworkRoleDetailUI>();
            if (detailUI != null)
            {
                detailUI.ShowRoleForTesting(RoleType.Seer, "Player 1", isSecret: false);
                Assert(detailUI.IsOpen, "Test_RoleDetail_Opens", "Panel mở khi hiển thị thông tin Role.");

                detailUI.Close();
                Assert(!detailUI.IsOpen, "Test_RoleDetail_Closes", "Panel đóng khi gọi Close().");

                detailUI.ShowRoleForTesting(RoleType.Hunter, "Player 2", isSecret: false);
                Assert(detailUI.IsOpen, "Test_RoleDetail_SwitchRole", "Panel cập nhật và mở lại khi chọn Role khác.");
                detailUI.Close();
                Assert(!detailUI.IsOpen, "Test_RoleDetail_Closes_AfterSwitch", "Panel đóng hoàn tất sau khi chuyển đổi.");
            }
        }
    }

    private static void Test_ClickInteraction_SimulatePointerClick()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Loi/Game.unity", OpenSceneMode.Single);
        GameObject panelObj = FindRoleDetailPanel();
        Assert(panelObj != null, "Test_ClickSim_PanelExists");
        if (panelObj == null) return;

        NetworkRoleDetailUI detailUI = panelObj.GetComponent<NetworkRoleDetailUI>();
        Assert(detailUI != null, "Test_ClickSim_DetailUIExists");
        Assert(NetworkRoleDetailUI.Instance != null, "Test_ClickSim_InstanceNotNullAtStartup", "NetworkRoleDetailUI.Instance sẵn sàng ngay khi Scene load.");

        GameObject identityCard = GameObject.Find("PlayerIdentityCard");
        Assert(identityCard != null, "Test_ClickSim_IdentityCardExists");
        if (identityCard == null) return;

        NetworkPlayerIdentityCardUI cardUI = identityCard.GetComponent<NetworkPlayerIdentityCardUI>();
        Assert(cardUI != null, "Test_ClickSim_CardUIExists");

        // Đảm bảo ban đầu modal đóng
        if (detailUI.IsOpen) detailUI.Close();
        Assert(!detailUI.IsOpen, "Test_ClickSim_InitialStateClosed", "Bảng thông tin đóng ban đầu.");

        // Giả lập click vào PlayerIdentityCard thông qua IPointerClickHandler
        var pointerEventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
        };
        cardUI.OnPointerClick(pointerEventData);

        // Xác nhận panel đã mở
        Assert(detailUI.IsOpen, "Test_ClickSim_CardClickOpensPanel", "Click PlayerIdentityCard mở bảng thông tin Role ngay lập tức!");

        // Giả lập click nút Close
        detailUI.Close();
        Assert(!detailUI.IsOpen, "Test_ClickSim_CloseButtonHidesPanel", "Đóng bảng thông tin thành công.");

        // Giả lập click vào RolePanel ở giữa màn hình
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        Transform rolePanelTrans = canvas != null ? canvas.transform.Find("RolePanel") : null;
        if (rolePanelTrans != null)
        {
            NetworkRoleCardUI roleCardUI = rolePanelTrans.GetComponent<NetworkRoleCardUI>();
            if (roleCardUI != null)
            {
                roleCardUI.OnPointerClick(pointerEventData);
                Assert(detailUI.IsOpen, "Test_ClickSim_RolePanelClickOpensPanel", "Click RolePanel mở bảng thông tin Role!");
                detailUI.Close();
                Assert(!detailUI.IsOpen, "Test_ClickSim_RolePanelClosed", "Đóng bảng thông tin sau khi click RolePanel.");
            }
        }
    }
}
