# BÁO CÁO CÔNG VIỆC NGÀY 16/10/2026

**Dự án:** WerewolfGame_Team4_Game

**Hạng mục:** Lobby, gameplay và kết quả trận Thương Online

**Scene áp dụng:** `Assets/Scenes/Thuong/OnlineSceneGamePlay.unity`

**Giao diện tham chiếu:** `Assets/Scenes/Thuong/Prototype/ThuongGameplayPrototype.unity`

## 1. Tổng quan

Trong ngày đã khôi phục và cập nhật luồng chơi Thương Online từ phòng chờ đến kết thúc trận: sửa đồng bộ khi bắt đầu game, bổ sung lobby tối đa 25 người, phân role và thẻ hướng dẫn, chỉnh Ready, khôi phục HUD, hoàn thiện voting và kết quả vote, triển khai điều kiện thắng/thua cùng UI kết quả cá nhân.

Các hạng mục dưới đây đã hoàn thành ở mức mã nguồn. Mức xác nhận được ghi riêng tại mục 4; phần Ready và thắng/thua sửa cuối ngày chưa được chạy lại trong Play Mode/LAN.

## 2. Công việc đã hoàn thành

| Hạng mục | Nội dung thực hiện |
| --- | --- |
| Sửa chuyển lobby vào game | Sửa cách quản lý phiên bản snapshot khi bắt đầu trận; bỏ snapshot cũ và tránh host nhận lại snapshot của chính mình. Khắc phục nguyên nhân client còn đứng ở lobby trong khi host đã vào game. |
| Lobby tối đa 25 người | Bổ sung chỉnh sức chứa 2–25 người; không giảm dưới số người đang kết nối. Danh sách có avatar nhân vật đã chọn, tên, ID, trạng thái READY/NOT READY và thanh cuộn. |
| Phân role theo người chơi thực tế | Dùng số người thật trong phòng và các lớp role hiện có. Ngoại hình tách khỏi role bí mật; mỗi máy chỉ nhận role của mình trước khi kết thúc trận. |
| Cập nhật UI theo form cung cấp | Tạo lobby theo bố cục avatar/tên/trạng thái; thẻ role có ảnh, tên, phe, mô tả kỹ năng và hướng dẫn. Dùng phong cách giấy/gỗ của prototype. |
| Tách các bước trước trận | Luồng: tập hợp người chơi → chủ phòng phân vai → xem role tối đa 25 giây hoặc bấm OK → trở về lobby → Ready → chủ phòng bắt đầu. Hết thời gian xem role không tự Ready hoặc tự vào game. Giữ nguyên role khi bắt đầu trận. |
| Sửa nút Ready bị khóa | Cho Ready sau OK hoặc hết thời gian mà không chờ gói xác nhận đọc role quay về. Server nhận Ready đồng thời ghi nhận xác nhận role. Trước khi phân vai, nút ghi rõ CHỜ PHÂN VAI. |
| Khôi phục HUD và điều khiển UI | Giữ HUD gameplay và UI kỹ năng của scene; ẩn lobby/cửa sổ THƯƠNG ONLINE khi vào game. Tắt nhận chuột cùng các Canvas đang ẩn để tránh chặn nút bên dưới. |
| Hoàn thiện voting | Dùng bố cục và template prototype, danh sách cuộn đủ người. Server kiểm tra giai đoạn, người còn sống, quyền vote, trọng số phiếu và từ chối vote trùng. |
| Hiển thị kết quả vote | Hiện tổng phiếu có trọng số, người bị loại, hòa hoặc không có phiếu. Có thể xem lại kết quả; bổ sung xem vote cuối trong màn hình kết thúc trận. |
| Lập và triển khai luật thắng/thua | Rà logic cũ, lập plan rồi triển khai bộ xét riêng cho Thương Online. Server xét sau toàn bộ xử lý đêm, vote, bẫy và chết dây chuyền; không chốt thắng giữa chuỗi hiệu ứng. |
| UI chiến thắng/thất bại | Mỗi người thấy CHIẾN THẮNG, THẤT BẠI hoặc HÒA; phe thắng, lý do, role/phe của mình và bảng kết quả cuối ván tối đa 25 người. Đồng đội đã chết vẫn thắng nếu phe của họ thắng. |
| Khóa và giữ kết quả cuối trận | Dừng timer, di chuyển và thao tác gameplay sau GameOver. Kết quả được chốt một lần. Người chơi đã nhận kết quả vẫn xem được khi chủ phòng rời; rời sau GameOver không xóa kết quả của những người khác. |
| Vòng đời phiên chơi | Dọn dữ liệu role, nhiệm vụ, timer, avatar và UI khi rời phòng để tạo phiên mới. Tháo hook và ẩn UI riêng khi chuyển sang scene khác. |
| Tài liệu và lưu dấu công việc | Ghi tài liệu khôi phục, form UI, plan và luật thắng/thua thực tế; lưu log, ảnh và các bản backup cho giai đoạn khôi phục/form UI. |

## 3. Điều kiện thắng/thua đã triển khai

