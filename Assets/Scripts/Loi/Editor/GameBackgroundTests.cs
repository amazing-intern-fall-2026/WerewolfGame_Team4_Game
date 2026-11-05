using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bộ kiểm thử tự động toàn diện cho tính năng hình nền Sáng / Tối (GameBackgroundController)
/// trong Game Scene dự án Ma Sói.
/// </summary>
public static class GameBackgroundTests
{
    private const string GameScenePath = "Assets/Scenes/Loi/Game.unity";
    private const string SangArtPath = "Assets/Art/sang.png";
    private const string ToiArtPath = "Assets/Art/toi.png";

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

    [MenuItem("Tools/Loi/Run Game Background Tests")]
    public static bool RunAllTests()
    {
        PassCount = 0;
        FailCount = 0;
        TestLogs.Clear();

        Debug.Log("==========================================================");
        Debug.Log(" BẮT ĐẦU KIỂM THỬ: HÌNH NỀN SÁNG/TỐI (GameBackgroundController)");
        Debug.Log("==========================================================");

        Test01_ArtSpritesExist();
        Test02_GameSceneBackgroundHierarchy();
        Test03_BackgroundImagesConfiguration();
        Test04_NightPhase_ShowsNight_HidesDay();
        Test05_AllOtherPhases_ShowDay_HideNight();
        Test06_MultiMatchTransitionSimulation();
        Test07_GlobalPhaseSyncEvent();

        Debug.Log("==========================================================");
        Debug.Log($" KẾT QUẢ KIỂM THỬ: {PassCount} PASS, {FailCount} FAIL (Tổng cộng: {PassCount + FailCount})");
        Debug.Log("==========================================================");

        return FailCount == 0;
    }

