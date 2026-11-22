using System.Collections.Generic;
using NUnit.Framework;
using Thuong.Online;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Unit Tests kiểm thử toàn bộ chức năng Ông Tơ Bà Nguyệt (WeaverOfFate) & Xà Tinh (SerpentSpirit)
/// theo yêu cầu kỹ thuật và nghiệp vụ game Thương Online.
/// </summary>
public class ThuongOnlineWeaverOfFateTests
{
    private GameObject testRoot;
    private NetworkSkillUI skillUI;
    private GameObject attackBtn;
    private TextMeshProUGUI attackBtnText;
    private GameObject targetPanel;
    private NetworkTargetListUI targetListUI;
    private NetworkTargetCardUI card1UI;
    private NetworkTargetCardUI card2UI;
    private GameObject card1Go;
    private GameObject card2Go;

    [MenuItem("Tools/Tests/Run Weaver Of Fate Tests")]
    public static void RunAllTests()
    {
        Debug.Log("=================================================");
        Debug.Log("BẮT ĐẦU KIỂM THỬ: ROLE ÔNG TƠ BÀ NGUYỆT (WEAVER OF FATE) - 14 TEST CASES");
        Debug.Log("=================================================");

        var test = new ThuongOnlineWeaverOfFateTests();
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

        ExecuteTest("TC01: Nhận diện Role WeaverOfFate trong Adapter", () => test.TC01_WeaverOfFate_Recognized());
        ExecuteTest("TC02: WeaverOfFate ở ban đêm chưa dùng kỹ năng -> Có thể ghép đôi", () => test.TC02_WeaverOfFate_CanAct_Night());
        ExecuteTest("TC03: WeaverOfFate ở ban ngày -> Không thể ghép đôi", () => test.TC03_WeaverOfFate_CannotAct_Day());
        ExecuteTest("TC04: WeaverOfFate đã dùng kỹ năng -> Không thể ghép đôi lần 2", () => test.TC04_WeaverOfFate_CannotAct_AlreadyUsed());
        ExecuteTest("TC05: WeaverOfFate chết -> Không thể ghép đôi", () => test.TC05_WeaverOfFate_CannotAct_Dead());
        ExecuteTest("TC06: Mở UI chọn cặp đôi (ShowPairTargetList) -> Ở chế độ PairMode", () => test.TC06_ShowPairTargetList_EntersPairMode());
        ExecuteTest("TC07: Chọn người thứ nhất -> Nút xác nhận vẫn chưa thể bấm", () => test.TC07_SelectFirstTarget_ConfirmDisabled());
        ExecuteTest("TC08: Chọn đủ 2 người khác nhau -> Nút xác nhận bấm được", () => test.TC08_SelectBothTargets_ConfirmEnabled());
        ExecuteTest("TC09: Click lại người đã chọn -> Hủy chọn mục tiêu đó", () => test.TC09_ClickSameTarget_Deselects());
        ExecuteTest("TC10: Nhấn nút HỦY -> Xóa toàn bộ lựa chọn và đóng panel", () => test.TC10_Cancel_ResetsSelections());
        ExecuteTest("TC11: LoverManager luôn tự động có Instance hợp lệ (không bao giờ null)", () => test.TC11_LoverManager_AutoInstance_NotNull());
        ExecuteTest("TC12: Server thực thi ghép đôi -> Cặp đôi được lưu vào loverID của 2 PlayerData", () => test.TC12_ExecuteMakeLovers_UpdatesPlayerLoverIds());
        ExecuteTest("TC13: Server từ chối ghép đôi nếu 2 mục tiêu trùng nhau", () => test.TC13_ExecuteMakeLovers_RejectsSameTargets());
        ExecuteTest("TC14: Server từ chối ghép đôi nếu mục tiêu đã chết", () => test.TC14_ExecuteMakeLovers_RejectsDeadTargets());

        Debug.Log($"KẾT QUẢ KIỂM THỬ: {passed}/14 TEST CASES ĐẠT! (Thất bại: {failed})");
    }

