using System;
using System.IO;
using System.Linq;
using Thuong.Online;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ThuongMainFlowChecks
{
    public static void VerifyAndBuild()
    {
        ThuongEmoteSystemChecks.Run();
        ThuongMinigameChecks.Run();
        ThuongRoomUpgradeChecks.Run();
        ThuongCommunicationChecks.Run();
        BuildPlayer();
    }

    [MenuItem("Tools/Thuong/Main Flow/Build Multiplayer Verification")]
    public static void BuildPlayer()
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (!scenes.Contains(ThuongMainFlow.GameplayPath) || !scenes[0].EndsWith("/MainMenu.unity"))
            throw new Exception("Main menu and Thuong gameplay must belong to the same normal build.");
        Directory.CreateDirectory("Logs/MainFlow/Player");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = BuildTarget.StandaloneWindows64,
            locationPathName = "Logs/MainFlow/Player/ThuongMainFlow.exe",
            options = BuildOptions.Development,
            extraScriptingDefines = new[] { "THUONG_MAIN_FLOW_QA" }
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Main flow build failed: " + report.summary.result);
        Debug.Log("THUONG_MAIN_FLOW_BUILD_PASSED");
    }
}
