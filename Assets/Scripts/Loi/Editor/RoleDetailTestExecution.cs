using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RoleDetailTestExecution
{
    public static void Run()
    {
        StringBuilder sb = new StringBuilder();
        void Log(string s)
        {
            Debug.Log(s);
            sb.AppendLine(s);
        }

        Log("=================================================================");
        Log("BẮT ĐẦU CHẠY CÀI ĐẶT VÀ BỘ KIỂM THỬ TỰ ĐỘNG ROLE DETAIL & PLAYER CARD");
        Log("Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("=================================================================");

        try
        {
            Log(">>> [BƯỚC 1] CÀI ĐẶT UI: PLAYER CARD VÀ ROLE DETAIL PANEL <<<");
            RoleDetailUIInstaller.InstallAll();
            Log("[BƯỚC 1 HOÀN TẤT]");

            Log(">>> [BƯỚC 2] KIỂM THỬ HỆ THỐNG <<<");
            RoleDetailSystemTests.RunAllTests();

            foreach (var l in RoleDetailSystemTests.TestLogs)
            {
                sb.AppendLine(l);
            }

            Log($"[KẾT QUẢ TỔNG HỢP]: PASS = {RoleDetailSystemTests.PassCount} | FAIL = {RoleDetailSystemTests.FailCount}");
        }
        catch (Exception ex)
        {
            Log("[LỖI NGOẠI LỆ]: " + ex.ToString());
            Debug.LogException(ex);
        }
        finally
        {
            File.WriteAllText("ui_test_report.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(">>> ĐÃ GHI BÁO CÁO VÀO: ui_test_report.txt");
        }
    }
}