    [SetUp]
    public void SetUp()
    {
        testRoot = new GameObject("WeaverTestRoot");

        var skillGo = new GameObject("NetworkSkillUI");
        skillGo.transform.SetParent(testRoot.transform);
        skillUI = skillGo.AddComponent<NetworkSkillUI>();

        attackBtn = new GameObject("AttackButton");
        attackBtn.transform.SetParent(skillGo.transform);
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(attackBtn.transform);
        attackBtnText = txtGo.AddComponent<TextMeshProUGUI>();

        targetPanel = new GameObject("TargetPanel");
        targetPanel.transform.SetParent(skillGo.transform);
        targetListUI = targetPanel.AddComponent<NetworkTargetListUI>();

        var container = new GameObject("CardContainer");
        container.transform.SetParent(targetPanel.transform);

        card1Go = new GameObject("Card1");
        card1Go.transform.SetParent(container.transform);
        card1UI = card1Go.AddComponent<NetworkTargetCardUI>();

        card2Go = new GameObject("Card2");
        card2Go.transform.SetParent(container.transform);
        card2UI = card2Go.AddComponent<NetworkTargetCardUI>();

        var confirmPanel = new GameObject("ConfirmPanel");
        confirmPanel.transform.SetParent(targetPanel.transform);

        var confirmBtnGo = new GameObject("ConfirmAttackButton");
        confirmBtnGo.transform.SetParent(confirmPanel.transform);
        var confBtn = confirmBtnGo.AddComponent<Button>();

        var cancelBtnGo = new GameObject("CancelButton");
        cancelBtnGo.transform.SetParent(confirmPanel.transform);
        var canBtn = cancelBtnGo.AddComponent<Button>();

        var nameTextGo = new GameObject("SelectedTargetName");
        nameTextGo.transform.SetParent(confirmPanel.transform);
        var nameTxt = nameTextGo.AddComponent<TextMeshProUGUI>();

        targetListUI.SetupForTest(targetPanel, confirmPanel, nameTxt, null, confBtn, canBtn);
    }

    [TearDown]
    public void TearDown()
    {
        if (testRoot != null)
        {
            Object.DestroyImmediate(testRoot);
        }
    }

