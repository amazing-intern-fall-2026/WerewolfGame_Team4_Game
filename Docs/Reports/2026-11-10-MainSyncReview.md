# Cập nhật main và review ngày 10/11/2026

Các mục ưu tiên về phân vai, vote, kết thúc phòng và reconnect đã được triển khai sau báo cáo này. Xem [báo cáo triển khai và kết quả kiểm chứng](2026-11-10-OnlineAuthorityImplementation.md). Phần bên dưới ghi nhận trạng thái trước khi sửa.

## Kết luận

Đã cập nhật nhánh làm việc `codex/thuong-mainflow-communications` từ `4fd7309` lên `b83c6022a942aeee1f0b8aa7577e16a405132ac2`, trùng `origin/main` tại thời điểm fetch. Nhánh local `main` cũng đã được cập nhật tới cùng commit. Có 21 commit mới, thay đổi 62 tệp.

Nên ưu tiên sửa quyền gửi yêu cầu mạng, quyền riêng tư của phiếu bầu và vòng đời reconnect trước khi thêm chức năng lớn. Mã đã có nhiều tính năng mới; vấn đề chính hiện nay là các bộ điều khiển của Lợi và Thương cùng can thiệp vào một trạng thái, có thể làm sai luật hoặc phá luồng của nhau.

Đây là review mã nguồn và kiểm tra Editor. Các cách tái hiện lỗi mạng dưới đây là kịch bản cần chạy để nghiệm thu bản sửa, chưa được xác nhận bằng nhiều client thật trong lượt này.

## Đã cập nhật những gì

Các phần mới trong main gồm cấu hình số lượng vai trò, adapter đổi phiếu và tô màu lựa chọn, hộp thoại quay về menu, vote “Không loại ai”, spectator, sổ tay, tutorial và mode tương tác gần/báo xác. Có thêm chỉnh sửa map, HUD và giao diện cấu hình vai trò.

Giữ lại toàn bộ phần việc cục bộ đang có, bao gồm các tệp chưa được Git theo dõi. Đã xử lý bốn xung đột:

- `ThuongOnlineMaps.cs`: giữ protocol `THUONG10|` của main.
- `ThuongOnlineSession.cs`: giữ tương tác E và di chuyển đêm trong mode mới, đồng thời giữ khóa di chuyển/tương tác khi nhập chat.
- `ThuongOnlineRecovery.md` và `ThuongRoomGameplayUpgrades.md`: kết hợp tài liệu main với luồng lobby chung của bản cục bộ.
- Đồng bộ dấu phiên bản lobby trong `ThuongMainFlow.cs` từ `THUONG9|lobby` lên `THUONG10|lobby`, tránh nhận bản cũ trước khi chuyển tới gameplay có packet mới.

Không commit hoặc push. Các vấn đề review bên dưới chưa được sửa trong lượt này. Chỉ sửa phần kết hợp cập nhật và tương thích phiên bản cần thiết.

Bản sao lưu các thay đổi tracked nằm trong stash có tên `codex backup before main update 2026-11-10`. Bản patch và bản sao từng tệp trước khi cập nhật nằm ở `Logs/MainReview20261110/local-changes-before-update.patch` và `Logs/MainReview20261110/LocalBackup`. Stash không chứa các tệp untracked; các tệp đó được giữ tại chỗ.

## Các vấn đề cần sửa

### 1. [P1] Client có thể gửi phiếu thay người khác

Nguồn: `Assets/Scripts/Loi/ThuongOnlineVoteNetworkAdapter.cs:264–300`.

`OnReceiveVoteRequest` đọc `voterId` từ payload do client gửi. `senderClientId` chỉ được ghi log, không được ánh xạ sang ID người chơi thật. Hàm tiếp tục xóa `hasVoted` và gọi `TryVote` bằng ID do client khai. Đường gửi này cũng thiếu epoch, ngày, phase và sequence mà endpoint chính của Thương đang kiểm tra.

Tác động: trong lượt Voting, một client có thể gửi ID của một người sống khác để ghi hoặc đổi phiếu của người đó. Việc gọi endpoint chính an toàn song song không bảo vệ được endpoint adapter này.

