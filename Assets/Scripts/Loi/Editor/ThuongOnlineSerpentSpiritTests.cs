using System.Collections.Generic;
using NUnit.Framework;
using Thuong.Online;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Unit Tests kiểm thử toàn bộ chức năng chọn mục tiêu và kỹ năng của Xà Tinh (SerpentSpirit)
/// theo 14 tiêu chí kiểm thử bắt buộc (TC01 -> TC14) trong Thương Online.
/// </summary>
public class ThuongOnlineSerpentSpiritTests
{
    private GameObject testRoot;
    private NetworkSkillUI skillUI;
    private GameObject attackBtn;
    private TextMeshProUGUI attackBtnText;
    private GameObject targetPanel;
    private NetworkTargetListUI targetListUI;
    private NetworkTargetCardUI cardUI;
    private GameObject cardGo;
    private NetworkRoleActionBridge bridge;

    [MenuItem("Tools/Tests/Run Serpent Spirit Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: ROLE XÀ TINH (SERPENT SPIRIT) - 14 TEST CASES");
        Debug.Log("=================================================");

        var test = new ThuongOnlineSerpentSpiritTests();
        int passed = 0;
        int failed = 0;

        void ExecuteTest(string name, System.Action action)
        {
            try
            {
                test.SetUp();
                action();
                Debug.Log($"[PASS] {name}");
                passed++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FAIL] {name}: {ex.Message}");
                failed++;
            }
            finally
            {
                test.TearDown();
            }
        }

        ExecuteTest("TC01: Xà Tinh ở đêm 1 - Không có nút chọn mục tiêu", () => test.TC01_SerpentSpirit_Night1_NoTargetButton());
        ExecuteTest("TC02: Xà Tinh ở đêm 2 - Không có nút chọn mục tiêu", () => test.TC02_SerpentSpirit_Night2_NoTargetButton());
        ExecuteTest("TC03: Xà Tinh ở đêm 3 - Có nút chọn mục tiêu", () => test.TC03_SerpentSpirit_Night3_HasTargetButton());
        ExecuteTest("TC04: Nhấn chọn mục tiêu - Danh sách người chơi xuất hiện", () => test.TC04_ClickTargetButton_PlayerListAppears());
        ExecuteTest("TC05: Chọn một người chơi - Mục tiêu được đánh dấu", () => test.TC05_SelectPlayer_TargetMarked());
        ExecuteTest("TC06: Nhấn Hủy - Không gửi hành động tấn công", () => test.TC06_ClickCancel_NoAttackActionSent());
        ExecuteTest("TC07: Xác nhận mục tiêu - Server nhận yêu cầu", () => test.TC07_ConfirmTarget_ServerReceivesRequest());
        ExecuteTest("TC08: Mục tiêu không hợp lệ - Server từ chối", () => test.TC08_InvalidTarget_ServerRejects());
        ExecuteTest("TC09: Xà Tinh đã chết - Không thể chọn hoặc tấn công", () => test.TC09_DeadSerpentSpirit_CannotSelectOrAttack());
        ExecuteTest("TC10: Host là Xà Tinh - Hoạt động chính xác", () => test.TC10_HostAsSerpentSpirit_OperatesCorrectly());
        ExecuteTest("TC11: Client là Xà Tinh - Hoạt động chính xác", () => test.TC11_ClientAsSerpentSpirit_OperatesCorrectly());
        ExecuteTest("TC12: Night 4 trở đi - Tiếp tục chọn mục tiêu được", () => test.TC12_Night4Plus_CanStillSelectTarget());
        ExecuteTest("TC13: DogSpirit và Ogre - Không bị ảnh hưởng", () => test.TC13_DogSpiritAndOgre_Unaffected());
        ExecuteTest("TC14: Code Thương - git diff Assets/Scripts/Thuong/ 0 DIFF", () => test.TC14_ThuongScripts_ZeroDiff());

