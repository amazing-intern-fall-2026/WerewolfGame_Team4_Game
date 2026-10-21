using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ThuongMapPlayerBuild
{
    public static void VisualsAndQaBuild() { ThuongMapVisualChecks.Run(); BuildQa(); }
    public static void InstallVerifyAndBuild() { ThuongMapInstaller.Install(); ThuongMapVisualChecks.Run(); BuildQa(); }
    public static void BuildReleaseAndOnlineChecks() { BuildRelease(); ThuongMinigameOnlineChecks.Run(); }
    public static void FinalizeAssetsAndPlayer() { ThuongMapInstaller.Install(); ThuongMapVisualChecks.Run(); BuildRelease(); }
    public static void FinishAllMapsAndChecks() { ThuongMapInstaller.Install(); ThuongMapVisualChecks.Run(); BuildQa(); BuildRelease(); ThuongMinigameOnlineChecks.Run(); }
    public static void BuildBothPlayers() { BuildQa(); BuildRelease(); }
    public static void VerifyShutdownAndBuild() { BuildBothPlayers(); ThuongMinigameOnlineChecks.Run(); }
    [MenuItem("Tools/Thuong/Maps/Build Independent Multiplayer QA")]
    public static void BuildQa() => Build(true);
    [MenuItem("Tools/Thuong/Maps/Build Windows Player")]
    public static void BuildRelease() => Build(false);
    private static void Build(bool qa)
    {
        Directory.CreateDirectory("Logs/Maps/"+(qa?"QaPlayer":"Player"));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=new[]{"Assets/Scenes/Thuong/OnlineSceneGamePlay.unity"},target=BuildTarget.StandaloneWindows64,
            locationPathName="Logs/Maps/"+(qa?"QaPlayer":"Player")+"/ThuongMaps.exe",
            options=BuildOptions.Development,
            extraScriptingDefines=qa?new[]{"THUONG_MAP_QA"}:Array.Empty<string>()
        });
        if (report.summary.result!=BuildResult.Succeeded) throw new Exception("Map player build failed: "+report.summary.result);
        Debug.Log("THUONG_MAP_PLAYER_BUILD_PASSED: "+report.summary.totalSize+" bytes; QA="+qa);
    }
}