Hướng sửa: hợp nhất thành một đường vote trên host; suy ra người bỏ phiếu từ kết nối, kiểm tra mã ván/ngày/phase/thứ tự yêu cầu, rồi chỉ gửi xác nhận khi `TryVote` thực sự thành công. Đổi phiếu nên là thao tác có luật riêng trong VoteManager, thay vì xóa `hasVoted` mỗi frame.

Nghiệm thu: client B khai voter A phải bị từ chối; gói cũ sau chơi lại phải bị bỏ qua; phiếu từ người chết hoặc bị khóa phải không đổi kết quả hay màu xác nhận.

### 2. [P1] Thông báo kết thúc phiên không xác thực người gửi

Nguồn: `Assets/Scripts/Loi/NetworkReturnToMenuController.cs:125–147`, `396–407`.

Controller đăng ký `HostSessionTerminatedMessage` ở cả host lẫn client, nhưng `ReceiveHostSessionTerminated` không kiểm tra `senderId` và không bỏ qua khi máy nhận là server. Bất kỳ thông điệp nào tới handler này đều dừng gameplay và bắt đầu quy trình về menu.

Tác động: một client có thể gửi thông báo giả “host đã thoát” tới host; host sẽ chạy luồng dọn phiên và đóng trận của mọi người.

Hướng sửa: chỉ client nhận thông báo này, chỉ chấp nhận sender là server của phiên hiện tại và kiểm tra mã phiên. Phân biệt thông báo kết thúc có chủ ý với mất kết nối tạm thời.

Nghiệm thu: client gửi loại message này tới host không thay đổi trận; thông báo từ host thật chỉ được xử lý một lần.

### 3. [P1] Lựa chọn phiếu riêng vẫn được gửi tới mọi client

Nguồn: `Assets/Scripts/Loi/ThuongOnlineVoteNetworkAdapter.cs:220–260`.

`BroadcastVoteSyncToAllClients` ghi toàn bộ cặp `voterId → targetId` rồi gửi cùng payload tới toàn bộ kết nối. Thay đổi UI tại `343–348` chỉ dùng lựa chọn của bản thân để tô màu, nhưng dữ liệu của tất cả người khác đã có trên máy nhận.

Tác động: client sửa đổi có thể xem người khác đang bỏ phiếu cho ai trước khi công bố kết quả. Việc ẩn màu trên giao diện không bảo vệ dữ liệu này.

Hướng sửa: gửi riêng xác nhận lựa chọn của từng người; chỉ công khai tổng/kết quả ở thời điểm luật cho phép. Handler nhận sync cũng phải kiểm tra sender là host.

Nghiệm thu: máy B không nhận cặp lựa chọn của A trong khi Voting; sau khi chốt, mọi máy nhận cùng kết quả công khai.

### 4. [P1] Controller quay về menu phá reconnect và giữ kết quả

Nguồn: `Assets/Scripts/Loi/NetworkReturnToMenuController.cs:429–463`; đối chiếu `Assets/Scripts/Thuong/Online/ThuongOnlineReconnect.cs:143–169` và `ThuongOnlineSession.cs:374–414`.

Controller mới bắt cả trường hợp client mất kết nối của chính mình và lập tức dọn dữ liệu, hủy session rồi về MainMenu. Trong khi đó, session Thương thiết kế giữ chỗ 60 giây, giữ token trong bộ nhớ và hiện lựa chọn kết nối lại. Session còn cố giữ bảng kết quả nếu host rời sau GameOver.

Tác động: rớt mạng ngắn có thể mất cơ hội vào lại; host rời sau trận có thể kéo client khỏi màn kết quả. Controller tồn tại xuyên scene nên cần kiểm tra cả việc tạo phòng lần thứ hai và đăng ký lại handler.

Hướng sửa: giao quyền xử lý mất kết nối cho session sở hữu trận. Chỉ luồng rời chủ động hoặc thông báo host kết thúc hợp lệ mới ép về menu. Khi mạng/CustomMessagingManager được tạo lại, đăng ký handler theo đúng instance mới.

Nghiệm thu: mất mạng rồi vào lại trong 60 giây giữ ID/vai/tiến độ; quá hạn bị loại đúng một lần; sau GameOver vẫn xem được kết quả; rời rồi tạo phòng mới vẫn nhận đủ callback.

### 5. [P1] Cấu hình vai trò có thể đưa role không hỗ trợ vào mode tương tác gần

