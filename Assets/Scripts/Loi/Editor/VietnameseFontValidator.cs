using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VietnameseFontValidator
{
    public const string VietnameseSampleText =
        "aAàÀảẢãÃáÁạẠăĂằẰẳẲẵẴắẮặẶâÂầẦẩẨẫẪấẤậẬbBcCdDđĐeEèÈẻẺẽẼéÉẹẸêÊềỀểỂễỄếẾệỆfFgGhHiIìÌỉỈĩĨíÍịỊjJkKlLmMnNoOòÒỏỎõÕóÓọỌôÔồỒổỔỗỖốỐộỘơƠờỜởỞỡỠớỚợỢpPqQrRsStTuUùÙủỦũŨúÚụỤưƯừỪửỬữỮứỨựỰvVwWxXyYỳỲỷỶỹỸýÝỵỴzZ0123456789!@#$%^&*()_+-=[]{}|;':\",./<>?~` " +
        "Dân làng Ma sói Tiên tri Bảo vệ Thị trưởng Kẻ ngốc Bạch khuyển Xà tinh Yêu tinh Thợ săn Pháp sư Dệt duyên Kẻ bị nguyền Trẻ ranh Kẻ điên Cáo tinh Sát nhân Còn sống Đã chết Đã bị loại Bí mật Chưa tiết lộ Kỹ năng Cách sử dụng Giới hạn Điều kiện Đóng Thông tin vai trò Người chơi Phe Phiếu Bỏ phiếu Thảo luận Ban đêm Ban ngày";

    [MenuItem("Tools/Tests/Verify Vietnamese Font Glyphs")]
    public static bool VerifyAndEnsureVietnameseGlyphs()
    {
        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM TRA FONT GLYPH TIẾNG VIỆT (TMP)");
        Debug.Log("=================================================");

        TMP_FontAsset mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        );

        if (mainFont == null)
        {
            Debug.LogError("[FAIL] Không tìm thấy LiberationSans SDF.asset!");
            return false;
        }

        TMP_FontAsset fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
        );

        // Ensure fallbackFont is in mainFont fallback table
        if (fallbackFont != null)
        {
            if (mainFont.fallbackFontAssetTable == null)
            {
                mainFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            if (!mainFont.fallbackFontAssetTable.Contains(fallbackFont))
            {
                mainFont.fallbackFontAssetTable.Insert(0, fallbackFont);
                EditorUtility.SetDirty(mainFont);
                Debug.Log("[FONT] Đã gắn LiberationSans SDF - Fallback vào đầu bảng fallback.");
            }

            // Ensure fallbackFont has all characters pre-added
            bool added = fallbackFont.TryAddCharacters(VietnameseSampleText, out string missingChars);
            EditorUtility.SetDirty(fallbackFont);
            if (!string.IsNullOrEmpty(missingChars))
            {
                Debug.LogWarning($"[FONT] Ký tự chưa thêm được vào fallback: {missingChars}");
            }
            else
            {
                Debug.Log("[FONT] Toàn bộ ký tự tiếng Việt đã được nạp thành công vào Fallback Font Atlas!");
            }
        }

        // Now test all sample characters
        List<char> missingGlyphs = new List<char>();
        HashSet<char> uniqueChars = new HashSet<char>(VietnameseSampleText);

        foreach (char c in uniqueChars)
        {
            bool hasChar = mainFont.HasCharacter(c, searchFallbacks: true);
            if (!hasChar)
            {
                missingGlyphs.Add(c);
            }
        }

        if (missingGlyphs.Count == 0)
        {
            Debug.Log($"[PASS] KIỂM TRA FONT: 100% ký tự tiếng Việt ({uniqueChars.Count} ký tự) đều có glyph hợp lệ!");
            Debug.Log("=================================================");
            return true;
        }
        else
        {
            Debug.LogError($"[FAIL] KIỂM TRA FONT: Còn thiếu {missingGlyphs.Count} ký tự: {new string(missingGlyphs.ToArray())}");
            Debug.Log("=================================================");
            return false;
        }
    }
}