- **Dân làng:** tổng nhiệm vụ đạt 100%, hoặc vẫn còn dân sống và đã hết sói thường, sát nhân, Bạch Khuyển thức tỉnh.
- **Ma Sói:** chỉ còn sói thường sống; hoặc đạt thế cân bằng quân số khi không còn phe tấn công đối địch; hoặc hết ngày tối đa (mặc định ngày 7) mà nhiệm vụ chưa hoàn thành và sói vẫn sống.
- **Kẻ Điên:** bị vote loại trong ngày đầu tiên.
- **Cặp đôi:** hai người có liên kết tình nhân hai chiều là hai người cuối cùng còn sống.
- **Hồ Ly:** còn sống, đã mê hoặc ít nhất hai người và toàn bộ người đã mê hoặc đều còn sống. Đợi xử lý xong đêm trước khi kết luận để tính cả các đòn đánh đang chờ.
- **Bạch Khuyển thức tỉnh:** chỉ còn một cá thể sống, hết sói thường/sát nhân đối địch và còn tối đa hai dân sống.
- **Sát nhân:** chỉ còn một sát nhân sống, hết sói thường/Bạch Khuyển thức tỉnh đối địch và còn tối đa hai dân sống.
- **Hòa:** tất cả chết, bế tắc không thể đạt điều kiện riêng, hoặc hết số ngày mà không còn phe đủ điều kiện thắng.

Đã quy định thứ tự ưu tiên cho các điều kiện cùng phát sinh. Phòng thử hai người tắt thắng do cân bằng quân số để tránh kết thúc ngay ở tỷ lệ 1–1; vẫn xét thắng khi loại hết đối phương, nhiệm vụ và giới hạn ngày. Bộ role online được điều chỉnh trước khi công bố để tránh thế cân bằng ngay khi bắt đầu phòng nhỏ.

## 4. Kết quả kiểm tra và giới hạn xác nhận

| Phần việc | Kết quả đã ghi nhận |
| --- | --- |
| Giai đoạn lobby/form UI, trước các sửa Ready và thắng/thua cuối ngày | Bài kiểm tra tự động với 1 host + 24 client LAN báo PASS, **5.458 kiểm tra**. Bao gồm roster, phân role, thẻ 25 giây/OK, Ready, chủ phòng bắt đầu, voting/kết quả và tạo phòng lại. |
| Sửa Ready cuối ngày | Biên dịch C# thành công. Chưa chạy lại thao tác trong Play Mode/LAN sau bản sửa này. |
| Điều kiện thắng/thua và UI kết thúc | Biên dịch C# runtime và Editor thành công. Chưa chạy Play Mode/LAN để xác nhận toàn bộ luật và giao diện thực tế. |
| Phạm vi thay đổi | Mã mới nằm trong phần Thương. Các nhánh dùng chung chỉ chuyển sang luật mới khi session Thương Online gắn bộ xét riêng; các scene khác giữ nhánh xử lý cũ. Không sửa script Nhat/Loi hoặc scene/prefab trong phần triển khai thắng/thua. |

Kết quả PASS của giai đoạn lobby/form UI **không được dùng để kết luận** bản thắng/thua và Ready sửa sau đó đã vượt qua kiểm tra LAN.

## 5. Tài liệu và minh chứng

- [Khôi phục lobby và UI](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongOnlineRecovery.md).
- [Form lobby và thẻ role](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongOnlineUIForm.md).
- [Plan thắng/thua](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongWinLossPlan.md).
- [Luật và UI kết quả đã triển khai](D:/Project/WerewolfGame_Team4_Game/Docs/ThuongOnlineEndgame.md).
- [Kết quả kiểm tra lobby/form UI](D:/Project/WerewolfGame_Team4_Game/Logs/OnlineUIForm/result.txt).
- [Log biên dịch runtime](D:/Project/WerewolfGame_Team4_Game/Logs/OnlineRecovery/endgame-compile.log) và [Editor](D:/Project/WerewolfGame_Team4_Game/Logs/OnlineRecovery/endgame-editor-compile.log).

Các bản backup đã có cho giai đoạn khôi phục/form UI: `Logs/OnlineRecovery/ThuongOnlineRecovery-2026-10-16.zip` và `Logs/OnlineUIForm/ThuongOnlineUIForm-2026-10-16.zip`. Đây là bản lưu ở các giai đoạn trước, không đại diện cho toàn bộ mã thắng/thua triển khai sau đó.

## 6. Công việc cần xác nhận tiếp

1. Chạy lại Ready bằng thao tác thực tế trên host và client: sau OK, sau hết 25 giây và khi mạng có độ trễ.
2. Kiểm tra các điều kiện thắng riêng, thắng phe, hòa và thứ tự ưu tiên khi nhiều điều kiện cùng phát sinh.
3. Kiểm tra UI kết quả cá nhân, danh sách 25 người và vote cuối trong Play Mode/LAN.
4. Kiểm tra rời phòng sau GameOver, giữ kết quả khi host rời, tạo phiên mới và chuyển scene.

**Lưu ý triển khai:** snapshot hiện dùng phiên bản v4; host và client cần cùng bản mã/build mới.

## 7. Cập nhật sau khi lập báo cáo

Theo yêu cầu tiếp theo, luồng role online được đổi thành **PHÂN VAI → XEM PHÂN VAI → OK → READY → BẮT ĐẦU GAME**. Không tự mở thẻ sau khi phân vai, không còn giới hạn 25 giây hoặc tự xác nhận theo thời gian; từng người chủ động mở và có thể xem lại role trước trận. Nút Bắt đầu game được tách riêng cho chủ phòng. Countdown của prototype offline giữ nguyên. Kết quả kiểm tra 5.458 mục ở trên thuộc luồng cũ, không xác nhận thay đổi mới này.