Nguồn: `Assets/Scripts/Loi/ThuongOnlineRoleConfigAdapter.cs:141–204`, `713–824`; đối chiếu `Assets/Scripts/Thuong/Online/ThuongRoomRules.cs:41–45` và `ThuongOnlineRealtime.cs:251–314`.

Mode tương tác gần yêu cầu preset Basic và chỉ có kỹ năng E cho Khuyển tinh, Bảo vệ, Tiên tri. Tuy nhiên adapter tự lưu cấu hình mặc định có Hunter cho phòng 3 người trở lên vào `NetworkGameRoleSetup`; hook RoleReveal sau đó gán role từ cấu hình này mà không đối chiếu `roomRules.AllowedRoles` hoặc mode. Dialog tùy chỉnh cũng cho chọn toàn bộ role.

Tác động: một phòng được thông báo dùng Basic/realtime vẫn có thể nhận Hunter hoặc các role khác chưa có đường kỹ năng E, trong khi endpoint kỹ năng toàn map đã bị tắt.

Hướng sửa: dùng một nguồn cấu hình vai trò trên host; kiểm tra pool tương thích với mode trước khi lưu và trước khi phân vai. Phân vai một lần trước khi gửi thẻ riêng, tránh gán mặc định rồi khôi phục vai ở coroutine frame sau.

Nghiệm thu: Basic không sinh role ngoài danh sách; realtime mặc định và tùy chỉnh đều từ chối pool chưa hỗ trợ; cấu hình sai sĩ số phải báo lỗi trước khi khóa lobby.

### 6. [P2] Reset cấu hình lại tạo ngay một cấu hình được xem là đã lưu

Nguồn: `Assets/Scripts/Loi/ThuongOnlineRoleConfigAdapter.cs:900–908`, `141–152`.

`ResetCaches` gọi `NetworkGameRoleSetup.Clear()`, nhưng ngay sau đó gọi `InitializeDefaults`, vốn lại gọi `SaveToNetworkGameRoleSetup`. Vì vậy reset không để `HasSetup()` về false như kiểm thử TC11 mong đợi. Đây là nhận định từ mã; bộ TC11 chưa chạy được tới assertion do lỗi SetUp nêu ở phần kiểm thử.

Hướng sửa: tách dữ liệu mặc định để hiển thị khỏi cấu hình host đã xác nhận. Khi rời phòng xóa cấu hình; khi chơi lại cùng phòng giữ cấu hình một cách có chủ ý.

### 7. [P2] Bộ kiểm thử mới chưa chạy được trong EditMode

Nguồn: `Assets/Scripts/Loi/Editor/ThuongOnlineRoleConfigTests.cs`, `NetworkReturnToMenuTests.cs`; kết quả thực tế: `Logs/MainReview20261110/editmode-results.xml`.

Đã chạy 24 test: 3 đạt, 21 thất bại. Cả 21 lỗi đều do fixture khởi tạo đối tượng gọi `DontDestroyOnLoad` trong EditMode: 18 test vai trò lỗi ngay ở SetUp, 3 test menu lỗi khi tạo controller/dialog. Các kết quả này không có nghĩa là đã xác nhận 21 lỗi gameplay.

Ngoài ra, một số test chỉ tự tính biến thay vì gọi endpoint thật, ví dụ TC08 tự kiểm tra `clientSenderId != 0`, nên không phát hiện được lỗi quyền mạng của handler production.

Hướng sửa: đưa các bài cần vòng đời scene/network sang PlayMode; kiểm tra luật thuần bằng dữ liệu không phụ thuộc MonoBehaviour. Với quyền mạng, dùng client thật hoặc gọi handler production với sender/payload giả lập và kiểm tra trạng thái sau xử lý.

### 8. [P2] Khởi động Editor ném lỗi trong kiểm tra GameOver

Nguồn: `Assets/Scripts/Loi/NetworkGameOverManager.cs:1599–1600`; log thực tế: `Logs/MainReview20261110/compile-full-access.log`.

Lần mở Editor để biên dịch tái hiện `NullReferenceException` ở `EnsureLoiMatchEvaluatorInstalled`: `game.gameObject.scene.path` có thể null trong scene kiểm thử tạm, nhưng được gọi `StartsWith` trực tiếp. Bộ `NetworkGameOverTests` chạy bằng `InitializeOnLoadMethod` bị dừng giữa chừng ở `Test_MonstersDead_ThirdPartyStillAlive_VillagersWaitUntilAllEliminated`.