    [Test]
    public void TC01_WeaverOfFate_Recognized()
    {
        var player = new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true };
        bool isWeaver = ThuongOnlineWeaverOfFateAdapter.IsWeaverOfFate(player);
        Assert.IsTrue(isWeaver, "Role WeaverOfFate phải được nhận diện chính xác.");
    }

    [Test]
    public void TC02_WeaverOfFate_CanAct_Night()
    {
        var player = new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true };
        bool canAct = ThuongOnlineWeaverOfFateAdapter.CanWeaverOfFatePair(player, GamePhase.Night, hasUsedAbility: false);
        Assert.IsTrue(canAct, "WeaverOfFate ở Night chưa dùng kỹ năng phải có quyền ghép đôi.");
    }

    [Test]
    public void TC03_WeaverOfFate_CannotAct_Day()
    {
        var player = new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true };
        bool canAct = ThuongOnlineWeaverOfFateAdapter.CanWeaverOfFatePair(player, GamePhase.Discussion, hasUsedAbility: false);
        Assert.IsFalse(canAct, "WeaverOfFate ở ban ngày không được phép ghép đôi.");
    }

    [Test]
    public void TC04_WeaverOfFate_CannotAct_AlreadyUsed()
    {
        var player = new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true };
        bool canAct = ThuongOnlineWeaverOfFateAdapter.CanWeaverOfFatePair(player, GamePhase.Night, hasUsedAbility: true);
        Assert.IsFalse(canAct, "WeaverOfFate đã dùng kỹ năng 1 lần thì không được dùng tiếp lần 2.");
    }

    [Test]
    public void TC05_WeaverOfFate_CannotAct_Dead()
    {
        var player = new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = false };
        bool canAct = ThuongOnlineWeaverOfFateAdapter.CanWeaverOfFatePair(player, GamePhase.Night, hasUsedAbility: false);
        Assert.IsFalse(canAct, "WeaverOfFate đã chết không thể ghép đôi.");
    }

    [Test]
    public void TC06_ShowPairTargetList_EntersPairMode()
    {
        targetListUI.ShowPairTargetList();
        Assert.IsTrue(targetListUI.IsPairSelectionMode, "targetListUI phải chuyển sang PairSelectionMode.");
    }

    [Test]
    public void TC07_SelectFirstTarget_ConfirmDisabled()
    {
        targetListUI.ShowPairTargetList();
        targetListUI.OnCardSelected(card1UI, 1, "Player 2");

        Assert.AreEqual(1, targetListUI.FirstSelectedTargetId, "Mục tiêu 1 phải là ID 1.");
        Assert.AreEqual(-1, targetListUI.SecondSelectedTargetId, "Mục tiêu 2 chưa được chọn (-1).");
        Assert.IsFalse(targetListUI.ConfirmAttackButton.interactable, "Chưa chọn đủ 2 người thì nút xác nhận không được bấm.");
    }

    [Test]
    public void TC08_SelectBothTargets_ConfirmEnabled()
    {
        targetListUI.ShowPairTargetList();
        targetListUI.OnCardSelected(card1UI, 1, "Player 2");
        targetListUI.OnCardSelected(card2UI, 2, "Player 3");

        Assert.AreEqual(1, targetListUI.FirstSelectedTargetId, "Mục tiêu 1 phải là ID 1.");
        Assert.AreEqual(2, targetListUI.SecondSelectedTargetId, "Mục tiêu 2 phải là ID 2.");
        Assert.IsTrue(targetListUI.ConfirmAttackButton.interactable, "Đã chọn đủ 2 người khác nhau thì nút xác nhận phải interactable = true.");
    }

    [Test]
    public void TC09_ClickSameTarget_Deselects()
    {
        targetListUI.ShowPairTargetList();
        targetListUI.OnCardSelected(card1UI, 1, "Player 2");
        Assert.AreEqual(1, targetListUI.FirstSelectedTargetId);

        // Click lại người 1 -> Hủy chọn
        targetListUI.OnCardSelected(card1UI, 1, "Player 2");
        Assert.AreEqual(-1, targetListUI.FirstSelectedTargetId, "Click lại người 1 phải hủy chọn.");
    }

    [Test]
    public void TC10_Cancel_ResetsSelections()
    {
        targetListUI.ShowPairTargetList();
        targetListUI.OnCardSelected(card1UI, 1, "Player 2");
        targetListUI.OnCardSelected(card2UI, 2, "Player 3");

        targetListUI.OnCancelClicked();

        Assert.AreEqual(-1, targetListUI.FirstSelectedTargetId, "Sau khi hủy, mục tiêu 1 phải là -1.");
        Assert.AreEqual(-1, targetListUI.SecondSelectedTargetId, "Sau khi hủy, mục tiêu 2 phải là -1.");
        Assert.IsFalse(targetListUI.IsPairSelectionMode, "Chế độ PairMode phải được tắt.");
    }

    [Test]
    public void TC11_LoverManager_AutoInstance_NotNull()
    {
        var lm = ThuongOnlineWeaverOfFateAdapter.EnsureLoverManager();
        Assert.IsNotNull(lm, "LoverManager phải luôn tồn tại và không được null.");
        Assert.IsNotNull(LoverManager.Instance, "LoverManager.Instance static phải khác null.");
    }

    [Test]
    public void TC12_ExecuteMakeLovers_UpdatesPlayerLoverIds()
    {
        // Setup PlayerManager mock
        var pmGo = new GameObject("PlayerManagerMock");
        var pm = pmGo.AddComponent<PlayerManager>();
        pm.players = new List<PlayerData>
        {
            new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true },
            new PlayerData { playerID = 1, playerName = "Romeo", roleType = RoleType.Villager, isAlive = true },
            new PlayerData { playerID = 2, playerName = "Juliet", roleType = RoleType.Villager, isAlive = true }
        };

        try
        {
            bool success = ThuongOnlineWeaverOfFateAdapter.ExecuteMakeLovers(0, 1, 2, out string feedback);
            Assert.IsTrue(success, $"Ghép đôi phải thành công. Feedback: {feedback}");

            var p1 = pm.GetplayerByID(1);
            var p2 = pm.GetplayerByID(2);
            Assert.AreEqual(2, p1.loverID, "loverID của Player 1 phải là Player 2.");
            Assert.AreEqual(1, p2.loverID, "loverID của Player 2 phải là Player 1.");
        }
        finally
        {
            Object.DestroyImmediate(pmGo);
        }
    }

    [Test]
    public void TC13_ExecuteMakeLovers_RejectsSameTargets()
    {
        bool success = ThuongOnlineWeaverOfFateAdapter.ExecuteMakeLovers(0, 1, 1, out string feedback);
        Assert.IsFalse(success, "Server phải từ chối nếu ghép đôi cùng 1 người với chính mình.");
    }

    [Test]
    public void TC14_ExecuteMakeLovers_RejectsDeadTargets()
    {
        var pmGo = new GameObject("PlayerManagerMock");
        var pm = pmGo.AddComponent<PlayerManager>();
        pm.players = new List<PlayerData>
        {
            new PlayerData { playerID = 0, playerName = "Weaver", roleType = RoleType.WeaverOfFate, isAlive = true },
            new PlayerData { playerID = 1, playerName = "AlivePlayer", roleType = RoleType.Villager, isAlive = true },
            new PlayerData { playerID = 2, playerName = "DeadPlayer", roleType = RoleType.Villager, isAlive = false }
        };

        try
        {
            bool success = ThuongOnlineWeaverOfFateAdapter.ExecuteMakeLovers(0, 1, 2, out string feedback);
            Assert.IsFalse(success, "Server phải từ chối ghép đôi nếu có người chơi đã chết.");
        }
        finally
        {
            Object.DestroyImmediate(pmGo);
        }
    }
}

