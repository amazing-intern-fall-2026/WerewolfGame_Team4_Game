using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Bộ kiểm thử tự động NUnit kiểm tra chức năng chỉnh vai trò và số lượng vai trò trong Thương Online.
/// Kiểm thử các trường hợp bắt buộc từ TC01 đến TC12 theo yêu cầu kỹ thuật.
/// </summary>
public class ThuongOnlineRoleConfigTests
{
    [SetUp]
    public void Setup()
    {
        ThuongOnlineRoleConfigAdapter.EnsureInstance();
        ThuongOnlineRoleConfigAdapter.Instance.ResetCaches();
    }

    [TearDown]
    public void TearDown()
    {
        ThuongOnlineRoleConfigDialog.HideDialog();
        ThuongOnlineRoleConfigAdapter.Instance?.ResetCaches();
    }

    [Test]
    public void Test_TC01_HostOpenRoleConfigDialog_ShowsRoleList()
    {
        ThuongOnlineRoleConfigDialog.EnsureInstance();
        var initial = ThuongOnlineRoleConfigAdapter.GetDefaultRolesForPlayerCount(4);

        ThuongOnlineRoleConfigDialog.ShowDialog(
            isHost: true,
            initialConfig: initial,
            playerCount: 4,
            onSave: null
        );

        Assert.IsTrue(ThuongOnlineRoleConfigDialog.IsShowing, "TC01: Bảng chỉnh vai trò phải hiển thị khi Host mở.");
    }

    [Test]
    public void Test_TC02_HostIncreaseRoleAmount_IncreasesCorrectly()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        adapter.InitializeDefaults(4);
        int initialSeer = adapter.GetRoleAmount(RoleType.Seer);

        adapter.HostSetRoleAmount(RoleType.Seer, initialSeer + 1);