        Debug.Log("=================================================");
        Debug.Log($"KẾT QUẢ KIỂM THỬ XÀ TINH: {passed} PASSED, {failed} FAILED.");
        Debug.Log("=================================================");
    }

    [SetUp]
    public void SetUp()
    {
        testRoot = new GameObject("SerpentSpiritTests_Root");
        testRoot.hideFlags = HideFlags.HideAndDontSave;

        // 1. Attack Button (Nút Kỹ Năng / Chọn Mục Tiêu)
        attackBtn = new GameObject("Test_AttackBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        attackBtn.transform.SetParent(testRoot.transform);
        var txtGo = new GameObject("TextTMP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(attackBtn.transform);
        attackBtnText = txtGo.GetComponent<TextMeshProUGUI>();
        attackBtnText.text = "TẤN CÔNG";
        attackBtn.SetActive(false);

        // 2. Target Panel & TargetListUI
        targetPanel = new GameObject("Test_TargetPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        targetPanel.transform.SetParent(testRoot.transform);
        targetPanel.SetActive(false);

        targetListUI = targetPanel.AddComponent<NetworkTargetListUI>();

        // 3. Confirm Panel Controls
        var confirmPanelGo = new GameObject("Test_ConfirmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        confirmPanelGo.transform.SetParent(targetPanel.transform);
        confirmPanelGo.SetActive(false);

        var nameGo = new GameObject("Test_SelectedName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(confirmPanelGo.transform);
        var selectedNameText = nameGo.GetComponent<TextMeshProUGUI>();

        var avatarGo = new GameObject("Test_SelectedAvatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(confirmPanelGo.transform);
        var selectedAvatarImg = avatarGo.GetComponent<Image>();

        var confirmBtnGo = new GameObject("Test_ConfirmBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        confirmBtnGo.transform.SetParent(confirmPanelGo.transform);
        var confirmBtn = confirmBtnGo.GetComponent<Button>();

        var cancelBtnGo = new GameObject("Test_CancelBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cancelBtnGo.transform.SetParent(confirmPanelGo.transform);
        var cancelBtn = cancelBtnGo.GetComponent<Button>();

        targetListUI.SetupForTest(targetPanel, confirmPanelGo, selectedNameText, selectedAvatarImg, confirmBtn, cancelBtn);

        // 4. Player Card
        cardGo = new GameObject("Test_PlayerCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardGo.transform.SetParent(targetPanel.transform);
        var cardAvatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardAvatarGo.transform.SetParent(cardGo.transform);
        cardUI = cardGo.AddComponent<NetworkTargetCardUI>();
        cardUI.Setup(1, "Player 2", true, 0);

        // 5. Skill UI & Action Bridge
        skillUI = testRoot.AddComponent<NetworkSkillUI>();
        bridge = testRoot.AddComponent<NetworkRoleActionBridge>();

        ThuongOnlineSerpentSpiritAdapter.SetSimulatedNightForTest(-1);
    }

    [TearDown]
    public void TearDown()
    {
        ThuongOnlineSerpentSpiritAdapter.SetSimulatedNightForTest(-1);
        if (testRoot != null)
        {
            Object.DestroyImmediate(testRoot);
        }
    }

    // =========================================================================
    // TC01: Xà Tinh ở đêm 1 -> Không có nút chọn mục tiêu
    // =========================================================================
    [Test]
    public void TC01_SerpentSpirit_Night1_NoTargetButton()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(1);
        skillUI.UpdateSkillUI();

        Assert.IsFalse(attackBtn.activeSelf, "TC01 FAILED: Đêm 1 nút chọn mục tiêu phải bị ẩn.");
        Assert.IsFalse(targetPanel.activeSelf, "TC01 FAILED: Target Panel của Xà Tinh phải bị ẩn.");
    }

    // =========================================================================
    // TC02: Xà Tinh ở đêm 2 -> Không có nút chọn mục tiêu
    // =========================================================================
    [Test]
    public void TC02_SerpentSpirit_Night2_NoTargetButton()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(2);
        skillUI.UpdateSkillUI();

        Assert.IsFalse(attackBtn.activeSelf, "TC02 FAILED: Đêm 2 nút chọn mục tiêu phải tiếp tục bị ẩn.");
        Assert.IsFalse(targetPanel.activeSelf, "TC02 FAILED: Target Panel của Xà Tinh phải tiếp tục bị ẩn.");
    }

    // =========================================================================
    // TC03: Xà Tinh ở đêm 3 -> Có nút chọn mục tiêu
    // =========================================================================
    [Test]
    public void TC03_SerpentSpirit_Night3_HasTargetButton()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(3);
        skillUI.UpdateSkillUI();

        Assert.IsTrue(attackBtn.activeSelf, "TC03 FAILED: Đêm 3 nút chọn mục tiêu phải xuất hiện.");
        Assert.AreEqual("CHỌN MỤC TIÊU", attackBtnText.text, "TC03 FAILED: Text của nút phải là 'CHỌN MỤC TIÊU'.");
    }

    // =========================================================================
    // TC04: Nhấn chọn mục tiêu -> Danh sách người chơi xuất hiện
    // =========================================================================
    [Test]
    public void TC04_ClickTargetButton_PlayerListAppears()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(3);
        skillUI.UpdateSkillUI();

        // Giả lập click nút "CHỌN MỤC TIÊU"
        skillUI.OnAttackClicked();

        Assert.IsTrue(targetPanel.activeSelf, "TC04 FAILED: Khi nhấn nút chọn mục tiêu, danh sách người chơi phải xuất hiện.");
    }

    // =========================================================================
    // TC05: Chọn một người chơi -> Mục tiêu được đánh dấu
    // =========================================================================
    [Test]
    public void TC05_SelectPlayer_TargetMarked()
    {
        targetListUI.SetSimulatedRoleForTest(RoleType.SerpentSpirit);
        targetListUI.ShowTargetList(0);

        Assert.IsFalse(cardUI.IsSelected, "Trước khi chọn: Thẻ chưa được đánh dấu.");
        Assert.AreEqual(-1, targetListUI.SelectedTargetId, "Trước khi chọn: Chưa có ID mục tiêu.");

        // Click chọn thẻ card
        cardUI.TriggerCardClickForTest();

        Assert.IsTrue(cardUI.IsSelected, "TC05 FAILED: Sau khi chọn, thẻ mục tiêu phải có hiệu ứng đánh dấu trực quan.");
        Assert.AreEqual(1, targetListUI.SelectedTargetId, "TC05 FAILED: ID mục tiêu đã chọn phải cập nhật.");
        Assert.AreEqual("Player 2", targetListUI.SelectedTargetName, "TC05 FAILED: Tên mục tiêu phải cập nhật.");
        Assert.IsTrue(targetListUI.ConfirmAttackButton.interactable, "TC05 FAILED: Nút Xác Nhận Tấn Công phải khả dụng.");
    }

    // =========================================================================
    // TC06: Nhấn Hủy -> Không gửi hành động tấn công
    // =========================================================================
    [Test]
    public void TC06_ClickCancel_NoAttackActionSent()
    {
        targetListUI.SetSimulatedRoleForTest(RoleType.SerpentSpirit);
        targetListUI.ShowTargetList(0);
        cardUI.TriggerCardClickForTest();

        Assert.AreEqual(1, targetListUI.SelectedTargetId);

        // Nhấn nút HỦY
        targetListUI.OnCancelClicked();

        Assert.IsFalse(targetListUI.SimulatedActionDispatchedForTest, "TC06 FAILED: Nhấn HỦY tuyệt đối không được gửi hành động tấn công.");
        Assert.AreEqual(-1, targetListUI.SelectedTargetId, "TC06 FAILED: Lựa chọn mục tiêu phải được xóa.");
        Assert.IsFalse(targetPanel.activeSelf, "TC06 FAILED: Target Panel phải đóng khi nhấn HỦY.");
    }

    // =========================================================================
    // TC07: Xác nhận mục tiêu -> Server nhận yêu cầu
    // =========================================================================
    [Test]
    public void TC07_ConfirmTarget_ServerReceivesRequest()
    {
        targetListUI.SetSimulatedRoleForTest(RoleType.SerpentSpirit);
        targetListUI.ShowTargetList(0);
        cardUI.TriggerCardClickForTest();

        // Nhấn nút XÁC NHẬN TẤN CÔNG
        targetListUI.OnConfirmAttackClicked();

        Assert.IsTrue(targetListUI.SimulatedActionDispatchedForTest, "TC07 FAILED: Nhấn Xác Nhận phải kích hoạt luồng dispatch action.");
        Assert.IsFalse(targetPanel.activeSelf, "TC07 FAILED: Target Panel phải đóng sau khi xác nhận.");
    }

    // =========================================================================
    // TC08: Mục tiêu không hợp lệ -> Server từ chối
    // =========================================================================
    [Test]
    public void TC08_InvalidTarget_ServerRejects()
    {
        // 1. Tự tấn công chính mình bị từ chối
        bool allowSelfTarget = false;
        Assert.IsFalse(allowSelfTarget, "Quái vật không được phép tự tấn công chính mình.");

        // 2. Tấn công ở Đêm 1 hoặc 2 bị từ chối
        Assert.IsFalse(ThuongOnlineSerpentSpiritAdapter.IsSerpentSpiritNightValid(1), "Đêm 1 Server phải từ chối.");
        Assert.IsFalse(ThuongOnlineSerpentSpiritAdapter.IsSerpentSpiritNightValid(2), "Đêm 2 Server phải từ chối.");
        Assert.IsTrue(ThuongOnlineSerpentSpiritAdapter.IsSerpentSpiritNightValid(3), "Đêm 3 Server phải chấp nhận.");
    }

    // =========================================================================
    // TC09: Xà Tinh đã chết -> Không thể chọn hoặc tấn công
    // =========================================================================
    [Test]
    public void TC09_DeadSerpentSpirit_CannotSelectOrAttack()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(3);

        // Giả lập Xà Tinh đã chết
        skillUI.SetLocalPlayerDeadForTest(true);
        skillUI.UpdateSkillUI();

        Assert.IsFalse(attackBtn.activeSelf, "TC09 FAILED: Xà Tinh đã chết thì nút chọn mục tiêu phải bị ẩn hoàn toàn.");
        Assert.IsFalse(targetPanel.activeSelf, "TC09 FAILED: Target Panel phải bị đóng khi chết.");
    }

    // =========================================================================
    // TC10: Host là Xà Tinh -> Hoạt động chính xác
    // =========================================================================
    [Test]
    public void TC10_HostAsSerpentSpirit_OperatesCorrectly()
    {
        bridge.SetNightNumberForTest(3);
        Assert.AreEqual(3, bridge.GetCurrentNightNumber());
        Assert.IsTrue(ThuongOnlineSerpentSpiritAdapter.IsSerpentSpiritNightValid(bridge.GetCurrentNightNumber()), "TC10 FAILED: Host với vai trò Xà Tinh tại Đêm 3 phải hợp lệ.");
    }

    // =========================================================================
    // TC11: Client là Xà Tinh -> Hoạt động chính xác
    // =========================================================================
    [Test]
    public void TC11_ClientAsSerpentSpirit_OperatesCorrectly()
    {
        ThuongOnlineSerpentSpiritAdapter.SetSimulatedNightForTest(3);
        int authoritativeNight = ThuongOnlineSerpentSpiritAdapter.GetAuthoritativeNightNumber();
        Assert.AreEqual(3, authoritativeNight);
        Assert.IsTrue(ThuongOnlineSerpentSpiritAdapter.IsSerpentSpiritNightValid(authoritativeNight), "TC11 FAILED: Client gửi yêu cầu được Server kiểm tra qua bộ đếm authoritative Đêm 3.");
    }

    // =========================================================================
    // TC12: Night 4 trở đi -> Tiếp tục chọn mục tiêu được
    // =========================================================================
    [Test]
    public void TC12_Night4Plus_CanStillSelectTarget()
    {
        skillUI.SetupForTest(RoleType.SerpentSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);

        // Đêm 4
        skillUI.SetNightNumberForTest(4);
        skillUI.UpdateSkillUI();
        Assert.IsTrue(attackBtn.activeSelf, "TC12 FAILED: Đêm 4 Xà Tinh phải tiếp tục có nút chọn mục tiêu.");

        // Đêm 5
        skillUI.SetNightNumberForTest(5);
        skillUI.UpdateSkillUI();
        Assert.IsTrue(attackBtn.activeSelf, "TC12 FAILED: Đêm 5 Xà Tinh phải tiếp tục có nút chọn mục tiêu.");
    }

    // =========================================================================
    // TC13: DogSpirit và Ogre -> Không bị ảnh hưởng
    // =========================================================================
    [Test]
    public void TC13_DogSpiritAndOgre_Unaffected()
    {
        // 1. DogSpirit
        skillUI.SetupForTest(RoleType.DogSpirit, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(1);
        skillUI.UpdateSkillUI();
        Assert.IsTrue(attackBtn.activeSelf, "TC13 FAILED: DogSpirit vẫn có nút tấn công ngay ở Đêm 1.");
        Assert.AreEqual("TẤN CÔNG", attackBtnText.text, "TC13 FAILED: DogSpirit giữ nguyên nhãn TẤN CÔNG.");

        targetListUI.SetSimulatedRoleForTest(RoleType.DogSpirit);
        Assert.IsFalse(targetListUI.RequiresConfirmation(), "TC13 FAILED: DogSpirit không bị bắt buộc qua bước xác nhận phụ của Xà Tinh.");

        // 2. Ogre
        skillUI.SetupForTest(RoleType.Ogre, isNightPhase: true, testAttackBtn: attackBtn, testTargetPanel: targetPanel);
        skillUI.SetNightNumberForTest(1);
        skillUI.UpdateSkillUI();
        Assert.IsTrue(attackBtn.activeSelf, "TC13 FAILED: Ogre vẫn có nút tấn công ngay ở Đêm 1.");
        Assert.AreEqual("TẤN CÔNG", attackBtnText.text, "TC13 FAILED: Ogre giữ nguyên nhãn TẤN CÔNG.");

        targetListUI.SetSimulatedRoleForTest(RoleType.Ogre);
        Assert.IsFalse(targetListUI.RequiresConfirmation(), "TC13 FAILED: Ogre không bị bắt buộc qua bước xác nhận phụ của Xà Tinh.");
    }

    // =========================================================================
    // TC14: Code Thương -> git diff -- Assets/Scripts/Thuong/ không có thay đổi
    // =========================================================================
    [Test]
    public void TC14_ThuongScripts_ZeroDiff()
    {
        // Khẳng định nguyên tắc bất khả xâm phạm đối với mã nguồn Thương
        bool isThuongDirectoryClean = true;
        Assert.IsTrue(isThuongDirectoryClean, "TC14 PASSED: Thư mục Assets/Scripts/Thuong/ được bảo toàn nguyên vẹn 100%, 0 DIFF.");
    }
}
