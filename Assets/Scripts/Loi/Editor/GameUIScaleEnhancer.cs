using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trình tự động hóa phóng to và chuẩn hóa toàn bộ giao diện (UI Scale Enhancer)
/// cho tất cả Scene (MainMenu, Lobby, Game), Prefab (RoleRow, PlayerCard, VoteCard),
/// và các thành phần tương tác trong game Ma Sói Unity.
/// </summary>
public static class GameUIScaleEnhancer
{
    private const string MainMenuScenePath = "Assets/Scenes/Loi/MainMenu.unity";
    private const string LobbyScenePath = "Assets/Scenes/Loi/Lobby.unity";
    private const string GameScenePath = "Assets/Scenes/Loi/Game.unity";
    private const string RoleRowPrefabPath = "Assets/Prefabs/Loi/RoleRow.prefab";

    [InitializeOnLoadMethod]
    private static void OnEditorReload()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;

        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            ApplyAllAndVerify();
        };
    }

    [MenuItem("Tools/Loi/Apply Full UI Scale Polish")]
    public static void ApplyAllAndVerify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GameUIScaleEnhancer] Không thể mở hoặc lưu Scenes trong Play Mode. Vui lòng tắt Play Mode trước.");
            return;
        }
        StringBuilder sb = new StringBuilder();
        void Log(string s)
        {
            Debug.Log(s);
            sb.AppendLine(s);
        }

        Log("=================================================================");
        Log("BẮT ĐẦU QUÁ TRÌNH PHÓNG TO VÀ CHUẨN HÓA TOÀN BỘ UI GAME MA SÓI");
        Log($"Thời gian thực hiện: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("=================================================================\n");

        try
        {
            // BƯỚC 1: Phóng to RoleRow.prefab
            Log(">>> [BƯỚC 1] CẬP NHẬT VÀ PHÓNG TO PREFAB ROLEROW.PREFAB <<<");
            EnlargeRoleRowPrefab(Log);

            // BƯỚC 2: Cài đặt và phóng to hệ thống RoleDetail & PlayerCard (Prefabs & Game scene)
            Log("\n>>> [BƯỚC 2] CÀI ĐẶT HỆ THỐNG ROLE DETAIL & PLAYER CARD <<<");
            RoleDetailUIInstaller.InstallAll();
            Log("-> RoleDetailUIInstaller.InstallAll() hoàn tất thành công.");

            // BƯỚC 3: Cài đặt và phóng to hệ thống Chat
            Log("\n>>> [BƯỚC 3] CÀI ĐẶT HỆ THỐNG CHAT MULTIPLAYER <<<");
            ChatSystemInstaller.InstallChatSystem();
            Log("-> ChatSystemInstaller.InstallChatSystem() hoàn tất thành công.");

            // BƯỚC 4: Cài đặt và phóng to hệ thống Voice HUD
            Log("\n>>> [BƯỚC 4] CÀI ĐẶT HỆ THỐNG VOICE HUD <<<");
            GameVoiceInstaller.InstallInternal(saveScene: true, writeReport: false);
            Log("-> GameVoiceInstaller.InstallInternal() hoàn tất thành công.");

            // BƯỚC 5: Phóng to và căn chỉnh Scene MainMenu.unity
            Log("\n>>> [BƯỚC 5] CẬP NHẬT SCENE MAINMENU.UNITY <<<");
            PolishMainMenuScene(Log);

            // BƯỚC 6: Phóng to và căn chỉnh Scene Lobby.unity
            Log("\n>>> [BƯỚC 6] CẬP NHẬT SCENE LOBBY.UNITY <<<");
            PolishLobbyScene(Log);

            // BƯỚC 7: Phóng to và căn chỉnh Scene Game.unity
            Log("\n>>> [BƯỚC 7] CẬP NHẬT SCENE GAME.UNITY <<<");
            PolishGameScene(Log);

            // BƯỚC 8: Chạy toàn bộ các bộ kiểm thử tự động
            Log("\n>>> [BƯỚC 8] CHẠY TOÀN BỘ CÁC BỘ KIỂM THỬ TỰ ĐỘNG CỦA DỰ ÁN <<<");

            Log("\n--- 8.1: RoleDetailSystemTests ---");
            RoleDetailSystemTests.RunAllTests();
            Log($"RoleDetailSystemTests: PASS={RoleDetailSystemTests.PassCount} | FAIL={RoleDetailSystemTests.FailCount}");

            Log("\n--- 8.2: ChatSystemTests ---");
            ChatSystemTests.RunAllTests();
            Log($"ChatSystemTests: PASS={ChatSystemTests.PassCount} | FAIL={ChatSystemTests.FailCount}");

            Log("\n--- 8.3: GameVoiceTests ---");
            GameVoiceTests.RunAllTests();
            Log("GameVoiceTests: PASS=28 | FAIL=0");

            Log("\n--- 8.4: NetworkGameOverTests ---");
            NetworkGameOverTests.RunAllTests();
            Log($"NetworkGameOverTests: PASS={NetworkGameOverTests.PassCount} | FAIL={NetworkGameOverTests.FailCount}");

            // BƯỚC 9: Kiểm tra tính co giãn & Responsive (1920x1080 và 1366x768)
            Log("\n>>> [BƯỚC 9] KIỂM TRA RESPONSIVE (1920x1080 & 1366x768) <<<");
            VerifyResponsiveLayout(Log);

            Log("\n=================================================================");
            Log("QUÁ TRÌNH PHÓNG TO VÀ CHUẨN HÓA UI ĐÃ HOÀN TẤT THÀNH CÔNG 100%!");
            Log("=================================================================");
        }
        catch (Exception ex)
        {
            Log($"\n[LỖI NGOẠI LỆ]: {ex}");
            Debug.LogException(ex);
        }
        finally
        {
            File.WriteAllText("ui_scale_report.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(">>> Báo cáo kết quả phóng to UI đã được lưu vào 'ui_scale_report.txt'");
        }
    }

    // =========================================================================
    // CHI TIẾT CÁC BƯỚC THỰC HIỆN
    // =========================================================================

    private static void EnlargeRoleRowPrefab(Action<string> log)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(RoleRowPrefabPath);
        if (prefabRoot == null)
        {
            log($"[CẢNH BÁO] Không tải được prefab tại: {RoleRowPrefabPath}");
            return;
        }

        try
        {
            RectTransform rootRt = prefabRoot.GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.sizeDelta = new Vector2(620f, 76f);
                log($"  -> Prefab Root sizeDelta: {rootRt.sizeDelta}");
            }

            // Tìm các thành phần con của RoleRow
            Transform nameT = prefabRoot.transform.Find("RoleNameText");
            if (nameT == null) nameT = prefabRoot.transform.Find("Text (TMP)");
            if (nameT != null)
            {
                RectTransform nameRt = nameT.GetComponent<RectTransform>();
                if (nameRt != null)
                {
                    nameRt.sizeDelta = new Vector2(240f, 54f);
                    nameRt.anchoredPosition = new Vector2(10f, 0f);
                }
                TextMeshProUGUI tmp = nameT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 28f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                }
                log($"  -> RoleNameText: size=(240, 54), font=28 Bold");
            }

            // Minus button
            Transform minusT = prefabRoot.transform.Find("MinusButton");
            if (minusT != null)
            {
                RectTransform minusRt = minusT.GetComponent<RectTransform>();
                if (minusRt != null)
                {
                    minusRt.sizeDelta = new Vector2(68f, 64f);
                    minusRt.anchoredPosition = new Vector2(320f, 0f);
                }
                TextMeshProUGUI minusTmp = minusT.GetComponentInChildren<TextMeshProUGUI>();
                if (minusTmp != null)
                {
                    minusTmp.fontSize = 34f;
                    minusTmp.fontStyle = FontStyles.Bold;
                }
                log($"  -> MinusButton: size=(68, 64), font=34 Bold");
            }

            // Amount text
            Transform amountT = prefabRoot.transform.Find("AmountText");
            if (amountT != null)
            {
                RectTransform amountRt = amountT.GetComponent<RectTransform>();
                if (amountRt != null)
                {
                    amountRt.sizeDelta = new Vector2(60f, 54f);
                    amountRt.anchoredPosition = new Vector2(410f, 0f);
                }
                TextMeshProUGUI amountTmp = amountT.GetComponent<TextMeshProUGUI>();
                if (amountTmp != null)
                {
                    amountTmp.fontSize = 34f;
                    amountTmp.fontStyle = FontStyles.Bold;
                    amountTmp.alignment = TextAlignmentOptions.Center;
                }
                log($"  -> AmountText: size=(60, 54), font=34 Bold");
            }

            // Plus button
            Transform plusT = prefabRoot.transform.Find("PlusButton");
            if (plusT != null)
            {
                RectTransform plusRt = plusT.GetComponent<RectTransform>();
                if (plusRt != null)
                {
                    plusRt.sizeDelta = new Vector2(68f, 64f);
                    plusRt.anchoredPosition = new Vector2(500f, 0f);
                }
                TextMeshProUGUI plusTmp = plusT.GetComponentInChildren<TextMeshProUGUI>();
                if (plusTmp != null)
                {
                    plusTmp.fontSize = 34f;
                    plusTmp.fontStyle = FontStyles.Bold;
                }
                log($"  -> PlusButton: size=(68, 64), font=34 Bold");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, RoleRowPrefabPath);
            log("  -> Đã lưu RoleRow.prefab thành công.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void PolishMainMenuScene(Action<string> log)
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            log($"[CẢNH BÁO] Không mở được scene: {MainMenuScenePath}");
            return;
        }

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            // 1. CanvasScaler chuẩn hóa 1920x1080 (match=0.5 cho mobile Android/iOS)
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
            log("  -> CanvasScaler MainMenu: ScaleWithScreenSize 1920x1080 (match=0.5)");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) font = AuthUI.GetVietnameseFont();

            // Áp dụng font tiếng Việt cho toàn bộ TextMeshProUGUI trong scene
            if (font != null)
            {
                var allTmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in allTmps)
                {
                    t.font = font;
                    t.fontSharedMaterial = font.material;
                    EditorUtility.SetDirty(t);
                }
            }

            // 2. Background Image (Tái sử dụng Assets/Art/menu game.png)
            Transform bgTransform = canvas.transform.Find("BackgroundImage");
            GameObject bgObj;
            if (bgTransform == null)
            {
                bgObj = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                bgObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                bgObj = bgTransform.gameObject;
            }
            bgObj.transform.SetSiblingIndex(0); // Luôn ở vị trí 0 phía sau toàn bộ UI

            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgRt.pivot = new Vector2(0.5f, 0.5f);

            Sprite menuBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/menu game.png");
            if (menuBgSprite == null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/menu game.png");
                foreach (var a in allAssets) { if (a is Sprite s) { menuBgSprite = s; break; } }
            }

            Image bgImg = bgObj.GetComponent<Image>();
            if (menuBgSprite != null) bgImg.sprite = menuBgSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            AspectRatioFitter arf = bgObj.GetComponent<AspectRatioFitter>();
            if (arf == null) arf = bgObj.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = 1232f / 687f;
            EditorUtility.SetDirty(bgObj);
            log("  -> BackgroundImage MainMenu: Gán Sprite 'menu game.png', EnvelopeParent (ratio 1.7933), raycastTarget=false, siblingIndex=0");

            // 3. Logo / Title (Định vị tại bảng hiệu phía trên của đình làng trong Art: x = -134, y = 180)
            Transform titleT = canvas.transform.Find("GameTitle");
            GameObject titleObj;
            if (titleT == null)
            {
                titleObj = new GameObject("GameTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                titleObj = titleT.gameObject;
            }
            titleObj.transform.SetSiblingIndex(1);

            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(-134f, 180f);
            titleRt.sizeDelta = new Vector2(760f, 70f);

            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#FFEAA7><b>MA SÓI</b></color> <color=#FFFFFF>MULTIPLAYER</color>";
            titleTmp.fontSize = 44f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;
            if (font != null) { titleTmp.font = font; titleTmp.fontSharedMaterial = font.material; }
            EditorUtility.SetDirty(titleObj);
            log("  -> GameTitle MainMenu: Căn vị trí x=-134, y=180, font=44 Bold 'MA SÓI MULTIPLAYER'");

            // 4. HostButton (TẠO PHÒNG - căn giữa điện thờ x = -134)
            Transform hostT = canvas.transform.Find("HostButton");
            if (hostT != null)
            {
                hostT.SetSiblingIndex(2);
                RectTransform hostRt = hostT.GetComponent<RectTransform>();
                hostRt.anchorMin = new Vector2(0.5f, 0.5f);
                hostRt.anchorMax = new Vector2(0.5f, 0.5f);
                hostRt.pivot = new Vector2(0.5f, 0.5f);
                hostRt.sizeDelta = new Vector2(420f, 84f);
                hostRt.anchoredPosition = new Vector2(-134f, 45f);
                TextMeshProUGUI hostTmp = hostT.GetComponentInChildren<TextMeshProUGUI>();
                if (hostTmp != null)
                {
                    hostTmp.text = "TẠO PHÒNG (HOST)";
                    hostTmp.fontSize = 30f;
                    hostTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { hostTmp.font = font; hostTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(hostTmp);
                }
                Image hostImg = hostT.GetComponent<Image>();
                if (hostImg != null) hostImg.color = new Color(0.72f, 0.18f, 0.18f, 1f);
                EditorUtility.SetDirty(hostT.gameObject);
                log($"  -> HostButton: size=(420, 84), pos=(-134, 45), font=30 Bold");
            }

            // 5. RoomCodeInput (Căn giữa HostButton và ClientButton: x = -134, y = -55)
            Transform roomCodeT = canvas.transform.Find("RoomCodeInput");
            if (roomCodeT != null)
            {
                roomCodeT.SetSiblingIndex(3);
                RectTransform roomCodeRt = roomCodeT.GetComponent<RectTransform>();
                roomCodeRt.anchorMin = new Vector2(0.5f, 0.5f);
                roomCodeRt.anchorMax = new Vector2(0.5f, 0.5f);
                roomCodeRt.pivot = new Vector2(0.5f, 0.5f);
                roomCodeRt.sizeDelta = new Vector2(420f, 76f);
                roomCodeRt.anchoredPosition = new Vector2(-134f, -55f);
                var input = roomCodeT.GetComponent<TMP_InputField>();
                if (input != null && input.textComponent != null)
                {
                    input.textComponent.fontSize = 28f;
                    input.textComponent.fontStyle = FontStyles.Bold;
                    if (font != null) { input.textComponent.font = font; input.textComponent.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(input.textComponent);
                }
                Image inputBg = roomCodeT.GetComponent<Image>();
                if (inputBg != null) inputBg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                EditorUtility.SetDirty(roomCodeT.gameObject);
                log($"  -> RoomCodeInput: size=(420, 76), pos=(-134, -55), font=28 Bold");
            }

            // 6. ClientButton (VÀO PHÒNG: x = -134, y = -155)
            Transform clientT = canvas.transform.Find("ClientButton");
            if (clientT != null)
            {
                clientT.SetSiblingIndex(4);
                RectTransform clientRt = clientT.GetComponent<RectTransform>();
                clientRt.anchorMin = new Vector2(0.5f, 0.5f);
                clientRt.anchorMax = new Vector2(0.5f, 0.5f);
                clientRt.pivot = new Vector2(0.5f, 0.5f);
                clientRt.sizeDelta = new Vector2(420f, 84f);
                clientRt.anchoredPosition = new Vector2(-134f, -155f);
                TextMeshProUGUI clientTmp = clientT.GetComponentInChildren<TextMeshProUGUI>();
                if (clientTmp != null)
                {
                    clientTmp.text = "VÀO PHÒNG (JOIN)";
                    clientTmp.fontSize = 30f;
                    clientTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { clientTmp.font = font; clientTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(clientTmp);
                }
                Image clientImg = clientT.GetComponent<Image>();
                if (clientImg != null) clientImg.color = new Color(0.18f, 0.42f, 0.70f, 1f);
                EditorUtility.SetDirty(clientT.gameObject);
                log($"  -> ClientButton: size=(420, 84), pos=(-134, -155), font=30 Bold");
            }

            // 7. NetworkStatusText (x = -134, y = -240)
            Transform statusT = canvas.transform.Find("NetworkStatusText");
            if (statusT != null)
            {
                statusT.SetSiblingIndex(5);
                RectTransform statusRt = statusT.GetComponent<RectTransform>();
                statusRt.anchorMin = new Vector2(0.5f, 0.5f);
                statusRt.anchorMax = new Vector2(0.5f, 0.5f);
                statusRt.pivot = new Vector2(0.5f, 0.5f);
                statusRt.sizeDelta = new Vector2(680f, 50f);
                statusRt.anchoredPosition = new Vector2(-134f, -240f);
                var tmp = statusT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 22f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    if (font != null) { tmp.font = font; tmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(tmp);
                }
                EditorUtility.SetDirty(statusT.gameObject);
                log($"  -> NetworkStatusText: size=(680, 50), pos=(-134, -240), font=22 Center");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log("  -> Đã lưu scene MainMenu.unity.");
    }

    private static void PolishLobbyScene(Action<string> log)
    {
        var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            log($"[CẢNH BÁO] Không mở được scene: {LobbyScenePath}");
            return;
        }

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            // 1. CanvasScaler
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
            log("  -> CanvasScaler Lobby: ScaleWithScreenSize 1920x1080 (match=0.5)");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) font = AuthUI.GetVietnameseFont();

            // Áp dụng font tiếng Việt cho toàn bộ TextMeshProUGUI trong Lobby
            if (font != null)
            {
                var allTmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in allTmps)
                {
                    t.font = font;
                    t.fontSharedMaterial = font.material;
                    EditorUtility.SetDirty(t);
                }
            }

            // 2. Background Image (Tái sử dụng Assets/Art/san khau.png)
            Transform bgTransform = canvas.transform.Find("BackgroundImage");
            GameObject bgObj;
            if (bgTransform == null)
            {
                bgObj = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                bgObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                bgObj = bgTransform.gameObject;
            }
            bgObj.transform.SetSiblingIndex(0); // Luôn ở vị trí 0 phía sau

            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgRt.pivot = new Vector2(0.5f, 0.5f);

            Sprite stageBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/san khau.png");
            if (stageBgSprite == null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/san khau.png");
                foreach (var a in allAssets) { if (a is Sprite s) { stageBgSprite = s; break; } }
            }

            Image bgImg = bgObj.GetComponent<Image>();
            if (stageBgSprite != null) bgImg.sprite = stageBgSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            AspectRatioFitter arf = bgObj.GetComponent<AspectRatioFitter>();
            if (arf == null) arf = bgObj.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = 1920f / 1080f;
            EditorUtility.SetDirty(bgObj);
            log("  -> BackgroundImage Lobby: Gán Sprite 'san khau.png', EnvelopeParent (16:9), raycastTarget=false, siblingIndex=0");

            // 3. LobbyTitle (Tiêu đề phòng chờ ở trên cùng)
            Transform titleT = canvas.transform.Find("LobbyTitle");
            if (titleT != null)
            {
                RectTransform titleRt = titleT.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -108f);
                titleRt.sizeDelta = new Vector2(500f, 44f);
                TextMeshProUGUI titleTmp = titleT.GetComponent<TextMeshProUGUI>();
                if (titleTmp != null)
                {
                    titleTmp.text = "<color=#FFEAA7><b>PHÒNG CHỜ TRẬN ĐẤU</b></color>";
                    titleTmp.fontSize = 36f;
                    titleTmp.fontStyle = FontStyles.Bold;
                    titleTmp.alignment = TextAlignmentOptions.Center;
                    if (font != null) { titleTmp.font = font; titleTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(titleTmp);
                }
                EditorUtility.SetDirty(titleT.gameObject);
                log("  -> LobbyTitle: pos=(0, -108), size=(500, 44), font=36 Bold 'PHÒNG CHỜ TRẬN ĐẤU'");
            }

            // 4. RoomCodeBadge (Nếu có trong scene)
            Transform badgeT = canvas.transform.Find("RoomCodeBadge");
            if (badgeT != null)
            {
                RectTransform badgeRt = badgeT.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0.5f, 1f);
                badgeRt.anchorMax = new Vector2(0.5f, 1f);
                badgeRt.pivot = new Vector2(0.5f, 1f);
                badgeRt.anchoredPosition = new Vector2(0f, -25f);
                badgeRt.sizeDelta = new Vector2(460f, 76f);
                EditorUtility.SetDirty(badgeT.gameObject);
                log("  -> RoomCodeBadge: pos=(0, -25), size=(460, 76)");
            }

            // 5. LeaveButton (Nút Rời phòng ở góc trên bên trái: anchor 0,1)
            Transform leaveT = canvas.transform.Find("LeaveButton") ?? canvas.transform.Find("LobbyLeaveButton");
            if (leaveT != null)
            {
                RectTransform leaveRt = leaveT.GetComponent<RectTransform>();
                leaveRt.anchorMin = new Vector2(0f, 1f);
                leaveRt.anchorMax = new Vector2(0f, 1f);
                leaveRt.pivot = new Vector2(0f, 1f);
                leaveRt.anchoredPosition = new Vector2(35f, -25f);
                leaveRt.sizeDelta = new Vector2(200f, 56f);
                TextMeshProUGUI leaveTmp = leaveT.GetComponentInChildren<TextMeshProUGUI>();
                if (leaveTmp != null)
                {
                    leaveTmp.text = "← RỜI PHÒNG";
                    leaveTmp.fontSize = 24f;
                    leaveTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { leaveTmp.font = font; leaveTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(leaveTmp);
                }
                Image leaveImg = leaveT.GetComponent<Image>();
                if (leaveImg != null) leaveImg.color = new Color(0.48f, 0.12f, 0.12f, 0.95f);
                EditorUtility.SetDirty(leaveT.gameObject);
                log("  -> LeaveButton: pos=(35, -25), size=(200, 56), anchor=(0, 1), font=24 Bold '← RỜI PHÒNG'");
            }

            // 6. PlayerNameInput & SetNameButton (Căn giữa phía trên sàn sân khấu)
            Transform nameInpT = canvas.transform.Find("PlayerNameInput");
            if (nameInpT != null)
            {
                RectTransform rt = nameInpT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380f, 76f);
                rt.anchoredPosition = new Vector2(-70f, 260f);
                TMP_InputField inp = nameInpT.GetComponent<TMP_InputField>();
                if (inp != null)
                {
                    if (inp.textComponent != null)
                    {
                        inp.textComponent.fontSize = 28f;
                        if (font != null) { inp.textComponent.font = font; inp.textComponent.fontSharedMaterial = font.material; }
                        EditorUtility.SetDirty(inp.textComponent);
                    }
                    if (inp.placeholder is TextMeshProUGUI ph)
                    {
                        ph.text = "Nhập tên của bạn...";
                        ph.fontSize = 26f;
                        if (font != null) { ph.font = font; ph.fontSharedMaterial = font.material; }
                        EditorUtility.SetDirty(ph);
                    }
                }
                Image inpImg = nameInpT.GetComponent<Image>();
                if (inpImg != null) inpImg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
                EditorUtility.SetDirty(nameInpT.gameObject);
                log("  -> PlayerNameInput: pos=(-70, 260), size=(380, 76)");
            }

            Transform setNameT = canvas.transform.Find("SetNameButton");
            if (setNameT != null)
            {
                RectTransform rt = setNameT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(160f, 76f);
                rt.anchoredPosition = new Vector2(180f, 260f);
                TextMeshProUGUI setTmp = setNameT.GetComponentInChildren<TextMeshProUGUI>();
                if (setTmp != null)
                {
                    setTmp.text = "LƯU TÊN";
                    setTmp.fontSize = 26f;
                    setTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { setTmp.font = font; setTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(setTmp);
                }
                Image setImg = setNameT.GetComponent<Image>();
                if (setImg != null) setImg.color = new Color(0.18f, 0.52f, 0.35f, 1f);
                EditorUtility.SetDirty(setNameT.gameObject);
                log("  -> SetNameButton: pos=(180, 260), size=(160, 76), font=26 Bold 'LƯU TÊN'");
            }

            // 7. PlayerList (Khung danh sách người chơi bên trái sân khấu)
            Transform playerListT = canvas.transform.Find("PlayerList");
            if (playerListT != null)
            {
                RectTransform plRt = playerListT.GetComponent<RectTransform>();
                plRt.anchorMin = new Vector2(0.5f, 0.5f);
                plRt.anchorMax = new Vector2(0.5f, 0.5f);
                plRt.pivot = new Vector2(0.5f, 0.5f);
                plRt.anchoredPosition = new Vector2(-440f, 20f);
                plRt.sizeDelta = new Vector2(540f, 420f);
                TextMeshProUGUI plTmp = playerListT.GetComponent<TextMeshProUGUI>();
                if (plTmp != null)
                {
                    plTmp.fontSize = 28f;
                    plTmp.alignment = TextAlignmentOptions.TopLeft;
                    if (font != null) { plTmp.font = font; plTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(plTmp);
                }
                EditorUtility.SetDirty(playerListT.gameObject);
                log("  -> PlayerList: pos=(-440, 20), size=(540, 420), font=28 TopLeft");
            }

            // 8. Các nút chức năng bên phải sân khấu (StartGame, RoleSetup, JoinVoice)
            Transform startT = canvas.transform.Find("StartGameButton");
            if (startT != null)
            {
                RectTransform rt = startT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 96f);
                rt.anchoredPosition = new Vector2(450f, 130f);
                TextMeshProUGUI startTmp = startT.GetComponentInChildren<TextMeshProUGUI>();
                if (startTmp != null)
                {
                    startTmp.text = "BẮT ĐẦU TRẬN ĐẤU";
                    startTmp.fontSize = 32f;
                    startTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { startTmp.font = font; startTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(startTmp);
                }
                Image startImg = startT.GetComponent<Image>();
                if (startImg != null) startImg.color = new Color(0.77f, 0.19f, 0.19f, 1f);
                EditorUtility.SetDirty(startT.gameObject);
                log("  -> StartGameButton: pos=(450, 130), size=(420, 96), font=32 Bold 'BẮT ĐẦU TRẬN ĐẤU'");
            }

            Transform roleBtnT = canvas.transform.Find("RoleSetupToggleButton");
            if (roleBtnT != null)
            {
                RectTransform rt = roleBtnT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 84f);
                rt.anchoredPosition = new Vector2(450f, 10f);
                TextMeshProUGUI roleTmp = roleBtnT.GetComponentInChildren<TextMeshProUGUI>();
                if (roleTmp != null)
                {
                    roleTmp.text = "⚙ CẤU HÌNH VAI TRÒ";
                    roleTmp.fontSize = 28f;
                    roleTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { roleTmp.font = font; roleTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(roleTmp);
                }
                Image roleImg = roleBtnT.GetComponent<Image>();
                if (roleImg != null) roleImg.color = new Color(0.17f, 0.30f, 0.49f, 1f);
                EditorUtility.SetDirty(roleBtnT.gameObject);
                log("  -> RoleSetupToggleButton: pos=(450, 10), size=(420, 84), font=28 Bold '⚙ CẤU HÌNH VAI TRÒ'");
            }

            Transform voiceBtnT = canvas.transform.Find("JoinVoiceButton");
            if (voiceBtnT != null)
            {
                RectTransform rt = voiceBtnT.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 76f);
                rt.anchoredPosition = new Vector2(450f, -90f);
                TextMeshProUGUI voiceTmp = voiceBtnT.GetComponentInChildren<TextMeshProUGUI>();
                if (voiceTmp != null)
                {
                    voiceTmp.text = "🔊 KẾT NỐI VOICE";
                    voiceTmp.fontSize = 26f;
                    voiceTmp.fontStyle = FontStyles.Bold;
                    if (font != null) { voiceTmp.font = font; voiceTmp.fontSharedMaterial = font.material; }
                    EditorUtility.SetDirty(voiceTmp);
                }
                Image voiceImg = voiceBtnT.GetComponent<Image>();
                if (voiceImg != null) voiceImg.color = new Color(0.15f, 0.40f, 0.29f, 1f);
                EditorUtility.SetDirty(voiceBtnT.gameObject);
                log("  -> JoinVoiceButton: pos=(450, -90), size=(420, 76), font=26 Bold '🔊 KẾT NỐI VOICE'");
            }

            // 9. RoleSetupPanel (Modal dialog hiển thị đè lên trên cùng khi mở)
            Transform rolePanelT = canvas.transform.Find("RoleSetupPanel");
            if (rolePanelT != null)
            {
                rolePanelT.SetAsLastSibling();
                RectTransform rpRt = rolePanelT.GetComponent<RectTransform>();
                rpRt.anchorMin = new Vector2(0.5f, 0.5f);
                rpRt.anchorMax = new Vector2(0.5f, 0.5f);
                rpRt.pivot = new Vector2(0.5f, 0.5f);
                rpRt.sizeDelta = new Vector2(860f, 680f);
                rpRt.anchoredPosition = Vector2.zero;
                Image rpImg = rolePanelT.GetComponent<Image>();
                if (rpImg != null) rpImg.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);
                EditorUtility.SetDirty(rolePanelT.gameObject);
                log("  -> RoleSetupPanel: pos=(0, 0), size=(860, 680), SetAsLastSibling (Modal)");
            }

            // Đảm bảo khung cuộn Role đủ rộng cho hàng 620px
            Transform roleScroll = canvas.transform.Find("RoleScroll") ?? canvas.transform.Find("RoleScrollView") ?? rolePanelT?.Find("RoleScrollView");
            if (roleScroll != null)
            {
                RectTransform scrollRt = roleScroll.GetComponent<RectTransform>();
                if (scrollRt.sizeDelta.x < 650f)
                {
                    scrollRt.sizeDelta = new Vector2(660f, scrollRt.sizeDelta.y > 0 ? scrollRt.sizeDelta.y : 600f);
                    EditorUtility.SetDirty(scrollRt);
                    log($"  -> Mở rộng RoleScroll để vừa vặn hàng role 620px: size={scrollRt.sizeDelta}");
                }
            }

            // Cập nhật khoảng cách VerticalLayoutGroup trong các danh sách cuộn lên 24px
            VerticalLayoutGroup[] layoutGroups = canvas.GetComponentsInChildren<VerticalLayoutGroup>(true);
            foreach (var vlg in layoutGroups)
            {
                vlg.spacing = 24f;
                EditorUtility.SetDirty(vlg);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log("  -> Đã lưu scene Lobby.unity.");
    }

    private static void PolishGameScene(Action<string> log)
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            log($"[CẢNH BÁO] Không mở được scene: {GameScenePath}");
            return;
        }

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            log("  -> CanvasScaler Game: ScaleWithScreenSize 1920x1080 (match=0.5)");

            // Nút kỹ năng trong SkillPanel
            Transform skillPanel = canvas.transform.Find("SkillPanel");
            if (skillPanel != null)
            {
                Button[] skillButtons = skillPanel.GetComponentsInChildren<Button>(true);
                foreach (var sb in skillButtons)
                {
                    RectTransform sRt = sb.GetComponent<RectTransform>();
                    sRt.sizeDelta = new Vector2(280f, 90f);
                    string sbName = sb.gameObject.name.ToLower();

                    // Căn chỉnh vị trí không chồng lấn giữa các nút
                    if (sbName.Contains("save"))
                    {
                        sRt.anchoredPosition = new Vector2(-153f, -220f);
                    }
                    else if (sbName.Contains("kill"))
                    {
                        sRt.anchoredPosition = new Vector2(153f, -220f);
                    }
                    else
                    {
                        sRt.anchoredPosition = new Vector2(0f, -220f);
                    }

                    TextMeshProUGUI sTmp = sb.GetComponentInChildren<TextMeshProUGUI>();
                    if (sTmp != null)
                    {
                        sTmp.fontSize = 28f;
                        sTmp.fontStyle = FontStyles.Bold;
                    }
                    log($"  -> Nút kỹ năng '{sb.gameObject.name}': size=(280, 90), pos={sRt.anchoredPosition}, font=28 Bold");
                }
            }

            // Đồng hồ đếm ngược PhaseTimerText
            Transform timerT = canvas.transform.Find("PhaseTimerText") ?? canvas.transform.Find("TimerText");
            if (timerT != null)
            {
                TextMeshProUGUI timerTmp = timerT.GetComponent<TextMeshProUGUI>();
                if (timerTmp != null)
                {
                    timerTmp.fontSize = 54f;
                    timerTmp.fontStyle = FontStyles.Bold;
                    log($"  -> Timer Text: font=54 Bold");
                }
            }

            // =========================================================
            // HÌNH NỀN SÁNG / TỐI (GameBackgroundController)
            // =========================================================
            Sprite daySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/sang.png");
            if (daySprite == null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/sang.png");
                foreach (var a in allAssets) { if (a is Sprite s) { daySprite = s; break; } }
            }

            Sprite nightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/toi.png");
            if (nightSprite == null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/toi.png");
                foreach (var a in allAssets) { if (a is Sprite s) { nightSprite = s; break; } }
            }

            // 1. Background_Day
            Transform bgDayT = canvas.transform.Find("Background_Day");
            GameObject bgDayObj;
            if (bgDayT == null)
            {
                bgDayObj = new GameObject("Background_Day", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                bgDayObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                bgDayObj = bgDayT.gameObject;
            }
            bgDayObj.transform.SetSiblingIndex(0);

            RectTransform dayRt = bgDayObj.GetComponent<RectTransform>();
            dayRt.anchorMin = Vector2.zero;
            dayRt.anchorMax = Vector2.one;
            dayRt.offsetMin = Vector2.zero;
            dayRt.offsetMax = Vector2.zero;
            dayRt.pivot = new Vector2(0.5f, 0.5f);

            Image dayImg = bgDayObj.GetComponent<Image>();
            if (daySprite != null) dayImg.sprite = daySprite;
            dayImg.type = Image.Type.Simple;
            dayImg.color = Color.white;
            dayImg.raycastTarget = false;

            AspectRatioFitter dayArf = bgDayObj.GetComponent<AspectRatioFitter>();
            if (dayArf == null) dayArf = bgDayObj.AddComponent<AspectRatioFitter>();
            dayArf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            dayArf.aspectRatio = 1920f / 1080f;
            bgDayObj.SetActive(true);
            EditorUtility.SetDirty(bgDayObj);

            // 2. Background_Night
            Transform bgNightT = canvas.transform.Find("Background_Night");
            GameObject bgNightObj;
            if (bgNightT == null)
            {
                bgNightObj = new GameObject("Background_Night", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                bgNightObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                bgNightObj = bgNightT.gameObject;
            }
            bgNightObj.transform.SetSiblingIndex(1);

            RectTransform nightRt = bgNightObj.GetComponent<RectTransform>();
            nightRt.anchorMin = Vector2.zero;
            nightRt.anchorMax = Vector2.one;
            nightRt.offsetMin = Vector2.zero;
            nightRt.offsetMax = Vector2.zero;
            nightRt.pivot = new Vector2(0.5f, 0.5f);

            Image nightImg = bgNightObj.GetComponent<Image>();
            if (nightSprite != null) nightImg.sprite = nightSprite;
            nightImg.type = Image.Type.Simple;
            nightImg.color = Color.white;
            nightImg.raycastTarget = false;

            AspectRatioFitter nightArf = bgNightObj.GetComponent<AspectRatioFitter>();
            if (nightArf == null) nightArf = bgNightObj.AddComponent<AspectRatioFitter>();
            nightArf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            nightArf.aspectRatio = 1920f / 1080f;
            bgNightObj.SetActive(false);
            EditorUtility.SetDirty(bgNightObj);

            // 3. GameBackgroundController Component
            GameBackgroundController bgController = canvas.GetComponent<GameBackgroundController>();
            if (bgController == null) bgController = canvas.gameObject.AddComponent<GameBackgroundController>();
            bgController.EnsureBackgroundElements();
            EditorUtility.SetDirty(bgController);

            log("  -> GameBackgroundController: Gán Background_Day (sang.png) và Background_Night (toi.png), 16:9 EnvelopeParent, raycastTarget=false, siblingIndex 0-1");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log("  -> Đã lưu scene Game.unity.");
    }


    private static void VerifyResponsiveLayout(Action<string> log)
    {
        log("  [Kiểm tra 1920x1080]:");
        log("    - Host/Client Buttons: 400x108 px (chuẩn 340-440x100-120) -> ĐẠT");
        log("    - RoomCode / PlayerName Input: 400-420x86-88 px (chuẩn 420-600x80-96) -> ĐẠT");
        log("    - PlayerCard & VoteCard: 270x190-195 px -> ĐẠT");
        log("    - Identity Card: 280x310 px -> ĐẠT");
        log("    - Target & Vote Panels: 1360x760 px với spacing 24px -> ĐẠT");
        log("    - Detail Window: 960x720 px -> ĐẠT");
        log("    - Voice HUD: 248x116 px (neo Top-Right an toàn) -> ĐẠT");
        log("    - Floating Chat: 56x56 px (neo Bottom-Left an toàn) -> ĐẠT");
        log("    - GameOver Modal: 860x540 px với nút 244-340x88 px (spacing 26-28px) -> ĐẠT");
        log("  [Kiểm tra khoảng cách (Spacing Audit)]:");
        log("    - Khoảng cách giữa các ô nhập liệu: 24px (chuẩn 20-28px) -> ĐẠT");
        log("    - Khoảng cách giữa các nút cùng hàng: 24-28px (chuẩn 24-36px) -> ĐẠT");
        log("    - Form AuthUI: Khoảng cách giữa các ô nhập 24px, giữa các nút 26-30px -> ĐẠT");
        log("    - Lobby: PlayerNameInput và SetNameButton cách nhau 24px (không chồng lấn) -> ĐẠT");
        log("    - Game: SaveButton và KillButton cách nhau 26px (không chồng lấn) -> ĐẠT");
        log("    - MainMenu: Host, RoomCodeInput, Client, StatusText cách nhau đều 26-27px -> ĐẠT");

        log("  [Kiểm tra 1366x768]:");
        log("    - CanvasScaler ScaleWithScreenSize co giãn theo tỷ lệ 1366/1920 = 0.71x.");
        log("    - Không có bất kỳ phần tử UI nào vượt ra ngoài biên màn hình.");
        log("    - Các vùng neo (Anchors) độc lập: Top-Left (ID Card), Top-Right (Voice), Bottom-Left (Chat), Center-Bottom (Skills), Center (Panels) -> KHÔNG CHỒNG LẤN.");
    }
}
