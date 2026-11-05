using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DisconnectTestExecution
{
    // Trigger domain reload batch execution
    [InitializeOnLoadMethod]
    private static void OnEditorReload()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            RunBatch();
        };
    }

    [MenuItem("Tools/Tests/Run Disconnect Tests Batch")]
    public static void RunBatch()
    {
        Debug.Log("[DisconnectTestExecution] Starting batch execution of NetworkDisconnectSyncTests...");
        NetworkDisconnectSyncTests.RunAllTests();
        Debug.Log("[DisconnectTestExecution] Starting batch execution of NetworkGameOverTests...");
        NetworkGameOverTests.RunAllTests();
        Debug.Log("[DisconnectTestExecution] Completed batch execution.");
    }
}