Hướng sửa: kiểm tra scene hợp lệ và chuỗi path trước khi dùng; tách bộ test khỏi khởi tạo Editor tự động để một test hỏng không tác động mọi lần mở project.

## Rủi ro riêng khi kết hợp phần việc cục bộ với main

`Assets/Scripts/Thuong/Online/ThuongOnlineMainFlow.cs:23` đang xóa `NetworkGameRoleSetup` khi tiếp quản kết nối từ lobby chính. Trong khi đó, main mới dùng dữ liệu này làm cấu hình nguồn khi phân vai. Cần kiểm tra cấu hình host xuyên luồng MainMenu → Lobby → Thương → chơi lại: không để cấu hình trong lobby chính bị mất hoặc bị mặc định của adapter ghi đè. Đây là điểm giao giữa mã cục bộ có sẵn và main mới, cần xử lý cùng việc hợp nhất nguồn cấu hình ở mục 5–6.

## Nên cải thiện và bổ sung gì

Thứ tự đề xuất:

1. **Ổn định online:** xử lý mục 1–5; hợp nhất đường xử lý vote, cấu hình role và rời phiên. Mỗi trạng thái có một nơi quyết định trên host.
2. **Làm kiểm thử đáng tin:** sửa fixture EditMode/PlayMode và lỗi GameOver; thêm kiểm tra quyền gửi, gói thuộc ván cũ, vote “Không loại ai” sau đổi phiếu, cấu hình role xuyên lobby/chơi lại.
3. **Cải thiện trải nghiệm:** hiện rõ yêu cầu đang chờ host, bị từ chối vì sao, trạng thái mạng/ping và thời gian giữ chỗ reconnect. Thông báo lỗi thao tác nên hiện trong game thay vì chỉ có log.
4. **Tính năng mới sau khi nền ổn:** tutorial thực hành ngắn cho di chuyển, nhiệm vụ, vote và E/báo xác; thêm lịch sử sự kiện công khai và kết quả kỹ năng đã được gửi riêng cho chính người chơi vào sổ tay. Sổ tay và tutorial dạng trang đã có, không cần làm lại từ đầu.
5. **Nếu phát hành trên điện thoại:** kiểm tra điều khiển di chuyển/tương tác cảm ứng và safe area trên thiết bị thật; việc dialog co giãn theo màn hình chưa xác nhận gameplay có thể chơi đầy đủ bằng touch.

Chưa nên ưu tiên thêm role hoặc mode nữa trước khi pool hiện tại và quyền mạng vượt qua nghiệm thu.

## Kiểm tra đã thực hiện và giới hạn

- Fetch trực tiếp `origin/main`; kiểm tra HEAD, local main và origin/main cùng commit `b83c602`.
- Giải quyết xung đột, kiểm tra không còn unmerged entries hoặc marker xung đột; `git diff --check` đạt.
- Unity 6000.6.0f1 mở batchmode và biên dịch Editor thành công, thoát mã 0. Vẫn có cảnh báo và lỗi InitializeOnLoad nói ở mục 8; mã thoát 0 không được coi là toàn bộ test đạt.
- Chạy hai nhóm NUnit mới qua Unity Test Runner: 24 test, 3 đạt, 21 lỗi khởi tạo; runner thoát mã 2. Đã tổng hợp nguyên nhân ở mục 7.
- Các thay đổi font và thiết lập Unity tự sinh trong lúc kiểm tra được trả về bản main; các chỉnh sửa vốn có của người dùng được giữ lại. Báo cáo background tự sinh được chuyển vào thư mục log.
- Chưa chạy nghiệm thu host + hai client, Relay qua Internet, âm thanh Vivox, cảm ứng hoặc hình ảnh UI của bản vừa kết hợp. Không coi các nhận định tĩnh là kết quả playtest.

Log và bằng chứng nằm trong `Logs/MainReview20261110`. Trước khi phát hành, cần chạy hai ván liên tiếp với host + hai client, có đổi phiếu/skip, role tùy chỉnh, chết, rớt mạng/kết nối lại, host rời và tạo phòng mới; sau đó xác nhận trên ít nhất hai máy qua Relay.
