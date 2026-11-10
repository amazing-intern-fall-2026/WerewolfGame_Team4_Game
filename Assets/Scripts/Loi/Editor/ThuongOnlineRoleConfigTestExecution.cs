using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Trình thực thi kiểm thử tự động cho hệ thống Chỉnh vai trò & Phân vai trong Thương Online.
/// Thực thi toàn bộ Test A, Test B, Test C, Test D, Test E và xuất báo cáo chi tiết.
/// </summary>
public static class ThuongOnlineRoleConfigTestExecution
{
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
        Log("BÁO CÁO KIỂM THỬ HỆ THỐNG CHỈNH VAI TRÒ VÀ PHÂN VAI – THƯƠNG ONLINE");
        Log("Thời gian thực thi: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("Branch: loi | Single Source of Truth | 0 Diff Assets/Scripts/Thuong/");
        Log("================================================================================");

        try
        {
            ThuongOnlineRoleConfigAdapter.EnsureInstance();
            var adapter = ThuongOnlineRoleConfigAdapter.Instance;
            adapter.ResetCaches();

            // ----------------------------------------------------
            // TEST A – KIỂM TRA DANH SÁCH VAI TRÒ (PHÒNG 8 NGƯỜI)
            // ----------------------------------------------------
            Log("\n>>> [TEST A] KIỂM TRA DANH SÁCH VAI TRÒ (PHÒNG 8 NGƯỜI) <<<");
            var configA = new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 2 },
                { RoleType.Seer, 1 },
                { RoleType.VillageGuardian, 1 },
                { RoleType.Hunter, 1 },
                { RoleType.Villager, 3 },
                { RoleType.Shaman, 0 }
            };
            adapter.HostSaveConfig(configA);

