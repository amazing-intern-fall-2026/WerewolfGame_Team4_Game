using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GameBackgroundTestExecution
{
    private const string ReportPath = "Assets/Scripts/Loi/Editor/background_test_results.txt";

    [InitializeOnLoadMethod]
    private static void OnEditorReload()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
            RunAndSaveReport();
        };
    }

    [MenuItem("Tools/Loi/Run Background Tests And Save Report")]
    public static void RunAndSaveReport()
    {
        bool success = GameBackgroundTests.RunAllTests();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[GAME BACKGROUND TEST RESULTS - {DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
        sb.AppendLine($"Success: {success}");
        sb.AppendLine($"Pass: {GameBackgroundTests.PassCount}, Fail: {GameBackgroundTests.FailCount}");
        sb.AppendLine("----------------------------------------------------------");
        foreach (var log in GameBackgroundTests.TestLogs)
        {
            sb.AppendLine(log);
        }
        sb.AppendLine("----------------------------------------------------------");
        File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[GameBackgroundTestExecution] Wrote results to {ReportPath}");
    }
}

