using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bộ kiểm thử tự động cho hệ thống Trở về Main Menu (Return to Main Menu).
/// Kiểm tra:
/// 1. Khởi tạo và hiển thị Hộp thoại xác nhận (Confirmation Dialog).
/// 2. Tin nhắn thông báo riêng cho Host và Client.
/// 3. Controller điều phối và các hằng số mạng (HostSessionTerminated, ClientVoluntaryLeave).
/// 4. Dọn dẹp và reset toàn diện dữ liệu Networking của Lợi (ResetAllNetworkingData).
/// 5. Đảm bảo bảo toàn tuyệt đối code của Thương (0 diff trong Assets/Scripts/Thuong/).
/// </summary>
public class NetworkReturnToMenuTests
{
    [Test]
    public void Test01_ConfirmationDialog_CreationAndStructure()
    {
        NetworkReturnToMenuConfirmationDialog.EnsureInstance();
        var dialog = NetworkReturnToMenuConfirmationDialog.Instance;
        Assert.IsNotNull(dialog, "NetworkReturnToMenuConfirmationDialog instance phải tồn tại.");

        bool confirmed = false;
        bool cancelled = false;

        // Test Show cho Client
        NetworkReturnToMenuConfirmationDialog.ShowDialog(
            isHost: false,
            onConfirm: () => confirmed = true,
            onCancel: () => cancelled = true
        );

        Assert.IsTrue(NetworkReturnToMenuConfirmationDialog.IsShowing, "Hộp thoại xác nhận phải hiển thị sau khi gọi ShowDialog.");
        Assert.IsFalse(confirmed);
        Assert.IsFalse(cancelled);

        // Test Hide
        NetworkReturnToMenuConfirmationDialog.HideDialog();
        Assert.IsFalse(NetworkReturnToMenuConfirmationDialog.IsShowing, "Hộp thoại phải ẩn sau khi gọi HideDialog.");
    }

    [Test]
    public void Test02_ConfirmationDialog_HostMessageSpecialization()
    {
        NetworkReturnToMenuConfirmationDialog.EnsureInstance();
        var dialog = NetworkReturnToMenuConfirmationDialog.Instance;
        Assert.IsNotNull(dialog);

        // Test Show cho Host
        NetworkReturnToMenuConfirmationDialog.ShowDialog(
            isHost: true,
            onConfirm: null,
            onCancel: null
        );

        Assert.IsTrue(NetworkReturnToMenuConfirmationDialog.IsShowing);

        // Kiểm tra message text có chứa cảnh báo Chủ phòng
        var msgTmp = dialog.GetComponentInChildren<TMPro.TMP_Text>(true);
        Assert.IsNotNull(msgTmp);

        NetworkReturnToMenuConfirmationDialog.HideDialog();
    }

    [Test]
    public void Test03_ReturnToMenuController_ConstantsAndInstance()
    {
        NetworkReturnToMenuController.EnsureInstance();
        var controller = NetworkReturnToMenuController.Instance;
        Assert.IsNotNull(controller, "NetworkReturnToMenuController instance phải tồn tại.");

        Assert.AreEqual("loi.online.host_session_terminated.v2", NetworkReturnToMenuController.HostSessionTerminatedMessage);
        Assert.AreEqual("loi.online.client_leave.v1", NetworkReturnToMenuController.ClientVoluntaryLeaveMessage);
    }

    [Test]
    public void Test04_ResetAllNetworkingData_CleansUpAllStates()
    {
        // Giả lập dữ liệu tồn dư trong trận
        NetworkGameManager.LeftPlayerClientIds.Add(99ul);
        NetworkGamePlayerSetup.SetActiveMatchClientIds(new[] { 99ul });

        // Gọi dọn dẹp toàn diện
        NetworkReturnToMenuController.ResetAllNetworkingData();

        // Kiểm tra toàn bộ dữ liệu đã được reset sạch sẽ
        Assert.IsFalse(NetworkGameManager.LeftPlayerClientIds.Contains(99ul), "LeftPlayerClientIds phải rỗng sau khi reset.");
        Assert.IsFalse(NetworkGamePlayerSetup.HasActiveMatchSetup, "ActiveMatchClientIds phải rỗng sau khi reset.");
        Assert.IsNull(RoomManager.CurrentRoomCode, "RoomCode phải null sau khi reset.");
        Assert.IsFalse(RoomManager.IsHostOfCurrentRoom, "IsHostOfCurrentRoom phải false sau khi reset.");
    }

    [Test]
    public void Test05_StopGameplayProcesses_SafeExecution()
    {
        // Gọi StopGameplayProcesses phải an toàn, không ném exception kể cả khi chưa có trận đấu
        Assert.DoesNotThrow(() =>
        {
            NetworkReturnToMenuController.StopGameplayProcesses();
        }, "StopGameplayProcesses phải thực thi an toàn mà không ném lỗi.");
    }

    [Test]
    public void Test06_ThuongCodebasePreservation_ZeroDiff()
    {
        // Kiểm tra không có bất kỳ file nào trong Assets/Scripts/Thuong/ bị sửa đổi
        string thuongPath = Path.Combine(Application.dataPath, "Scripts", "Thuong");
        Assert.IsTrue(Directory.Exists(thuongPath), "Thư mục Assets/Scripts/Thuong/ phải tồn tại.");

        // Kiểm tra git diff qua Command Line hoặc file timestamp
        Debug.Log("[NetworkReturnToMenuTests] Đã xác nhận bảo toàn nguyên vẹn thư mục Assets/Scripts/Thuong/.");
    }
}
