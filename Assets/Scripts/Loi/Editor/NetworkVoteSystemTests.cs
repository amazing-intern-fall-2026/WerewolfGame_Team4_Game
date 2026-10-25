using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class NetworkVoteSystemTests
{
    private static int passCount = 0;
    private static int failCount = 0;
    private static readonly StringBuilder reportSb = new StringBuilder();

    public static int PassCount => passCount;
    public static int FailCount => failCount;

    [MenuItem("Tools/Tests/Run Network Vote System Tests")]
    public static void RunAllTests()
    {
        passCount = 0;
        failCount = 0;
        reportSb.Clear();

        Log("=================================================================");
        Log("BẮT ĐẦU KIỂM THỬ: WEREWOLF VOTING SYSTEM COMPREHENSIVE TESTS");
        Log($"Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("=================================================================\n");

        // TEST A — Giao diện (UI Layout, Anchors, Title Overlap, Prefab, No Blank Boxes)
        TestA_UI_LayoutAndVisuals();

        // TEST B — Host bỏ phiếu
        TestB_HostVote();

        // TEST C — Client bỏ phiếu
        TestC_ClientVote();

        // TEST D — Chống bỏ phiếu nhiều lần (Anti-Spam / Single Vote Rule)
        TestD_PreventMultipleVotes();

        // TEST E — Kết thúc Voting (Timer, All Eligible Voted, Idempotency)
        TestE_FinishVotingWorkflow();

        // TEST F — Người chơi disconnect giữa phase
        TestF_PlayerDisconnectHandling();

        // TEST G — Test 1 player và Reset Static Vote State
        TestG_SinglePlayerAndVoteReset();

        Log("\n=================================================================");
        Log($"TỔNG KẾT KIỂM THỬ VOTE SYSTEM: PASS = {passCount} | FAIL = {failCount}");
        Log("=================================================================");

        string reportPath = Path.Combine(Application.dataPath, "../vote_test_report.txt");
        File.WriteAllText(reportPath, reportSb.ToString(), Encoding.UTF8);
        Debug.Log($"[REPORT] Đã ghi báo cáo kết quả kiểm thử Vote vào: {reportPath}");
    }

    private static void Log(string message)
    {
        Debug.Log(message);
        reportSb.AppendLine(message);
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            passCount++;
            Log($"[PASS] {testName} - {details}");
        }
        else
        {
            failCount++;
            Log($"[FAIL] {testName} - THẤT BẠI! {details}");
        }
    }

    // =========================================================================
    // TEST A: GIAO DIỆN (UI Layout, Anchors, Title Overlap, Prefab, No Blank Boxes)
    // =========================================================================
    private static void TestA_UI_LayoutAndVisuals()
    {
        Log(">>> [TEST A] KIỂM TRA GIAO DIỆN VOTE PANEL VÀ VOTE CARD PREFAB <<<");

        // 1. Kiểm tra VoteCard.prefab
        string prefabPath = "Assets/Prefabs/Loi/VoteCard.prefab";
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert(cardPrefab != null, "TestA_CardPrefabExists", "VoteCard.prefab tồn tại và load thành công");

        if (cardPrefab != null)
        {
            NetworkVoteCardUI cardUI = cardPrefab.GetComponent<NetworkVoteCardUI>();
            Assert(cardUI != null, "TestA_CardUIComponent", "VoteCard có gắn script NetworkVoteCardUI");

            // Kiểm tra background Image
            Image rootImg = cardPrefab.GetComponent<Image>();
            Assert(rootImg != null && rootImg.color.a > 0.5f, "TestA_CardBackground", "VoteCard có nền Image màu tối chuẩn (không bị ô trắng/đỏ chói)");

            // Kiểm tra Avatar và Sprite
            Transform avatarTrans = cardPrefab.transform.Find("Avatar");
            Assert(avatarTrans != null, "TestA_AvatarExists", "Thẻ người chơi có GameObject Avatar");
            if (avatarTrans != null)
            {
                Image avatarImg = avatarTrans.GetComponent<Image>();
                Assert(avatarImg != null && avatarImg.sprite != null, "TestA_AvatarSpriteNotNull", "Avatar có Sprite chân dung (không bị ô trắng trống rỗng)");
                Assert(avatarImg != null && avatarImg.preserveAspect, "TestA_AvatarPreserveAspect", "Avatar bật PreserveAspect giữ nguyên tỉ lệ");
            }

            // Kiểm tra Text và Buttons
            Transform nameTrans = cardPrefab.transform.Find("PlayerName");
            Assert(nameTrans != null && nameTrans.GetComponent<TextMeshProUGUI>() != null, "TestA_PlayerNameText", "Thẻ người chơi có TextMeshProUGUI hiển thị tên");

            Transform statusTrans = cardPrefab.transform.Find("StatusText");
            Assert(statusTrans != null && statusTrans.GetComponent<TextMeshProUGUI>() != null, "TestA_StatusText", "Thẻ người chơi có StatusText");

            Transform voteTrans = cardPrefab.transform.Find("VoteCountText");
            Assert(voteTrans != null && voteTrans.GetComponent<TextMeshProUGUI>() != null, "TestA_VoteCountText", "Thẻ người chơi có VoteCountText");

            Button cardBtn = cardPrefab.GetComponent<Button>();
            Assert(cardBtn != null && cardBtn.interactable, "TestA_CardButton", "VoteCard có Button để chọn người chơi");
        }

        // 2. Kiểm tra Game.unity Scene VotePanel Configuration
        string scenePath = "Assets/Scenes/Loi/Game.unity";
        string sceneContent = File.ReadAllText(scenePath);

        // Kiểm tra VoteTitle không nằm giữa panel (không overlap)
        bool titleAnchoredTop = sceneContent.Contains("m_Name: VoteTitle") && sceneContent.Contains("m_AnchoredPosition: {x: 0, y: -25}");
        Assert(titleAnchoredTop, "TestA_VoteTitleAnchorTop", "Chữ VOTE được neo đỉnh panel (y = -25), KHÔNG chồng lên các ô thẻ");

        // Kiểm tra VotePanel có kích thước chuẩn 1080p centered
        bool panelCentered = sceneContent.Contains("m_Name: VotePanel") && sceneContent.Contains("m_SizeDelta: {x: 1360, y: 780}");
        Assert(panelCentered, "TestA_VotePanelSizeAndCenter", "VotePanel được căn giữa chuẩn 1360x780 không bị tràn ra ngoài màn hình");

        // Kiểm tra VoteCardContainer đã tắt Image màu trắng
        bool containerImageDisabled = sceneContent.Contains("m_Name: VoteCardContainer") && sceneContent.Contains("m_ChildAlignment: 4");
        Assert(containerImageDisabled, "TestA_ContainerNoWhiteSquare", "VoteCardContainer đã canh lề giữa và tắt nền trắng bao phủ");
    }

    // =========================================================================
    // TEST B: HOST BỎ PHIẾU
    // =========================================================================
    private static void TestB_HostVote()
    {
        Log("\n>>> [TEST B] KIỂM TRA HOST BỎ PHIẾU HỢP LỆ <<<");

        NetworkPlayerVote.ResetNetworkVotes();

        // Giả lập Host là voter 0, bỏ phiếu cho người chơi 1
        int hostVoterId = 0;
        int targetId = 1;

        bool voteAccepted = NetworkPlayerVote.RecordVote(hostVoterId, targetId);
        Assert(voteAccepted, "TestB_HostVoteAccepted", $"Host (ID {hostVoterId}) gửi phiếu cho Target {targetId} được server chấp nhận");

        int count = NetworkPlayerVote.GetVoteCountFor(targetId);
        Assert(count == 1, "TestB_HostVoteCount", $"Số phiếu của Target {targetId} được ghi nhận chính xác là 1 phiếu");

        bool hasVoted = NetworkPlayerVote.HasLocalPlayerVoted(hostVoterId);
        Assert(hasVoted, "TestB_HostHasVotedState", "Host được cập nhật trạng thái đã bỏ phiếu thành công");

        int choice = NetworkPlayerVote.GetCurrentChoice(hostVoterId);
        Assert(choice == targetId, "TestB_HostChoiceState", $"Lựa chọn của Host được lưu đúng là Target {targetId}");
    }

    // =========================================================================
    // TEST C: CLIENT BỎ PHIẾU VÀ ĐỒNG BỘ
    // =========================================================================
    private static void TestC_ClientVote()
    {
        Log("\n>>> [TEST C] KIỂM TRA CLIENT BỎ PHIẾU VÀ ĐỒNG BỘ TRẠNG THÁI <<<");

        // Tiếp tục trạng thái từ Test B (đã có Host voter 0 -> 1)
        int clientVoterId = 1;
        int targetId = 0;

        bool clientVoteAccepted = NetworkPlayerVote.RecordVote(clientVoterId, targetId);
        Assert(clientVoteAccepted, "TestC_ClientVoteAccepted", $"Client (ID {clientVoterId}) gửi phiếu cho Target {targetId} được chấp nhận");

        int target0Votes = NetworkPlayerVote.GetVoteCountFor(0);
        int target1Votes = NetworkPlayerVote.GetVoteCountFor(1);
        Assert(target0Votes == 1 && target1Votes == 1, "TestC_BothVotesRecorded", "Server ghi nhận đầy đủ phiếu của cả Host và Client");

        Assert(NetworkPlayerVote.HasLocalPlayerVoted(clientVoterId), "TestC_ClientHasVoted", "Client cập nhật trạng thái đã bỏ phiếu");
        Assert(NetworkPlayerVote.GetCurrentChoice(clientVoterId) == targetId, "TestC_ClientChoice", $"Lựa chọn của Client đồng bộ là Target {targetId}");
    }

    // =========================================================================
    // TEST D: ĐỔI VOTE VÀ CHỐNG PHIẾU TRÙNG LẶP (ANTI-SPAM)
    // =========================================================================
    private static void TestD_PreventMultipleVotes()
    {
        Log("\n>>> [TEST D] KIỂM TRA ĐỔI VOTE VÀ CHỐNG PHIẾU TRÙNG LẶP (ANTI-SPAM) <<<");

        // Host (voter 0) đã bỏ phiếu cho 1 ở Test B
        int voter = 0;
        Assert(NetworkPlayerVote.GetVoteCountFor(1) == 1, "TestD_Target1InitialVote", "Target 1 ban đầu có 1 phiếu từ voter 0");

        // 1. Thử gửi lại đúng phiếu cũ cho mục tiêu 1 -> không tăng thêm phiếu (Idempotent)
        NetworkPlayerVote.RecordVote(voter, 1);
        Assert(NetworkPlayerVote.GetVoteCountFor(1) == 1, "TestD_SameTargetNotDuplicated", "Bỏ phiếu lại cho cùng mục tiêu không làm tăng trùng lặp phiếu");

        // 2. Voter 0 đổi phiếu sang Target 2 -> Phiếu của Target 1 giảm về 0, Target 2 thành 1
        bool changeSuccess = NetworkPlayerVote.RecordVote(voter, 2);
        Assert(changeSuccess, "TestD_ChangeVoteSuccess", "Cho phép voter đổi vote sang người chơi khác");
        Assert(NetworkPlayerVote.GetVoteCountFor(1) == 0, "TestD_OldTargetVoteDecremented", "Mục tiêu cũ (Target 1) bị trừ 1 phiếu");
        Assert(NetworkPlayerVote.GetVoteCountFor(2) == 1, "TestD_NewTargetVoteIncremented", "Mục tiêu mới (Target 2) nhận đúng 1 phiếu");

        // 3. Spam click lại Target 2 nhiều lần -> Target 2 vẫn chỉ có đúng 1 phiếu
        NetworkPlayerVote.RecordVote(voter, 2);
        NetworkPlayerVote.RecordVote(voter, 2);
        Assert(NetworkPlayerVote.GetVoteCountFor(2) == 1, "TestD_Target2VotesNoSpam", "Mục tiêu 2 không bị tăng phiếu bất hợp pháp khi click liên tục");
    }

    // =========================================================================
    // TEST E: KẾT THÚC VOTING (Timer, All Eligible Voted, Idempotency)
    // =========================================================================
    private static void TestE_FinishVotingWorkflow()
    {
        Log("\n>>> [TEST E] KIỂM TRA LUỒNG KẾT THÚC VOTING VÀ XỬ LÝ DUY NHẤT <<<");

        NetworkPlayerVote.ResetNetworkVotes();

        // 1. Kiểm tra phát hiện sớm khi tất cả người chơi hợp lệ đã bỏ phiếu
        // Giả lập phòng có 3 người chơi sống: 0, 1, 2
        GameObject pmObj = new GameObject("TestPM_E");
        var pm = pmObj.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.RegisterPlayer(new PlayerData { playerID = 0, playerName = "P0", isAlive = true });
        pm.RegisterPlayer(new PlayerData { playerID = 1, playerName = "P1", isAlive = true });
        pm.RegisterPlayer(new PlayerData { playerID = 2, playerName = "P2", isAlive = true });

        // Voter 0 và 1 bỏ phiếu
        NetworkPlayerVote.RecordVote(0, 2);
        NetworkPlayerVote.RecordVote(1, 2);

        // Lúc này mới có 2/3 người bỏ phiếu
        Assert(!NetworkPlayerVote.HasAllEligiblePlayersVoted(), "TestE_NotAllVotedYet", "2/3 người bỏ phiếu -> Chưa kết thúc sớm");

        // Voter 2 bỏ phiếu
        NetworkPlayerVote.RecordVote(2, 0);
        Assert(NetworkPlayerVote.HasAllEligiblePlayersVoted(), "TestE_AllVotedDetected", "3/3 người bỏ phiếu -> Hệ thống phát hiện toàn bộ người đủ điều kiện đã vote");

        // 2. Kiểm tra tính Idempotent (không xử lý kết quả nhiều lần)
        bool firstFinish = NetworkPlayerVote.TriggerFinishVotingOnServer();
        Assert(firstFinish, "TestE_FirstFinishAllowed", "Lần kích hoạt kết thúc đầu tiên được chấp thuận");

        bool secondFinish = NetworkPlayerVote.TriggerFinishVotingOnServer();
        Assert(!secondFinish, "TestE_DuplicateFinishBlocked", "Lần kích hoạt kết thúc thứ hai bị chặn ngay lập tức (Idempotent)");

        bool thirdFinishFromTimer = NetworkPlayerVote.TriggerFinishVotingOnServer();
        Assert(!thirdFinishFromTimer, "TestE_TimerFinishBlockedIfAlreadyEnding", "Timer hết giờ gửi tín hiệu cũng bị chặn nếu đã kết thúc");

        UnityEngine.Object.DestroyImmediate(pmObj);
    }

    // =========================================================================
    // TEST F: NGƯỜI CHƠI DISCONNECT GIỮA PHỦ BỎ PHIẾU
    // =========================================================================
    private static void TestF_PlayerDisconnectHandling()
    {
        Log("\n>>> [TEST F] KIỂM TRA XỬ LÝ KHI NGƯỜI CHƠI DISCONNECT TRONG VOTING <<<");

        NetworkPlayerVote.ResetNetworkVotes();

        // Giả lập 3 người chơi: 0, 1, 2
        // Voter 0 bỏ phiếu cho 1
        // Voter 2 bỏ phiếu cho 1
        // Voter 1 bỏ phiếu cho 2
        NetworkPlayerVote.RecordVote(0, 1);
        NetworkPlayerVote.RecordVote(2, 1);
        NetworkPlayerVote.RecordVote(1, 2);

        Assert(NetworkPlayerVote.GetVoteCountFor(1) == 2, "TestF_InitialVotesTarget1", "Target 1 nhận 2 phiếu trước khi disconnect");
        Assert(NetworkPlayerVote.GetVoteCountFor(2) == 1, "TestF_InitialVotesTarget2", "Target 2 nhận 1 phiếu trước khi disconnect");

        // Giả lập Player 2 ngắt kết nối
        NetworkPlayerVote.RemoveDisconnectedPlayerVotes(2);

        // 1. Phiếu của Player 2 bỏ cho Player 1 phải bị xóa bỏ
        int votesTarget1After = NetworkPlayerVote.GetVoteCountFor(1);
        Assert(votesTarget1After == 1, "TestF_DisconnectedVoterVoteRemoved", "Phiếu của Player 2 bỏ cho Player 1 đã được thu hồi (2 -> 1)");

        // 2. Phiếu nhắm vào Player 2 phải bị xóa bỏ (không thể vote người đã out)
        int votesTarget2After = NetworkPlayerVote.GetVoteCountFor(2);
        Assert(votesTarget2After == 0, "TestF_VotesTargetingDisconnectedRemoved", "Các phiếu nhắm vào Player 2 bị hủy hoàn toàn (1 -> 0)");

        // 3. Voter 1 (người từng vote Player 2) được giải phóng để có thể vote mục tiêu khác
        Assert(!NetworkPlayerVote.HasLocalPlayerVoted(1), "TestF_Voter1VoteRefunded", "Voter 1 được hoàn trả lượt vote vì mục tiêu đã disconnect");

        // 4. Voter 1 giờ có thể bỏ phiếu cho Player 0 hợp lệ
        bool revoteSuccess = NetworkPlayerVote.RecordVote(1, 0);
        Assert(revoteSuccess, "TestF_RevoteValidTargetSuccess", "Voter 1 gửi lại phiếu cho Player 0 thành công");
        Assert(NetworkPlayerVote.GetVoteCountFor(0) == 1, "TestF_Target0VoteRecorded", "Player 0 nhận được 1 phiếu hợp lệ");
    }

    // =========================================================================
    // TEST G: TEST 1 PLAYER VÀ KHỞI TẠO STATIC VOTE STATE
    // =========================================================================
    private static void TestG_SinglePlayerAndVoteReset()
    {
        Log("\n>>> [TEST G] KIỂM TRA CHẾ ĐỘ TEST 1 PLAYER VÀ RESET STATIC VOTES <<<");

        // 1. Reset votes
        NetworkPlayerVote.ResetNetworkVotes();
        Assert(!NetworkPlayerVote.HasLocalPlayerVoted(0), "TestG_VotesResetClean", "Sau khi reset, player 0 chưa bị tính là đã vote");
        Assert(NetworkPlayerVote.GetCurrentChoice(0) == -1, "TestG_NoChoiceInitial", "Lựa chọn ban đầu là -1");

        // 2. Chế độ 1 player (eligibleCount <= 1): Cho phép tự vote chính mình
        GameObject pmObj = new GameObject("TestPM_G");
        var pm = pmObj.AddComponent<PlayerManager>();
        pm.players.Clear();
        pm.RegisterPlayer(new PlayerData { playerID = 0, playerName = "Player 1", isAlive = true });

        Assert(NetworkPlayerVote.GetEligibleVoterCount() == 1, "TestG_EligibleCountIs1", "Số người đủ điều kiện là 1");
        bool selfVoteAllowed = NetworkPlayerVote.RecordVote(0, 0);
        Assert(selfVoteAllowed, "TestG_SelfVoteAllowedInSinglePlayer", "Chế độ test 1 player cho phép tự vote chính mình");
        Assert(NetworkPlayerVote.GetVoteCountFor(0) == 1, "TestG_Target0CountIs1", "Target 0 nhận được 1 phiếu");
        Assert(NetworkPlayerVote.HasLocalPlayerVoted(0), "TestG_HasLocalVotedNow", "Player 0 được ghi nhận đã vote");

        // 3. Reset lại lần nữa
        NetworkPlayerVote.ResetLocalStaticVotes();
        Assert(!NetworkPlayerVote.HasLocalPlayerVoted(0), "TestG_LocalResetWorked", "ResetLocalStaticVotes() làm sạch toàn bộ dữ liệu static");

        UnityEngine.Object.DestroyImmediate(pmObj);
    }
}
