using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DisconnectTestExecution
{
    [MenuItem("Tools/Tests/Run Disconnect Tests Batch")]
    public static void RunBatch()
    {
        Debug.Log("[DisconnectTestExecution] Starting batch execution of NetworkDisconnectSyncTests...");
        NetworkDisconnectSyncTests.RunAllTests();
        Debug.Log("[DisconnectTestExecution] Completed batch execution.");
    }
}
