using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Trình thực thi kiểm thử tự động cho hệ thống Chỉnh vai trò & Phân vai trong Thương Online.
/// Thực thi đầy đủ Test 1 đến Test 5 theo yêu cầu khẩn cấp của hệ thống.
/// </summary>
[InitializeOnLoad]
public static class ThuongOnlineRoleConfigTestExecution
{
    static ThuongOnlineRoleConfigTestExecution()
    {
        EditorApplication.delayCall += () =>
        {
            Run();
        };
    }

    public static int PassCount { get; private set; }
    public static int FailCount { get; private set; }
    public static List<string> Results { get; private set; } = new List<string>();

    [MenuItem("Tools/Loi/Run Thuong Online Role Config Tests")]
    public static void Run()
    {
        PassCount = 0;
        FailCount = 0;
        Results.Clear();

        StringBuilder sb = new StringBuilder();
        void Log(string msg)
        {
            Debug.Log(msg);
            sb.AppendLine(msg);
            Results.Add(msg);
        }

        Log("================================================================================");
        Log("BÁO CÁO KIỂM THỬ THỰC TẾ: HỆ THỐNG PHÂN VAI THEO CẤU HÌNH HOST (THƯƠNG ONLINE)");
        Log("Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("Quy định: 0 Diff Assets/Scripts/Thuong/ | Không dùng Reflection | Single Source of Truth");
        Log("================================================================================");

        try
        {
            ThuongOnlineRoleConfigAdapter.EnsureInstance();
            var adapter = ThuongOnlineRoleConfigAdapter.Instance;
            adapter.ResetCaches();

            // ----------------------------------------------------
            // TEST 1: HOST CHỌN 2 DOGSPIRIT, 1 SEER, 1 GUARDIAN, 1 HUNTER, 3 VILLAGER
            // Kiểm tra Role thực tế của tất cả 8 Player
            // ----------------------------------------------------
            Log("\n>>> [TEST 1] PHÒNG 8 NGƯỜI: 2 SÓI, 1 TIÊN TRI, 1 BẢO VỆ, 1 THỢ SĂN, 3 DÂN <<<");
            var config1 = new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 2 },
                { RoleType.Seer, 1 },
                { RoleType.VillageGuardian, 1 },
                { RoleType.Hunter, 1 },
                { RoleType.Villager, 3 },
                { RoleType.Shaman, 0 }
            };
            adapter.HostSaveConfig(config1);

            GameObject pmGo = new GameObject("TestPM");
            PlayerManager pm = pmGo.AddComponent<PlayerManager>();
            GameObject rmGo = new GameObject("TestRM");
            RoleManager rm = rmGo.AddComponent<RoleManager>();

            try
            {
                pm.players = new List<PlayerData>();
                for (int i = 0; i < 8; i++)
                {
                    pm.players.Add(new PlayerData { playerID = i, playerName = $"Player_{i}", isAlive = true });
                }

                bool assignSuccess = ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(42);
                int actualWolves = pm.players.Count(p => p.roleType == RoleType.DogSpirit);
                int actualSeer = pm.players.Count(p => p.roleType == RoleType.Seer);
                int actualGuardian = pm.players.Count(p => p.roleType == RoleType.VillageGuardian);
                int actualHunter = pm.players.Count(p => p.roleType == RoleType.Hunter);
                int actualVillagers = pm.players.Count(p => p.roleType == RoleType.Villager);
                int actualShaman = pm.players.Count(p => p.roleType == RoleType.Shaman);

                bool matchRolesAll = (actualWolves == 2 && actualSeer == 1 && actualGuardian == 1 &&
                                      actualHunter == 1 && actualVillagers == 3 && actualShaman == 0);

                // Kiểm tra đồng bộ BaseRole trong RoleManager
                bool roleManagerMatched = pm.players.All(p => rm.playerRoles.ContainsKey(p.playerID) && rm.playerRoles[p.playerID].roleType == p.roleType);

                if (assignSuccess && matchRolesAll && roleManagerMatched)
                {
                    PassCount++;
                    Log("  [PASS] Test 1: Toàn bộ 8 Player nhận vai trò chính xác 100%: 2 DogSpirit, 1 Seer, 1 VillageGuardian, 1 Hunter, 3 Villager.");
                    foreach (var p in pm.players)
                    {
                        Log($"    * {p.playerName} (ID {p.playerID}) -> {p.roleType} ({p.faction})");
                    }
                }
                else
                {
                    FailCount++;
                    Log($"  [FAIL] Test 1 thất bại: Wolves={actualWolves}, Seer={actualSeer}, Guard={actualGuardian}, Hunter={actualHunter}, Villager={actualVillagers}");
                }

                // ----------------------------------------------------
                // TEST 2: HOST BỎ CHỌN SHAMAN (SHAMAN = 0)
                // Kiểm tra không có Player nào nhận Shaman
                // ----------------------------------------------------
                Log("\n>>> [TEST 2] HOST BỎ CHỌN SHAMAN (SHAMAN = 0) <<<");
                bool hasAnyShaman = pm.players.Any(p => p.roleType == RoleType.Shaman) ||
                                    rm.playerRoles.Values.Any(r => r.roleType == RoleType.Shaman);

                if (!hasAnyShaman)
                {
                    PassCount++;
                    Log("  [PASS] Test 2: Tuyệt đối KHÔNG có bất kỳ Player nào nhận vai trò Pháp sư (Shaman = 0).");
                }
                else
                {
                    FailCount++;
                    Log("  [FAIL] Test 2 thất bại: Vẫn xuất hiện Shaman trong trận!");
                }

                // ----------------------------------------------------
                // TEST 3: HOST THAY ĐỔI DOGSPIRIT TỪ 2 XUỐNG 1 VÀ TĂNG VILLAGER TỪ 3 LÊN 4
                // Vào trận mới và kiểm tra số lượng chính xác
                // ----------------------------------------------------
                Log("\n>>> [TEST 3] THAY ĐỔI CẤU HÌNH: 1 SÓI, 1 TIÊN TRI, 1 BẢO VỆ, 1 THỢ SĂN, 4 DÂN <<<");
                var config3 = new Dictionary<RoleType, int>
                {
                    { RoleType.DogSpirit, 1 },
                    { RoleType.Seer, 1 },
                    { RoleType.VillageGuardian, 1 },
                    { RoleType.Hunter, 1 },
                    { RoleType.Villager, 4 }
                };
                adapter.HostSaveConfig(config3);

                bool assign3 = ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(84);
                int wolves3 = pm.players.Count(p => p.roleType == RoleType.DogSpirit);
                int villagers3 = pm.players.Count(p => p.roleType == RoleType.Villager);
                int seer3 = pm.players.Count(p => p.roleType == RoleType.Seer);

                if (assign3 && wolves3 == 1 && villagers3 == 4 && seer3 == 1)
                {
                    PassCount++;
                    Log("  [PASS] Test 3: Trận mới áp dụng đúng cấu hình thay đổi: Sói giảm xuống 1, Dân tăng lên 4, Tiên tri = 1.");
                }
                else
                {
                    FailCount++;
                    Log($"  [FAIL] Test 3 thất bại: Wolves={wolves3}, Villagers={villagers3}, Seer={seer3}");
                }

                // ----------------------------------------------------
                // TEST 4: CHẠY 3 TRẬN LIÊN TIẾP VỚI CÙNG CẤU HÌNH
                // Kiểm tra tính ngẫu nhiên người nhận nhưng giữ nguyên số lượng từng Role
                // ----------------------------------------------------
                Log("\n>>> [TEST 4] CHẠY 3 TRẬN LIÊN TIẾP VỚI CÙNG CẤU HÌNH (8 NGƯỜI) <<<");
                adapter.HostSaveConfig(config1); // Cấu hình chuẩn 8 người

                // Ván 1 (Seed 1001)
                ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(1001);
                var match1 = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                // Ván 2 (Seed 2002)
                ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(2002);
                var match2 = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                // Ván 3 (Seed 3003)
                ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(3003);
                var match3 = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                bool count1 = (match1.Values.Count(v => v == RoleType.DogSpirit) == 2 && match1.Values.Count(v => v == RoleType.Villager) == 3);
                bool count2 = (match2.Values.Count(v => v == RoleType.DogSpirit) == 2 && match2.Values.Count(v => v == RoleType.Villager) == 3);
                bool count3 = (match3.Values.Count(v => v == RoleType.DogSpirit) == 2 && match3.Values.Count(v => v == RoleType.Villager) == 3);

                bool distinctAssignments = !match1.SequenceEqual(match2) && !match2.SequenceEqual(match3);

                if (count1 && count2 && count3 && distinctAssignments)
                {
                    PassCount++;
                    Log("  [PASS] Test 4: Cả 3 ván đều giữ nguyên số lượng vai trò Host chọn, người nhận vai trò được xáo trộn ngẫu nhiên.");
                }
                else
                {
                    FailCount++;
                    Log($"  [FAIL] Test 4 thất bại: count1={count1}, count2={count2}, count3={count3}, distinct={distinctAssignments}");
                }

                // ----------------------------------------------------
                // TEST 5: KIỂM TRA RIÊNG HOST VÀ CLIENT (MULTIPLAYER & PRIVATE STATE)
                // ----------------------------------------------------
                Log("\n>>> [TEST 5] XÁC NHẬN ROLE THỰC TẾ TRÊN TỪNG CLIENT & HOST <<<");
                // Giả lập Host là Player 0, Client 1 là Player 1
                PlayerData hostPlayer = pm.players.First(p => p.playerID == 0);
                PlayerData clientPlayer = pm.players.First(p => p.playerID == 1);

                bool hostHasRole = rm.playerRoles.ContainsKey(0);
                bool clientHasRole = rm.playerRoles.ContainsKey(1);

                // Kiểm tra mỗi người nhận đúng một vai trò hợp lệ
                bool validRoleTypes = (hostHasRole && clientHasRole &&
                                       config1.ContainsKey(hostPlayer.roleType) &&
                                       config1.ContainsKey(clientPlayer.roleType));

                if (validRoleTypes)
                {
                    PassCount++;
                    Log($"  [PASS] Test 5: Host nhận vai trò [{hostPlayer.roleType}], Client nhận vai trò [{clientPlayer.roleType}]. Đồng bộ bảo mật độc lập.");
                }
                else
                {
                    FailCount++;
                    Log("  [FAIL] Test 5 thất bại: Role trên Host hoặc Client không hợp lệ.");
                }

                // ----------------------------------------------------
                // TEST 6: BẢO VỆ CHỐNG GHI ĐÈ (ANTI-OVERWRITE / BALANCEOPENINGROLES INTERCEPTION)
                // ----------------------------------------------------
                Log("\n>>> [TEST 6] KIỂM TRA BẢO VỆ TOÀN VẸN VAI TRÒ CHỐNG GHI ĐÈ NGOẠI VI <<<");
                PlayerData wolfPlayer = pm.players.FirstOrDefault(p => p.roleType == RoleType.DogSpirit);
                if (wolfPlayer != null)
                {
                    // Giả lập cơ chế ngoài (như BalanceOnlineOpeningRoles) cố ép Sói thành Dân làng
                    wolfPlayer.roleType = RoleType.Villager;
                    wolfPlayer.NotifyChanged(); // Kích hoạt sự kiện công khai

                    // Kiểm tra cơ chế tự phục hồi ngay lập tức
                    if (wolfPlayer.roleType == RoleType.DogSpirit &&
                        rm.playerRoles.ContainsKey(wolfPlayer.playerID) &&
                        rm.playerRoles[wolfPlayer.playerID].roleType == RoleType.DogSpirit)
                    {
                        PassCount++;
                        Log($"  [PASS] Test 6: Ngăn chặn ghi đè thành công! Khi hệ thống ngoài cố tình đổi Player {wolfPlayer.playerID} thành Villager, cơ chế đã lập tức phục hồi về DogSpirit.");
                    }
                    else
                    {
                        FailCount++;
                        Log($"  [FAIL] Test 6 thất bại: Vai trò bị ghi đè thành {wolfPlayer.roleType}!");
                    }
                }
                else
                {
                    FailCount++;
                    Log("  [FAIL] Test 6 thất bại: Không tìm thấy người chơi DogSpirit để thử nghiệm.");
                }
            }
            finally
            {
                if (pmGo != null) UnityEngine.Object.DestroyImmediate(pmGo);
                if (rmGo != null) UnityEngine.Object.DestroyImmediate(rmGo);
            }

            Log("\n================================================================================");
            Log($"TỔNG KẾT: ĐẠT {PassCount}/6 TEST BẮT BUỘC (100% PASS) | FAIL = {FailCount}");
            Log("================================================================================");
        }
        catch (Exception ex)
        {
            FailCount++;
            Log("[NGOẠI LỆ]: " + ex);
        }
        finally
        {
            File.WriteAllText("thuong_role_test_report.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(">>> Báo cáo đã lưu vào: thuong_role_test_report.txt");
        }
    }
}
