using System;
using UnityEditor;
using UnityEngine;

public static class VoteTestExecution
{
    [InitializeOnLoadMethod]
    private static void OnEditorReload()
    {
        EditorApplication.delayCall += () =>
        {
            RunTests();
        };
    }

    [MenuItem("Tools/Tests/Execute All Vote Tests Batch")]
    public static void RunTests()
    {
        Debug.Log("[VoteTestExecution] Tự động thực thi toàn bộ test hệ thống Voting...");
        NetworkVoteSystemTests.RunAllTests();
        Debug.Log($"[VoteTestExecution] Hoàn tất kiểm thử Voting: PASS={NetworkVoteSystemTests.PassCount}, FAIL={NetworkVoteSystemTests.FailCount}");
    }
}
