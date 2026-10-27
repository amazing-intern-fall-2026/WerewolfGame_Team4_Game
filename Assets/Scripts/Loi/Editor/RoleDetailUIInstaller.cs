using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class RoleDetailUIInstaller
{
    private const string GameScenePath = "Assets/Scenes/Loi/Game.unity";
    private const string PlayerCardPrefabPath = "Assets/Prefabs/Loi/PlayerCard.prefab";
    private const string VoteCardPrefabPath = "Assets/Prefabs/Loi/VoteCard.prefab";
    private const string MainFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Loi/Install Role Detail and Player Card Polish")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            Debug.LogWarning("[RoleDetailUIInstaller] Không thể cài đặt UI và sửa Scene trong Play Mode.");
            return;
        }

        Debug.Log(">>> BẮT ĐẦU CÀI ĐẶT UI: PLAYER CARD VÀ ROLE DETAIL PANEL <<<");

        // 1. Đảm bảo font tiếng Việt
        VietnameseFontValidator.VerifyAndEnsureVietnameseGlyphs();
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);

        // 2. Cập nhật PlayerCard.prefab
        UpdatePlayerCardPrefab(fontAsset);

        // 3. Cập nhật VoteCard.prefab
        UpdateVoteCardPrefab(fontAsset);

        // 4. Cập nhật Game.unity
        UpdateGameScene(fontAsset);

        // 5. Cài đặt hệ thống Chat Multiplayer nhỏ gọn tại góc dưới bên trái
        ChatSystemInstaller.InstallChatSystem();

        // 6. Cài đặt hệ thống Voice HUD (Microphone & Speaker) nhỏ gọn tại góc trên bên phải
        GameVoiceInstaller.InstallInternal(saveScene: false, writeReport: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(">>> CÀI ĐẶT VÀ TINH CHỈNH HOÀN TẤT THÀNH CÔNG! <<<");
    }

    private static void UpdatePlayerCardPrefab(TMP_FontAsset fontAsset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerCardPrefabPath);
        if (root == null)
        {
            Debug.LogError($"Không load được prefab tại: {PlayerCardPrefabPath}");
            return;
        }

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.sizeDelta = new Vector2(270, 195);
            }

            // PlayerName
            Transform nameTrans = root.transform.Find("PlayerName");
            if (nameTrans != null)
            {
                RectTransform rt = nameTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 62);
                rt.sizeDelta = new Vector2(245, 36);

                TextMeshProUGUI tmp = nameTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 26;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 18;
                    tmp.fontSizeMax = 28;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.text = "TÊN NGƯỜI CHƠI";
                }
            }

            // Avatar
            Transform avatarTrans = root.transform.Find("Avatar");
            if (avatarTrans != null)
            {
                RectTransform rt = avatarTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 4);
                rt.sizeDelta = new Vector2(80, 80);
            }

            // StatusText
            Transform statusTrans = root.transform.Find("StatusText");
            if (statusTrans != null)
            {
                RectTransform rt = statusTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, -62);
                rt.sizeDelta = new Vector2(210, 32);

                TextMeshProUGUI tmp = statusTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 20;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 16;
                    tmp.fontSizeMax = 22;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.text = "CÒN SỐNG";
                }
            }

            // InfoButton
            Transform infoBtnTrans = root.transform.Find("InfoButton");
            GameObject infoBtnObj;
            if (infoBtnTrans == null)
            {
                infoBtnObj = new GameObject("InfoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                infoBtnObj.transform.SetParent(root.transform, false);
            }
            else
            {
                infoBtnObj = infoBtnTrans.gameObject;
            }

            RectTransform infoRt = infoBtnObj.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(1, 1);
            infoRt.anchorMax = new Vector2(1, 1);
            infoRt.pivot = new Vector2(1, 1);
            infoRt.anchoredPosition = new Vector2(-6, -6);
            infoRt.sizeDelta = new Vector2(36, 36);

            Image infoImg = infoBtnObj.GetComponent<Image>();
            infoImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            Button infoBtn = infoBtnObj.GetComponent<Button>();
            infoBtn.targetGraphic = infoImg;

            // InfoButton Icon (Child text)
            Transform iconTrans = infoBtnObj.transform.Find("IconText");
            TextMeshProUGUI iconTmp;
            if (iconTrans == null)
            {
                GameObject iconObj = new GameObject("IconText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                iconObj.transform.SetParent(infoBtnObj.transform, false);
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
            iconTmp.text = "i";
            iconTmp.fontSize = 22;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.color = new Color(0.3f, 0.85f, 1f, 1f); // Cyan
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.raycastTarget = false;

            // Wire NetworkTargetCardUI
            NetworkTargetCardUI targetCardUI = root.GetComponent<NetworkTargetCardUI>();
            if (targetCardUI != null)
            {
                SerializedObject so = new SerializedObject(targetCardUI);
                SerializedProperty propInfoBtn = so.FindProperty("infoButton");
                if (propInfoBtn != null)
                {
                    propInfoBtn.objectReferenceValue = infoBtn;
                    so.ApplyModifiedProperties();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerCardPrefabPath);
            Debug.Log("[PREFAB] PlayerCard.prefab đã cập nhật layout 1080p và InfoButton thành công.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateVoteCardPrefab(TMP_FontAsset fontAsset)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VoteCardPrefabPath);
        if (root == null)
        {
            Debug.LogError($"Không load được prefab tại: {VoteCardPrefabPath}");
            return;
        }

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.sizeDelta = new Vector2(270, 195);
            }

            // Root Card Background Image & Button
            Image rootImg = root.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
                rootImg.raycastTarget = true;
            }

            Button rootBtn = root.GetComponent<Button>();
            if (rootBtn != null && rootImg != null)
            {
                rootBtn.targetGraphic = rootImg;
            }

            // SelectedIndicator (Border highlight when selected)
            Transform selTrans = root.transform.Find("SelectedIndicator");
            GameObject selObj;
            if (selTrans == null)
            {
                selObj = new GameObject("SelectedIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                selObj.transform.SetParent(root.transform, false);
            }
            else
            {
                selObj = selTrans.gameObject;
            }
            RectTransform selRt = selObj.GetComponent<RectTransform>();
            selRt.anchorMin = Vector2.zero;
            selRt.anchorMax = Vector2.one;
            selRt.offsetMin = new Vector2(-4, -4);
            selRt.offsetMax = new Vector2(4, 4);

            Image selImg = selObj.GetComponent<Image>();
            selImg.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            selImg.raycastTarget = false;
            selObj.SetActive(false);

            // PlayerName
            Transform nameTrans = root.transform.Find("PlayerName");
            if (nameTrans != null)
            {
                RectTransform rt = nameTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 62);
                rt.sizeDelta = new Vector2(245, 36);

                TextMeshProUGUI tmp = nameTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 24;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 16;
                    tmp.fontSizeMax = 26;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.raycastTarget = false;
                }
            }

            // Avatar
            Transform avatarTrans = root.transform.Find("Avatar");
            if (avatarTrans != null)
            {
                RectTransform rt = avatarTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 4);
                rt.sizeDelta = new Vector2(80, 80);

                Image aImg = avatarTrans.GetComponent<Image>();
                if (aImg != null)
                {
                    Sprite defaultPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/Thuong/Prototype/Art/Medieval/Resources/RolePortraits/Villager.png");
                    if (defaultPortrait != null)
                    {
                        aImg.sprite = defaultPortrait;
                        aImg.color = Color.white;
                    }
                    else
                    {
                        aImg.color = new Color(0.22f, 0.28f, 0.38f, 1f);
                    }
                    aImg.preserveAspect = true;
                    aImg.raycastTarget = false;
                }
            }

            // StatusText
            Transform statusTrans = root.transform.Find("StatusText");
            if (statusTrans != null)
            {
                RectTransform rt = statusTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-55, -62);
                rt.sizeDelta = new Vector2(120, 32);

                TextMeshProUGUI tmp = statusTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 18;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 14;
                    tmp.fontSizeMax = 20;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(0.2f, 1f, 0.4f);
                    tmp.raycastTarget = false;
                }
            }

            // VoteCountText
            Transform voteTrans = root.transform.Find("VoteCountText");
            if (voteTrans != null)
            {
                RectTransform rt = voteTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(55, -62);
                rt.sizeDelta = new Vector2(120, 32);

                TextMeshProUGUI tmp = voteTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 18;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 14;
                    tmp.fontSizeMax = 20;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(1f, 0.85f, 0.25f);
                    tmp.raycastTarget = false;
                }
            }

            // InfoButton
            Transform infoBtnTrans = root.transform.Find("InfoButton");
            GameObject infoBtnObj;
            if (infoBtnTrans == null)
            {
                infoBtnObj = new GameObject("InfoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                infoBtnObj.transform.SetParent(root.transform, false);
            }
            else
            {
                infoBtnObj = infoBtnTrans.gameObject;
            }

            RectTransform infoRt = infoBtnObj.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(1, 1);
            infoRt.anchorMax = new Vector2(1, 1);
            infoRt.pivot = new Vector2(1, 1);
            infoRt.anchoredPosition = new Vector2(-6, -6);
            infoRt.sizeDelta = new Vector2(36, 36);

            Image infoImg = infoBtnObj.GetComponent<Image>();
            infoImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            Button infoBtn = infoBtnObj.GetComponent<Button>();
            infoBtn.targetGraphic = infoImg;

            // InfoButton Icon (Child text)
            Transform iconTrans = infoBtnObj.transform.Find("IconText");
            TextMeshProUGUI iconTmp;
            if (iconTrans == null)
            {
                GameObject iconObj = new GameObject("IconText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                iconObj.transform.SetParent(infoBtnObj.transform, false);
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
            iconTmp.text = "i";
            iconTmp.fontSize = 22;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.color = new Color(0.3f, 0.85f, 1f, 1f);
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.raycastTarget = false;

            // Wire NetworkVoteCardUI
            NetworkVoteCardUI voteCardUI = root.GetComponent<NetworkVoteCardUI>();
            if (voteCardUI != null)
            {
                SerializedObject so = new SerializedObject(voteCardUI);

                SerializedProperty propName = so.FindProperty("playerNameText");
                if (propName != null && nameTrans != null) propName.objectReferenceValue = nameTrans.GetComponent<TextMeshProUGUI>();

                SerializedProperty propStatus = so.FindProperty("statusText");
                if (propStatus != null && statusTrans != null) propStatus.objectReferenceValue = statusTrans.GetComponent<TextMeshProUGUI>();

                SerializedProperty propVote = so.FindProperty("voteCountText");
                if (propVote != null && voteTrans != null) propVote.objectReferenceValue = voteTrans.GetComponent<TextMeshProUGUI>();

                SerializedProperty propAvatar = so.FindProperty("avatarImage");
                if (propAvatar != null && avatarTrans != null) propAvatar.objectReferenceValue = avatarTrans.GetComponent<Image>();

                SerializedProperty propBg = so.FindProperty("cardBackgroundImage");
                if (propBg != null && rootImg != null) propBg.objectReferenceValue = rootImg;

                SerializedProperty propSel = so.FindProperty("selectedIndicator");
                if (propSel != null && selObj != null) propSel.objectReferenceValue = selObj;

                SerializedProperty propCardBtn = so.FindProperty("cardButton");
                if (propCardBtn != null && rootBtn != null) propCardBtn.objectReferenceValue = rootBtn;

                SerializedProperty propInfoBtn = so.FindProperty("infoButton");
                if (propInfoBtn != null) propInfoBtn.objectReferenceValue = infoBtn;

                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, VoteCardPrefabPath);
            Debug.Log("[PREFAB] VoteCard.prefab đã cập nhật layout 1080p và UI references thành công.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateGameScene(TMP_FontAsset fontAsset)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            Debug.LogWarning("[RoleDetailUIInstaller] Bỏ qua UpdateGameScene vì đang trong Play Mode.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Không tìm thấy Canvas trong Game Scene!");
            return;
        }

        // 1. Tinh chỉnh PlayerIdentityCard
        GameObject identityCard = GameObject.Find("PlayerIdentityCard");
        if (identityCard != null)
        {
            RectTransform cardRt = identityCard.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 1f);
            cardRt.anchorMax = new Vector2(0f, 1f);
            cardRt.pivot = new Vector2(0f, 1f);
            cardRt.sizeDelta = new Vector2(280, 310);
            cardRt.anchoredPosition = new Vector2(20, -20);

            Image cardImg = identityCard.GetComponent<Image>();
            if (cardImg != null) cardImg.raycastTarget = true;

            Button btn = identityCard.GetComponent<Button>();
            if (btn == null)
            {
                btn = identityCard.AddComponent<Button>();
                btn.targetGraphic = cardImg;
            }
            btn.interactable = true;

            NetworkPlayerIdentityCardUI idUI = identityCard.GetComponent<NetworkPlayerIdentityCardUI>();
            if (idUI != null)
            {
                SerializedObject so = new SerializedObject(idUI);
                SerializedProperty btnProp = so.FindProperty("cardButton");
                if (btnProp != null)
                {
                    btnProp.objectReferenceValue = btn;
                    so.ApplyModifiedProperties();
                }
            }

            Transform playerNameTrans = identityCard.transform.Find("PlayerName");
            if (playerNameTrans != null)
            {
                RectTransform rt = playerNameTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 120);
                rt.sizeDelta = new Vector2(260, 42);

                TextMeshProUGUI tmp = playerNameTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 26;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 20;
                    tmp.fontSizeMax = 28;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                }
            }

            Transform charImgTrans = identityCard.transform.Find("CharacterImage");
            if (charImgTrans != null)
            {
                RectTransform rt = charImgTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 8);
                rt.sizeDelta = new Vector2(170, 170);

                Image cImg = charImgTrans.GetComponent<Image>();
                if (cImg != null) cImg.raycastTarget = false;
            }

            Transform roleNameTrans = identityCard.transform.Find("RoleName");
            if (roleNameTrans != null)
            {
                RectTransform rt = roleNameTrans.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, -118);
                rt.sizeDelta = new Vector2(260, 40);

                TextMeshProUGUI tmp = roleNameTrans.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (fontAsset != null) tmp.font = fontAsset;
                    tmp.fontSize = 24;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 18;
                    tmp.fontSizeMax = 26;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                }
            }
            Debug.Log("[SCENE] PlayerIdentityCard đã được nâng cấp kích thước và nút bấm xem vai trò.");
        }

        // 2. Tinh chỉnh RolePanel ở giữa màn hình
        Transform rolePanelTrans = canvas.transform.Find("RolePanel");
        if (rolePanelTrans != null)
        {
            GameObject rolePanel = rolePanelTrans.gameObject;
            Button roleBtn = rolePanel.GetComponent<Button>();
            if (roleBtn == null)
            {
                roleBtn = rolePanel.AddComponent<Button>();
                roleBtn.targetGraphic = rolePanel.GetComponent<Image>();
            }
            roleBtn.interactable = true;

            NetworkRoleCardUI roleCardUI = rolePanel.GetComponent<NetworkRoleCardUI>();
            if (roleCardUI == null)
            {
                roleCardUI = rolePanel.AddComponent<NetworkRoleCardUI>();
            }

            foreach (var img in rolePanel.GetComponentsInChildren<Image>())
            {
                if (img.gameObject != rolePanel) img.raycastTarget = false;
            }
            foreach (var tmp in rolePanel.GetComponentsInChildren<TextMeshProUGUI>())
            {
                tmp.raycastTarget = false;
            }
            Debug.Log("[SCENE] RolePanel đã được cấu hình Button để click xem vai trò.");
        }

        // 3. Tắt DeathNotification và VoteResultNotification mặc định trong scene để không che phủ chuột
        Transform deathTrans = canvas.transform.Find("DeathNotification");
        if (deathTrans != null)
        {
            deathTrans.gameObject.SetActive(false);
            Debug.Log("[SCENE] DeathNotification đã được tắt mặc định để không chặn click.");
        }

        Transform voteResultTrans = canvas.transform.Find("VoteResultNotification");
        if (voteResultTrans != null)
        {
            voteResultTrans.gameObject.SetActive(false);
            Debug.Log("[SCENE] VoteResultNotification đã được tắt mặc định để không chặn click.");
        }

        // 4. Tinh chỉnh TargetPanel & CardContainer
        Transform tpTrans = canvas.transform.Find("TargetPanel");
        GameObject targetPanel = tpTrans != null ? tpTrans.gameObject : GameObject.Find("TargetPanel");
        if (targetPanel != null)
        {
            RectTransform tpRt = targetPanel.GetComponent<RectTransform>();
            tpRt.sizeDelta = new Vector2(1360, 760);

            Transform cardContainer = targetPanel.transform.Find("CardContainer");
            if (cardContainer != null)
            {
                GridLayoutGroup grid = cardContainer.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(270, 195);
                    grid.spacing = new Vector2(24, 24);
                }
            }

            // Thêm CloseButton cho TargetPanel nếu chưa có
            Transform tpCloseTrans = targetPanel.transform.Find("CloseButton");
            Button tpCloseBtn;
            if (tpCloseTrans == null)
            {
                GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeBtnObj.transform.SetParent(targetPanel.transform, false);
                RectTransform rt = closeBtnObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-15, -15);
                rt.sizeDelta = new Vector2(220, 76);

                Image img = closeBtnObj.GetComponent<Image>();
                img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);
                tpCloseBtn = closeBtnObj.GetComponent<Button>();
                tpCloseBtn.targetGraphic = img;

                GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(closeBtnObj.transform, false);
                RectTransform txtRt = textObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI btnTmp = textObj.GetComponent<TextMeshProUGUI>();
                if (fontAsset != null) btnTmp.font = fontAsset;
                btnTmp.text = "ĐÓNG";
                btnTmp.fontSize = 28;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.color = Color.white;
            }
            else
            {
                tpCloseBtn = tpCloseTrans.GetComponent<Button>();
            }

            NetworkTargetListUI targetListUI = targetPanel.GetComponent<NetworkTargetListUI>();
            if (targetListUI != null && tpCloseBtn != null)
            {
                SerializedObject so = new SerializedObject(targetListUI);
                SerializedProperty propClose = so.FindProperty("closeButton");
                if (propClose != null)
                {
                    propClose.objectReferenceValue = tpCloseBtn;
                    so.ApplyModifiedProperties();
                }
            }
        }

        // 5. Tinh chỉnh VotePanel, VoteCardContainer & Controls
        Transform vpTrans = canvas.transform.Find("VotePanel");
        GameObject votePanel = vpTrans != null ? vpTrans.gameObject : GameObject.Find("VotePanel");
        if (votePanel != null)
        {
            RectTransform vpRt = votePanel.GetComponent<RectTransform>();
            vpRt.anchorMin = new Vector2(0.5f, 0.5f);
            vpRt.anchorMax = new Vector2(0.5f, 0.5f);
            vpRt.pivot = new Vector2(0.5f, 0.5f);
            vpRt.anchoredPosition = Vector2.zero;
            vpRt.sizeDelta = new Vector2(1360, 780);

            Image vpImg = votePanel.GetComponent<Image>();
            if (vpImg != null)
            {
                vpImg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);
                vpImg.raycastTarget = true;
            }

            // VoteTitle
            Transform vtTrans = votePanel.transform.Find("VoteTitle");
            GameObject vtObj;
            if (vtTrans == null)
            {
                vtObj = new GameObject("VoteTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                vtObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                vtObj = vtTrans.gameObject;
            }
            RectTransform vtRt = vtObj.GetComponent<RectTransform>();
            vtRt.anchorMin = new Vector2(0.5f, 1f);
            vtRt.anchorMax = new Vector2(0.5f, 1f);
            vtRt.pivot = new Vector2(0.5f, 1f);
            vtRt.anchoredPosition = new Vector2(0, -20);
            vtRt.sizeDelta = new Vector2(600, 50);

            TextMeshProUGUI vtTmp = vtObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) vtTmp.font = fontAsset;
            vtTmp.text = "BỎ PHIẾU TREO CỔ";
            vtTmp.fontSize = 36;
            vtTmp.fontStyle = FontStyles.Bold;
            vtTmp.alignment = TextAlignmentOptions.Center;
            vtTmp.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Subtitle
            Transform subTrans = votePanel.transform.Find("VoteSubtitle");
            GameObject subObj;
            if (subTrans == null)
            {
                subObj = new GameObject("VoteSubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                subObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                subObj = subTrans.gameObject;
            }
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0, -70);
            subRt.sizeDelta = new Vector2(900, 30);

            TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) subTmp.font = fontAsset;
            subTmp.text = "Chọn một người chơi để bỏ phiếu treo cổ. Nhấn Xác Nhận để gửi phiếu.";
            subTmp.fontSize = 20;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(0.8f, 0.85f, 0.9f, 1f);

            // VoteCardContainer
            Transform voteContainer = votePanel.transform.Find("VoteCardContainer");
            if (voteContainer != null)
            {
                RectTransform vcRt = voteContainer.GetComponent<RectTransform>();
                vcRt.anchorMin = new Vector2(0.5f, 0.5f);
                vcRt.anchorMax = new Vector2(0.5f, 0.5f);
                vcRt.pivot = new Vector2(0.5f, 0.5f);
                vcRt.anchoredPosition = new Vector2(0, 10);
                vcRt.sizeDelta = new Vector2(1260, 480);

                Image vcImg = voteContainer.GetComponent<Image>();
                if (vcImg != null)
                {
                    vcImg.enabled = false;
                }

                GridLayoutGroup grid = voteContainer.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.cellSize = new Vector2(270, 195);
                    grid.spacing = new Vector2(24, 20);
                    grid.childAlignment = TextAnchor.MiddleCenter;
                    grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                    grid.constraintCount = 2;
                }
            }

            // SelectedTargetText
            Transform selTxtTrans = votePanel.transform.Find("SelectedTargetText");
            GameObject selTxtObj;
            if (selTxtTrans == null)
            {
                selTxtObj = new GameObject("SelectedTargetText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                selTxtObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                selTxtObj = selTxtTrans.gameObject;
            }
            RectTransform selTxtRt = selTxtObj.GetComponent<RectTransform>();
            selTxtRt.anchorMin = new Vector2(0.5f, 0f);
            selTxtRt.anchorMax = new Vector2(0.5f, 0f);
            selTxtRt.pivot = new Vector2(0.5f, 0f);
            selTxtRt.anchoredPosition = new Vector2(0, 78);
            selTxtRt.sizeDelta = new Vector2(800, 32);

            TextMeshProUGUI selTmp = selTxtObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) selTmp.font = fontAsset;
            selTmp.text = "Mục tiêu đã chọn: Chưa chọn";
            selTmp.fontSize = 22;
            selTmp.fontStyle = FontStyles.Bold;
            selTmp.alignment = TextAlignmentOptions.Center;
            selTmp.color = new Color(0.35f, 0.9f, 1f, 1f);

            // ConfirmVoteButton
            Transform confirmBtnTrans = votePanel.transform.Find("ConfirmVoteButton");
            GameObject confirmBtnObj;
            if (confirmBtnTrans == null)
            {
                confirmBtnObj = new GameObject("ConfirmVoteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                confirmBtnObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                confirmBtnObj = confirmBtnTrans.gameObject;
            }
            RectTransform cRt = confirmBtnObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0f);
            cRt.anchorMax = new Vector2(0.5f, 0f);
            cRt.pivot = new Vector2(0.5f, 0f);
            cRt.anchoredPosition = new Vector2(110, 24);
            cRt.sizeDelta = new Vector2(190, 48);

            Image cImg = confirmBtnObj.GetComponent<Image>();
            cImg.color = new Color(0.18f, 0.65f, 0.28f, 1f);
            Button cBtn = confirmBtnObj.GetComponent<Button>();
            cBtn.targetGraphic = cImg;

            Transform cTextTrans = confirmBtnObj.transform.Find("Text (TMP)");
            TextMeshProUGUI cTextTmp;
            if (cTextTrans == null)
            {
                GameObject tObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tObj.transform.SetParent(confirmBtnObj.transform, false);
                cTextTmp = tObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                cTextTmp = cTextTrans.GetComponent<TextMeshProUGUI>();
            }
            RectTransform cTxtRt = cTextTmp.GetComponent<RectTransform>();
            cTxtRt.anchorMin = Vector2.zero;
            cTxtRt.anchorMax = Vector2.one;
            cTxtRt.sizeDelta = Vector2.zero;
            if (fontAsset != null) cTextTmp.font = fontAsset;
            cTextTmp.text = "XÁC NHẬN";
            cTextTmp.fontSize = 22;
            cTextTmp.fontStyle = FontStyles.Bold;
            cTextTmp.color = Color.white;
            cTextTmp.alignment = TextAlignmentOptions.Center;

            // CancelVoteButton
            Transform cancelBtnTrans = votePanel.transform.Find("CancelVoteButton");
            GameObject cancelBtnObj;
            if (cancelBtnTrans == null)
            {
                cancelBtnObj = new GameObject("CancelVoteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                cancelBtnObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                cancelBtnObj = cancelBtnTrans.gameObject;
            }
            RectTransform canRt = cancelBtnObj.GetComponent<RectTransform>();
            canRt.anchorMin = new Vector2(0.5f, 0f);
            canRt.anchorMax = new Vector2(0.5f, 0f);
            canRt.pivot = new Vector2(0.5f, 0f);
            canRt.anchoredPosition = new Vector2(-110, 24);
            canRt.sizeDelta = new Vector2(190, 48);

            Image canImg = cancelBtnObj.GetComponent<Image>();
            canImg.color = new Color(0.35f, 0.38f, 0.45f, 1f);
            Button canBtn = cancelBtnObj.GetComponent<Button>();
            canBtn.targetGraphic = canImg;

            Transform canTextTrans = cancelBtnObj.transform.Find("Text (TMP)");
            TextMeshProUGUI canTextTmp;
            if (canTextTrans == null)
            {
                GameObject tObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tObj.transform.SetParent(cancelBtnObj.transform, false);
                canTextTmp = tObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                canTextTmp = canTextTrans.GetComponent<TextMeshProUGUI>();
            }
            RectTransform canTxtRt = canTextTmp.GetComponent<RectTransform>();
            canTxtRt.anchorMin = Vector2.zero;
            canTxtRt.anchorMax = Vector2.one;
            canTxtRt.sizeDelta = Vector2.zero;
            if (fontAsset != null) canTextTmp.font = fontAsset;
            canTextTmp.text = "HỦY CHỌN";
            canTextTmp.fontSize = 22;
            canTextTmp.fontStyle = FontStyles.Bold;
            canTextTmp.color = Color.white;
            canTextTmp.alignment = TextAlignmentOptions.Center;

            // StatusMessageText
            Transform statusMsgTrans = votePanel.transform.Find("StatusMessageText");
            GameObject statusMsgObj;
            if (statusMsgTrans == null)
            {
                statusMsgObj = new GameObject("StatusMessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                statusMsgObj.transform.SetParent(votePanel.transform, false);
            }
            else
            {
                statusMsgObj = statusMsgTrans.gameObject;
            }
            RectTransform smRt = statusMsgObj.GetComponent<RectTransform>();
            smRt.anchorMin = new Vector2(0.5f, 0f);
            smRt.anchorMax = new Vector2(0.5f, 0f);
            smRt.pivot = new Vector2(0.5f, 0f);
            smRt.anchoredPosition = new Vector2(0, 115);
            smRt.sizeDelta = new Vector2(800, 28);

            TextMeshProUGUI smTmp = statusMsgObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) smTmp.font = fontAsset;
            smTmp.text = "";
            smTmp.fontSize = 18;
            smTmp.alignment = TextAlignmentOptions.Center;
            smTmp.color = new Color(1f, 0.85f, 0.4f, 1f);

            // Wire NetworkVoteUI component
            NetworkVoteUI netVoteUI = UnityEngine.Object.FindFirstObjectByType<NetworkVoteUI>(FindObjectsInactive.Include);
            if (netVoteUI != null)
            {
                SerializedObject so = new SerializedObject(netVoteUI);

                SerializedProperty pVp = so.FindProperty("votePanel");
                if (pVp != null) pVp.objectReferenceValue = votePanel;

                SerializedProperty pVcp = so.FindProperty("voteCardPrefab");
                if (pVcp != null) pVcp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(VoteCardPrefabPath);

                SerializedProperty pVcc = so.FindProperty("voteCardContainer");
                if (pVcc != null && voteContainer != null) pVcc.objectReferenceValue = voteContainer;

                SerializedProperty pVt = so.FindProperty("voteTitleText");
                if (pVt != null && vtTmp != null) pVt.objectReferenceValue = vtTmp;

                SerializedProperty pSt = so.FindProperty("selectedTargetText");
                if (pSt != null && selTmp != null) pSt.objectReferenceValue = selTmp;

                SerializedProperty pSm = so.FindProperty("statusMessageText");
                if (pSm != null && smTmp != null) pSm.objectReferenceValue = smTmp;

                SerializedProperty pCb = so.FindProperty("confirmVoteButton");
                if (pCb != null && cBtn != null) pCb.objectReferenceValue = cBtn;

                SerializedProperty pCanB = so.FindProperty("cancelVoteButton");
                if (pCanB != null && canBtn != null) pCanB.objectReferenceValue = canBtn;

                so.ApplyModifiedProperties();
            }
        }

        // 6. Tạo hoặc tinh chỉnh GameRoleDetailPanel
        GameObject roleDetailPanel = CreateOrGetRoleDetailPanel(canvas.transform, fontAsset, identityCard);

        // 7. Thêm nút "Danh sách người chơi" trên HUD để xem tất cả Card bất cứ lúc nào
        Button hudBtn = CreateOrUpdateHUDPlayerListButton(canvas.transform, fontAsset, targetPanel);
        if (targetPanel != null && hudBtn != null)
        {
            NetworkTargetListUI targetListUI = targetPanel.GetComponent<NetworkTargetListUI>();
            if (targetListUI != null)
            {
                SerializedObject so = new SerializedObject(targetListUI);
                SerializedProperty propHud = so.FindProperty("hudPlayerListButton");
                if (propHud != null)
                {
                    propHud.objectReferenceValue = hudBtn;
                    so.ApplyModifiedProperties();
                }
            }
        }

        // 8. Thêm nút "Thoát phòng" (Leave Room) ngay bên dưới nút Danh sách người chơi
        CreateOrUpdateInGameLeaveButton(canvas.transform, fontAsset);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SCENE] Game.unity đã lưu toàn bộ cấu hình UI thành công.");
    }

    private static GameObject CreateOrGetRoleDetailPanel(Transform canvasTrans, TMP_FontAsset fontAsset, GameObject identityCard)
    {
        // Dọn dẹp RoleDetailPanel cũ nếu có để tránh trùng lặp
        Transform oldPanel = canvasTrans.Find("RoleDetailPanel");
        if (oldPanel != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
        }

        // Tìm hoặc tạo GameRoleDetailPanel (Canvas Controller - Luôn Active)
        Transform existing = canvasTrans.Find("GameRoleDetailPanel");
        GameObject panelObj;
        if (existing == null)
        {
            panelObj = new GameObject("GameRoleDetailPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            panelObj = existing.gameObject;
        }

        // QUAN TRỌNG: GameRoleDetailPanel phải luôn Active để Awake() chạy và Singleton khởi tạo ngay khi Scene load!
        panelObj.SetActive(true);
        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        // NetworkRoleDetailUI component gắn trên GameRoleDetailPanel
        NetworkRoleDetailUI detailUI = panelObj.GetComponent<NetworkRoleDetailUI>();
        if (detailUI == null)
        {
            detailUI = panelObj.AddComponent<NetworkRoleDetailUI>();
        }

        // Tạo RoleDetailBackdrop (Nền modal bao phủ toàn màn hình, đóng khi click ra ngoài)
        Transform backdropTrans = panelObj.transform.Find("RoleDetailBackdrop");
        GameObject backdropObj;
        if (backdropTrans == null)
        {
            backdropObj = new GameObject("RoleDetailBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backdropObj.transform.SetParent(panelObj.transform, false);
        }
        else
        {
            backdropObj = backdropTrans.gameObject;
        }

        RectTransform backdropRt = backdropObj.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        Image backdropImg = backdropObj.GetComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.72f);
        backdropImg.raycastTarget = true;

        Button backdropBtn = backdropObj.GetComponent<Button>();
        backdropBtn.targetGraphic = backdropImg;
        backdropBtn.interactable = true;

        // Tạo DetailWindow (Hộp thoại chứa nội dung thông tin Role)
        Transform windowTrans = backdropObj.transform.Find("DetailWindow");
        GameObject windowObj;
        if (windowTrans == null)
        {
            windowObj = new GameObject("DetailWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            windowObj.transform.SetParent(backdropObj.transform, false);
        }
        else
        {
            windowObj = windowTrans.gameObject;
        }

        RectTransform windowRt = windowObj.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = Vector2.zero;
        windowRt.sizeDelta = new Vector2(960, 720);

        Image windowImg = windowObj.GetComponent<Image>();
        windowImg.color = new Color(0.07f, 0.09f, 0.13f, 0.98f); // Dark luxury glassmorphism
        windowImg.raycastTarget = true;

        // Các component con của DetailWindow:
        // HeaderTitle
        TextMeshProUGUI headerTmp = EnsureChildTMP(windowObj.transform, "HeaderTitle", new Vector2(0, 310), new Vector2(880, 52), fontAsset, 34, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
        headerTmp.text = "THÔNG TIN VAI TRÒ";

        // PlayerName
        TextMeshProUGUI playerNameTmp = EnsureChildTMP(windowObj.transform, "PlayerNameText", new Vector2(-200, 240), new Vector2(460, 44), fontAsset, 26, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        playerNameTmp.text = "Người chơi: <color=#FFDD44>Player 1</color>";

        // RoleName
        TextMeshProUGUI roleNameTmp = EnsureChildTMP(windowObj.transform, "RoleNameText", new Vector2(-200, 190), new Vector2(460, 44), fontAsset, 28, FontStyles.Bold, new Color(0.2f, 1f, 0.65f), TextAlignmentOptions.Left);
        roleNameTmp.text = "Vai trò: <color=#00FFAA>Tiên tri</color>";

        // FactionText
        TextMeshProUGUI factionTmp = EnsureChildTMP(windowObj.transform, "FactionText", new Vector2(-200, 140), new Vector2(460, 40), fontAsset, 24, FontStyles.Bold, new Color(0.3f, 0.85f, 1f), TextAlignmentOptions.Left);
        factionTmp.text = "Phe: <color=#44CCFF>Phe Dân làng</color>";

        // RoleImage
        Transform roleImgTrans = windowObj.transform.Find("RoleImage");
        Image roleImg;
        if (roleImgTrans == null)
        {
            GameObject imgObj = new GameObject("RoleImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgObj.transform.SetParent(windowObj.transform, false);
            roleImg = imgObj.GetComponent<Image>();
        }
        else
        {
            roleImg = roleImgTrans.GetComponent<Image>();
        }
        RectTransform roleImgRt = roleImg.GetComponent<RectTransform>();
        roleImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        roleImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        roleImgRt.anchoredPosition = new Vector2(320, 190);
        roleImgRt.sizeDelta = new Vector2(160, 160);
        roleImg.preserveAspect = true;

        // DescriptionText
        TextMeshProUGUI descTmp = EnsureChildTMP(windowObj.transform, "DescriptionText", new Vector2(0, 40), new Vector2(880, 100), fontAsset, 20, FontStyles.Normal, new Color(0.92f, 0.92f, 0.95f), TextAlignmentOptions.TopLeft);
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        descTmp.text = "Mô tả vai trò chi tiết sẽ được hiển thị ở đây.";

        // SkillNameText
        TextMeshProUGUI skillNameTmp = EnsureChildTMP(windowObj.transform, "SkillNameText", new Vector2(0, -35), new Vector2(880, 40), fontAsset, 24, FontStyles.Bold, new Color(0.3f, 0.85f, 1f), TextAlignmentOptions.Left);
        skillNameTmp.text = "Kỹ năng: <color=#44CCFF>Thấu thị</color>";

        // SkillDescriptionText
        TextMeshProUGUI skillDescTmp = EnsureChildTMP(windowObj.transform, "SkillDescriptionText", new Vector2(0, -95), new Vector2(880, 75), fontAsset, 20, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.TopLeft);
        skillDescTmp.textWrappingMode = TextWrappingModes.Normal;
        skillDescTmp.text = "Cách sử dụng kỹ năng trong các giai đoạn của ván chơi.";

        // RestrictionsText
        TextMeshProUGUI restrictTmp = EnsureChildTMP(windowObj.transform, "RestrictionsText", new Vector2(0, -165), new Vector2(880, 60), fontAsset, 18, FontStyles.Italic, new Color(1f, 0.68f, 0.28f), TextAlignmentOptions.TopLeft);
        restrictTmp.textWrappingMode = TextWrappingModes.Normal;
        restrictTmp.text = "Giới hạn & Điều kiện sử dụng kỹ năng.";

        // CloseButton
        Transform closeBtnTrans = windowObj.transform.Find("CloseButton");
        GameObject closeBtnObj;
        if (closeBtnTrans == null)
        {
            closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeBtnObj.transform.SetParent(windowObj.transform, false);
        }
        else
        {
            closeBtnObj = closeBtnTrans.gameObject;
        }
        RectTransform closeBtnRt = closeBtnObj.GetComponent<RectTransform>();
        closeBtnRt.anchorMin = new Vector2(0.5f, 0.5f);
        closeBtnRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeBtnRt.anchoredPosition = new Vector2(0, -305);
        closeBtnRt.sizeDelta = new Vector2(260, 78);

        Image closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);
        Button closeBtn = closeBtnObj.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;

        TextMeshProUGUI closeTxt = EnsureChildTMP(closeBtnObj.transform, "Text (TMP)", Vector2.zero, Vector2.zero, fontAsset, 28, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        RectTransform closeTxtRt = closeTxt.GetComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxt.text = "ĐÓNG";

        // DebugToggleButton
        Transform debugBtnTrans = windowObj.transform.Find("DebugToggleButton");
        GameObject debugBtnObj;
        if (debugBtnTrans == null)
        {
            debugBtnObj = new GameObject("DebugToggleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            debugBtnObj.transform.SetParent(windowObj.transform, false);
        }
        else
        {
            debugBtnObj = debugBtnTrans.gameObject;
        }
        RectTransform debugRt = debugBtnObj.GetComponent<RectTransform>();
        debugRt.anchorMin = new Vector2(0, 1);
        debugRt.anchorMax = new Vector2(0, 1);
        debugRt.pivot = new Vector2(0, 1);
        debugRt.anchoredPosition = new Vector2(20, -20);
        debugRt.sizeDelta = new Vector2(160, 44);

        Image debugImg = debugBtnObj.GetComponent<Image>();
        debugImg.color = new Color(0.18f, 0.2f, 0.28f, 0.9f);
        Button debugBtn = debugBtnObj.GetComponent<Button>();
        debugBtn.targetGraphic = debugImg;

        TextMeshProUGUI debugStatusTmp = EnsureChildTMP(debugBtnObj.transform, "DebugStatusText", Vector2.zero, Vector2.zero, fontAsset, 15, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);
        RectTransform dbgTxtRt = debugStatusTmp.GetComponent<RectTransform>();
        dbgTxtRt.anchorMin = Vector2.zero;
        dbgTxtRt.anchorMax = Vector2.one;
        debugStatusTmp.text = "DEBUG: TẮT";

        // Wire NetworkRoleDetailUI SerializedObject
        SerializedObject so = new SerializedObject(detailUI);
        so.FindProperty("panelRoot").objectReferenceValue = backdropObj;
        so.FindProperty("backdropCloseButton").objectReferenceValue = backdropBtn;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("headerText").objectReferenceValue = headerTmp;
        so.FindProperty("playerNameText").objectReferenceValue = playerNameTmp;
        so.FindProperty("roleNameText").objectReferenceValue = roleNameTmp;
        so.FindProperty("factionText").objectReferenceValue = factionTmp;
        so.FindProperty("roleImage").objectReferenceValue = roleImg;
        so.FindProperty("descriptionText").objectReferenceValue = descTmp;
        so.FindProperty("skillNameText").objectReferenceValue = skillNameTmp;
        so.FindProperty("skillDescriptionText").objectReferenceValue = skillDescTmp;
        so.FindProperty("restrictionsText").objectReferenceValue = restrictTmp;
        so.FindProperty("debugModeToggleButton").objectReferenceValue = debugBtn;
        so.FindProperty("debugModeStatusText").objectReferenceValue = debugStatusTmp;

        // Copy roleImages from identityCard
        if (identityCard != null)
        {
            NetworkPlayerIdentityCardUI idUI = identityCard.GetComponent<NetworkPlayerIdentityCardUI>();
            if (idUI != null)
            {
                SerializedObject idSo = new SerializedObject(idUI);
                SerializedProperty srcImages = idSo.FindProperty("roleImages");
                SerializedProperty destImages = so.FindProperty("roleImages");

                if (srcImages != null && destImages != null)
                {
                    destImages.arraySize = srcImages.arraySize;
                    for (int i = 0; i < srcImages.arraySize; i++)
                    {
                        SerializedProperty srcElem = srcImages.GetArrayElementAtIndex(i);
                        SerializedProperty destElem = destImages.GetArrayElementAtIndex(i);
                        destElem.FindPropertyRelative("role").intValue = srcElem.FindPropertyRelative("role").intValue;
                        destElem.FindPropertyRelative("image").objectReferenceValue = srcElem.FindPropertyRelative("image").objectReferenceValue;
                    }
                }
            }
        }

        so.ApplyModifiedProperties();

        // Nền backdrop mặc định là tắt khi khởi động (chỉ bật khi người chơi click Player Card)
        backdropObj.SetActive(false);
        // GameRoleDetailPanel luôn ở lớp trên cùng (LastSibling)
        panelObj.transform.SetAsLastSibling();

        return panelObj;
    }

    private static Button CreateOrUpdateHUDPlayerListButton(Transform canvasTrans, TMP_FontAsset fontAsset, GameObject targetPanel)
    {
        Transform existing = canvasTrans.Find("PlayerListHUDButton");
        GameObject btnObj;
        if (existing == null)
        {
            btnObj = new GameObject("PlayerListHUDButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            btnObj = existing.gameObject;
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20, -345);
        rt.sizeDelta = new Vector2(280, 78);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI txt = EnsureChildTMP(btnObj.transform, "Text (TMP)", Vector2.zero, Vector2.zero, fontAsset, 24, FontStyles.Bold, new Color(1f, 0.88f, 0.35f), TextAlignmentOptions.Center);
        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txt.text = "DANH SÁCH NGƯỜI CHƠI";

        return btn;
    }

    private static Button CreateOrUpdateInGameLeaveButton(Transform canvasTrans, TMP_FontAsset fontAsset)
    {
        Transform existing = canvasTrans.Find("InGameLeaveButton");
        GameObject btnObj;
        if (existing == null)
        {
            btnObj = new GameObject("InGameLeaveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasTrans, false);
        }
        else
        {
            btnObj = existing.gameObject;
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20, -435);
        rt.sizeDelta = new Vector2(280, 72);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.48f, 0.12f, 0.12f, 0.95f);
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            Debug.Log("[NetworkGameManager] InGameLeaveButton clicked -> LeaveRoomToMainMenu");
            SceneLoader.Instance?.LeaveRoomToMainMenu();
        });

        TextMeshProUGUI txt = EnsureChildTMP(btnObj.transform, "Text (TMP)", Vector2.zero, Vector2.zero, fontAsset, 24, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txt.text = "THOÁT PHÒNG";

        return btn;
    }

    private static TextMeshProUGUI EnsureChildTMP(Transform parent, string name, Vector2 pos, Vector2 size, TMP_FontAsset font, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        Transform child = parent.Find(name);
        TextMeshProUGUI tmp;
        if (child == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            tmp = obj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            tmp = child.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        if (size != Vector2.zero)
        {
            rt.sizeDelta = size;
        }

        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        return tmp;
    }
}
