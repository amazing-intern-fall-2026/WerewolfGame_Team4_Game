# Triển khai phân vai, vote, kết thúc phòng và reconnect — 10/11/2026

## Kết quả

Đã triển khai trên nhánh `codex/thuong-mainflow-communications`, nền main `b83c602`. Các thay đổi cục bộ có sẵn được giữ lại; chưa commit hoặc push.

- Host chọn vai trò và số lượng; người nhận được xáo trộn ngẫu nhiên mỗi ván, độc lập vị trí trong lobby, ID và nhân vật đã chọn. Giữ nguyên cơ cấu host đã lưu.
- Phiếu đi qua một endpoint được host xác thực. Không còn đường adapter cho client khai ID người bỏ phiếu hoặc phát bảng lựa chọn của mọi người.
- Thông báo kết thúc phòng chỉ được nhận từ host thật, trên kết nối hiện tại và đúng mã ván.
- Mất kết nối tạm thời giữ cơ hội reconnect 60 giây; controller menu không tự dọn session Thương. Kết quả sau GameOver được giữ nếu host rời.
- Mode tương tác gần chỉ nhận Dân làng, Khuyển tinh, Tiên tri và Bảo vệ; cấu hình không tương thích bị chặn trước khi phân vai.

## Phân vai và cấu hình

`RolePoolBuilder.TryBuildConfigured` kiểm tra sĩ số, loại vai hợp lệ, số lượng, dữ liệu trùng và khả năng hỗ trợ của mode trước khi tạo pool chính xác. Không âm thầm thêm hoặc đổi vai host đã chọn. Pool cần ít nhất một Ma sói và một người thuộc phe khác.

`RoleManager.exactRolePoolOverride` nhận pool đã kiểm tra và xáo trộn người nhận trước khi tạo role. Không còn hook RoleReveal/coroutine phân lại role ở frame sau. `ThuongOnlineSession` là nơi duy nhất bắt đầu phân vai online, trước khi gửi thẻ vai riêng.

Cấu hình được giữ xuyên MainMenu → lobby chính → Thương → quay lại lobby/chơi lại. Khi tạo phòng mới, dữ liệu phòng cũ bị xóa. Dữ liệu mặc định để hiển thị không còn tự đánh dấu thành cấu hình host đã lưu.

Host không chỉnh cấu hình sau khi phân vai. Client chỉ xem. Đối với mode tương tác gần, UI chặn tăng role không hỗ trợ, host kiểm tra lại khi lưu, khi đổi mode và ngay trước khi khóa lobby để phân vai. Nếu cấu hình hiện tại có Hunter hoặc role nâng cao, host cần chỉnh lại pool trước khi bật mode tương tác gần.

Các manager vai trò cũ của Lợi không được phân lại role trong khi Thương sở hữu trận.

## Vote và quyền riêng tư

`VoteButton` gửi một yêu cầu qua `ThuongOnlineSession`; host suy ra người bỏ phiếu từ kết nối. Endpoint kiểm tra mã ván, request sequence, ngày/phase, trạng thái kết nối, sống/chết, quyền bỏ phiếu và mục tiêu. Gói thiếu dữ liệu được bỏ qua an toàn.

`VoteManager.AllowVoteChanges` được bật riêng cho phiên online. Đổi phiếu thay thế lựa chọn trước đó, kể cả chuyển qua lại với “Không loại ai”, không đặt lại `hasVoted` mỗi frame. Chế độ offline vẫn giữ mặc định chỉ bỏ phiếu một lần.

`ThuongOnlineVoteNetworkAdapter` chỉ tô màu lựa chọn đã được host xác nhận trong private state của chính người chơi. Adapter không đăng ký endpoint vote/sync cũ, không gửi lựa chọn của người khác và không ghi vào VoteManager. Đường RPC vote cũ của Lợi cũng được chặn khi Thương sở hữu trận.

Mã ván tăng xuyên việc tải lại scene qua lobby chung. Vì vậy gói ván trước không trở thành hợp lệ khi ván sau dùng lại kết nối, ngày và phase tương tự.

## Kết thúc phòng và reconnect

`NetworkReturnToMenuController` kiểm tra sender là server, máy nhận là client đang kết nối, định dạng hợp lệ và mã ván trước khi giao thông báo cho session. Host bỏ qua thông báo kết thúc do client gửi giả.

Khi rời chủ động, session gửi thông báo phù hợp, cho gói tin thời gian được gửi rồi đóng mạng. Client rời gửi yêu cầu với mã ván thật, thay cho đường cũ luôn gửi `0`.

Controller menu, SceneLoader và NetworkGameManager nhường xử lý mất kết nối cho session Thương. Client giữ token và có thể kết nối lại vào ID/vai/tiến độ cũ trong thời gian giữ chỗ. Handler được đăng ký lại theo NetworkManager và CustomMessagingManager hiện tại, kể cả sau reconnect hoặc tạo phòng mới.

Host rời giữa ván đưa phòng chưa kết thúc về menu. Nếu đã nhận kết quả cuối, client vẫn có thể đọc kết quả trước khi tự rời.

