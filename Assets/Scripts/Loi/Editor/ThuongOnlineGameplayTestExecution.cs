using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runner tự động chạy toàn bộ Test Suites của Thương Online:
/// 1. ThuongOnlineSerpentSpiritTests (14 test cases)
/// 2. ThuongOnlineWeaverOfFateTests (14 test cases)
/// 3. ThuongOnlineShamanTests (16 test cases)
/// </summary>
public static class ThuongOnlineGameplayTestExecution
{
    [MenuItem("Tools/Tests/Run All Gameplay Tests (SerpentSpirit + WeaverOfFate + Shaman)")]
    public static void RunAll()
    {
        Debug.Log("==================================================================");
        Debug.Log("BẮT ĐẦU CHẠY TỔNG HỢP KIỂM THỬ: XÀ TINH + ÔNG TƠ BÀ NGUYỆT + PHÁP SƯ");
        Debug.Log("==================================================================");

        try
        {
            Debug.Log(">>> [1/3] CHẠY TEST XÀ TINH (SERPENT SPIRIT)...");
            ThuongOnlineSerpentSpiritTests.RunAllTests();

            Debug.Log(">>> [2/3] CHẠY TEST ÔNG TƠ BÀ NGUYỆT (WEAVER OF FATE)...");
            ThuongOnlineWeaverOfFateTests.RunAllTests();

            Debug.Log(">>> [3/3] CHẠY TEST PHÁP SƯ (SHAMAN)...");
            ThuongOnlineShamanTests.RunAllTests();

            Debug.Log("==================================================================");
            Debug.Log("TẤT CẢ TEST SUITES ĐÃ HOÀN TẤT THÀNH CÔNG!");
            Debug.Log("==================================================================");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayTestExecution] Có lỗi phát sinh khi chạy batch tests: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
