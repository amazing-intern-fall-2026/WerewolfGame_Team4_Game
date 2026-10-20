using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ChatTestExecution
{
    [MenuItem("Tools/Loi/Run Chat System Tests And Export Report")]
    public static void Run()
    {
        ChatSystemTests.RunAllTests();
    }
}