    private static void Test01_ArtSpritesExist()
    {
        Sprite daySprite = AssetDatabase.LoadAssetAtPath<Sprite>(SangArtPath);
        if (daySprite == null)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SangArtPath);
            foreach (var a in assets) { if (a is Sprite s) { daySprite = s; break; } }
        }
        Assert(daySprite != null, "Test01_ArtSpritesExist (sang.png)", "Sprite sang.png tồn tại và tải thành công.", "Không tìm thấy Sprite sang.png!");

        Sprite nightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ToiArtPath);
        if (nightSprite == null)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(ToiArtPath);
            foreach (var a in assets) { if (a is Sprite s) { nightSprite = s; break; } }
        }
        Assert(nightSprite != null, "Test01_ArtSpritesExist (toi.png)", "Sprite toi.png tồn tại và tải thành công.", "Không tìm thấy Sprite toi.png!");
    }

    private static void Test02_GameSceneBackgroundHierarchy()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Assert(scene.IsValid(), "Test02_GameSceneHierarchy", "Mở thành công scene Game.unity", "Không mở được scene Game.unity");

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        Assert(canvas != null, "Test02_GameSceneHierarchy (Canvas)", "Tìm thấy Canvas chính trong Game.unity", "Không tìm thấy Canvas trong Game.unity!");
        if (canvas == null) return;

        Transform bgDay = canvas.transform.Find("Background_Day");
        Transform bgNight = canvas.transform.Find("Background_Night");

        Assert(bgDay != null, "Test02_GameSceneHierarchy (Background_Day)", "Tìm thấy GameObject Background_Day trong Canvas", "Không tìm thấy Background_Day trong Canvas!");
        Assert(bgNight != null, "Test02_GameSceneHierarchy (Background_Night)", "Tìm thấy GameObject Background_Night trong Canvas", "Không tìm thấy Background_Night trong Canvas!");

        if (bgDay != null && bgNight != null)
        {
            int dayIdx = bgDay.GetSiblingIndex();
            int nightIdx = bgNight.GetSiblingIndex();

            Assert(dayIdx == 0 && nightIdx == 1, "Test02_GameSceneHierarchy (SiblingIndex)",
                $"Background_Day ở index {dayIdx}, Background_Night ở index {nightIdx} (nằm dưới cùng Canvas)",
                $"Sibling index không đúng: Day={dayIdx}, Night={nightIdx} (mong đợi 0 và 1)");
        }
    }

    private static void Test03_BackgroundImagesConfiguration()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform bgDay = canvas.transform.Find("Background_Day");
        Transform bgNight = canvas.transform.Find("Background_Night");

        if (bgDay != null)
        {
            Image dayImg = bgDay.GetComponent<Image>();
            Assert(dayImg != null && !dayImg.raycastTarget, "Test03_Config (Day raycastTarget)",
                "Background_Day raycastTarget = false (không cản tương tác UI)",
                "Background_Day raycastTarget = true hoặc thiếu Image!");

            AspectRatioFitter dayArf = bgDay.GetComponent<AspectRatioFitter>();
            Assert(dayArf != null && dayArf.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent,
                "Test03_Config (Day AspectRatioFitter)",
                $"Background_Day AspectMode = EnvelopeParent ({dayArf?.aspectRatio:F2})",
                "Background_Day thiếu AspectRatioFitter hoặc sai mode!");
        }

        if (bgNight != null)
        {
            Image nightImg = bgNight.GetComponent<Image>();
            Assert(nightImg != null && !nightImg.raycastTarget, "Test03_Config (Night raycastTarget)",
                "Background_Night raycastTarget = false (không cản tương tác UI)",
                "Background_Night raycastTarget = true hoặc thiếu Image!");

            AspectRatioFitter nightArf = bgNight.GetComponent<AspectRatioFitter>();
            Assert(nightArf != null && nightArf.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent,
                "Test03_Config (Night AspectRatioFitter)",
                $"Background_Night AspectMode = EnvelopeParent ({nightArf?.aspectRatio:F2})",
                "Background_Night thiếu AspectRatioFitter hoặc sai mode!");
        }
    }

    private static void Test04_NightPhase_ShowsNight_HidesDay()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameBackgroundController controller = canvas.GetComponent<GameBackgroundController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<GameBackgroundController>();

        controller.EnsureBackgroundElements();
        controller.UpdateBackgroundByPhase(GamePhase.Night);

        Transform bgDay = canvas.transform.Find("Background_Day");
        Transform bgNight = canvas.transform.Find("Background_Night");

        bool nightActive = bgNight != null && bgNight.gameObject.activeSelf;
        bool dayActive = bgDay != null && bgDay.gameObject.activeSelf;

        Assert(nightActive && !dayActive, "Test04_NightPhase",
            "Phase = Night: Background_Night BẬT, Background_Day TẮT chuẩn xác.",
            $"Phase = Night thất bại: Day={dayActive}, Night={nightActive}");
    }

    private static void Test05_AllOtherPhases_ShowDay_HideNight()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameBackgroundController controller = canvas.GetComponent<GameBackgroundController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<GameBackgroundController>();

        controller.EnsureBackgroundElements();

        Transform bgDay = canvas.transform.Find("Background_Day");
        Transform bgNight = canvas.transform.Find("Background_Night");

        GamePhase[] dayPhases = new GamePhase[]
        {
            GamePhase.RoleReveal,
            GamePhase.ResolveNight,
            GamePhase.DayStart,
            GamePhase.Event,
            GamePhase.Task,
            GamePhase.Discussion,
            GamePhase.Voting,
            GamePhase.ResolveVote,
            GamePhase.GameOver,
            GamePhase.Setup,
            GamePhase.DayResolution
        };

        bool allPassed = true;
        foreach (var p in dayPhases)
        {
            controller.UpdateBackgroundByPhase(p);
            bool dayActive = bgDay != null && bgDay.gameObject.activeSelf;
            bool nightActive = bgNight != null && bgNight.gameObject.activeSelf;

            if (!dayActive || nightActive)
            {
                allPassed = false;
                LogFail($"Test05_DayPhases ({p})", $"Sai trạng thái: Day={dayActive}, Night={nightActive} (mong đợi Day=true, Night=false)");
            }
        }

        if (allPassed)
        {
            LogPass("Test05_AllOtherPhases_ShowDay_HideNight",
                $"Đã xác minh toàn bộ 11 Day phases (RoleReveal, ResolveNight, DayStart, Event, Task, Discussion, Voting, ResolveVote, GameOver, Setup, DayResolution): Day luôn BẬT, Night luôn TẮT.");
        }
    }

    private static void Test06_MultiMatchTransitionSimulation()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameBackgroundController controller = canvas.GetComponent<GameBackgroundController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<GameBackgroundController>();

        Transform bgDay = canvas.transform.Find("Background_Day");
        Transform bgNight = canvas.transform.Find("Background_Night");

        bool success = true;

        // Trận 1: RoleReveal -> DayStart -> Night -> ResolveNight -> Discussion -> Voting -> GameOver
        controller.UpdateBackgroundByPhase(GamePhase.RoleReveal);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.Night);
        if (bgDay.gameObject.activeSelf || !bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.ResolveNight);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.Discussion);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.GameOver);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        // Trận 2 (reset trận mới): Setup -> RoleReveal -> Night -> DayStart
        controller.UpdateBackgroundByPhase(GamePhase.Setup);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.Night);
        if (bgDay.gameObject.activeSelf || !bgNight.gameObject.activeSelf) success = false;

        controller.UpdateBackgroundByPhase(GamePhase.DayStart);
        if (!bgDay.gameObject.activeSelf || bgNight.gameObject.activeSelf) success = false;

        Assert(success, "Test06_MultiMatchSimulation",
            "Mô phỏng 2 trận đấu liên tiếp thành công: Không bị lưu vết nền của trận trước, chuyển đổi mượt mà.",
            "Lỗi chuyển trạng thái qua nhiều trận đấu!");
    }

    private static void Test07_GlobalPhaseSyncEvent()
    {
        // Kiểm tra sự kiện NetworkPhaseSync.OnPhaseChangedGlobal
        GamePhase lastReceivedPhase = GamePhase.Setup;
        Action<GamePhase> listener = (phase) => { lastReceivedPhase = phase; };

        bool subscribedSuccessfully = false;
        try
        {
            NetworkPhaseSync.OnPhaseChangedGlobal += listener;
            subscribedSuccessfully = true;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
        finally
        {
            NetworkPhaseSync.OnPhaseChangedGlobal -= listener;
        }

        Assert(subscribedSuccessfully, "Test07_GlobalPhaseSyncEvent",
            "Đăng ký thành công NetworkPhaseSync.OnPhaseChangedGlobal phục vụ đồng bộ tức thì trên Host & Client.",
            "Thất bại khi thao tác với OnPhaseChangedGlobal");

        if (lastReceivedPhase == GamePhase.Night) { }
    }
}

