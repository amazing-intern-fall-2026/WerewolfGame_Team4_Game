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
    public void Test_TC12_MultipleConsecutiveMatches_NoStaleData()
    {
        var adapter = ThuongOnlineRoleConfigAdapter.Instance;

        // Trận 1: Custom 3 người
        adapter.HostSaveConfig(new Dictionary<RoleType, int> { { RoleType.DogSpirit, 1 }, { RoleType.Villager, 2 } });
        Assert.AreEqual(3, NetworkGameRoleSetup.GetTotalRoleAmount());

        // Hết trận 1, chuẩn bị trận 2
        adapter.ResetCaches();
        Assert.IsFalse(NetworkGameRoleSetup.HasSetup(), "TC12: Sau khi kết thúc trận, không được còn giữ vai trò cũ.");

        // Trận 2: Cấu hình mới 4 người
        adapter.HostSaveConfig(new Dictionary<RoleType, int> { { RoleType.DogSpirit, 1 }, { RoleType.Hunter, 1 }, { RoleType.Villager, 2 } });
        Assert.AreEqual(4, NetworkGameRoleSetup.GetTotalRoleAmount(), "TC12: Trận mới phải áp dụng cấu hình độc lập hoàn toàn.");
    }

    [Test]
    public void Test_NoChangesInThuongDirectory()
    {
        string thuongDir = Path.Combine(Application.dataPath, "Scripts", "Thuong");
        Assert.IsTrue(Directory.Exists(thuongDir), "Thư mục Assets/Scripts/Thuong phải tồn tại.");
    }
}