## Phiên bản và tài nguyên

- Dấu kết nối: `THUONG11|`, lobby `THUONG11|lobby`. Host/client cần cùng bản mới.
- Thông báo kết thúc: `loi.online.host_session_terminated.v2`.
- Đồng bộ/đọc cấu hình role: `loi.online.role_config_sync.v2` và `loi.online.role_config_request.v2`. Request chỉ đọc cấu hình, không thay đổi dữ liệu host.
- Packet vote gameplay giữ cấu trúc hiện tại; các endpoint adapter vote v1 không còn được dùng.
- Đã sửa 6 tham chiếu pipeline trong QualitySettings vốn trỏ tới GUID không tồn tại từ main; dùng lại pipeline 2D hiện có. Các tài nguyên pipeline/renderer có sẵn trong phần việc cục bộ được giữ nguyên.

## Kiểm chứng thực tế

Unity **6000.6.0f1**, Windows Development build:

| Bộ kiểm tra | Kết quả |
| --- | --- |
| Luật phân vai/vote mới | 246 kiểm tra đạt |
| Emote | 53 kiểm tra đạt |
| Minigame | 367 kiểm tra đạt |
| Luật phòng, nhiệm vụ, route | 1.401 kiểm tra đạt |
| Quyền chat/voice và tài nguyên | 207 kiểm tra đạt |
| Ba tiến trình host + hai client | 163 kiểm tra đạt: host 84, client 38 và 41 |
| Build Windows Development | Thành công, Unity thoát mã 0 |

Bộ luật thử 64 seed trên cùng pool 8 người: giữ đúng số lượng từng role, ánh xạ role/owner khớp ID không liên tiếp; mọi vị trí đều có thể nhận Ma sói. Kiểm tra cả hai chiều đổi phiếu/skip, trọng số, người chết, CannotVote, disconnect, sai phase và kết quả chỉ xử lý một lần.

Kịch bản ba tiến trình chạy hai ván liên tiếp từ lobby chính, có tên Unicode và chọn map. Gửi thật qua mạng các gói giả ID vote ở endpoint cũ, thông báo giả kết thúc host, action thiếu dữ liệu, action ván cũ với sequence lớn và thông báo kết thúc sai epoch. Sau đó kiểm tra vote hợp lệ vẫn được nhận, chỉ xác nhận riêng cho chủ phiếu, đổi phiếu không tăng số ballot và mọi máy nhận cùng kết quả công khai.

Kịch bản còn ngắt/nối lại một client, xác nhận ID/transport được giữ, giữ đúng cơ cấu vai qua chơi lại và mã ván tăng. Sau khi xem kết quả, ba tiến trình về menu, tạo một phòng mới, kiểm tra cấu hình cũ đã được xóa, bắt đầu trận mới rồi host rời giữa ván; hai client tự về menu qua thông báo hợp lệ.

Log cuối: `Logs/OnlineFixes20261110/Final/slot-0-result.txt`, `slot-1-result.txt`, `slot-2-result.txt` và các log cùng thư mục. Kết quả luật: `Logs/OnlineFixes20261110/rules-results.txt`. Log build có kiểm tra luật ở `replay-build.log`; build cuối bổ sung kiểm tra tạo phòng mới ở `fresh-room-build.log`.

## Chạy lại

Trong Unity:

- **Tools → Thuong → Online → Check Role Assignment And Vote Authority**: chạy kiểm tra luật.
- Gọi `ThuongOnlineAuthorityChecks.VerifyAndBuild` để chạy các bộ kiểm tra và tạo bản game có harness trong define `THUONG_MAIN_FLOW_QA`.
- Bản kiểm chứng ở `Logs/MainFlow/Player/ThuongMainFlow.exe`.

Mở ba tiến trình với slot `0`, `1`, `2`, dùng chung cổng LAN và đường dẫn kết quả/log riêng:

```text
-batchmode -nographics -communication-qa -authority-qa 1 -flow-slot 0|1|2 -flow-port 18612 -flow-output <đường dẫn tuyệt đối> -logFile <đường dẫn tuyệt đối>
```

Harness chỉ được đưa vào bản kiểm chứng; không bật trong bản phát hành bình thường.

## Giới hạn

Kiểm tra mạng sử dụng ba executable và transport loopback trên cùng máy. Harness chủ động giữ/chuyển phase và đặt tiến độ nhiệm vụ để kiểm tra các nhánh cần thiết; không thay thế một trận playtest đầy đủ. Chưa nghiệm thu Relay qua Internet, âm thanh Vivox thật, touch hoặc hình ảnh giao diện ở các độ phân giải trong lượt sửa này.

Bộ `NetworkGameOverTests` cũ tự chạy khi mở Editor vẫn có lỗi fixture TC48 gọi FinishVoting khi thiếu VoteManager. Lỗi scene.path null trước đó đã được chặn; bộ GameOver cũ không được tính là đạt trong các kết quả trên.

Bản sao các tệp trước khi triển khai nằm trong `Logs/OnlineFixes20261110/Before`. Các thay đổi Unity tự sinh ngoài phạm vi được dọn lại sau kiểm chứng.
