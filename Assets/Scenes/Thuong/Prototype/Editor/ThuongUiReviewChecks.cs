using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thuong.Online;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class ThuongRoomUpgradeOnlineChecks
{
    private const string UiReviewOutput = "Logs/MainIntegration20261127";

    [MenuItem("Tools/Thuong/Project Review/Check Lobby And Match UI")]
    public static void RunUiReview()
    {
        Directory.CreateDirectory(UiReviewOutput);
        SessionState.SetBool(RunKey + ".UiReview", true);
        Run();
    }

    [MenuItem("Tools/Thuong/Project Review/Check Scenes And Gameplay Regressions")]
    public static void RunProjectRegressions()
    {
        Directory.CreateDirectory(UiReviewOutput);
        foreach (var entry in EditorBuildSettings.scenes.Where(entry => entry.enabled))
        {
            var scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).Any(c => c == null))
                    throw new InvalidOperationException("Missing scripts in " + entry.path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        ThuongReviewImprovementChecks.Run();
        ThuongRoomUpgradeChecks.Run();
        ThuongMinigameChecks.Run();
        ThuongCommunicationChecks.Run();
        ThuongEndgameChecks.Run();
        ThuongEmoteSystemChecks.Run();
        File.WriteAllText(UiReviewOutput + "/regressions-result.txt", "THUONG_PROJECT_REVIEW_REGRESSIONS_PASSED\n");
        Debug.Log("THUONG_PROJECT_REVIEW_REGRESSIONS_PASSED");
    }

    private static IEnumerator ExerciseUiReview()
    {
        lastStep = "UI: starting four-player LAN room";
        Check((bool)Call(host, "SetHostMapSelection", false, "village", GameEventType.None),
            "UI: village map selected for reproducible gameplay screenshots");
        Set(host, "portText", Port.ToString()); Call(host, "HostLan");
        while (!Get<bool>(host, "connected")) yield return null;
        Check((bool)Call(host, "SetRoomRules", new ThuongRoomRules
        { preset = ThuongRolePreset.Basic, automaticTimes = false, day = 300 }), "UI: manual room rules applied");
        AddClient(0); AddClient(1); AddClient(2);
        while (Net(host).ConnectedClientsIds.Count != 4 || !(bool)Property(host, "AllMapsReady")) yield return null;
        RefreshUi();
        var lobby = Get<Canvas>(host, "lobbyCanvas");
        var hud = Get<GameHUD>(host, "sceneHud");
        Check(lobby.gameObject.activeInHierarchy && !Get<Canvas>(host, "sceneHudCanvas").enabled,
            "UI: lobby visible without gameplay HUD");
        CaptureUi("lobby");

        // Leave an independently sorted lobby modal open during the role transition.
        Call(host, "OpenRulesSettings");
        NetworkGameRoleSetup.Save(new List<RoleAmountData> { new(RoleType.Villager, 3), new(RoleType.DogSpirit, 1) });
        Call(host, "AssignLobbyRolesOnServer");
        while (clients.Any(client => !client.HasLocalRole)) yield return null;
        host.ViewLocalRole(); RefreshUi();
        Check(host.ShowRoleCard && !lobby.gameObject.activeInHierarchy &&
            !Get<GameObject>(host, "rulesSettingsRoot").activeSelf,
            "UI: role reveal hides lobby and closes its independently sorted settings");
        Check(lobby.GetComponentsInChildren<Canvas>(true).All(canvas => !canvas.isActiveAndEnabled),
            "UI: hidden lobby has no active child canvases");
        CaptureUi("role");
        host.ReadLocalRole(); RefreshUi();
        Check(lobby.gameObject.activeInHierarchy, "UI: confirming role restores lobby controls");
        var actionType = typeof(ThuongOnlineSession).Assembly.GetType("Thuong.Online.ThuongOnlineAction");
        foreach (var member in Members())
        {
            Call(host, "ApplyServerAction", Get<ulong>(member, "ClientId"), 2u, Enum.Parse(actionType, "RevealRead"), -1, -1,
                GameRoleManager.Instance.currentDay, GameRoleManager.Instance.currentPhase, host.MatchEpoch);
            Call(host, "SetServerReady", Get<ulong>(member, "ClientId"), true);
        }
        Call(host, "StartMatchOnServer");
        while (clients.Any(client => !client.MatchStarted)) yield return null;
        RefreshUi();
        Check(host.MatchStarted && !lobby.gameObject.activeInHierarchy && Get<Canvas>(host, "sceneHudCanvas").enabled,
            "UI: starting match completely deactivates lobby and restores gameplay HUD");
        Check(Get<CanvasGroup>(host, "sceneHudVisibility").alpha == 1, "UI: HUD controller remains active and restores its content");
        Check(!Get<TMP_Text>(host, "realtimePrompt").gameObject.activeSelf,
            "UI: daytime task guidance has no duplicate interaction prompt");
        var roleCanvas = GameObject.Find("Role Ability Canvas");
        CleanGhostCanvases.CleanActiveSceneIfApplicable();
        Check(roleCanvas != null && roleCanvas.GetComponent<Canvas>() != null,
            "UI: editor ghost cleanup preserves the live role ability canvas");
        var minimap = Get<RectTransform>(host, "minimapPanel");
        var playerMarkers = Get<System.Collections.IDictionary>(host, "minimapPlayerMarkers");
        var minimapPlayer = Get<Image>(playerMarkers[Get<int>(host, "localPlayerId")], "Image");
        Check(minimap.gameObject.activeInHierarchy && Get<Image>(host, "minimapMapImage").sprite == host.SelectedMap.thumbnail,
            "UI: gameplay minimap displays the selected map artwork");
        Check(Get<Dictionary<int, Image>>(host, "minimapStationMarkers").Values.Count(marker => marker.gameObject.activeSelf) ==
            Get<List<TaskData>>(TaskManager.Instance, "currentTasks").Count,
            "UI: minimap marks the active daily task stations");
        Get<Button>(host, "minimapHeaderButton").onClick.Invoke();
        Check(!Get<RectTransform>(host, "minimapViewport").gameObject.activeSelf && minimap.gameObject.activeInHierarchy,
            "UI: main minimap collapse control remains available");
        Get<Button>(host, "minimapHeaderButton").onClick.Invoke();
        Check(Get<RectTransform>(host, "minimapViewport").gameObject.activeSelf,
            "UI: main minimap expands again before gameplay checks");
        var localTag = Object.FindObjectsByType<PlayerNameTag>().First(tag => tag.GetComponent<TMP_Text>()?.text.StartsWith("P01") == true);
        var localAvatar = localTag.GetComponentInParent<Assets.Scripts.Thuong.PlayerMovement>().transform;
        Vector3 originalPosition = localAvatar.position;
        Vector2 originalAnchor = minimapPlayer.rectTransform.anchorMin;
        localAvatar.position += Vector3.right * 1.4f; Call(host, "RefreshMinimap");
        Check(minimapPlayer.rectTransform.anchorMin.x > originalAnchor.x,
            "UI: minimap local-player marker follows movement");
        localAvatar.position = originalPosition; Call(host, "RefreshMinimap");
        var projection = typeof(ThuongOnlineSession).GetMethod("MinimapWorldRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        foreach (var map in Resources.Load<Thuong.Maps.ThuongMapCatalog>(Thuong.Maps.ThuongMapCatalog.ResourcePath).Released)
        {
            Rect area = (Rect)projection.Invoke(null, new object[] { map });
            Check(area.Contains(map.boundsMin) && area.Contains(map.boundsMax) &&
                Vector2.Distance(area.center, (map.boundsMin + map.boundsMax) / 2) < .001f,
                "UI: minimap projection includes the full map and thumbnail margins: " + map.mapId);
        }
        foreach (var tag in Object.FindObjectsByType<PlayerNameTag>())
        {
            var text = tag.GetComponent<TMP_Text>();
            if (text == null) continue;
            text.ForceMeshUpdate();
            Check(text.textBounds.size.x <= 1.25f, "UI: player name stays inside its spawn slot: " + text.text.Replace('\n', ' '));
        }
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1600, 1200), new Vector2Int(2560, 1080) })
        {
            CaptureUi("match", size.x, size.y);
        }

        Call(host, "ToggleNotebook"); RefreshUi();
        Check(!minimap.gameObject.activeInHierarchy, "UI: notebook hides the gameplay minimap");
        Check(!Get<Canvas>(host, "taskGuideCanvas").gameObject.activeInHierarchy &&
            Get<RectTransform>(host, "notebookPanel").GetComponent<Canvas>().sortingOrder > 1800,
            "UI: notebook covers communication controls without task guidance bleeding through");
        var notebook = Get<RectTransform>(host, "notebookPanel");
        var input = Get<TMP_InputField>(host, "notebookInput");
        Check(!WorldRect(input.GetComponent<RectTransform>()).Overlaps(WorldRect(notebook.Find("ĐÓNG") as RectTransform)) &&
            !WorldRect(input.GetComponent<RectTransform>()).Overlaps(WorldRect(notebook.Find("GHI") as RectTransform)) &&
            !WorldRect(input.GetComponent<RectTransform>()).Overlaps(WorldRect(Get<TMP_Text>(host, "notebookHistory").rectTransform)),
            "UI: notebook entry, history and action buttons have separate rows");
        input.text = "Ghi chú kiểm tra tiếng Việt"; Call(host, "SaveNotebookEntry");
        Check(Get<List<string>>(host, "notebookEntries").Count == 1 && input.text.Length == 0,
            "UI: notebook saves a Vietnamese entry and clears its input");
        CaptureUi("notebook"); CaptureUi("notebook", 1920, 1080); Call(host, "ToggleNotebook");
        Call(host, "OpenMatchTutorial"); RefreshUi();
        var tutorial = hud.transform.Find("Match tutorial");
        Check(tutorial.gameObject.activeInHierarchy && tutorial.GetComponent<Canvas>().sortingOrder > 1800 &&
            !Get<Canvas>(host, "taskGuideCanvas").gameObject.activeInHierarchy,
            "UI: tutorial renders above gameplay and chat");
        CaptureUi("tutorial"); Call(host, "CloseTutorial");

        Call(host, "ToggleVillagePanel"); RefreshUi();
        Check(Get<GameObject>(host, "villagePanel").activeInHierarchy &&
            !Get<Canvas>(host, "taskGuideCanvas").gameObject.activeInHierarchy,
            "UI: inventory hides the world task prompt");
        CaptureUi("inventory"); Call(host, "CloseVillagePanel");
        var camera = Get<ThuongTopDownCamera>(host, "worldCamera");
        Get<Button>(host, "minimapMapButton").onClick.Invoke();
        RefreshUi();
        Check(Get<GameObject>(host, "taskMapPanel").activeInHierarchy && !minimap.gameObject.activeInHierarchy,
            "UI: clicking minimap opens the full task map without a duplicate minimap overlay");
        CaptureUi("task-map"); camera.overview = false;
        var game = GameRoleManager.Instance; game.EndDay();
        RefreshUi();
        Check(minimap.gameObject.activeInHierarchy && playerMarkers.Keys.Cast<int>().All(id => id == Get<int>(host, "localPlayerId")),
            "UI: minimap remains available at night without remote-player markers");
        CaptureUi("night");
        game.FinishNight(); game.StartVoting(); RefreshUi();
        Check(hud.meetingPanel.activeInHierarchy, "UI: meeting roster opens during voting");
        Check(!minimap.gameObject.activeInHierarchy, "UI: voting hides the gameplay minimap");
        CaptureUi("voting");

        Call(host, "ResetToRoomLobby", "UI regression replay");
        while (clients.Any(client => client.MatchStarted)) yield return null;
        RefreshUi();
        Check(lobby.gameObject.activeInHierarchy && !Get<Canvas>(host, "sceneHudCanvas").enabled &&
            Get<CanvasGroup>(host, "sceneHudVisibility").alpha == 0,
            "UI: replay restores lobby and hides all gameplay HUD content");
        Check(!minimap.gameObject.activeInHierarchy, "UI: replay hides the minimap in the lobby");
        Check(!Get<Canvas>(host, "villageCanvas").gameObject.activeInHierarchy &&
            !Get<Canvas>(host, "taskGuideCanvas").gameObject.activeInHierarchy,
            "UI: replay removes village and task overlays");
        CaptureUi("replay");
        NetworkGameRoleSetup.Clear();
        Call(host, "LeaveSession");
        while (Get<bool>(host, "busy") || Net(host).IsListening) yield return null;
        RefreshUi();
        Check(!lobby.gameObject.activeInHierarchy && !Get<GameObject>(host, "communicationRoot").activeSelf,
            "UI: leaving room hides lobby and communication controls");
        Call(host, "OpenVillagePractice");
        var practice = Get<Canvas>(host, "practiceCanvas");
        var village = Get<Canvas>(host, "villageCanvas");
        var guidance = Get<Canvas>(host, "taskGuideCanvas");
        // Simulate retained overlays during asynchronous scene/network teardown.
        village.gameObject.SetActive(true); guidance.gameObject.SetActive(true);
        minimap.gameObject.SetActive(true);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var next = UnityEngine.SceneManagement.SceneManager.CreateScene("UI review destination");
        Call(host, "HandleActiveSceneChanged", previous, next);
        Check(!practice.gameObject.activeSelf && !village.gameObject.activeSelf && !guidance.gameObject.activeSelf && !minimap.gameObject.activeSelf,
            "UI: scene change immediately hides practice, village and task overlays before asynchronous teardown");
    }

    private static void RefreshUi()
    {
        // Manual phase changes in this scenario still pass through the production
        // snapshot path before the local HUD reads the replicated visible state.
        if (Net(host).IsListening && Net(host).IsServer) Call(host, "BroadcastSnapshot");
        Call(host, "RefreshLobbyView");
        host.RefreshHud(Get<GameHUD>(host, "sceneHud"));
        Call(host, "RefreshVillageUi");
        Set(host, "nextGuideRefresh", 0f); Call(host, "RefreshTaskGuidance");
        Canvas.ForceUpdateCanvases();
    }

    private static void CheckFooter(GameHUD hud)
    {
        var minimap = Get<RectTransform>(host, "minimapPanel");
        var role = GameObject.Find("Role Ability Canvas")?.transform.Find("Open Role Ability") as RectTransform;
        Check(role != null && !WorldRect(minimap).Overlaps(WorldRect(role)) &&
            !WorldRect(minimap).Overlaps(WorldRect(hud.transform.Find("TopBar") as RectTransform)),
            "UI: minimap does not overlap the role button or phase header");
        var mapImage = Get<Image>(host, "minimapMapImage");
        Check(Mathf.Abs(mapImage.rectTransform.rect.width / mapImage.rectTransform.rect.height -
            mapImage.sprite.rect.width / mapImage.sprite.rect.height) < .005f,
            "UI: minimap artwork keeps its aspect ratio");
        Check(minimap.Cast<Transform>().Where(child => child.name.EndsWith(" legend"))
                .All(legend => !WorldRect(mapImage.rectTransform).Overlaps(WorldRect((RectTransform)legend))),
            "UI: minimap legend is separate from the map artwork and parchment border");
        string[] names = { "Asset credits button", "Footer", "HƯỚNG DẪN", "Xem kết quả vote", "Rời phòng", "SỔ TAY" };
        var emotes = Object.FindAnyObjectByType<Thuong.Emotes.PlayerEmoteWheelUI>();
        var slots = names.Select(name => hud.transform.Find(name) as RectTransform)
            .Append(emotes != null ? emotes.transform.Find("Keyboard hint") as RectTransform : null)
            .Append(Get<Button>(host, "microphoneButton").transform as RectTransform)
            .Append(Get<Button>(host, "speakerButton").transform as RectTransform)
            .Where(rect => rect != null).ToArray();
        for (int i = 0; i < slots.Length; i++)
        for (int j = i + 1; j < slots.Length; j++)
            Check(!WorldRect(slots[i]).Overlaps(WorldRect(slots[j])), "UI: footer slots do not overlap: " + slots[i].name + "/" + slots[j].name);
    }

    private static Rect WorldRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void CaptureUi(string name, int width = 1280, int height = 720)
    {
        string path = $"{UiReviewOutput}/{name}-{width}x{height}.png";
        // Poison pixels outside the camera viewport, simulating a previously drawn
        // lobby. Normal preview capture pre-clears them and masks this regression.
        ThuongMedievalSceneChecks.SavePreview(path, width, height, clearColor: Color.magenta,
            verifyLayout: name == "match" ? () => CheckFooter(Get<GameHUD>(host, "sceneHud")) : (Action)null);
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(path));
            foreach (var point in new[] { new Vector2(.005f, .5f), new Vector2(.995f, .5f), new Vector2(.5f, .995f), new Vector2(.5f, .005f) })
            {
                Color color = texture.GetPixel((int)(point.x * width), (int)(point.y * height));
                Check(color.r < .65f || color.g > .2f || color.b < .65f, $"UI: old screen pixels repainted at {point} in {name} {width}x{height}");
            }
        }
        finally { Object.DestroyImmediate(texture); }
    }
}