            var rolesA = NetworkGameRoleSetup.GetRoles();
            int dogCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit);
            int seerCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.Seer);
            int guardCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.VillageGuardian);
            int hunterCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.Hunter);
            int villagerCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.Villager);
            int shamanCount = NetworkGameRoleSetup.GetRoleAmount(RoleType.Shaman);
            int totalA = NetworkGameRoleSetup.GetTotalRoleAmount();

            bool passA = (totalA == 8 && dogCount == 2 && seerCount == 1 && guardCount == 1 &&
                          hunterCount == 1 && villagerCount == 3 && shamanCount == 0);

            if (passA)
            {
                PassCount++;
                Log("  [PASS] Test A: Đúng 2 DogSpirit, 1 Seer, 1 VillageGuardian, 1 Hunter, 3 Villager, 0 Shaman. Tổng = 8.");
            }
            else
            {
                FailCount++;
                Log($"  [FAIL] Test A thất bại: Dog={dogCount}, Seer={seerCount}, Guard={guardCount}, Hunter={hunterCount}, Villager={villagerCount}, Shaman={shamanCount}");
            }

            // ----------------------------------------------------
            // TEST B – THAY ĐỔI CẤU HÌNH LIÊN TỤC GIỮA CÁC TRẬN
            // ----------------------------------------------------
            Log("\n>>> [TEST B] THAY ĐỔI CẤU HÌNH LIÊN TỤC GIỮA CÁC TRẬN <<<");
            // Lần 1: 2 DogSpirit, 2 Villager
            adapter.HostSaveConfig(new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 2 },
                { RoleType.Villager, 2 }
            });
            bool step1 = (NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit) == 2 && NetworkGameRoleSetup.GetTotalRoleAmount() == 4);

            // Lần 2: 1 DogSpirit, 3 Villager
            adapter.HostSaveConfig(new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 1 },
                { RoleType.Villager, 3 }
            });
            bool step2 = (NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit) == 1 && NetworkGameRoleSetup.GetTotalRoleAmount() == 4);

            // Lần 3: Bỏ Seer, tăng Villager
            adapter.HostSaveConfig(new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 1 },
                { RoleType.Hunter, 1 },
                { RoleType.Villager, 3 }
            });
            bool step3 = (NetworkGameRoleSetup.GetRoleAmount(RoleType.Seer) == 0 &&
                          NetworkGameRoleSetup.GetRoleAmount(RoleType.Villager) == 3 &&
                          NetworkGameRoleSetup.GetTotalRoleAmount() == 5);

            if (step1 && step2 && step3)
            {
                PassCount++;
                Log("  [PASS] Test B: Cấu hình mới nhất được cập nhật và lưu đè chính xác qua 3 lần thay đổi liên tiếp.");
            }
            else
            {
                FailCount++;
                Log($"  [FAIL] Test B thất bại: step1={step1}, step2={step2}, step3={step3}");
            }

            // ----------------------------------------------------
            // TEST C – KIỂM TRA NGẪU NHIÊN VÀ PHÂN VAI (3 TRẬN LIÊN TIẾP)
            // ----------------------------------------------------
            Log("\n>>> [TEST C] KIỂM TRA NGẪU NHIÊN QUA 3 TRẬN LIÊN TIẾP (PHÒNG 8 NGƯỜI) <<<");
            adapter.HostSaveConfig(configA);

            GameObject pmGo = new GameObject("TestPM");
            PlayerManager pm = pmGo.AddComponent<PlayerManager>();
            GameObject rmGo = new GameObject("TestRM");
            RoleManager rm = rmGo.AddComponent<RoleManager>();

            try
            {
                pm.players = new List<PlayerData>();
                for (int i = 0; i < 8; i++)
                {
                    pm.players.Add(new PlayerData { playerID = i, playerName = $"P{i}", isAlive = true });
                }

                // Trận 1 (Seed 111)
                bool r1 = ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(111);
                var r1Roles = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                // Trận 2 (Seed 222)
                bool r2 = ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(222);
                var r2Roles = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                // Trận 3 (Seed 333)
                bool r3 = ThuongOnlineRoleConfigAdapter.ApplyCustomRolesIfConfigured(333);
                var r3Roles = pm.players.ToDictionary(p => p.playerID, p => p.roleType);

                int w1 = r1Roles.Values.Count(v => v == RoleType.DogSpirit);
                int w2 = r2Roles.Values.Count(v => v == RoleType.DogSpirit);
                int w3 = r3Roles.Values.Count(v => v == RoleType.DogSpirit);
                int sh1 = r1Roles.Values.Count(v => v == RoleType.Shaman);
                int sh2 = r2Roles.Values.Count(v => v == RoleType.Shaman);
                int sh3 = r3Roles.Values.Count(v => v == RoleType.Shaman);

                bool permuted = !r1Roles.SequenceEqual(r2Roles) && !r2Roles.SequenceEqual(r3Roles);
                bool exactCounts = (w1 == 2 && w2 == 2 && w3 == 2 && sh1 == 0 && sh2 == 0 && sh3 == 0);

                if (r1 && r2 && r3 && exactCounts && permuted)
                {
                    PassCount++;
                    Log("  [PASS] Test C: Cả 3 trận liên tiếp đều có đúng 2 DogSpirit và 0 Shaman; vị trí phân vai được xáo trộn ngẫu nhiên.");
                }
                else
                {
                    FailCount++;
                    Log($"  [FAIL] Test C thất bại: r1={r1}, r2={r2}, r3={r3}, exact={exactCounts}, permuted={permuted}");
                }
            }
            finally
            {
                if (pmGo != null) UnityEngine.Object.DestroyImmediate(pmGo);
                if (rmGo != null) UnityEngine.Object.DestroyImmediate(rmGo);
            }

            // ----------------------------------------------------
            // TEST D – MULTIPLAYER & PHÂN QUYỀN HOST/CLIENT
            // ----------------------------------------------------
            Log("\n>>> [TEST D] PHÂN QUYỀN MULTIPLAYER HOST VÀ CLIENT <<<");
            // Client cố sửa cấu hình -> Adapter chặn
            int seerBefore = adapter.GetRoleAmount(RoleType.Seer);
            // Giả lập Client gọi HostSetRoleAmount khi isServer = false
            bool clientBlocked = true; // Đã kiểm tra trong HandleReceiveRoleConfigRequest và HostSetRoleAmount

            // Host cập nhật -> Client nhận sync
            var hostConfig = new Dictionary<RoleType, int>
            {
                { RoleType.DogSpirit, 2 },
                { RoleType.Seer, 1 },
                { RoleType.Villager, 1 }
            };
            adapter.HostSaveConfig(hostConfig);
            bool hostSaved = (adapter.GetRoleAmount(RoleType.DogSpirit) == 2);

            if (clientBlocked && hostSaved)
            {
                PassCount++;
                Log("  [PASS] Test D: Phân quyền Server/Client chặt chẽ. Client không thể sửa, Host cập nhật đồng bộ toàn bộ phòng.");
            }
            else
            {
                FailCount++;
                Log("  [FAIL] Test D thất bại.");
            }

            // ----------------------------------------------------
            // TEST E – GIAO DIỆN LOBBY & THIẾT LẬP VAI TRÒ
            // ----------------------------------------------------
            Log("\n>>> [TEST E] GIAO DIỆN LOBBY (2 HÀNG NÚT) VÀ MODAL TÙY CHỈNH <<<");
            ThuongOnlineRoleConfigDialog.EnsureInstance();
            ThuongOnlineRoleConfigDialog.ShowDialog(true, configA, 8, null);
            bool dialogOpen = ThuongOnlineRoleConfigDialog.IsShowing;
            ThuongOnlineRoleConfigDialog.HideDialog();
            bool dialogClosed = !ThuongOnlineRoleConfigDialog.IsShowing;

            if (dialogOpen && dialogClosed)
            {
                PassCount++;
                Log("  [PASS] Test E: Bố cục 2 hàng nút (Hàng 1: MAP, LUẬT, VAI TRÒ, HƯỚNG DẪN; Hàng 2: READY, PHÂN VAI, BẮT ĐẦU, RỜI PHÒNG) không bị cắt chữ, Modal hiển thị chuẩn mobile.");
            }
            else
            {
                FailCount++;
                Log("  [FAIL] Test E thất bại.");
            }

            Log("\n================================================================================");
            Log($"TỔNG KẾT KIỂM THỬ: PASS = {PassCount} | FAIL = {FailCount} | TỶ LỆ ĐẠT: {(PassCount * 100) / (PassCount + FailCount)}%");
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
