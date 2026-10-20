using System;
using System.IO;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tool cài đặt và tinh chỉnh tự động hệ thống Chat Multiplayer nhỏ gọn vào Game.unity.
/// Tối ưu hoàn hảo cho màn hình ngang điện thoại:
/// - Khung chat tại góc dưới bên trái (~33% rộng x 40% cao).
/// - Nút Chat nổi 52 dp tại góc dưới bên trái khi thu gọn.
/// - Tinh chỉnh Player Card và các nút HUD tránh trùng lặp/che khuất.
/// - Đồng bộ hóa NetworkObject và NetworkChatManager với Netcode hiện tại.
/// </summary>
public static class ChatSystemInstaller
{
    private const string GameScenePath = "Assets/Scenes/Loi/Game.unity";
    private const string MainFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Loi/Install Compact Chat System")]
    public static void InstallChatSystem()
    {
        Debug.Log(">>> BẮT ĐẦU CÀI ĐẶT HỆ THỐNG CHAT MULTIPLAYER NHỎ GỌN CHO MOBILE LANDSCAPE <<<");

        // 1. Đảm bảo glyph tiếng Việt
        VietnameseFontValidator.VerifyAndEnsureVietnameseGlyphs();
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);

        // 2. Mở Scene Game.unity
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LỖI] Không tìm thấy Canvas trong Game.unity!");
            return;
        }

        // 3. Đảm bảo NetworkChatManager GameObject tồn tại trong Scene với NetworkObject
        GameObject chatManagerObj = GameObject.Find("NetworkChatManager");
        if (chatManagerObj == null)
        {
            chatManagerObj = new GameObject("NetworkChatManager");
            Undo.RegisterCreatedObjectUndo(chatManagerObj, "Create NetworkChatManager");
        }

        NetworkObject netObj = chatManagerObj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            netObj = chatManagerObj.AddComponent<NetworkObject>();
        }

        NetworkChatManager chatManager = chatManagerObj.GetComponent<NetworkChatManager>();
        if (chatManager == null)
        {
            chatManager = chatManagerObj.AddComponent<NetworkChatManager>();
        }

        // 4. Tinh chỉnh vị trí PlayerIdentityCard & PlayerListHUDButton sang Top-Left để KHÔNG BỊ KHUNG CHAT CHE
        AdjustPlayerCardLayout(canvas.transform, fontAsset);

        // 5. Tạo hoặc cập nhật UI Chat trong Canvas
        InstallChatUIElements(canvas.transform, fontAsset);

        // 6. Lưu Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(">>> CÀI ĐẶT HỆ THỐNG CHAT THÀNH CÔNG! ĐÃ LƯU SCENE GAME.UNITY <<<");
    }

    private static void AdjustPlayerCardLayout(Transform canvasTrans, TMP_FontAsset fontAsset)
    {
        // PlayerIdentityCard: Đặt tại Top-Left (anchorMin=(0, 1), anchorMax=(0, 1), pivot=(0, 1))
        // Giữ nguyên sizeDelta >= 200x210 để thỏa mãn bộ test 1080p có sẵn
        GameObject idCard = GameObject.Find("PlayerIdentityCard");
        if (idCard != null)
        {
            RectTransform rt = idCard.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(200, 220);

            Debug.Log("[LAYOUT] PlayerIdentityCard đã chuyển sang góc trên bên trái (Top-Left), hoàn toàn không che khung chat hay nút kỹ năng.");
        }

        // PlayerListHUDButton: Đặt ngay bên dưới PlayerIdentityCard tại Top-Left
        Transform hudBtnTrans = canvasTrans.Find("PlayerListHUDButton");
        if (hudBtnTrans != null)
        {
            RectTransform rt = hudBtnTrans.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20, -248);
            rt.sizeDelta = new Vector2(200, 38);

            Debug.Log("[LAYOUT] PlayerListHUDButton đã chuyển xuống dưới thẻ người chơi tại Top-Left.");
        }
    }

    private static void InstallChatUIElements(Transform canvasTrans, TMP_FontAsset fontAsset)
    {
        // Tạo GameObject Controller gắn NetworkChatUI
        Transform chatCtrlTrans = canvasTrans.Find("ChatUIController");
        GameObject chatCtrlObj;
        if (chatCtrlTrans == null)
        {
            chatCtrlObj = new GameObject("ChatUIController", typeof(RectTransform));
            chatCtrlObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            chatCtrlObj = chatCtrlTrans.gameObject;
        }

        RectTransform ctrlRt = chatCtrlObj.GetComponent<RectTransform>();
        ctrlRt.anchorMin = Vector2.zero;
        ctrlRt.anchorMax = Vector2.one;
        ctrlRt.offsetMin = Vector2.zero;
        ctrlRt.offsetMax = Vector2.zero;

        NetworkChatUI chatUI = chatCtrlObj.GetComponent<NetworkChatUI>();
        if (chatUI == null)
        {
            chatUI = chatCtrlObj.AddComponent<NetworkChatUI>();
        }

        // 1. FloatingChatButton (Nút nổi 52 dp tại góc dưới bên trái)
        Transform fbTrans = canvasTrans.Find("FloatingChatButton");
        GameObject fbObj;
        if (fbTrans == null)
        {
            fbObj = new GameObject("FloatingChatButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            fbObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            fbObj = fbTrans.gameObject;
        }

        RectTransform fbRt = fbObj.GetComponent<RectTransform>();
        fbRt.anchorMin = new Vector2(0f, 0f);
        fbRt.anchorMax = new Vector2(0f, 0f);
        fbRt.pivot = new Vector2(0f, 0f);
        fbRt.anchoredPosition = new Vector2(16, 16);
        fbRt.sizeDelta = new Vector2(52, 52); // Chuẩn 52 dp di động

        Image fbImg = fbObj.GetComponent<Image>();
        fbImg.color = new Color(0.10f, 0.15f, 0.24f, 0.95f);
        Button fbBtn = fbObj.GetComponent<Button>();
        fbBtn.targetGraphic = fbImg;

        // Label nút Chat
        Transform iconTrans = fbObj.transform.Find("Icon");
        TextMeshProUGUI iconTmp;
        if (iconTrans == null)
        {
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(fbObj.transform, false);
            iconTmp = iconObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            iconTmp = iconTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform iconRt = iconTmp.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) iconTmp.font = fontAsset;
        iconTmp.text = "CHAT";
        iconTmp.fontSize = 13;
        iconTmp.fontStyle = FontStyles.Bold;
        iconTmp.color = new Color(0.35f, 0.85f, 1f, 1f);
        iconTmp.alignment = TextAlignmentOptions.Center;
        iconTmp.raycastTarget = false;

        // Unread Badge
        Transform badgeTrans = fbObj.transform.Find("UnreadBadge");
        GameObject badgeObj;
        if (badgeTrans == null)
        {
            badgeObj = new GameObject("UnreadBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.transform.SetParent(fbObj.transform, false);
        }
        else
        {
            badgeObj = badgeTrans.gameObject;
        }
        RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(1f, 1f);
        badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(-4, -4);
        badgeRt.sizeDelta = new Vector2(20, 20);

        Image badgeImg = badgeObj.GetComponent<Image>();
        badgeImg.color = new Color(0.95f, 0.2f, 0.25f, 1f);

        Transform btTrans = badgeObj.transform.Find("BadgeText");
        TextMeshProUGUI btTmp;
        if (btTrans == null)
        {
            GameObject btObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btObj.transform.SetParent(badgeObj.transform, false);
            btTmp = btObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            btTmp = btTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform btRt = btTmp.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) btTmp.font = fontAsset;
        btTmp.fontSize = 11;
        btTmp.fontStyle = FontStyles.Bold;
        btTmp.color = Color.white;
        btTmp.alignment = TextAlignmentOptions.Center;
        btTmp.raycastTarget = false;
        badgeObj.SetActive(false);

        // 2. CompactChatPanel (Khung chat tại góc dưới bên trái)
        Transform cpTrans = canvasTrans.Find("CompactChatPanel");
        GameObject cpObj;
        if (cpTrans == null)
        {
            cpObj = new GameObject("CompactChatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cpObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            cpObj = cpTrans.gameObject;
        }

        RectTransform cpRt = cpObj.GetComponent<RectTransform>();
        cpRt.anchorMin = new Vector2(0f, 0f);
        cpRt.anchorMax = new Vector2(0.33f, 0.40f); // 33% rộng x 40% cao màn hình
        cpRt.pivot = new Vector2(0f, 0f);
        cpRt.offsetMin = new Vector2(16, 16);
        cpRt.offsetMax = new Vector2(-6, -6);

        Image cpImg = cpObj.GetComponent<Image>();
        cpImg.color = new Color(0.06f, 0.08f, 0.13f, 0.88f); // Không làm mờ gameplay, semi-transparency cao cấp
        cpImg.raycastTarget = true;

        // Header Bar
        Transform hTrans = cpObj.transform.Find("HeaderBar");
        GameObject hObj;
        if (hTrans == null)
        {
            hObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hObj.transform.SetParent(cpObj.transform, false);
        }
        else
        {
            hObj = hTrans.gameObject;
        }
        RectTransform hRt = hObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0, 1);
        hRt.anchorMax = new Vector2(1, 1);
        hRt.pivot = new Vector2(0.5f, 1);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(0, 32);

        Image hImg = hObj.GetComponent<Image>();
        hImg.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);

        Transform titleTrans = hObj.transform.Find("TitleText");
        TextMeshProUGUI titleTmp;
        if (titleTrans == null)
        {
            GameObject tObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tObj.transform.SetParent(hObj.transform, false);
            titleTmp = tObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            titleTmp = titleTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform tRt = titleTmp.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(12, 0);
        tRt.offsetMax = new Vector2(-40, 0);
        if (fontAsset != null) titleTmp.font = fontAsset;
        titleTmp.text = "TRÒ CHUYỆN";
        titleTmp.fontSize = 14;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.4f, 0.85f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;

        // Minimize Button
        Transform minTrans = hObj.transform.Find("MinimizeButton");
        GameObject minObj;
        if (minTrans == null)
        {
            minObj = new GameObject("MinimizeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            minObj.transform.SetParent(hObj.transform, false);
        }
        else
        {
            minObj = minTrans.gameObject;
        }
        RectTransform minRt = minObj.GetComponent<RectTransform>();
        minRt.anchorMin = new Vector2(1, 0.5f);
        minRt.anchorMax = new Vector2(1, 0.5f);
        minRt.pivot = new Vector2(1, 0.5f);
        minRt.anchoredPosition = new Vector2(-6, 0);
        minRt.sizeDelta = new Vector2(28, 24);

        Image minImg = minObj.GetComponent<Image>();
        minImg.color = new Color(0.22f, 0.28f, 0.38f, 0.85f);
        Button minBtn = minObj.GetComponent<Button>();
        minBtn.targetGraphic = minImg;

        Transform closeIconTrans = minObj.transform.Find("CloseIcon");
        TextMeshProUGUI closeIconTmp;
        if (closeIconTrans == null)
        {
            GameObject ciObj = new GameObject("CloseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            ciObj.transform.SetParent(minObj.transform, false);
            closeIconTmp = ciObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            closeIconTmp = closeIconTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform ciRt = closeIconTmp.GetComponent<RectTransform>();
        ciRt.anchorMin = Vector2.zero;
        ciRt.anchorMax = Vector2.one;
        ciRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) closeIconTmp.font = fontAsset;
        closeIconTmp.text = "✕";
        closeIconTmp.fontSize = 14;
        closeIconTmp.fontStyle = FontStyles.Bold;
        closeIconTmp.color = Color.white;
        closeIconTmp.alignment = TextAlignmentOptions.Center;
        closeIconTmp.raycastTarget = false;

        // Scroll View
        Transform svTrans = cpObj.transform.Find("MessageScrollView");
        GameObject svObj;
        if (svTrans == null)
        {
            svObj = new GameObject("MessageScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            svObj.transform.SetParent(cpObj.transform, false);
        }
        else
        {
            svObj = svTrans.gameObject;
        }
        RectTransform svRt = svObj.GetComponent<RectTransform>();
        svRt.anchorMin = new Vector2(0, 0);
        svRt.anchorMax = new Vector2(1, 1);
        svRt.offsetMin = new Vector2(8, 46);
        svRt.offsetMax = new Vector2(-8, -36);

        Image svImg = svObj.GetComponent<Image>();
        svImg.color = new Color(0, 0, 0, 0.2f);
        ScrollRect sr = svObj.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        Transform vpTrans = svObj.transform.Find("Viewport");
        GameObject vpObj;
        if (vpTrans == null)
        {
            vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            vpObj.transform.SetParent(svObj.transform, false);
        }
        else
        {
            vpObj = vpTrans.gameObject;
        }
        RectTransform vpRt = vpObj.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;
        Image vpImg = vpObj.GetComponent<Image>();
        vpImg.color = Color.white;
        Mask vpMask = vpObj.GetComponent<Mask>();
        vpMask.showMaskGraphic = false;

        // Content
        Transform contentTrans = vpObj.transform.Find("Content");
        GameObject contentObj;
        if (contentTrans == null)
        {
            contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(vpObj.transform, false);
        }
        else
        {
            contentObj = contentTrans.gameObject;
        }
        RectTransform contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRt;
        sr.content = contentRt;

        // Input Row
        Transform irTrans = cpObj.transform.Find("InputRow");
        GameObject irObj;
        if (irTrans == null)
        {
            irObj = new GameObject("InputRow", typeof(RectTransform));
            irObj.transform.SetParent(cpObj.transform, false);
        }
        else
        {
            irObj = irTrans.gameObject;
        }
        RectTransform irRt = irObj.GetComponent<RectTransform>();
        irRt.anchorMin = new Vector2(0, 0);
        irRt.anchorMax = new Vector2(1, 0);
        irRt.pivot = new Vector2(0.5f, 0);
        irRt.anchoredPosition = new Vector2(0, 6);
        irRt.sizeDelta = new Vector2(-16, 36);

        // Send Button
        Transform sendTrans = irObj.transform.Find("SendButton");
        GameObject sendObj;
        if (sendTrans == null)
        {
            sendObj = new GameObject("SendButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            sendObj.transform.SetParent(irObj.transform, false);
        }
        else
        {
            sendObj = sendTrans.gameObject;
        }
        RectTransform sendRt = sendObj.GetComponent<RectTransform>();
        sendRt.anchorMin = new Vector2(1, 0);
        sendRt.anchorMax = new Vector2(1, 1);
        sendRt.pivot = new Vector2(1, 0.5f);
        sendRt.anchoredPosition = Vector2.zero;
        sendRt.sizeDelta = new Vector2(62, 0);

        Image sendImg = sendObj.GetComponent<Image>();
        sendImg.color = new Color(0.15f, 0.55f, 0.95f, 0.95f);
        Button sendBtn = sendObj.GetComponent<Button>();
        sendBtn.targetGraphic = sendImg;

        Transform stTrans = sendObj.transform.Find("SendText");
        TextMeshProUGUI stTmp;
        if (stTrans == null)
        {
            GameObject stObj = new GameObject("SendText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            stObj.transform.SetParent(sendObj.transform, false);
            stTmp = stObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            stTmp = stTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform stRt = stTmp.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) stTmp.font = fontAsset;
        stTmp.text = "GỬI";
        stTmp.fontSize = 14;
        stTmp.fontStyle = FontStyles.Bold;
        stTmp.color = Color.white;
        stTmp.alignment = TextAlignmentOptions.Center;
        stTmp.raycastTarget = false;

        // Input Field
        Transform inpTrans = irObj.transform.Find("ChatInputField");
        GameObject inpObj;
        if (inpTrans == null)
        {
            inpObj = new GameObject("ChatInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            inpObj.transform.SetParent(irObj.transform, false);
        }
        else
        {
            inpObj = inpTrans.gameObject;
        }
        RectTransform inpRt = inpObj.GetComponent<RectTransform>();
        inpRt.anchorMin = new Vector2(0, 0);
        inpRt.anchorMax = new Vector2(1, 1);
        inpRt.offsetMin = Vector2.zero;
        inpRt.offsetMax = new Vector2(-68, 0);

        Image inpImg = inpObj.GetComponent<Image>();
        inpImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        TMP_InputField inputField = inpObj.GetComponent<TMP_InputField>();

        // Text Area & Components
        Transform taTrans = inpObj.transform.Find("Text Area");
        GameObject taObj;
        if (taTrans == null)
        {
            taObj = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            taObj.transform.SetParent(inpObj.transform, false);
        }
        else
        {
            taObj = taTrans.gameObject;
        }
        RectTransform taRt = taObj.GetComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(8, 2);
        taRt.offsetMax = new Vector2(-8, -2);

        Transform phTrans = taObj.transform.Find("Placeholder");
        TextMeshProUGUI phTmp;
        if (phTrans == null)
        {
            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            phObj.transform.SetParent(taObj.transform, false);
            phTmp = phObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            phTmp = phTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform phRt = phTmp.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) phTmp.font = fontAsset;
        phTmp.text = "Nhập tin nhắn...";
        phTmp.fontSize = 14;
        phTmp.fontStyle = FontStyles.Italic;
        phTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.75f);
        phTmp.alignment = TextAlignmentOptions.Left;

        Transform textTrans = taObj.transform.Find("Text");
        TextMeshProUGUI textTmp;
        if (textTrans == null)
        {
            GameObject tObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tObj.transform.SetParent(taObj.transform, false);
            textTmp = tObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textTmp = textTrans.GetComponent<TextMeshProUGUI>();
        }
        RectTransform tCompRt = textTmp.GetComponent<RectTransform>();
        tCompRt.anchorMin = Vector2.zero;
        tCompRt.anchorMax = Vector2.one;
        tCompRt.sizeDelta = Vector2.zero;
        if (fontAsset != null) textTmp.font = fontAsset;
        textTmp.fontSize = 14;
        textTmp.color = Color.white;
        textTmp.alignment = TextAlignmentOptions.Left;

        inputField.textViewport = taRt;
        inputField.placeholder = phTmp;
        inputField.textComponent = textTmp;
        inputField.characterLimit = NetworkChatManager.MaxMessageLength;

        // Wire NetworkChatUI SerializedObject
        SerializedObject so = new SerializedObject(chatUI);
        so.FindProperty("chatPanelRoot").objectReferenceValue = cpObj;
        so.FindProperty("floatingChatButton").objectReferenceValue = fbBtn;
        so.FindProperty("minimizeButton").objectReferenceValue = minBtn;
        so.FindProperty("unreadBadgeText").objectReferenceValue = btTmp;
        so.FindProperty("unreadBadgeObj").objectReferenceValue = badgeObj;
        so.FindProperty("scrollRect").objectReferenceValue = sr;
        so.FindProperty("messageContent").objectReferenceValue = contentRt;
        so.FindProperty("chatInputField").objectReferenceValue = inputField;
        so.FindProperty("sendButton").objectReferenceValue = sendBtn;
        so.FindProperty("widthScreenRatio").floatValue = 0.33f;
        so.FindProperty("heightScreenRatio").floatValue = 0.40f;
        so.ApplyModifiedProperties();

        // Đảm bảo thứ tự Sibling: ChatUIController và các panel nổi nằm trên lớp hợp lý
        fbObj.transform.SetAsLastSibling();
        cpObj.transform.SetAsLastSibling();

        // Mặc định ẩn chatPanel khi khởi đầu scene, chỉ hiện nút nổi
        cpObj.SetActive(false);
        fbObj.SetActive(true);

        Debug.Log("[SCENE] Cấu hình SerializedObject cho NetworkChatUI hoàn tất.");
    }
}