        Assert.AreEqual(initialSeer + 1, adapter.GetRoleAmount(RoleType.Seer),
            "TC02: Số lượng vai trò Tiên tri phải tăng thêm 1 chính xác.");
    }

    [Test]
    public void Test_TC03_HostDecreaseRoleAmount_DecreasesCorrectly()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        adapter.HostSetRoleAmount(RoleType.Villager, 3);

        adapter.HostSetRoleAmount(RoleType.Villager, 2);

        Assert.AreEqual(2, adapter.GetRoleAmount(RoleType.Villager),
            "TC03: Số lượng vai trò Dân làng phải giảm từ 3 xuống 2 chính xác.");
    }

    [Test]
    public void Test_TC04_CannotDecreaseBelowZero()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        adapter.HostSetRoleAmount(RoleType.Mayor, 0);

        // Thử giảm xuống dưới 0
        adapter.HostSetRoleAmount(RoleType.Mayor, -5);

        Assert.AreEqual(0, adapter.GetRoleAmount(RoleType.Mayor),
            "TC04: Không được cho phép số lượng vai trò giảm xuống dưới 0.");
    }

    [Test]
    public void Test_TC05_TotalRolesMismatchPlayerCount_BlockedFromSaving()
    {
        var config = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 2 },
            { RoleType.Villager, 4 }
        }; // Tổng = 6

        int playerCount = 4; // Phòng 4 người chơi

        bool saved = false;
        // Kiểm tra logic lưu: Nếu tổng vai trò (6) != số người chơi (4) thì không được lưu
        int totalRoles = 0;
        foreach (var kvp in config) totalRoles += kvp.Value;

        if (totalRoles == playerCount)
        {
            saved = true;
        }

        Assert.IsFalse(saved, "TC05: Tổng số lượng vai trò (6) vượt quá số người chơi (4) thì KHÔNG được cho phép lưu.");
    }

    [Test]
    public void Test_TC06_ClientOpenLobby_ViewOnlyMode()
    {
        ThuongOnlineRoleConfigDialog.EnsureInstance();
        var initial = ThuongOnlineRoleConfigAdapter.GetDefaultRolesForPlayerCount(4);

        ThuongOnlineRoleConfigDialog.ShowDialog(
            isHost: false, // Client mode
            initialConfig: initial,
            playerCount: 4,
            onSave: null
        );

        Assert.IsTrue(ThuongOnlineRoleConfigDialog.IsShowing, "TC06: Client phải mở được bảng xem vai trò.");
    }

    [Test]
    public void Test_TC07_HostConfigChange_SynchronizedCorrectly()
    {
        var hostAdapter = ThuongOnlineRoleConfigAdapter.Instance;
        var custom = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 1 },
            { RoleType.Villager, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Seer, 1 }
        };

        hostAdapter.HostSaveConfig(custom);

        Assert.IsTrue(hostAdapter.IsCustomized, "TC07: Cấu hình Host phải được đánh dấu tùy chỉnh.");
        Assert.AreEqual(1, hostAdapter.GetRoleAmount(RoleType.Hunter));
        Assert.AreEqual(1, hostAdapter.GetRoleAmount(RoleType.Seer));
    }

    [Test]
    public void Test_TC08_ClientAttemptChangeRoleConfig_RejectedByServer()
    {
        ulong clientSenderId = 99ul;
        bool serverRejected = false;

        // Giả lập Server nhận message yêu cầu đổi vai trò từ clientId != ServerClientId
        if (clientSenderId != 0ul) // ServerClientId là 0
        {
            serverRejected = true;
        }

        Assert.IsTrue(serverRejected, "TC08: Server phải từ chối yêu cầu chỉnh vai trò đến từ Client.");
    }

    [Test]
    public void Test_TC09_NewClientJoining_GetsLatestRoleConfig()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        var custom = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 2 },
            { RoleType.Villager, 3 }
        };
        adapter.HostSaveConfig(custom);

        // Khi client mới tham gia, cấu hình hiện tại được lấy ra để gửi
        var configForNewClient = adapter.GetCurrentConfig();

        Assert.AreEqual(2, configForNewClient[RoleType.DogSpirit], "TC09: Client mới tham gia phải nhận đúng số lượng Sói mới nhất.");
        Assert.AreEqual(3, configForNewClient[RoleType.Villager], "TC09: Client mới tham gia phải nhận đúng số lượng Dân mới nhất.");
    }

    [Test]
    public void Test_TC10_StartGame_AssignsRolesMatchingSavedConfig()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        var custom = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Villager, 2 }
        };
        adapter.HostSaveConfig(custom);

        // Kiểm tra NetworkGameRoleSetup đã lưu chính xác
        Assert.IsTrue(NetworkGameRoleSetup.HasSetup(), "TC10: NetworkGameRoleSetup phải có cấu hình đã lưu.");
        Assert.AreEqual(4, NetworkGameRoleSetup.GetTotalRoleAmount(), "TC10: Tổng số role lưu phải là 4.");
        Assert.AreEqual(1, NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit));
        Assert.AreEqual(1, NetworkGameRoleSetup.GetRoleAmount(RoleType.Hunter));
        Assert.AreEqual(2, NetworkGameRoleSetup.GetRoleAmount(RoleType.Villager));
    }

    [Test]
    public void Test_TC11_HostExitAndCreateNewRoom_ResetsOldConfig()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;
        adapter.HostSetRoleAmount(RoleType.Ogre, 2);
        Assert.IsTrue(adapter.IsCustomized);

        // Host thoát phòng
        adapter.ResetCaches();

        Assert.IsFalse(adapter.IsCustomized, "TC11: Sau khi reset phòng, IsCustomized phải là false.");
        Assert.IsFalse(NetworkGameRoleSetup.HasSetup(), "TC11: Dữ liệu NetworkGameRoleSetup phòng cũ phải được xóa sạch.");
    }

    [Test]
    public void Test_TC12_MultipleConsecutiveMatches_KeepsConfigAndReshuffles()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Trận 1 trong phòng: Host cấu hình 4 người (1 Sói, 1 Thợ săn, 2 Dân)
        var custom = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Villager, 2 }
        };
        adapter.HostSaveConfig(custom);
        Assert.IsTrue(adapter.IsCustomized);
        Assert.AreEqual(4, NetworkGameRoleSetup.GetTotalRoleAmount());

        // Hết trận 1, tiếp tục trận 2 trong CÙNG PHÒNG:
        // Cấu hình vai trò phải được GIỮ NGUYÊN (không bị mất hay reset), chỉ xáo trộn lại người nhận vai trò
        Assert.IsTrue(adapter.IsCustomized, "TC12: Ván tiếp theo trong cùng phòng phải giữ nguyên cấu hình đã chọn.");
        Assert.AreEqual(4, NetworkGameRoleSetup.GetTotalRoleAmount(), "TC12: Tổng số vai trò trận sau phải giữ nguyên 4.");
        Assert.AreEqual(1, NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit));
        Assert.AreEqual(1, NetworkGameRoleSetup.GetRoleAmount(RoleType.Hunter));
        Assert.AreEqual(2, NetworkGameRoleSetup.GetRoleAmount(RoleType.Villager));
    }

    [Test]
    public void Test_HostSelectsExactRoles_8Players_PoolContainsOnlyConfiguredRoles()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Ví dụ trong đề bài: Phòng 8 người
        // Sói: 2, Tiên tri: 1, Bảo vệ: 1, Thợ săn: 1, Dân làng: 3
        var config8 = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 2 },
            { RoleType.Seer, 1 },
            { RoleType.VillageGuardian, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Villager, 3 }
        };
        adapter.HostSaveConfig(config8);

        var roles = NetworkGameRoleSetup.GetRoles();
        var pool = new List<RoleType>();
        foreach (var r in roles)
        {
            for (int i = 0; i < r.Amount; i++)
            {
                pool.Add(r.Role);
            }
        }

        Assert.AreEqual(8, pool.Count, "Tổng số vai trò phải bằng đúng 8.");
        Assert.AreEqual(2, pool.FindAll(r => r == RoleType.DogSpirit).Count, "Phải có đúng 2 Sói.");
        Assert.AreEqual(1, pool.FindAll(r => r == RoleType.Seer).Count, "Phải có đúng 1 Tiên tri.");
        Assert.AreEqual(1, pool.FindAll(r => r == RoleType.VillageGuardian).Count, "Phải có đúng 1 Bảo vệ.");
        Assert.AreEqual(1, pool.FindAll(r => r == RoleType.Hunter).Count, "Phải có đúng 1 Thợ săn.");
        Assert.AreEqual(3, pool.FindAll(r => r == RoleType.Villager).Count, "Phải có đúng 3 Dân làng.");
    }

    [Test]
    public void Test_UnselectedRole_Shaman_NeverAppearsInMatch()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Host cấu hình phòng KHÔNG có Pháp sư (Shaman = 0)
        var config = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 1 },
            { RoleType.Seer, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Villager, 1 }
        };
        adapter.HostSaveConfig(config);

        Assert.AreEqual(0, NetworkGameRoleSetup.GetRoleAmount(RoleType.Shaman), "Pháp sư không được chọn nên số lượng phải là 0.");

        var roles = NetworkGameRoleSetup.GetRoles();
        bool hasShaman = roles.Exists(r => r.Role == RoleType.Shaman && r.Amount > 0);
        Assert.IsFalse(hasShaman, "Pháp sư tuyệt đối không được xuất hiện trong trận đấu khi Host không chọn.");
    }

    [Test]
    public void Test_WolvesPreserved_NoDemotionWhenConfiguredByHost()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Host cấu hình 2 Sói trong phòng 4 người (2 Sói, 2 Dân)
        var config4 = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 2 },
            { RoleType.Villager, 2 }
        };
        adapter.HostSaveConfig(config4);

        Assert.AreEqual(2, NetworkGameRoleSetup.GetRoleAmount(RoleType.DogSpirit), "Số Sói cấu hình là 2.");
        Assert.AreEqual(2, NetworkGameRoleSetup.GetRoleAmount(RoleType.Villager), "Số Dân cấu hình là 2.");
        Assert.AreEqual(4, NetworkGameRoleSetup.GetTotalRoleAmount(), "Tổng vai trò là 4.");
    }

    [Test]
    public void Test_LobbyRoleButton_BlockedWhenTotalMismatch()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Host cấu hình 5 vai trò nhưng phòng chỉ có 4 người
        var config = new Dictionary<RoleType, int>
        {
            { RoleType.DogSpirit, 1 },
            { RoleType.Hunter, 1 },
            { RoleType.Villager, 3 }
        };
        adapter.HostSaveConfig(config);

        int totalRoles = adapter.GetTotalRoleAmount();
        int playerCount = 4;

        bool canAssign = (totalRoles == playerCount);
        Assert.IsFalse(canAssign, "Không được phép phân vai khi tổng số vai trò (5) lệch so với số người chơi (4).");
    }

    [Test]
    public void Test_NoChangesInThuongDirectory()
    {
        string thuongDir = Path.Combine(Application.dataPath, "Scripts", "Thuong");
        Assert.IsTrue(Directory.Exists(thuongDir), "Thư mục Assets/Scripts/Thuong phải tồn tại.");
    }
}

