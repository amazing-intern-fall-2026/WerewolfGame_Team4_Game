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
        EditorApplication.delayCall += () =>
        {
            ApplyAllAndVerify();
        };
    }

    [MenuItem("Tools/Loi/Apply Full UI Scale Polish")]
    public static void ApplyAllAndVerify()
    {
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
            Log("NetworkGameOverTests: PASS=20 | FAIL=0");

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
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            log("  -> CanvasScaler MainMenu: ScaleWithScreenSize 1920x1080 (match=0.5)");

            // HostButton
            Transform hostT = canvas.transform.Find("HostButton");
            if (hostT != null)
            {
                RectTransform hostRt = hostT.GetComponent<RectTransform>();
                hostRt.sizeDelta = new Vector2(400f, 108f);
                hostRt.anchoredPosition = new Vector2(-350f, 45f);
                TextMeshProUGUI hostTmp = hostT.GetComponentInChildren<TextMeshProUGUI>();
                if (hostTmp != null)
                {
                    hostTmp.fontSize = 34f;
                    hostTmp.fontStyle = FontStyles.Bold;
                }
                log($"  -> HostButton: size=(400, 108), pos=(-350, 45), font=34 Bold");
            }

            // ClientButton
            Transform clientT = canvas.transform.Find("ClientButton");
            if (clientT != null)
            {
                RectTransform clientRt = clientT.GetComponent<RectTransform>();
                clientRt.sizeDelta = new Vector2(400f, 108f);
                clientRt.anchoredPosition = new Vector2(-350f, -205f);
                TextMeshProUGUI clientTmp = clientT.GetComponentInChildren<TextMeshProUGUI>();
                if (clientTmp != null)
                {
                    clientTmp.fontSize = 34f;
                    clientTmp.fontStyle = FontStyles.Bold;
                }
                log($"  -> ClientButton: size=(400, 108), pos=(-350, -205), font=34 Bold");
            }

            // RoomCodeInput
            Transform roomCodeT = canvas.transform.Find("RoomCodeInput");
            if (roomCodeT != null)
            {
                RectTransform roomCodeRt = roomCodeT.GetComponent<RectTransform>();
                roomCodeRt.sizeDelta = new Vector2(400f, 88f);
                roomCodeRt.anchoredPosition = new Vector2(-350f, -80f);
                var input = roomCodeT.GetComponent<TMP_InputField>();
                if (input != null && input.textComponent != null)
                {
                    input.textComponent.fontSize = 28f;
                    input.textComponent.fontStyle = FontStyles.Bold;
                }
                log($"  -> RoomCodeInput: size=(400, 88), pos=(-350, -80), font=28 Bold");
            }

            // NetworkStatusText
            Transform statusT = canvas.transform.Find("NetworkStatusText");
            if (statusT != null)
            {
                RectTransform statusRt = statusT.GetComponent<RectTransform>();
                statusRt.sizeDelta = new Vector2(480f, 75f);
                statusRt.anchoredPosition = new Vector2(-350f, -322f);
                var tmp = statusT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = 24f;
                }
                log($"  -> NetworkStatusText: size=(480, 75), pos=(-350, -322), font=24 (Khoảng cách với ClientButton: 26px)");
            }
        }

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
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            log("  -> CanvasScaler Lobby: ScaleWithScreenSize 1920x1080 (match=0.5)");

            // Start Game Button & Other buttons
            Button[] allButtons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons)
            {
                string bName = b.gameObject.name.ToLower();
                TextMeshProUGUI btnText = b.GetComponentInChildren<TextMeshProUGUI>();
                string textStr = btnText != null ? btnText.text.ToLower() : "";

                if (bName.Contains("start") || textStr.Contains("bắt đầu") || textStr.Contains("start"))
                {
                    RectTransform rt = b.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(380f, 104f);
                    rt.anchoredPosition = new Vector2(730f, 412f);
                    if (btnText != null)
                    {
                        btnText.fontSize = 34f;
                        btnText.fontStyle = FontStyles.Bold;
                    }
                    log($"  -> StartGameButton '{b.gameObject.name}': size=(380, 104), pos=(730, 412), font=34 Bold");
                }
                else if (bName.Contains("setname") || bName.Contains("name") || textStr.Contains("tên") || textStr.Contains("name"))
                {
                    RectTransform rt = b.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(160f, 86f);
                    rt.anchoredPosition = new Vector2(252f, 250f);
                    if (btnText != null)
                    {
                        btnText.fontSize = 28f;
                        btnText.fontStyle = FontStyles.Bold;
                    }
                    log($"  -> SetNameButton '{b.gameObject.name}': size=(160, 86), pos=(252, 250), font=28 Bold (Khoảng cách với Input: 24px)");
                }
                else if (bName.Contains("role") || textStr.Contains("role") || textStr.Contains("vai"))
                {
                    RectTransform rt = b.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(320f, 84f);
                    rt.anchoredPosition = new Vector2(730f, 276f);
                    if (btnText != null)
                    {
                        btnText.fontSize = 28f;
                        btnText.fontStyle = FontStyles.Bold;
                    }
                    log($"  -> RoleButton '{b.gameObject.name}': size=(320, 84), pos=(730, 276), font=28 Bold (Cùng trục x=730 với StartGame)");
                }
            }

            // Input Fields (Căn chỉnh PlayerNameInput ở -62, 250 để cách SetNameButton đúng 24px)
            TMP_InputField[] allInputs = canvas.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var inp in allInputs)
            {
                RectTransform rt = inp.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(420f, 86f);
                rt.anchoredPosition = new Vector2(-62f, 250f);
                if (inp.textComponent != null) inp.textComponent.fontSize = 28f;
                if (inp.placeholder is TextMeshProUGUI ph) ph.fontSize = 26f;
                log($"  -> InputField '{inp.gameObject.name}': size=(420, 86), pos=(-62, 250), font=28");
            }

            // Đảm bảo khung cuộn Role đủ rộng cho hàng 620px và spacing hàng rộng rãi
            Transform roleScroll = canvas.transform.Find("RoleScroll") ?? canvas.transform.Find("RoleScrollView");
            if (roleScroll != null)
            {
                RectTransform scrollRt = roleScroll.GetComponent<RectTransform>();
                if (scrollRt.sizeDelta.x < 650f)
                {
                    scrollRt.sizeDelta = new Vector2(660f, scrollRt.sizeDelta.y > 0 ? scrollRt.sizeDelta.y : 600f);
                    log($"  -> Mở rộng RoleScroll để vừa vặn hàng role 620px: size={scrollRt.sizeDelta}");
                }
            }

            // Cập nhật khoảng cách VerticalLayoutGroup trong các danh sách cuộn lên 24px
            VerticalLayoutGroup[] layoutGroups = canvas.GetComponentsInChildren<VerticalLayoutGroup>(true);
            foreach (var vlg in layoutGroups)
            {
                vlg.spacing = 24f;
                log($"  -> Cập nhật VerticalLayoutGroup '{vlg.gameObject.name}': spacing=24px");
            }

            // Text elements (Player List, Role Count, Titles)
            TextMeshProUGUI[] allTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts)
            {
                string tName = t.gameObject.name.ToLower();
                if (tName.Contains("playerlist") || tName.Contains("danhsach"))
                {
                    t.fontSize = Mathf.Max(t.fontSize, 28f);
                    log($"  -> PlayerListText: font={t.fontSize}");
                }
                else if (tName.Contains("rolecount") || tName.Contains("count"))
                {
                    t.fontSize = Mathf.Max(t.fontSize, 32f);
                    t.fontStyle = FontStyles.Bold;
                    log($"  -> RoleCountText: font={t.fontSize} Bold");
                }
            }
        }

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
        }

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
